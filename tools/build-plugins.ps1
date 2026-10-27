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

    The result is what the packaged app ships in resources/plugins, and what the development environment (Aspire)
    and `pnpm start` give the local service and the user interface in development.

.PARAMETER Version
    The version to write in every manifest, such as 0.2.0-beta.3. Defaults to the one GitVersion computes for the
    current branch. (A version for each plugin, from its own history, comes with the per-component versions.)

.EXAMPLE
    pwsh tools/build-plugins.ps1
#>
[CmdletBinding()]
param(
    [string] $Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The repository path may hold characters PowerShell reads as wildcards (such as "[Dev Perso]"):
# every path below is used literally.
$root = (Get-Item -LiteralPath (Join-Path $PSScriptRoot '..')).FullName
$pluginsSource = Join-Path $root 'plugins'
$pluginsOutput = Join-Path $root 'artifacts/plugins'

# The assemblies the host shares with the plugins: a copy in a plugin folder would never be loaded.
$sharedAssemblies = @(
    'Voyager.Sdk', 'Voyager.Core', 'NodaTime',
    'Microsoft.Extensions.DependencyInjection.Abstractions', 'Microsoft.Extensions.Logging.Abstractions'
)

function Invoke-Step {
    param([string] $Title, [scriptblock] $Action)
    Write-Host "==> $Title" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed: $Title (exit code $LASTEXITCODE)."
    }
}

# Whether a manifest declares a part of the plugin ("dotnet", "ui"). Under strict mode a missing property is an error.
function Test-ManifestPart {
    param([object] $Manifest, [string] $Name)
    return $Manifest.PSObject.Properties.Name -contains $Name
}

Set-Location -LiteralPath $root

if (-not $Version) {
    Invoke-Step 'Compute the version (GitVersion)' {
        # The repository is passed explicitly: GitVersion does not find it from this folder otherwise.
        $script:Version = (dnx GitVersion.Tool --yes -- $root /showvariable SemVer | Select-Object -Last 1).Trim()
    }
}
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    throw "Not a version: '$Version'. Expected x.y.z, optionally followed by a suffix, such as 0.2.0-beta.3."
}

try {
    Remove-Item -LiteralPath $pluginsOutput -Recurse -Force -ErrorAction Stop
}
catch [System.Management.Automation.ItemNotFoundException] {
    # Nothing built yet: nothing to clean.
}
catch {
    # Windows keeps the assemblies of the plugins locked while the local service that loaded them runs.
    throw "The plugins folder could not be cleaned ($pluginsOutput): stop the local service (or Voyager) that uses it, then run this again. $($_.Exception.Message)"
}
New-Item -ItemType Directory -Path $pluginsOutput -Force | Out-Null

$manifests = Get-ChildItem -LiteralPath $pluginsSource -Directory |
    ForEach-Object { Join-Path $_.FullName 'plugin.json' } |
    Where-Object { Test-Path -LiteralPath $_ }

foreach ($manifestPath in $manifests) {
    $source = Split-Path -Parent $manifestPath
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $id = $manifest.id
    if ((Split-Path -Leaf $source) -ne $id) {
        throw "The folder $(Split-Path -Leaf $source) does not have the identifier of its manifest ($id)."
    }
    $output = Join-Path $pluginsOutput $id
    Write-Host "Plugin $id $Version" -ForegroundColor Green

    if (Test-ManifestPart $manifest 'dotnet') {
        Invoke-Step "Publish the .NET part of $id" {
            $project = Get-ChildItem -LiteralPath (Join-Path $source 'dotnet') -Filter '*.csproj' -Recurse | Select-Object -First 1
            # ContinuousIntegrationBuild keeps the paths of this machine out of the debug files (.pdb).
            dotnet publish $project.FullName --configuration Release `
                -p:DisableGitVersionTask=true -p:ContinuousIntegrationBuild=true "-p:Version=$Version" --output $output
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
    $built = [ordered] @{ id = $manifest.id; version = $Version }
    foreach ($property in $manifest.PSObject.Properties) {
        if ($property.Name -notin @('id', 'version')) {
            $built[$property.Name] = $property.Value
        }
    }
    $built | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'plugin.json') -Encoding utf8NoBOM
}

Write-Host "Done. Plugins: $pluginsOutput" -ForegroundColor Green
