# Group server image (ADR-0010). Build context: the repository root.
#   docker build -f deploy/GroupServer.Dockerfile --build-arg VERSION=<version> .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# The .git folder is not sent to Docker, so GitVersion cannot run here: the version comes from the
# caller (GitVersion on the host or in CI). "0.0.0-dev" marks a local, unversioned build.
ARG VERSION=0.0.0-dev
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/dotnet/ src/dotnet/
RUN dotnet publish src/dotnet/Voyager.GroupServer/Voyager.GroupServer.csproj \
    --configuration Release \
    --output /app \
    -p:DisableGitVersionTask=true \
    -p:Version=${VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# Inside the container the server listens on every interface; the host decides what to expose.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# The .NET images ship a non-root "app" user.
USER app
ENTRYPOINT ["dotnet", "Voyager.GroupServer.dll"]
