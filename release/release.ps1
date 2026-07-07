#Requires -Version 5.1
<#
.SYNOPSIS
  Builds a distributable Mentor Overseer installer: clean publish -> stage a
  self-contained data folder -> compile with Inno Setup -> checksum.

.PARAMETER Force
  Proceed even if the git working tree isn't clean. Without it, a dirty tree
  aborts before building - a release should be reproducible from a tagged
  commit, not from whatever happens to be on disk.

.PARAMETER SkipPublish
  Reuse whatever is already in dist\ (skips dotnet publish). Useful for
  iterating on the Inno script without waiting for a ~165MB republish.

.EXAMPLE
  .\release.ps1
.EXAMPLE
  .\release.ps1 -Force
#>
param(
    [switch]$Force,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$RepoRoot    = Split-Path -Parent $PSScriptRoot
$ReleaseDir  = $PSScriptRoot
$ProjectPath = Join-Path $RepoRoot "winui\MentorOverseer.App\MentorOverseer.App.csproj"
$DistDir     = Join-Path $ReleaseDir "dist"
$OutputDir   = Join-Path $ReleaseDir "output"
$InstallerIss = Join-Path $ReleaseDir "installer\app.iss"
$DefaultConfig = Join-Path $ReleaseDir "installer\config.default.json"

function Step($msg) { Write-Host "`n== $msg ==" -ForegroundColor Cyan }
function Fail($msg) { Write-Host "FAILED: $msg" -ForegroundColor Red; exit 1 }

# -- 1. version (single source of truth: the csproj) -----------------------
$csprojText = Get-Content $ProjectPath -Raw
if ($csprojText -notmatch '<Version>([^<]+)</Version>') { Fail "No <Version> in $ProjectPath" }
$Version = $Matches[1]
Step "Mentor Overseer v$Version"

# -- 2. clean tree check -----------------------------------------------------
Step "Checking git tree"
Push-Location $RepoRoot
try {
    $status = git status --porcelain
    if ($status -and -not $Force) {
        Write-Host $status
        Fail "Working tree isn't clean (commit/stash first, or pass -Force to override) - a release should be reproducible from a tagged commit."
    } elseif ($status) {
        Write-Host "Working tree is dirty - proceeding anyway (-Force)." -ForegroundColor Yellow
    } else {
        Write-Host "Clean."
    }
} finally {
    Pop-Location
}

# -- 3. publish (self-contained, no runtime install required) --------------
if (-not $SkipPublish) {
    Step "dotnet publish (self-contained win-x64) - this takes a minute"
    $dotnetDir = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
    if (Test-Path $dotnetDir) { $env:PATH = "$dotnetDir;$env:PATH" }

    & dotnet publish $ProjectPath -c Release -p:Platform=x64 `
        --self-contained true -r win-x64 --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { Fail "dotnet publish failed" }

    $PublishDir = Join-Path $RepoRoot `
        "winui\MentorOverseer.App\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish"
    if (-not (Test-Path $PublishDir)) { Fail "publish output not found at $PublishDir" }

    Step "Staging dist\"
    if (Test-Path $DistDir) { Remove-Item $DistDir -Recurse -Force }
    New-Item -ItemType Directory -Path $DistDir | Out-Null
    Copy-Item "$PublishDir\*" $DistDir -Recurse

    # First-run data: config.json + an empty plans/data tree. AppPaths.Root
    # walks up from the exe looking for a folder with BOTH config.json and a
    # "plans" subfolder - without these the very first launch throws.
    Copy-Item $DefaultConfig (Join-Path $DistDir "config.json")
    New-Item -ItemType Directory -Force -Path (Join-Path $DistDir "plans\active")   | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $DistDir "plans\archive")  | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $DistDir "data")           | Out-Null
} else {
    Step "Skipping publish (-SkipPublish) - reusing existing dist\"
    if (-not (Test-Path $DistDir)) { Fail "dist\ doesn't exist; run without -SkipPublish first" }
}

# -- 4. compile the installer ------------------------------------------------
Step "Compiling installer (Inno Setup)"
$iscc = Get-ChildItem -Path "$env:LOCALAPPDATA\Programs","C:\Program Files*" `
    -Filter "ISCC.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
if (-not $iscc) { Fail "ISCC.exe not found - install Inno Setup 6 (winget install JRSoftware.InnoSetup)" }

if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutputDir | Out-Null

& $iscc "/DAppVersion=$Version" $InstallerIss
if ($LASTEXITCODE -ne 0) { Fail "ISCC compile failed" }

# -- 5. checksums -------------------------------------------------------------
Step "Checksums"
$setupExe = Get-ChildItem $OutputDir -Filter "*-setup.exe" | Select-Object -First 1
if (-not $setupExe) { Fail "No setup exe produced in $OutputDir" }
$hash = Get-FileHash $setupExe.FullName -Algorithm SHA256
"$($hash.Hash)  $($setupExe.Name)" | Set-Content (Join-Path $OutputDir "SHA256SUMS.txt")
Write-Host "$($setupExe.Name)"
Write-Host "SHA256: $($hash.Hash)"

Step "Done - release\output\$($setupExe.Name)"
Write-Host "Next: run the verification checklist in release\README.md before calling this v$Version done." -ForegroundColor Yellow
