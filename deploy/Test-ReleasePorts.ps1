#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Lists commits on release/vX.Y branches that have not reached origin/main.

.DESCRIPTION
    A commit on a release branch (since it left main) counts as ported when ANY of:
      1. `git cherry` marks it '-'  (the same patch is already in main);
      2. a commit on origin/main says "cherry picked from commit <this full sha>";
      3. its own message says "cherry picked from commit <X>" and X is reachable from origin/main
         (it was picked FROM main into the release branch);
      4. its message has a line starting "No-Port:" (intentional: release-only revert, or fixed
         differently on main).

    Exit code: 0 = nothing unported, 1 = unported commits found, 2 = the check itself failed.

.PARAMETER RepoPath  Repo to check. Default: the repo containing this script.
.PARAMETER Branch    Refs to check (any ref works, e.g. a local test branch). Default: every origin/release/v* ref.
.PARAMETER Fetch     Run `git fetch origin --prune` first.
.PARAMETER AsJson    Emit the unported commits as a JSON array on stdout (no other output).

.EXAMPLE
    pwsh deploy/Test-ReleasePorts.ps1 -Fetch
    pwsh deploy/Test-ReleasePorts.ps1 -RepoPath ../collateral-appraisal-system-app -Branch origin/release/v6.1
#>
[CmdletBinding()]
param(
    [string]$RepoPath = (Split-Path -Parent $PSScriptRoot),   # deploy/ -> repo root
    [string[]]$Branch,
    [switch]$Fetch,
    [switch]$AsJson
)

$ErrorActionPreference = 'Stop'
$main = 'origin/main'
$pickRx = 'cherry picked from commit ([0-9a-f]{40})'

function Invoke-Git {
    $out = & git -C $RepoPath @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed in $RepoPath (exit $LASTEXITCODE)" }
    $out
}

function Test-Git {   # true when the git command exits 0; output is discarded
    & git -C $RepoPath @args 2>$null | Out-Null
    $LASTEXITCODE -eq 0
}

function Get-Unported {
    if ($Fetch) { Invoke-Git fetch origin --prune --quiet | Out-Null }
    if (-not (Test-Git rev-parse --verify --quiet "$main^{commit}")) {
        throw "$main not found in $RepoPath (use -Fetch, or check the path)."
    }

    $url  = @(Invoke-Git remote get-url origin)[0]
    $repo = (($url -replace '\.git$', '').TrimEnd('/') -split '[/:]')[-1]

    if (-not $Branch) {
        $Branch = @(Invoke-Git for-each-ref '--format=%(refname:short)' refs/remotes/origin/release/ |
            Where-Object { $_ -match '^origin/release/v\d' })
    }

    # Full shas that some main commit declares it was cherry-picked from (rule 2).
    $mainPicks = [System.Collections.Generic.HashSet[string]]::new()
    $mainLog = (@(Invoke-Git log $main '--grep=cherry picked from commit' '--format=%B')) -join "`n"
    foreach ($m in [regex]::Matches($mainLog, $pickRx)) { [void]$mainPicks.Add($m.Groups[1].Value) }

    $RS = [char]0x1e; $US = [char]0x1f
    foreach ($b in $Branch) {
        if (-not (Test-Git rev-parse --verify --quiet "$b^{commit}")) {
            throw "Branch '$b' not found in $repo. Is the release branch cut in this repo, and did you fetch?"
        }
        $base = @(Invoke-Git merge-base $main $b)[0]

        $samePatch = [System.Collections.Generic.HashSet[string]]::new()   # rule 1
        foreach ($l in @(Invoke-Git cherry $main $b $base)) {
            if ($l -like '- *') { [void]$samePatch.Add($l.Substring(2)) }
        }

        $raw = (@(Invoke-Git log --no-merges --date=short "--format=%H%x1f%an%x1f%ad%x1f%s%x1f%B%x1e" "$base..$b")) -join "`n"
        $count = 0
        foreach ($rec in $raw.Split($RS)) {
            if ([string]::IsNullOrWhiteSpace($rec)) { continue }
            $f = $rec.Trim().Split($US, 5)
            $sha = $f[0]; $body = $f[4]
            $count++

            $ok = $samePatch.Contains($sha) -or $mainPicks.Contains($sha) -or ($body -match '(?m)^No-Port:')
            if (-not $ok) {   # rule 3
                foreach ($m in [regex]::Matches($body, $pickRx)) {
                    if (Test-Git merge-base --is-ancestor $m.Groups[1].Value $main) { $ok = $true; break }
                }
            }
            if (-not $ok) {
                [pscustomobject]@{
                    Repo = $repo; Branch = $b; Sha = $sha; ShortSha = $sha.Substring(0, 8)
                    Author = $f[1]; Date = $f[2]; Subject = $f[3]
                }
            }
        }
        if (-not $AsJson) { Write-Host "${repo} ${b}: $count commit(s) since it left main" }
    }
}

try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8   # author names / subjects may be Thai
} catch { }   # no console attached: keep the default

try {
    $unported = @(Get-Unported)
} catch {
    [Console]::Error.WriteLine("Test-ReleasePorts failed: $($_.Exception.Message)")
    exit 2
}

if ($AsJson) {
    if ($unported.Count) { $unported | ConvertTo-Json -AsArray -Compress } else { '[]' }
} else {
    foreach ($u in $unported) {
        Write-Host "UNPORTED  $($u.Repo)  $($u.Branch)  $($u.ShortSha)  $($u.Author)  $($u.Date)  $($u.Subject)" -ForegroundColor Yellow
    }
    if ($unported.Count) {
        Write-Host "$($unported.Count) commit(s) on release branches are not in main. Port with 'git cherry-pick -x <sha>' onto a branch off origin/main, or put 'No-Port: <reason>' in the release commit message." -ForegroundColor Red
    } else {
        Write-Host 'All release commits have reached main.' -ForegroundColor Green
    }
}
exit ([int][bool]$unported.Count)
