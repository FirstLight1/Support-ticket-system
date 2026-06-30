FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SupportTicketSystem.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish ./

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
