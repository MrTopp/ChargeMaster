# --- Konfiguration ---
$ProjectName = "ChargeMaster"          # Namnet på din .csproj-fil (utan ändelse)
$PiUser = "thomas"                     # SSH-användare på Pi
$PiHost = "192.168.1.10"               # rasp5 IP-adress
$RemotePath = "/var/www/ChargeMaster"  # Mappen där appen bor på din Pi
$LocalPublishPath = "G:\rasp5\ChargeMaster"

Write-Host "--- Startar Deploy av $ProjectName ---" -ForegroundColor Cyan

# 1. Rensa lokal publish-katalog och bygg applikationen
Write-Host "1. Rensar lokal publish-katalog och bygger applikationen för Linux-arm64..." -ForegroundColor Yellow
if (Test-Path "$LocalPublishPath") {
    Remove-Item "$LocalPublishPath" -Recurse -Force
}
dotnet publish "ChargeMaster\ChargeMaster.csproj" -c Release -r linux-arm64 --no-self-contained -o "$LocalPublishPath"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Bygget misslyckades. Avbryter." -ForegroundColor Red
    exit
}

# 2. Stoppa ChargeMaster på rasp5
Write-Host "2. Stoppar tjänsten på Raspberry Pi..." -ForegroundColor Yellow
ssh $PiUser@$PiHost "sudo systemctl stop chargemaster-dotnet.service"

# 3 & 4. Rensa gamla filer och kopiera nya
Write-Host "3 & 4. Rensar och kopierar filer..." -ForegroundColor Yellow
ssh $PiUser@$PiHost "mkdir -p $RemotePath && rm -rf $RemotePath/*"
scp -r "$LocalPublishPath\*" "${PiUser}@${PiHost}:$RemotePath/"

# 5. Starta ChargeMaster
Write-Host "5. Startar tjänsten igen..." -ForegroundColor Green
ssh $PiUser@$PiHost "sudo systemctl start chargemaster-dotnet.service"

# Kontrollera status
Write-Host "--- Deploy klar! Kontrollerar status ---" -ForegroundColor Cyan
ssh $PiUser@$PiHost "systemctl is-active chargemaster-dotnet.service"
Write-Host "Loggar (senaste 5 raderna):" -ForegroundColor Gray
ssh $PiUser@$PiHost "journalctl -u chargemaster-dotnet.service -n 5 --no-pager"