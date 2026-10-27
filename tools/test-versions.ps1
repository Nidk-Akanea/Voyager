<#
.SYNOPSIS
    Checks that every component of Voyager gets the version of its own history (ADR-0007), on develop, release/* and main.

.DESCRIPTION
    Builds a throwaway copy of the repository, gives its own history to the application, the SDK and two plugins (tags,
    then commits that touch one component or another), and checks what tools/get-version.ps1 says in each situation:

    - a component that did not change keeps its version, on develop, on a release branch and on main;
    - a commit in the core changes the application only; a commit in a plugin changes that plugin only;
    - a release branch gives its number to the application, not to the SDK nor to the plugins (GitFlow would otherwise
      make the host refuse every plugin, which are written for the version of the SDK).

    The configurations and the scripts are those of the working tree, so that what is about to be committed is what
    is checked. The repository itself is not touched. It takes a minute or two (GitVersion runs about 30 times).

.EXAMPLE
    pwsh tools/test-versions.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Get-Item -LiteralPath (Join-Path $PSScriptRoot '..')).FullName
$work = Join-Path ([IO.Path]::GetTempPath()) "voyager-versions-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$failures = [System.Collections.Generic.List[string]]::new()

function Invoke-Git {
    git @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed (exit code $LASTEXITCODE)." }
}

# A commit that touches one file: what decides which components it counts for.
function New-TouchCommit {
    param([string] $Path, [string] $Message)
    $full = Join-Path $work $Path
    New-Item -ItemType Directory -Path (Split-Path -Parent $full) -Force | Out-Null
    Set-Content -LiteralPath $full -Value "$Message`n"
    Invoke-Git add -- $Path
    Invoke-Git commit -q -m $Message
}

# Records a failure (it does not stop the test) unless the component has the expected x.y.z and changed state.
function Assert-Component {
    param([string] $Situation, [object[]] $Rows, [string] $Component, [string] $MajorMinorPatch, [bool] $Changed)
    $row = $Rows | Where-Object { $_.Component -eq $Component }
    $actual = "$($row.MajorMinorPatch), changed=$($row.Changed)"
    $expected = "$MajorMinorPatch, changed=$Changed"
    if ($actual -ne $expected) {
        $failures.Add("[$Situation] ${Component}: expected $expected, got $actual (version $($row.Version))")
    }
}

# The versions of every component, in the repository of the copy.
function Get-Versions {
    return @(& (Join-Path $work 'tools/get-version.ps1') -All)
}

try {
    # The long paths option: some folders of the repository are deep, and Windows refuses them otherwise.
    Invoke-Git -c core.longpaths=true clone -q --no-local $root $work
    Set-Location -LiteralPath $work
    Invoke-Git config core.longpaths true
    Invoke-Git config user.email 'versions@example.invalid'
    Invoke-Git config user.name 'Version test'
    Invoke-Git config commit.gpgsign false
    # The history of the copy is its own: the real tags (and their numbers) must not leak into the test.
    git tag --list | ForEach-Object { Invoke-Git tag -d $_ | Out-Null }
    # Nor may its branches: the test makes its own develop, release/* and main (a checkout of the CI has a main), and
    # GitVersion would see the remote branches of the real repository next to them.
    Invoke-Git switch -q --detach
    Invoke-Git remote remove origin
    git for-each-ref --format='%(refname:short)' refs/heads | ForEach-Object { Invoke-Git branch -q -D $_ }
    Invoke-Git switch -q -c develop

    foreach ($file in 'GitVersion.yml', 'tools/get-version.ps1', 'src/dotnet/Voyager.Sdk/GitVersion.yml') {
        Copy-Item -LiteralPath (Join-Path $root $file) -Destination (Join-Path $work $file) -Force
    }
    Invoke-Git add -- GitVersion.yml tools/get-version.ps1 src/dotnet/Voyager.Sdk/GitVersion.yml
    Invoke-Git commit -q --allow-empty -m 'chore: take the configurations of the working tree'

    $plugins = @(Get-ChildItem -LiteralPath (Join-Path $work 'plugins') -Directory |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'plugin.json') } | ForEach-Object { $_.Name })
    if ($plugins.Count -lt 2) { throw 'The test needs two plugins in plugins/.' }
    $first, $second = $plugins[0], $plugins[1]

    # A first release of every component: a tag on a commit that touches it.
    New-TouchCommit 'src/dotnet/Voyager.Core/version-test-1.txt' 'chore: core'
    Invoke-Git tag v0.1.0
    New-TouchCommit 'src/dotnet/Voyager.Sdk/version-test-1.txt' 'chore: sdk'
    Invoke-Git tag sdk/v0.1.0
    foreach ($id in $first, $second) {
        New-TouchCommit "plugins/$id/version-test-1.txt" "chore: $id"
        Invoke-Git tag "plugins/$id/v0.1.0"
    }

    # What main holds once everything is released: the merge of a release is then measured against it.
    $released = (git rev-parse HEAD).Trim()

    $rows = Get-Versions
    Assert-Component 'released, develop' $rows 'sdk' '0.1.0' $false
    Assert-Component 'released, develop' $rows $first '0.1.0' $false
    Assert-Component 'released, develop' $rows $second '0.1.0' $false

    New-TouchCommit 'src/dotnet/Voyager.Core/version-test-2.txt' 'feat: something in the core'
    $rows = Get-Versions
    Assert-Component 'feat in the core, develop' $rows 'app' '0.2.0' $true
    Assert-Component 'feat in the core, develop' $rows 'sdk' '0.1.0' $false
    Assert-Component 'feat in the core, develop' $rows $first '0.1.0' $false
    Assert-Component 'feat in the core, develop' $rows $second '0.1.0' $false

    New-TouchCommit "plugins/$first/version-test-2.txt" "feat: something in $first"
    $rows = Get-Versions
    Assert-Component "feat in $first, develop" $rows 'sdk' '0.1.0' $false
    Assert-Component "feat in $first, develop" $rows $first '0.2.0' $true
    Assert-Component "feat in $first, develop" $rows $second '0.1.0' $false

    # The release branch is named 0.2.0: only what changed may take a new number, and never the one of the branch.
    Invoke-Git switch -q -c release/0.2.0
    $rows = Get-Versions
    Assert-Component 'release/0.2.0' $rows 'app' '0.2.0' $true
    Assert-Component 'release/0.2.0' $rows 'sdk' '0.1.0' $false
    Assert-Component 'release/0.2.0' $rows $first '0.2.0' $true
    Assert-Component 'release/0.2.0' $rows $second '0.1.0' $false

    # main after the merge of the release, before the tags: what the release is about to tag.
    Invoke-Git switch -q -c main $released
    Invoke-Git merge -q --no-ff release/0.2.0 -m 'Merge branch release/0.2.0'
    $rows = Get-Versions
    Assert-Component 'main, release merged' $rows 'app' '0.2.0' $true
    Assert-Component 'main, release merged' $rows 'sdk' '0.1.0' $false
    Assert-Component 'main, release merged' $rows $first '0.2.0' $true
    Assert-Component 'main, release merged' $rows $second '0.1.0' $false
    # MajorMinorPatch above would also match a pre-release: the SDK must be exactly its tag, as it did not change.
    $sdkOnMain = $rows | Where-Object { $_.Component -eq 'sdk' }
    if ($sdkOnMain.Version -ne '0.1.0') {
        $failures.Add("[main, release merged] sdk: expected the exact version 0.1.0, got $($sdkOnMain.Version)")
    }

    # The SDK moves on its own commits: a fix is a patch, a feature a minor version.
    Invoke-Git switch -q develop
    New-TouchCommit 'src/dotnet/Voyager.Sdk/version-test-2.txt' 'fix: something in the SDK'
    $rows = Get-Versions
    Assert-Component 'fix in the SDK, develop' $rows 'sdk' '0.1.1' $true
    Assert-Component 'fix in the SDK, develop' $rows $second '0.1.0' $false
    New-TouchCommit 'src/dotnet/Voyager.Sdk/version-test-3.txt' 'feat: something new in the SDK'
    $rows = Get-Versions
    Assert-Component 'feat in the SDK, develop' $rows 'sdk' '0.2.0' $true
    Assert-Component 'feat in the SDK, develop' $rows $second '0.1.0' $false
}
finally {
    Set-Location -LiteralPath $root
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    throw "$($failures.Count) version check(s) failed."
}
Write-Host 'Every component gets the version of its own history.' -ForegroundColor Green
