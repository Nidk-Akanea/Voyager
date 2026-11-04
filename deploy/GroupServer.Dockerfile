# Group server image (ADR-0010). Build context: the repository root.
#   docker build -f deploy/GroupServer.Dockerfile --build-arg VERSION=<version> .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# The .git folder is not sent to Docker, so GitVersion cannot run here: the versions come from the caller
# (GitVersion on the host or in CI). "0.0.0-dev" marks a local, unversioned build.
ARG VERSION=0.0.0-dev
# The SDK and the shipped plugins have versions of their own (ADR-0007), computed by tools/get-version.ps1.
ARG SDK_VERSION=0.0.0-dev
ARG PLUGINS_VERSION=0.0.0-dev
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
# The shipped plugins run in the server (ADR-0020): their .NET part goes to /app/plugins, the fixed folder the server
# reads them from. Their interface modules need Node.js, which this image does not have: they come with the desktop app.
COPY plugins/ plugins/
COPY tools/build-plugins.ps1 tools/
RUN pwsh -NoProfile -File tools/build-plugins.ps1 -Version ${PLUGINS_VERSION} -SkipInterface \
    && cp -r artifacts/plugins /app/plugins

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# Inside the container the server listens on every interface; the host decides what to expose.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# The .NET images ship a non-root "app" user.
USER app
ENTRYPOINT ["dotnet", "Voyager.GroupServer.dll"]
