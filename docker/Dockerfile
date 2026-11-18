# syntax=docker/dockerfile:1

# Build from the repository root:
#   docker build -f docker/Dockerfile -t fibotrail .

# Stage 1 builds the web app with Node.js. Its output is plain files, the same for every platform.
FROM --platform=$BUILDPLATFORM node:22-slim AS web
WORKDIR /web

# Install first, so a change to the code doesn't download the packages again.
COPY apps/web/package.json apps/web/package-lock.json ./
RUN npm ci

COPY apps/web/ ./
RUN npm run build

# Stage 2 builds the server with the .NET SDK.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /source

# Restore first, so a change to the code doesn't download the packages again.
COPY .editorconfig apps/server/global.json apps/server/Directory.Build.props ./
COPY apps/server/src/Fibotrail.Detection/Fibotrail.Detection.csproj src/Fibotrail.Detection/
COPY apps/server/src/Fibotrail.Immich/Fibotrail.Immich.csproj src/Fibotrail.Immich/
COPY apps/server/src/Fibotrail.Mcp/Fibotrail.Mcp.csproj src/Fibotrail.Mcp/
COPY apps/server/src/Fibotrail.Places/Fibotrail.Places.csproj src/Fibotrail.Places/
COPY apps/server/src/Fibotrail.Stats/Fibotrail.Stats.csproj src/Fibotrail.Stats/
COPY apps/server/src/Fibotrail.Server/Fibotrail.Server.csproj src/Fibotrail.Server/
RUN dotnet restore src/Fibotrail.Server -a "$TARGETARCH"

COPY apps/server/src/ src/
RUN dotnet publish src/Fibotrail.Server -c Release -a "$TARGETARCH" --no-restore -o /app

# Stage 3 is the image that runs. It holds the ASP.NET Core runtime only.
FROM mcr.microsoft.com/dotnet/aspnet:10.0

# The release workflow passes the version, such as 0.1.0. /healthz reports it.
ARG VERSION=dev

# Inside the container, the server listens on port 8080.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    FIBOTRAIL_VERSION=$VERSION

WORKDIR /app
COPY --from=build /app ./
COPY --from=web /web/dist/ ./wwwroot/

# The collection files: YAML, and a GeoJSON boundary for each place.
COPY collections/ ./collections/

# /data is the one writable folder. "fibotrail" on the path is what
# "docker exec -it fibotrail fibotrail reset-password" runs.
RUN mkdir /data \
    && chown "$APP_UID" /data \
    && ln -s /app/Fibotrail.Server /usr/local/bin/fibotrail

# The base image provides this non-root user.
USER $APP_UID
VOLUME /data
EXPOSE 8080

# The image has no curl or wget, so the server checks itself.
HEALTHCHECK --interval=30s --timeout=10s --start-period=20s --retries=3 CMD ["fibotrail", "healthcheck"]

ENTRYPOINT ["fibotrail"]
