# sync-archived-readme.ps1 - mirror the current README into the archived repo.
#
# The archived original repo (dparksports/autocommand-windows, frozen at
# v2026.10.25.4) must keep showing users the CURRENT app's readme, but with two
# edits GitHub cannot do itself: a short notice on top pointing to the new repo
# for the latest download, and every download/asset link repointed to the new
# repo (relative links would otherwise resolve against the archived tree and
# serve stale images and docs, and its Releases page stopped at the freeze).
#
# Called by release.ps1 after the GitHub release is published, so the mirror can
# never drift from the real readme. Safe to run standalone any time.
#
# Usage: powershell -File sync-archived-readme.ps1 [-ArchiveRepoPath <path>]
#
# NOTE: keep this file pure ASCII (same reason as release.ps1 - Windows
# PowerShell 5.1 reads BOM-less scripts as ANSI).

[CmdletBinding()]
param(
    [string]$ArchiveRepoPath
)

$ErrorActionPreference = 'Stop'

# $PSScriptRoot is not available inside param() defaults on Windows PowerShell
# 5.1, so the default is resolved here in the body. The archived repo lives in
# the workspace's archive\ folder - a sibling of this repo, not inside it.
if (-not $ArchiveRepoPath) {
    $ArchiveRepoPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'archive\autocommand1004'
}

function Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Die($msg)  { Write-Host "ERROR: $msg" -ForegroundColor Red; exit 1 }

$NewOwner = 'dparksports/autocommand-ii'
$NewRaw   = "https://raw.githubusercontent.com/$NewOwner/master"
$NewBlob  = "https://github.com/$NewOwner/blob/master"
$NewReleases = "https://github.com/$NewOwner/releases/latest"

if (-not (Test-Path (Join-Path $ArchiveRepoPath '.git'))) {
    Die "archived repo not found at $ArchiveRepoPath (pass -ArchiveRepoPath)"
}

# -- read the source readme and the archived repo's version -------------------
$md = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'README.md'))
$csproj = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'AutoCommand.csproj'))
$version = 'unknown'
if ($csproj -match '<Version>([^<]+)</Version>') { $version = $Matches[1] }

# -- transform: repoint every relative reference at the new repo ---------------
# Images go to raw.githubusercontent (renders inline); everything else to the
# blob page; the Releases link goes to the new repo's latest-release page.
$md = [regex]::Replace($md, '\]\(([^)]+)\)', {
    param($m)
    $t = $m.Groups[1].Value
    if ($t -match '^(https?://|#|mailto:)') { return $m.Value }
    if ($t -eq '../../releases') { return '](' + $NewReleases + ')' }
    if ($t -match '\.(png|jpe?g|gif|svg|webp)(\?|$)') {
        return '](' + $NewRaw + '/' + $t + ')'
    }
    return '](' + $NewBlob + '/' + $t + ')'
})

# -- short notice on top, per the archive policy -------------------------------
# The rocket emoji is composed from its codepoint so this script stays pure
# ASCII (see NOTE at top).
$truck = [char]::ConvertFromUtf32(0x1F69A)
$notice = @(
    "> **$truck AutoCommand has moved.** This repository is archived and kept for history only.",
    "> The readme below is a **live mirror** of the current project at **[$NewOwner](https://github.com/$NewOwner)**.",
    "> **Download the latest build from the [new Releases page]($NewReleases)** - releases on this repo stopped at v2026.10.25.4."
) -join "`n"
$out = $notice + "`n`n" + $md

# -- write + commit + push, only when something actually changed ----------------
$readmePath = Join-Path $ArchiveRepoPath 'README.md'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$old = if (Test-Path $readmePath) { [IO.File]::ReadAllText($readmePath) } else { '' }
if ($old -eq $out) {
    Step "Archived README already up to date (v$Version) - nothing to push"
    exit 0
}
[IO.File]::WriteAllText($readmePath, $out, $utf8NoBom)

Step "Committing mirrored README (from autocommand-ii v$Version)"
git -C $ArchiveRepoPath add README.md
if ($LASTEXITCODE -ne 0) { Die 'git add' }
git -C $ArchiveRepoPath commit -m "readme mirror: sync from autocommand-ii master (v$Version) - notice on top, downloads point to the new repo"
if ($LASTEXITCODE -ne 0) { Die 'git commit (no change or failure)' }

Step 'Pushing to the archived repo'
git -C $ArchiveRepoPath push origin master
if ($LASTEXITCODE -ne 0) { Die 'git push' }

Step "Mirrored README published at https://github.com/dparksports/autocommand-windows#autocommand-archived"
