<#
.SYNOPSIS
  Build locally and deploy to the live server (docs/PLAN.md §8 "Live server").

.DESCRIPTION
  The server has no Docker, SDK or Node: this publishes the API and builds the site here,
  uploads both with pscp, swaps the folders on the server (the previous versions stay as
  .old for a rollback), restarts smokemkk-api and checks health, locations and the poller.
  Secrets on the server (/etc/smokemkk.env) are not touched; edit them by hand.

.EXAMPLE
  .\scripts\deploy.ps1                 # deploy main as it is in the working tree
  .\scripts\deploy.ps1 -SkipBuild      # re-upload the last build
#>
param(
    [string]$Server = '31.70.83.196',
    [string]$User = 'root',
    [string]$Key = 'secrets/private.ppk',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$target = "$User@$Server"
$out = Join-Path $root '.deploy'   # git-ignored build output

foreach ($tool in 'plink', 'pscp', 'dotnet', 'npm') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool is not installed or not on PATH" }
}
if (-not (Test-Path $Key)) { throw "key $Key not found (it is git-ignored; copy it from the owner)" }

# The script goes via a file (plink -m): PowerShell 5.1 drops embedded double quotes from native
# arguments and puts a BOM in front of piped stdin.
function Remote([string]$command) {
    $file = Join-Path $env:TEMP 'smokemkk-remote.sh'
    [IO.File]::WriteAllText($file, $command.Replace("`r`n", "`n"), (New-Object System.Text.UTF8Encoding($false)))
    & plink -batch -i $Key -m $file $target
    if ($LASTEXITCODE -ne 0) { throw "remote command failed: $($command.Split("`n")[0])" }
}

# 1. Build. api.new / smokemkk.new are the folder names the server expects.
if (-not $SkipBuild) {
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    New-Item -ItemType Directory $out | Out-Null
    Write-Host '== publishing API'
    & dotnet publish backend -c Release -o (Join-Path $out 'api.new') --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
    Write-Host '== building site'
    & npm run build --prefix frontend
    if ($LASTEXITCODE -ne 0) { throw 'npm run build failed' }
    Copy-Item (Join-Path $root 'frontend\dist') (Join-Path $out 'smokemkk.new') -Recurse
}
foreach ($d in 'api.new', 'smokemkk.new') { if (-not (Test-Path (Join-Path $out $d))) { throw "$out\$d missing; run without -SkipBuild" } }

# 2. Upload to staging folders next to the live ones.
Write-Host "== uploading to $target"
Remote 'rm -rf /opt/smokemkk/api.new /var/www/smokemkk.new'
& pscp -batch -r -i $Key (Join-Path $out 'api.new') "${target}:/opt/smokemkk/" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'upload of the API failed' }
& pscp -batch -r -i $Key (Join-Path $out 'smokemkk.new') "${target}:/var/www/" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'upload of the site failed' }
Remote 'test -f /opt/smokemkk/api.new/SmokeMkk.Api.dll && test -f /var/www/smokemkk.new/index.html'

# 3. Swap, restart, check. One remote shell so a failure stops before the service is left down.
Write-Host '== switching over'
Remote @'
set -e
systemctl stop smokemkk-api
rm -rf /opt/smokemkk/api.old /var/www/smokemkk.old
mv /opt/smokemkk/api /opt/smokemkk/api.old; mv /opt/smokemkk/api.new /opt/smokemkk/api
mv /var/www/smokemkk /var/www/smokemkk.old; mv /var/www/smokemkk.new /var/www/smokemkk
systemctl start smokemkk-api
for i in $(seq 1 60); do curl -sf -m 2 http://127.0.0.1:8080/health >/dev/null && break; sleep 1; done
echo "health: $(curl -s -m 5 http://127.0.0.1:8080/health)"
echo "locations: $(curl -s -m 5 -o /dev/null -w %{http_code} http://127.0.0.1:8080/api/locations)"
echo "migration: $(sudo -u postgres psql -d smoke -tAc 'select max("MigrationId") from "__EFMigrationsHistory"')"
sleep 20
journalctl -u smokemkk-api --since '3 min ago' --no-pager | grep -E 'Vendon (poller|pass)|warn|fail|error' | tail -5
'@

Write-Host "`ndeployed. Rollback: on the server move the .old folders back and restart smokemkk-api."
