<#
.SYNOPSIS
    Builds the plugins of plugins/ into artifacts/plugins/<id>/, ready to be loaded (ADR-0015).

.DESCRIPTION
    For each plugin folder of plugins/ that holds a plugin.json:
    1. publishes its .NET part, without the assemblies the host shares with it (Voyager.Sdk, Voyager.Core,
       NodaTime and the two Microsoft abstractions);
    2. builds its interface module (pnpm) and copies it under ui/;
    3. copies its translations (i18n/);
    4. writes its plugin.json with the version injected: the manifest of the repository has none, a version is
       never written by hand (ADR-0007).

    Each plugin has its own version, computed from its own history (tools/get-version.ps1).

    A plugin is only built again when something it is made of changed since the last build: its own files, the SDKs
    it is written against, the shared build settings, or the version. Otherwise it is left as it is, which is also
    what lets this run while the local service has its assemblies loaded. A plugin whose folder was deleted from
    plugins/ is removed from the output.

    The result is what the packaged app ships in resources/plugins, and what the development environment (Aspire)
    and `pnpm start` give the local service and the user interface in development.

.PARAMETER Version
    The version to write in every manifest, such as 0.2.0-beta.3. Defaults to the version of each plugin, which
    GitVersion computes from the history of that plugin (about 2 seconds each). Development gives a fixed one (such
    as 0.0.0-dev) to skip that calculation.

.PARAMETER Configuration
    Release (the default, what the package ships) or Debug (what development gives, so that the code of a plugin can
    be debugged: no optimisation, and debug files that tell the debugger where the sources are).

.PARAMETER Force
    Builds every plugin again, even those that did not change.

.EXAMPLE
    pwsh tools/build-plugins.ps1

.EXAMPLE
    pwsh tools/build-plugins.ps1 -Version 0.0.0-dev
#>
[CmdletBinding()]
param(
    [string] $Version,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The repository path may hold characters PowerShell reads as wildcards (such as "[Dev Perso]"):
# every path below is used literally.
$root = (Get-Item -LiteralPath (Join-Path $PSScriptRoot '..')).FullName
$pluginsSource = Join-Path $root 'plugins'
$pluginsOutput = Join-Path $root 'artifacts/plugins'
# What each plugin was last built from. Kept apart from the output, which is shipped as it is.
$stampsFolder = Join-Path $root 'artifacts/plugin-stamps'

# The assemblies the host shares with the plugins: a copy in a plugin folder would never be loaded.
$sharedAssemblies = @(
    'Voyager.Sdk', 'Voyager.Core', 'NodaTime',
    'Microsoft.Extensions.DependencyInjection.Abstractions', 'Microsoft.Extensions.Logging.Abstractions'
)

# What every plugin is written against or built with, besides its own files.
$sharedInputs = @(
    'src/dotnet/Voyager.Sdk', 'src/dotnet/Voyager.Core', 'src/ts/plugin-sdk/src',
    'Directory.Build.props', 'Directory.Packages.props', 'global.json', 'pnpm-lock.yaml'
)

function Invoke-Step {
    param([string] $Title, [scriptblock] $Action)
    Write-Host "==> $Title" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed: $Title (exit code $LASTEXITCODE)."
    }
}

# Fails unless the value looks like a version; the owner says where it comes from.
function Assert-Version {
    param([string] $Value, [string] $Owner)
    if ($Value -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
        throw "Not a version: '$Value' ($Owner). Expected x.y.z, optionally followed by a suffix, such as 0.2.0-beta.3."
    }
}

# Whether a manifest declares a part of the plugin ("dotnet", "ui"). Under strict mode a missing property is an error.
function Test-ManifestPart {
    param([object] $Manifest, [string] $Name)
    return $Manifest.PSObject.Properties.Name -contains $Name
}

# The files of a folder or a single file, without what a build produces.
function Get-InputFile {
    param([string] $Path)
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        return Get-Item -LiteralPath $Path
    }
    return Get-ChildItem -LiteralPath $Path -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|dist|node_modules)[\\/]' }
}

