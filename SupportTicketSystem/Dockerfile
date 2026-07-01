FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SupportTicketSystem.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 400

ENV ASPNETCORE_HTTP_PORTS=400
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish ./

# Pre-create writable dirs and give them to the non-root app user so named
# volumes inherit writable ownership on first creation. The aspnet image sets
# APP_UID (1654); without this, Docker creates these root-owned and the non-root
# app user can't write SQLite WAL to /data, keys to /app/keys, logs to /app/logs,
# or uploads to /app/wwwroot/Images.
USER root
RUN mkdir -p /data /app/keys /app/logs /app/wwwroot/Images \
    && chown -R $APP_UID:$APP_UID /data /app/keys /app/logs /app/wwwroot/Images
USER $APP_UID

# Volumes for persistent state (mount these in docker-compose):
#   /data              — SQLite database (path set via ConnectionStrings env var)
#   /app/keys          — Data Protection key ring
#   /app/logs          — Serilog file sink
#   /app/wwwroot/Images — uploaded images
VOLUME ["/data", "/app/keys", "/app/logs", "/app/wwwroot/Images"]

# Run migrations on container start, then launch the app.
# --migrate is idempotent (no-op if already up to date) and exits on success,
# so the shell form runs it first, then starts the app without the flag.
ENTRYPOINT ["sh", "-c", "dotnet SupportTicketSystem.dll --migrate && dotnet SupportTicketSystem.dll"]
