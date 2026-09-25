<#
.SYNOPSIS
  Start or stop the Smoke MKK stack locally without Docker.

.DESCRIPTION
  Uses the installed PostgreSQL (C:\Program Files\PostgreSQL\<version>) to run a
  separate project database on port 5433 in .localdb\ (the system PostgreSQL
  service on 5432 is never touched). Then starts the API (http://localhost:5000)
  and the site (http://localhost:5174), each in its own window.

.EXAMPLE
  .\scripts\dev.ps1          # start everything
  .\scripts\dev.ps1 -Stop    # stop everything
#>
param([switch]$Stop)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$dbPort = 5433; $apiPort = 5000; $webPort = 5174
$dataDir = Join-Path $root '.localdb'

# ponytail: picks the newest installed PostgreSQL; pass a path if several versions matter.
$pgBin = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\pg_ctl.exe' -ErrorAction SilentlyContinue |
    Sort-Object { [int]$_.Directory.Parent.Name } | Select-Object -Last 1 | ForEach-Object { $_.DirectoryName }
if (-not $pgBin) { throw 'PostgreSQL not found under C:\Program Files\PostgreSQL. Install it or use Docker (docker compose up).' }

function Test-Port([int]$port) {
    [bool](Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
}
function Stop-Port([int]$port, [string]$name) {
    Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique |
        ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue; Write-Host "stopped $name (pid $_)" }
}
function Invoke-PgCtl([string[]]$arguments) {
    # pg_ctl leaves postgres holding its output handles, so '& pg_ctl' would never return.
    # Start it as its own process and wait only for pg_ctl itself.
    $p = Start-Process -FilePath (Join-Path $pgBin 'pg_ctl.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $p.WaitForExit()
    return $p.ExitCode
}

if ($Stop) {
    Stop-Port $webPort 'site'
    Stop-Port $apiPort 'API'
    if (Test-Path $dataDir) { [void](Invoke-PgCtl @('stop', '-D', "`"$dataDir`"", '-m', 'fast')); Write-Host 'stopped database' }
    return
}

# 1. Secrets: create .env with random values on first run.
if (-not (Test-Path '.env')) {
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    function New-Secret([int]$bytes) { $b = New-Object byte[] $bytes; $rng.GetBytes($b); [Convert]::ToBase64String($b).TrimEnd('=').Replace('+', '-').Replace('/', '_') }
    "# Local secrets, generated $(Get-Date -Format yyyy-MM-dd). Never commit.`nDB_PASSWORD=$(New-Secret 24)`nADMIN_API_KEY=$(New-Secret 32)`n" |
        Set-Content -Path '.env' -Encoding ascii -NoNewline
    Write-Host 'created .env with random secrets'
}
$envVars = @{}
Get-Content '.env' | Where-Object { $_ -match '^\s*[A-Z_]+=' } | ForEach-Object { $k, $v = $_ -split '=', 2; $envVars[$k.Trim()] = $v.Trim() }
foreach ($k in 'DB_PASSWORD', 'ADMIN_API_KEY') { if (-not $envVars[$k]) { throw "$k is missing in .env" } }
$env:PGPASSWORD = $envVars['DB_PASSWORD']

# 2. Database cluster on 5433.
if (-not (Test-Path (Join-Path $dataDir 'PG_VERSION'))) {
    Write-Host "creating database cluster in $dataDir"
    $pwFile = New-TemporaryFile
    try {
        [IO.File]::WriteAllText($pwFile, $envVars['DB_PASSWORD'])
        & (Join-Path $pgBin 'initdb.exe') -D $dataDir -U smoke "--pwfile=$pwFile" -A scram-sha-256 -E UTF8 --locale=C | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'initdb failed' }
    } finally { Remove-Item $pwFile -Force }
}
if (-not (Test-Port $dbPort)) {
    $code = Invoke-PgCtl @('start', '-D', "`"$dataDir`"", '-o', "`"-p $dbPort -c listen_addresses=localhost`"", '-l', "`"$dataDir\server.log`"", '-w')
    if ($code -ne 0) { throw "database did not start, see $dataDir\server.log" }
    Write-Host "database running on port $dbPort"
} else { Write-Host "database already running on port $dbPort" }
$exists = & (Join-Path $pgBin 'psql.exe') -h localhost -p $dbPort -U smoke -d postgres -tAc "select 1 from pg_database where datname = 'smoke'"
if ($exists -ne '1') {
    & (Join-Path $pgBin 'createdb.exe') -h localhost -p $dbPort -U smoke smoke
    if ($LASTEXITCODE -ne 0) { throw 'createdb failed' }
    Write-Host 'created database smoke'
}

# 3. API (applies migrations and seeds an empty database on startup).
if (Test-Port $apiPort) {
    Write-Host "port $apiPort is busy; assuming the API already runs"
} else {
    $env:ConnectionStrings__Default = "Host=localhost;Port=$dbPort;Database=smoke;Username=smoke;Password=$($envVars['DB_PASSWORD'])"
    $env:ADMIN_API_KEY = $envVars['ADMIN_API_KEY']
    Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$Host.UI.RawUI.WindowTitle='SMOKE API :$apiPort'; dotnet run --project backend --launch-profile http"
    Remove-Item Env:ConnectionStrings__Default, Env:ADMIN_API_KEY
}

# 4. Site.
if (Test-Port $webPort) {
    Write-Host "port $webPort is busy; assuming the site already runs"
} else {
    if (-not (Test-Path 'frontend\node_modules')) { Write-Host 'installing frontend packages'; npm ci --prefix frontend | Out-Null }
    Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$Host.UI.RawUI.WindowTitle='SMOKE site :$webPort'; npm run dev --prefix frontend -- --port $webPort --strictPort"
}

# 5. Wait until both answer, then open the site.
$deadline = (Get-Date).AddSeconds(90)
foreach ($url in "http://localhost:$apiPort/health", "http://localhost:$webPort/") {
    while ($true) {
        try { Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 2 | Out-Null; break }
        catch { if ((Get-Date) -gt $deadline) { throw "$url did not answer within 90 s; check its window" }; Start-Sleep -Milliseconds 500 }
    }
}
Write-Host "`nSMOKE is running:"
Write-Host "  site  http://localhost:$webPort"
Write-Host "  API   http://localhost:$apiPort   (admin key in .env)"
Write-Host "  stop  .\scripts\dev.ps1 -Stop"
Start-Process "http://localhost:$webPort"
