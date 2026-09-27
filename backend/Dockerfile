# syntax=docker/dockerfile:1
#
# Two images out of one context, both used by the repository-root docker-compose.yml:
#
#   --target api        the published ASP.NET app (the default target; last stage)
#   --target migrator   a one-shot `dotnet ef database update` runner
#
# The migrator exists because Task 2.0 decided that MIGRATIONS OWN THE SCHEMA: the container
# starts empty, EF Core creates the schema, the seed loads afterwards. Compose runs the same
# three steps in that order without needing the .NET SDK on the host, so the migrator needs the
# SDK and the sources — it is deliberately not the image the API is served from.

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:8.0
# Debian bookworm rather than -alpine or -chiseled: `-chiseled` and Alpine ship without tzdata,
# and although the timezone conversion happens in Postgres, `TimeProvider`-based local-date
# arithmetic for an account with no events resolves the account's IANA zone in .NET.
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:8.0

# ---------------------------------------------------------------------------------------------
# restore — csproj files first, so `dotnet restore` is cached until a dependency actually changes
# ---------------------------------------------------------------------------------------------
FROM ${DOTNET_SDK_IMAGE} AS restore
WORKDIR /src

# global.json pins the SDK feature band and Directory.Build.props carries the target framework and
# the warnings-as-errors policy — both are needed before any project file is evaluated.
COPY global.json Directory.Build.props Relay.sln ./
COPY src/Relay.Api/Relay.Api.csproj src/Relay.Api/
COPY src/Relay.Application/Relay.Application.csproj src/Relay.Application/
COPY src/Relay.Infrastructure/Relay.Infrastructure.csproj src/Relay.Infrastructure/
COPY tests/Relay.UnitTests/Relay.UnitTests.csproj tests/Relay.UnitTests/
RUN dotnet restore Relay.sln

COPY . .

# ---------------------------------------------------------------------------------------------
# publish
# ---------------------------------------------------------------------------------------------
FROM restore AS publish
RUN dotnet publish src/Relay.Api/Relay.Api.csproj --configuration Release --no-restore --output /app/publish

# ---------------------------------------------------------------------------------------------
# migrator — one-shot, exits 0 when the database is at the latest migration
# ---------------------------------------------------------------------------------------------
FROM restore AS migrator
ENV PATH="$PATH:/root/.dotnet/tools"
# Pinned to the EF Core version the Infrastructure project references, so the tool and the
# runtime cannot disagree about the migrations format.
RUN dotnet tool install --global dotnet-ef --version 8.0.11

# Relay.Infrastructure is both the migrations project and the startup project: EFCore.Design is a
# PrivateAssets="all" reference there and never flows to Relay.Api, so `-s src/Relay.Api` cannot
# load it. DesignTimeRelayDbContextFactory is what lets Infrastructure stand in, and it reads the
# connection string from RELAY_DB_CONNECTION (supplied by compose).
ENTRYPOINT ["dotnet", "ef", "database", "update", \
            "--project", "src/Relay.Infrastructure", \
            "--startup-project", "src/Relay.Infrastructure"]

# ---------------------------------------------------------------------------------------------
# api — the default target
# ---------------------------------------------------------------------------------------------
FROM ${DOTNET_RUNTIME_IMAGE} AS api
WORKDIR /app
COPY --from=publish /app/publish ./

# APP_UID (1654) is defined by the base image for exactly this purpose. Set after the COPY so the
# application files stay root-owned and read-only to the process that serves them.
USER $APP_UID

# ASPNETCORE_HTTP_PORTS defaults to 8080 in the base image; no HTTPS, no certificate to mount —
# the API is served over plain HTTP behind compose (see Program.cs).
EXPOSE 8080
ENTRYPOINT ["dotnet", "Relay.Api.dll"]
