<#
.SYNOPSIS
    Builds a package of plugin (.voyagerplugin) from a built plugin, and shows its fingerprint (ADR-0016, decision 2).

.DESCRIPTION
    Takes a plugin built by tools/build-plugins.ps1 (artifacts/plugins/<id>/) and writes
    artifacts/packages/<id>-<version>.voyagerplugin: a zip archive with plugin.json and the rest of the plugin at its
    root. Only what a package may hold is put in it, as Voyager checks it at the installation
    (src/dotnet/Voyager.PluginHost/Packages/PackageEntryRules.cs):
    - at the root: plugin.json, the .NET assemblies and their .pdb, .xml and .deps.json files;
    - ui/: .js, .mjs, .css, .json, .svg and .png files;
    - i18n/: .ftl files;
    - data/: files of any kind.
    Any other file is left out, and said so. The package is not signed: no signature key is decided yet (question
    Q31), so an installed plugin is "Not verified".

    The SHA-256 fingerprint printed at the end is the one the confirmation of the installation shows.

.PARAMETER Id
    The identifier of the plugin, such as diagnostics-clock.

.PARAMETER PluginsDirectory
    The folder of the built plugins. Defaults to artifacts/plugins.

.PARAMETER OutputDirectory
    Where to write the package. Defaults to artifacts/packages.

.EXAMPLE
    pwsh tools/build-plugins.ps1
    pwsh tools/pack-plugin.ps1 -Id diagnostics-clock
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z][a-z0-9]*(-[a-z0-9]+)*$')]
    [string] $Id,
    [string] $PluginsDirectory,
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# The repository path may hold characters PowerShell reads as wildcards (such as "[Dev Perso]"):
# every path below is used literally.
$root = (Get-Item -LiteralPath (Join-Path $PSScriptRoot '..')).FullName
if (-not $PluginsDirectory) { $PluginsDirectory = Join-Path $root 'artifacts/plugins' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts/packages' }

$pluginFolder = Join-Path $PluginsDirectory $Id
$manifestPath = Join-Path $pluginFolder 'plugin.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Plugin $Id is not built: there is no plugin.json in $([IO.Path]::GetRelativePath($root, $pluginFolder)). Run pwsh tools/build-plugins.ps1 first."
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.id -ne $Id -or -not $manifest.PSObject.Properties['version'] -or -not $manifest.version) {
    throw "The manifest of $Id has another identifier or no version: build the plugin again with pwsh tools/build-plugins.ps1."
}

# The same list as PackageEntryRules: what a package may hold, at its place.
function Test-AllowedEntry([string] $path) {
    $segments = $path.Split('/')
    if ($segments.Count -eq 1) {
        # Case-sensitive for the manifest, as Voyager reads it: "Plugin.json" is not a manifest.
        return $path -ceq 'plugin.json' -or $path -match '\.(dll|pdb|xml)$' -or $path -like '*.deps.json'
    }
    switch ($segments[0]) {
        'ui' { return $path -match '\.(js|mjs|css|json|svg|png)$' }
        'i18n' { return $path -match '\.ftl$' }
        'data' { return $true }
        default { return $false }
    }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$package = Join-Path $OutputDirectory "$Id-$($manifest.version).voyagerplugin"
Remove-Item -LiteralPath $package -Force -ErrorAction SilentlyContinue

$folderLength = (Get-Item -LiteralPath $pluginFolder).FullName.TrimEnd('\', '/').Length + 1
$files = Get-ChildItem -LiteralPath $pluginFolder -Recurse -File | Sort-Object FullName
$archive = [System.IO.Compression.ZipFile]::Open($package, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        # Entries with forward slashes, relative to the plugin: the format of a package, whatever the system.
        $entryName = $file.FullName.Substring($folderLength).Replace('\', '/')
        if ($file.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            Write-Warning "Left out, a link: $entryName"
            continue
        }
        if (-not (Test-AllowedEntry $entryName)) {
            Write-Warning "Left out, not allowed in a package: $entryName"
            continue
        }
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $file.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

$sha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
# Relative to the repository: a console output pasted somewhere never shows the folders of this computer.
Write-Host "Package: $([IO.Path]::GetRelativePath($root, $package))"
Write-Host "SHA-256: $sha256"
