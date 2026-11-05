# Group server image (ADR-0010). Build context: the repository root.
#   docker build -f deploy/GroupServer.Dockerfile --build-arg VERSION=<version> .

# The interface modules of the shipped plugins: the server publishes them for the desktop app to download (ADR-0020,
# decision 8). Only what they need of the pnpm workspace is installed: the root tools, the plugin SDK and the plugins.
FROM node:22-bookworm-slim AS interface
WORKDIR /src
# pnpm in the version package.json names (packageManager).
RUN corepack enable
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml tsconfig.base.json ./
COPY src/ts/plugin-sdk/ src/ts/plugin-sdk/
COPY plugins/ plugins/
# No install script: the root one generates the gRPC client, which the plugins do not use.
RUN pnpm install --frozen-lockfile --ignore-scripts --filter . --filter ./src/ts/plugin-sdk --filter "./plugins/*/ui"
RUN pnpm --filter "./plugins/*/ui" run build \
    && for dist in plugins/*/ui/dist; do \
         id="$(basename "$(dirname "$(dirname "$dist")")")"; \
         mkdir -p "/interface/$id/ui" && cp -r "$dist/." "/interface/$id/ui/"; \
       done

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# The .git folder is not sent to Docker, so GitVersion cannot run here: the versions come from the caller
# (GitVersion on the host or in CI). "0.0.0-dev" marks a local, unversioned build.
ARG VERSION=0.0.0-dev
# The SDK and each shipped plugin have versions of their own (ADR-0007), computed by tools/get-version.ps1:
# tools/release.ps1 -Docker gives them, the plugins as id=version pairs separated by commas. A plugin it does not
# name takes 0.0.0-dev.
ARG SDK_VERSION=0.0.0-dev
ARG PLUGIN_VERSIONS=
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./
# Voyager.Contracts generates its C# code from the .proto contracts at build time.
COPY proto/ proto/
COPY src/dotnet/ src/dotnet/
RUN dotnet publish src/dotnet/Voyager.GroupServer/Voyager.GroupServer.csproj \
    --configuration Release \
    --output /app \
    -p:DisableGitVersionTask=true \
    -p:Version=${VERSION} \
    -p:SdkVersion=${SDK_VERSION}
# The shipped plugins run in the server (ADR-0020): they go to /app/plugins, the fixed folder the server reads them from,
# with their .NET part and translations built here, and their interface modules built by the Node.js stage above.
COPY plugins/ plugins/
COPY tools/build-plugins.ps1 tools/
RUN pwsh -NoProfile -File tools/build-plugins.ps1 -Version 0.0.0-dev -PluginVersions "${PLUGIN_VERSIONS}" -SkipInterface
COPY --from=interface /interface/ artifacts/plugins/
RUN cp -r artifacts/plugins /app/plugins

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# Inside the container the server listens on every interface; the host decides what to expose.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# The .NET images ship a non-root "app" user.
USER app
ENTRYPOINT ["dotnet", "Voyager.GroupServer.dll"]
