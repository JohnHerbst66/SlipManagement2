<#
    Stages a clean payload from bin\Release and builds the installer.

        .\Build-Package.ps1                 build payload + compile installer
        .\Build-Package.ps1 -StageOnly      stage the payload and stop

    The staging step exists because bin\Release is a BUILD output, not a shippable
    package. It carries IntelliSense .xml files and a .pdb the customer has no use
    for, and - far more importantly - it is where a stray WeighbridgeData.db can
    appear if the program is ever run from there. Shipping a database would hand the
    customer someone else's slips AND skip First-Time Setup on their machine, so they
    would silently run on the wrong printer and paper size. Everything below is
    filtered by an allow-list rather than a block-list: a file has to be recognised
    to be shipped, so anything new turns up in the report instead of in the package.
#>
param(
    [switch]$StageOnly,
    [string]$Config  = "Release",
    [string]$IsccExe = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
$here    = $PSScriptRoot
$project = Split-Path $here -Parent
$release = Join-Path $project "bin\$Config"
$payload = Join-Path $here "payload"

Write-Output "Project : $project"
Write-Output "Source  : $release"
Write-Output ""

if (-not (Test-Path (Join-Path $release "SlipManagement2.exe"))) {
    throw "No SlipManagement2.exe in $release. Build the $Config configuration first."
}

# --- 1. stage -----------------------------------------------------------------
if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }
New-Item -ItemType Directory $payload | Out-Null

# Allow-list. Anything not named here is reported and left behind.
$wanted = @(
    "SlipManagement2.exe",
    "SlipManagement2.exe.config",
    "LICENCE.txt",
    "ClosedXML.dll",
    "DocumentFormat.OpenXml.dll",
    "ExcelNumberFormat.dll",
    "Irony.dll",
    "SixLabors.Fonts.dll",
    "System.Buffers.dll",
    "System.Data.SQLite.dll",
    "System.IO.Packaging.dll",
    "System.Memory.dll",
    "System.Numerics.Vectors.dll",
    "System.Runtime.CompilerServices.Unsafe.dll",
    "XLParser.dll"
)

$copied = @(); $skipped = @()
foreach ($f in Get-ChildItem $release -File) {
    if ($wanted -contains $f.Name) {
        Copy-Item $f.FullName $payload
        $copied += $f.Name
    } else {
        $skipped += $f.Name
    }
}

# The native SQLite interop, both architectures. Without these the program starts
# and then fails the moment it touches the database.
foreach ($arch in @("x64", "x86")) {
    $src = Join-Path $release $arch
    if (Test-Path (Join-Path $src "SQLite.Interop.dll")) {
        New-Item -ItemType Directory (Join-Path $payload $arch) | Out-Null
        Copy-Item (Join-Path $src "SQLite.Interop.dll") (Join-Path $payload $arch)
        $copied += "$arch\SQLite.Interop.dll"
    } else {
        throw "Missing $arch\SQLite.Interop.dll in $release - the package would be unusable."
    }
}

# The operator-facing guide, if it is being kept alongside the installer.
$howTo = Join-Path $here "HOW TO USE THIS.txt"
if (Test-Path $howTo) { Copy-Item $howTo $payload; $copied += "HOW TO USE THIS.txt" }

Write-Output "--- staged $($copied.Count) files ---"
$copied | Sort-Object | ForEach-Object { Write-Output "    $_" }
if ($skipped.Count -gt 0) {
    Write-Output ""
    Write-Output "--- left out of the package ($($skipped.Count)) ---"
    $skipped | Sort-Object | ForEach-Object { Write-Output "    $_" }
}

# --- 2. refuse to ship anything that holds data -------------------------------
$leaks = Get-ChildItem $payload -Recurse -File |
         Where-Object { $_.Extension -in @(".db", ".db-journal", ".db-wal", ".db-shm", ".lic", ".pdb") }
if ($leaks) {
    $leaks | ForEach-Object { Write-Output ("  LEAK: " + $_.Name) }
    throw "Payload contains a database, licence or debug file. Refusing to build."
}

$mb = [math]::Round((Get-ChildItem $payload -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 2)
Write-Output ""
Write-Output "Payload clean: $($copied.Count) files, $mb MB"

if ($StageOnly) { Write-Output "Stopped after staging (-StageOnly)."; return }

# --- 3. compile ---------------------------------------------------------------
if (-not (Test-Path $IsccExe)) {
    Write-Output ""
    Write-Output "Inno Setup not found at:"
    Write-Output "    $IsccExe"
    Write-Output "Install it from https://jrsoftware.org/isdl.php, then run this again."
    Write-Output "The payload is staged and ready, so only the compile step is outstanding."
    return
}

& $IsccExe (Join-Path $here "UitvalSlips.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE." }

Write-Output ""
Get-ChildItem (Join-Path $here "Output") -Filter *.exe |
    ForEach-Object { Write-Output ("Installer: " + $_.FullName + "   " + [math]::Round($_.Length/1MB,2) + " MB") }
