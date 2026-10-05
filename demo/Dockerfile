# Supertext Orchard Core demo: Orchard Core 3 CMS (EN, DE-CH, FR-CH, IT-CH) + this module.
# Build context is the repository root:
#   docker build -f demo/Dockerfile -t supertext-orchardcore-demo .
# State (SQLite database, media, data protection keys) lives in /data (mount a volume).
# See docs/DEVELOPER.md -> Demo.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ src/
COPY demo/SupertextDemo/ demo/SupertextDemo/
RUN dotnet publish demo/SupertextDemo/SupertextDemo.csproj -c Release -o /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
COPY demo/entrypoint.sh /usr/local/bin/orchard-entrypoint
# wwwroot must exist (ImageSharp media cache); robots.txt keeps the demo out of search engines.
RUN chmod +x /usr/local/bin/orchard-entrypoint && mkdir -p /app/wwwroot

# Railway terminates TLS at its proxy; trust X-Forwarded-Proto so Orchard Core builds https URLs.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

# No VOLUME instruction: Railway rejects it and mounts its own volume at /data.
ENTRYPOINT ["orchard-entrypoint"]
