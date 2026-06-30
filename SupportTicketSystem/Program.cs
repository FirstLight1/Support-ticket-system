using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SupportTicketSystem.data;
using SupportTicketSystem.Utils;
using Serilog;

namespace SupportTicketSystem;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((ctx, services, cfg) => cfg
                .ReadFrom.Configuration(ctx.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = "MyApp.Auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    // 14 days, sliding — kept in sync with the ExpiresUtc set at SignInAsync
                    // (Authenticator.SignIn). One source of truth for cookie lifetime.
                    options.ExpireTimeSpan = TimeSpan.FromDays(14);
                    options.SlidingExpiration = true;
                    options.LoginPath = "/Auth/";
                    options.LogoutPath = "/Auth/Logout";
                    options.AccessDeniedPath = "/Auth/AccessDenied";
                });

            builder.Services.AddControllersWithViews(options =>
            {
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            });

            // Add services to the container.
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
            builder.Services.AddScoped<TicketAccess>();

            builder.Services.AddHealthChecks()
                .AddDbContextCheck<AppDbContext>("database");

            // Persist the Data Protection key ring to disk so auth cookies and antiforgery
            // tokens survive container redeployments. The key directory is configurable via
            // DataProtection:KeyPath (default "keys" relative to the content root); in Docker
            // this path must be a mounted volume.
            var keyPath = builder.Configuration["DataProtection:KeyPath"] ?? "keys";
            var keyDir = Path.Combine(builder.Environment.ContentRootPath, keyPath);
            Directory.CreateDirectory(keyDir);
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keyDir))
                .SetApplicationName("SupportTicketSystem");

            var rateLimitConfig = builder.Configuration.GetSection("RateLimiting");
            var loginPolicy = rateLimitConfig.GetSection("Login").Get<RateLimitPolicyOptions>() ?? new RateLimitPolicyOptions();
            var registerPolicy = rateLimitConfig.GetSection("Register").Get<RateLimitPolicyOptions>() ?? new RateLimitPolicyOptions();
            var mutationPolicy = rateLimitConfig.GetSection("Mutation").Get<RateLimitPolicyOptions>() ?? new RateLimitPolicyOptions();

            builder.Services.AddRateLimiter(options =>
            {
                options.OnRejected = (context, _) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) && retryAfter is TimeSpan retry)
                    {
                        context.HttpContext.Response.Headers["Retry-After"] = ((int)Math.Ceiling(retry.TotalSeconds)).ToString();
                    }
                    return ValueTask.CompletedTask;
                };

                options.AddPolicy("login", httpContext =>
                {
                    var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = loginPolicy.PermitLimit,
                        Window = loginPolicy.Window,
                        AutoReplenishment = true,
                        QueueLimit = 0
                    });
                });

                options.AddPolicy("register", httpContext =>
                {
                    var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = registerPolicy.PermitLimit,
                        Window = registerPolicy.Window,
                        AutoReplenishment = true,
                        QueueLimit = 0
                    });
                });

                options.AddPolicy("mutation", httpContext =>
                {
                    var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var key = !string.IsNullOrEmpty(userId) ? userId : (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                    return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = mutationPolicy.PermitLimit,
                        Window = mutationPolicy.Window,
                        AutoReplenishment = true,
                        QueueLimit = 0
                    });
                });
            });

            // Cap request body size at 6 MB — the image upload limit is 5 MB, so this leaves
            // headroom for the rest of the multipart form without accepting unbounded uploads.
            builder.Services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = 6 * 1024 * 1024);

            var app = builder.Build();

            // Explicit migrate-on-deploy. Run `dotnet run -- --migrate` to apply migrations
            // and exit. Auto-migrating on every boot was removed because it applies schema
            // changes with no review and is a concurrency hazard if multiple instances start.
            if (args.Contains("--migrate"))
            {
                using var migrateScope = app.Services.CreateScope();
                var db = migrateScope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
                Log.Information("Database migrated.");
                return;
            }

            if (args.Contains("--seed"))
            {
                if (app.Environment.IsProduction())
                {
                    Log.Error("Refusing to seed the database in {Env} environment", app.Environment.EnvironmentName);
                    return;
                }
                using var seedScope = app.Services.CreateScope();
                var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
                PopulateDb.SeedDb(db);
                Log.Information("Database seeded.");
                return;
            }

            // Idempotent: creates the first admin from BootstrapAdmin:* config if set and no
            // admin with that email exists yet. No-op when unconfigured or already present.
            using (var bootstrapScope = app.Services.CreateScope())
            {
                var db = bootstrapScope.ServiceProvider.GetRequiredService<AppDbContext>();
                AdminBootstrapper.EnsureAdmin(db, builder.Configuration);
            }

            var fwd = new ForwardedHeadersOptions {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };
            fwd.KnownIPNetworks.Clear(); // cloudflared is on the docker network, not loopback
            fwd.KnownProxies.Clear();    // safe here: only cloudflared can reach the app (no host port)
            app.UseForwardedHeaders(fwd);

            // Security response headers. Applied to every response before routing so they
            // cover errors and static files too. CSP allows the Google Fonts hosts used in
            // _Layout.cshtml; script-src is 'unsafe-inline' for the small inline <script>
            // blocks in Auth/Index.cshtml and Tickets/Edit.cshtml (nonces are a future step).
            app.Use(async (ctx, next) =>
            {
                var h = ctx.Response.Headers;
                h["X-Content-Type-Options"] = "nosniff";
                h["X-Frame-Options"] = "DENY";
                h["Referrer-Policy"] = "no-referrer";
                h["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=()";
                h["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline'; " +
                    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                    "font-src 'self' https://fonts.gstatic.com; " +
                    "img-src 'self' data:; " +
                    "connect-src 'self'; " +
                    "base-uri 'self'; " +
                    "frame-ancestors 'none'";
                await next();
            });


            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            // UseStaticFiles serves runtime-uploaded images from wwwroot/Images/. MapStaticAssets
            // only serves build-time-bundled assets, so without this uploaded images would 404.
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.UseSerilogRequestLogging();
            app.MapHealthChecks("/healthz");
            app.MapStaticAssets();
            app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
