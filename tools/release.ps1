<#
.SYNOPSIS
    Builds the installable Voyager app for Windows (ADR-0009).

.DESCRIPTION
    1. computes the version of the application with GitVersion (ADR-0007), unless -Version gives one;
    2. builds the plugins (tools/build-plugins.ps1, each at its own version), the user interface and the desktop
       app;
    3. assembles the app folder (asar, user interface, interface modules of the shipped plugins, Electron fuses) and
       packs it with Velopack (vpk) into an installer and update packages;
    4. with -Docker, builds the image of the group server, which runs the plugins (ADR-0018).

    Everything lands in artifacts/ (ignored by git). Nothing is published anywhere: sending the packages
    to a GitHub release is a separate step, done by hand until the first release.

.PARAMETER Version
    The version of the application to build, such as 0.2.0-beta.3. Defaults to the one GitVersion computes for the
    current branch. The SDK and each plugin keep the version of their own history, whatever this is.

.PARAMETER UpdateSource
    The https address (or http://localhost...) of a folder served by a web server, where the app built
    looks for updates instead of the GitHub releases. This is how an update is tried without publishing
    anything: build a version, copy artifacts/release/ to that folder, install it, then build a newer
    version and copy it there too. The address is written in the package, never in the repository.

.PARAMETER Docker
    Also builds the Docker image of the group server (needs Docker).

.EXAMPLE
    pwsh tools/release.ps1

.EXAMPLE
    pwsh tools/release.ps1 -Version 0.2.0 -UpdateSource http://localhost:8080/
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $UpdateSource,
    [switch] $Docker
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The repository path may hold characters PowerShell reads as wildcards (such as "[Dev Perso]"):
# every path below is used literally.
$root = (Get-Item -LiteralPath (Join-Path $PSScriptRoot '..')).FullName
$artifacts = Join-Path $root 'artifacts'
$desktop = Join-Path $root 'src/ts/desktop'

function Invoke-Step {
    param([string] $Title, [scriptblock] $Action)
    Write-Host "==> $Title" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed: $Title (exit code $LASTEXITCODE)."
    }
}

Set-Location -LiteralPath $root

if (-not $Version) {
    Invoke-Step 'Compute the version (GitVersion)' {
        $script:Version = (& (Join-Path $PSScriptRoot 'get-version.ps1') -Component app | Select-Object -Last 1).Trim()
    }
}
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    throw "Not a version: '$Version'. Expected x.y.z, optionally followed by a suffix, such as 0.2.0-beta.3."
}
# main gives stable versions, release/* and hotfix/* beta ones; develop ones are for local trials.
$channel = if ($Version -notmatch '-') { 'stable' } elseif ($Version -match '-beta\.\d+$') { 'beta' } else { 'alpha' }
Write-Host "Version $Version, channel $channel"
Write-Host $(if ($UpdateSource) { "Updates come from $UpdateSource" } else { 'Updates come from the GitHub releases' })

Invoke-Step 'Install the TypeScript dependencies' { pnpm install --frozen-lockfile }
Invoke-Step 'Build the user interface and the desktop app' { pnpm build }

# The interface modules of the plugins ship with the app, each at its own version, computed from its own history
# (ADR-0007): the app uses them instead of a download when a server runs the same version.
Invoke-Step 'Build the plugins' { & (Join-Path $PSScriptRoot 'build-plugins.ps1') }

Invoke-Step 'Assemble the app folder' {
    Push-Location -LiteralPath $desktop
    try {
        $packageArguments = @('--version', $Version, '--ui', (Join-Path $root 'src/ts/ui/dist'),
            '--plugins', (Join-Path $artifacts 'plugins'), '--out', $artifacts)
        if ($UpdateSource) {
            $packageArguments += @('--update-source', $UpdateSource)
        }
        node dist-tools/packageApp.mjs @packageArguments
    }
    finally {
        Pop-Location
    }
}
$appFolder = Join-Path $artifacts 'app/Voyager-win32-x64'

Invoke-Step 'Pack the installer and the update packages (Velopack)' {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { return }
    $release = Join-Path $artifacts 'release'
    # A package of an earlier run with the same version would stop vpk; the first release will download
    # the previous ones here instead, for the delta packages.
    Remove-Item -LiteralPath $release -Recurse -Force -ErrorAction SilentlyContinue
    # The pack id names the install folder (%LOCALAPPDATA%\VoyagerApp), which Velopack empties on
    # uninstall: the settings, the downloaded interface modules and the logs live next to it, in
    # %LOCALAPPDATA%\Voyager, and stay.
    dotnet tool run vpk -- pack --packId VoyagerApp --packTitle Voyager --packVersion $Version `
        --packDir $appFolder --mainExe Voyager.exe --runtime win-x64 --channel $channel --outputDir $release
}

if ($Docker) {
    # The SDK inside the server has its own version, not the one of the app (ADR-0007).
    Invoke-Step 'Compute the version of the SDK (GitVersion)' {
        $script:sdkVersion = (& (Join-Path $PSScriptRoot 'get-version.ps1') -Component sdk | Select-Object -Last 1).Trim()
    }
    # GitVersion cannot run inside Docker: each plugin gets the version of its own history from here (ADR-0007).
    Invoke-Step 'Compute the versions of the shipped plugins (GitVersion)' {
        $script:pluginVersions = (Get-ChildItem -LiteralPath (Join-Path $root 'plugins') -Directory |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'plugin.json') } |
            ForEach-Object {
                $pluginVersion = (& (Join-Path $PSScriptRoot 'get-version.ps1') -Component $_.Name | Select-Object -Last 1).Trim()
                "$($_.Name)=$pluginVersion"
            }) -join ','
    }
    Invoke-Step 'Build the group server image' {
        docker build --file (Join-Path $root 'deploy/GroupServer.Dockerfile') `
            --build-arg "VERSION=$Version" --build-arg "SDK_VERSION=$sdkVersion" --build-arg "PLUGIN_VERSIONS=$pluginVersions" `
            --tag "voyager-group-server:$Version" $root
    }
}

Write-Host "Done. Installer and packages: $(Join-Path $artifacts 'release')" -ForegroundColor Green