# A fingerprint of everything a plugin is built from: it changes when any of it, the version or the configuration does.
function Get-Fingerprint {
    param([string] $PluginSource, [string] $PluginVersion)
    $lines = @("version $PluginVersion", "configuration $Configuration")
    foreach ($path in @($PluginSource) + ($sharedInputs | ForEach-Object { Join-Path $root $_ })) {
        if (-not (Test-Path -LiteralPath $path)) { continue }
        foreach ($file in (Get-InputFile $path | Sort-Object FullName)) {
            $relative = $file.FullName.Substring($root.Length)
            $lines += "$relative $((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)"
        }
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    return [BitConverter]::ToString([Security.Cryptography.SHA256]::HashData($bytes)).Replace('-', '')
}

# Windows keeps the assemblies of a plugin locked while the local service that loaded them runs.
function Remove-PluginOutput {
    param([string] $Path)
    try {
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
    }
    catch [System.Management.Automation.ItemNotFoundException] {
        # Nothing built yet: nothing to clean.
    }
    catch {
        throw "A plugin could not be replaced ($Path): stop the local service (or Voyager) that uses it, then run this again. $($_.Exception.Message)"
    }
}

Set-Location -LiteralPath $root

if ($Version) {
    Assert-Version $Version 'the -Version parameter'
}

New-Item -ItemType Directory -Path $pluginsOutput -Force | Out-Null
New-Item -ItemType Directory -Path $stampsFolder -Force | Out-Null

$manifests = Get-ChildItem -LiteralPath $pluginsSource -Directory |
    ForEach-Object { Join-Path $_.FullName 'plugin.json' } |
    Where-Object { Test-Path -LiteralPath $_ }

$builtIds = @()
foreach ($manifestPath in $manifests) {
    $source = Split-Path -Parent $manifestPath
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $id = $manifest.id
    if ((Split-Path -Leaf $source) -ne $id) {
        throw "The folder $(Split-Path -Leaf $source) does not have the identifier of its manifest ($id)."
    }
    $builtIds += $id
    $pluginVersion = $Version
    if (-not $pluginVersion) {
        Write-Host "==> Compute the version of $id (GitVersion)" -ForegroundColor Cyan
        $pluginVersion = (& (Join-Path $PSScriptRoot 'get-version.ps1') -Component $id | Select-Object -Last 1).Trim()
        Assert-Version $pluginVersion "GitVersion, plugin $id"
    }
    $output = Join-Path $pluginsOutput $id
    $stamp = Join-Path $stampsFolder "$id.fingerprint"

    $fingerprint = Get-Fingerprint $source $pluginVersion
    $upToDate = -not $Force -and (Test-Path -LiteralPath (Join-Path $output 'plugin.json')) -and
        (Test-Path -LiteralPath $stamp) -and ((Get-Content -LiteralPath $stamp -Raw).Trim() -eq $fingerprint)
    if ($upToDate) {
        Write-Host "Plugin $id $pluginVersion is up to date" -ForegroundColor DarkGray
        continue
    }

    Write-Host "Plugin $id $pluginVersion" -ForegroundColor Green
    Remove-Item -LiteralPath $stamp -Force -ErrorAction SilentlyContinue
    Remove-PluginOutput $output

    if (Test-ManifestPart $manifest 'dotnet') {
        Invoke-Step "Publish the .NET part of $id" {
            $project = Get-ChildItem -LiteralPath (Join-Path $source 'dotnet') -Filter '*.csproj' -Recurse | Select-Object -First 1
            # What ships keeps the paths of this machine out of the debug files (.pdb); a Debug build keeps them, so
            # that the debugger finds the sources.
            $shipped = if ($Configuration -eq 'Release') { 'true' } else { 'false' }
            dotnet publish $project.FullName --configuration $Configuration `
                -p:DisableGitVersionTask=true "-p:ContinuousIntegrationBuild=$shipped" "-p:Version=$pluginVersion" --output $output
        }
        foreach ($shared in $sharedAssemblies) {
            # The assembly with its debug and documentation files.
            Get-ChildItem -LiteralPath $output -Filter "$shared.*" | Remove-Item -Force
        }
    }

    if (Test-ManifestPart $manifest 'ui') {
        Invoke-Step "Build the interface module of $id" {
            pnpm --filter "./plugins/$id/ui" run build
        }
        $uiOutput = Join-Path $output 'ui'
        New-Item -ItemType Directory -Path $uiOutput -Force | Out-Null
        # LiteralPath: the repository folder may hold characters that -Path would read as wildcards.
        Get-ChildItem -LiteralPath (Join-Path $source 'ui/dist') | Copy-Item -Destination $uiOutput -Recurse
    }

    $translations = Join-Path $source 'i18n'
    if (Test-Path -LiteralPath $translations) {
        Copy-Item -LiteralPath $translations -Destination (Join-Path $output 'i18n') -Recurse
    }

    New-Item -ItemType Directory -Path $output -Force | Out-Null
    # The manifest of the repository has no version: it is the one of the build that is written, in the same order.
    $built = [ordered] @{ id = $manifest.id; version = $pluginVersion }
    foreach ($property in $manifest.PSObject.Properties) {
        if ($property.Name -notin @('id', 'version')) {
            $built[$property.Name] = $property.Value
        }
    }
    $built | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'plugin.json') -Encoding utf8NoBOM
    # Written last: a build that failed half way leaves no stamp, and is done again.
    Set-Content -LiteralPath $stamp -Value $fingerprint -Encoding utf8NoBOM
}

# A plugin deleted from plugins/ must not stay in what is loaded or shipped.
foreach ($leftover in (Get-ChildItem -LiteralPath $pluginsOutput -Directory | Where-Object { $_.Name -notin $builtIds })) {
    Remove-PluginOutput $leftover.FullName
    Remove-Item -LiteralPath (Join-Path $stampsFolder "$($leftover.Name).fingerprint") -Force -ErrorAction SilentlyContinue
}

Write-Host "Done. Plugins: $pluginsOutput" -ForegroundColor Green
