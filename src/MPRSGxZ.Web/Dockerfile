# syntax=docker/dockerfile:1
# Build context is expected to be the repository root (see docker-compose.yml) so that the
# project reference to ../MPRSGxZ.Library resolves correctly.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first and restore, to take advantage of Docker layer caching when only
# source files (not dependencies) change.
COPY src/MPRSGxZ.Web/MPRSGxZ.Web.csproj src/MPRSGxZ.Web/
COPY src/MPRSGxZ.Library/MPRSGxZ.Library.csproj src/MPRSGxZ.Library/
RUN dotnet restore src/MPRSGxZ.Web/MPRSGxZ.Web.csproj

COPY src/MPRSGxZ.Web/ src/MPRSGxZ.Web/
COPY src/MPRSGxZ.Library/ src/MPRSGxZ.Library/

RUN dotnet publish src/MPRSGxZ.Web/MPRSGxZ.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Listen on plain HTTP; TLS is expected to be terminated by a reverse proxy in front of the
# container. See AMP_TRUST_PROXY_HEADERS below for forwarded-header handling.
ENV ASPNETCORE_URLS=http://+:8080
# Default to the Production environment; override per deployment if needed (compose/runtime
# env vars take precedence over this image default).
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Default persistence locations. Mount volumes at these paths (see docker-compose.yml) so the
# SQLite database and log files survive container restarts/recreation.
ENV AMP_DATA_DIR=/data
ENV AMP_LOG_DIR=/logs

# Trust X-Forwarded-Proto/X-Forwarded-For from any upstream by default, since the container is
# expected to sit behind a reverse proxy and isn't otherwise reachable directly. Set to "false"
# if exposing the container's port directly without a proxy in front of it.
ENV AMP_TRUST_PROXY_HEADERS=true

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "MPRSGxZ.Web.dll"]
