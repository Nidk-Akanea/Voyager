<#
.SYNOPSIS
    Computes the version of a component of Voyager with GitVersion (ADR-0007).

.DESCRIPTION
    Voyager has one version number per component, each computed from its own history and never written by hand:

    - app: the application (service, group server, user interface), tags v<x.y.z>. Commits that only touch
      plugins/ or docs/ do not count. Configuration: GitVersion.yml.
    - sdk: the plugin SDK (Voyager.Sdk, @voyager/plugin-sdk), tags sdk/v<x.y.z>. Only the commits that touch the SDK
      count. Configuration: src/dotnet/Voyager.Sdk/GitVersion.yml, the file the build of Voyager.Sdk reads too, so
      that the assembly carries this version (the host checks the plugins against it).
    - a plugin id such as diagnostics-clock: tags plugins/<id>/v<x.y.z>. Only the commits that touch plugins/<id>/
      count. The configuration is the one of the SDK with that tag prefix and that path filter, made on the fly: a
      new plugin needs no file.

    The rules that turn Conventional Commits into increments are the same for all (GitVersion.yml). The SDK and the
    plugins add to them that a component takes its number from its own tags and its own commits alone, never from the
    branch: on develop, release/* and hotfix/*, GitFlow would otherwise give every component the number of the
    application or of the release branch. The application has no such rule: its releases follow the branches.

.PARAMETER Component
    app (the default), sdk, or the identifier of a plugin of plugins/.

.PARAMETER Variable
    The GitVersion variable to print, such as SemVer (the default), MajorMinorPatch or FullSemVer.

.PARAMETER All
    Outputs a row for every component instead: its version, the x.y.z of that version (what a release tags, once
    the release branch is merged into main) and whether it changed since its last tag (a release tags only those).

.EXAMPLE
    pwsh tools/get-version.ps1 -Component diagnostics-clock

.EXAMPLE
    pwsh tools/get-version.ps1 -All
#>
[CmdletBinding(DefaultParameterSetName = 'One')]
param(
    [Parameter(ParameterSetName = 'One')]
    [string] $Component = 'app',
    [Parameter(ParameterSetName = 'One')]
    [string] $Variable = 'SemVer',
    [Parameter(ParameterSetName = 'All')]
    [switch] $All
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The repository path may hold characters PowerShell reads as wildcards (such as "[Dev Perso]"):
# every path below is used literally.
$root = (Get-Item -LiteralPath (Join-Path $PSScriptRoot '..')).FullName
$pluginsSource = Join-Path $root 'plugins'
$appConfig = Join-Path $root 'GitVersion.yml'
# Also the template of the configuration of a plugin: the rules of a component that is versioned apart.
$sdkConfig = Join-Path $root 'src/dotnet/Voyager.Sdk/GitVersion.yml'

# The same version as GitVersion.MsBuild (Directory.Packages.props): a test checks it, so that the numbers the build
# stamps and the ones this script gives never differ.
$gitVersionTool = 'GitVersion.Tool@6.8.2'

function Get-PluginId {
    return Get-ChildItem -LiteralPath $pluginsSource -Directory |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'plugin.json') } |
        ForEach-Object { $_.Name }
}

# The configuration of a plugin: the one of the SDK, with the tag prefix and the paths of the plugin.
function New-PluginConfig {
    param([string] $Id)
    # Case-sensitive: Git is, and so are the tag prefix and the paths below.
    if ($Id -cnotmatch '^[a-z][a-z0-9]*(-[a-z0-9]+)*$' -or -not (Test-Path -LiteralPath (Join-Path $pluginsSource $Id))) {
        throw "Unknown component '$Id': expected app, sdk or the identifier of a plugin of plugins/ ($((Get-PluginId) -join ', '))."
    }
    $text = Get-Content -LiteralPath $sdkConfig -Raw
    # What belongs to the SDK alone: its tag prefix and its ignored paths (a block that ends at the next key).
    $text = $text -replace '(?m)^tag-prefix:.*\r?\n', ''
    $text = $text -replace '(?ms)^ignore:.*?(?=^\S|\z)', ''
    # The prefix is optional as a whole: GitVersion reads next-version with it, and "0.1.0" has no prefix.
    $text = $text.TrimEnd() + "`ntag-prefix: '(plugins/$Id/v)?'`nignore:`n  paths:`n    - '^(?!plugins/$Id/).*'`n"
    $folder = Join-Path $root 'artifacts/plugin-stamps'
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $path = Join-Path $folder "gitversion-$Id.yml"
    Set-Content -LiteralPath $path -Value $text -Encoding utf8NoBOM
    return $path
}

# GitVersion's variables of a component, as an object.
function Get-ComponentVariable {
    param([string] $Name)
    $config = switch ($Name) {
        'app' { $appConfig }
        'sdk' { $sdkConfig }
        default { New-PluginConfig $Name }
    }
    # The repository is passed explicitly: GitVersion does not find it from this folder otherwise. Its log goes to
    # the error stream; the standard output is only the variables.
    $output = dnx $gitVersionTool --yes -- $root /config $config /output json 2>&1
    $exitCode = $LASTEXITCODE
    $errors = ($output | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | ForEach-Object { $_.ToString() }) -join "`n"
    $json = ($output | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] }) -join "`n"
    $start = $json.IndexOf('{')
    if ($exitCode -ne 0 -or $start -lt 0) {
        $detail = if ($errors -match 'No commits found') {
            ' It has no commit yet: commit it first (or, for a build, give a version with -Version).'
        } else {
            " $errors"
        }
        throw "GitVersion could not compute the version of '$Name'.$detail"
    }
    return $json.Substring($start) | ConvertFrom-Json
}

if ($All) {
    $components = @('app', 'sdk') + @(Get-PluginId)
    $rows = foreach ($name in $components) {
        $variables = Get-ComponentVariable $name
        # No tag yet (an empty source) counts as changed: the first release tags it.
        $changed = [string]::IsNullOrEmpty($variables.VersionSourceSha) -or $variables.CommitsSinceVersionSource -gt 0
        [pscustomobject] @{
            Component = $name
            Version = $variables.SemVer
            MajorMinorPatch = $variables.MajorMinorPatch
            Changed = $changed
        }
    }
    return $rows
}

$variables = Get-ComponentVariable $Component
if ($null -eq $variables.PSObject.Properties[$Variable]) {
    throw "GitVersion has no variable '$Variable'."
}
Write-Output $variables.$Variable
