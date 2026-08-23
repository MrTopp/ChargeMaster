# --- Konfiguration ---
$ProjectName = "ChargeMaster"          # Namnet på din .csproj-fil (utan ändelse)
$PiUser = "thomas"                     # SSH-användare på Pi
$PiHost = "192.168.1.129"               # rasp5 IP-adress
$RemotePath = "/var/www/ChargeMaster"  # Mappen där appen bor på din Pi
$LocalPublishPath = "G:\rasp5\ChargeMaster"

Write-Host "--- Startar Deploy av $ProjectName ---" -ForegroundColor Cyan

# 1. Rensa lokal publish-katalog och bygg applikationen
Write-Host "1. Rensar lokal publish-katalog och bygger applikationen för Linux-amd64..." -ForegroundColor Yellow
if (Test-Path "$LocalPublishPath") {
    Remove-Item "$LocalPublishPath" -Recurse -Force
}
dotnet publish "ChargeMaster\ChargeMaster.csproj" -c Release -r linux-x64 --no-self-contained -o "$LocalPublishPath"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Bygget misslyckades. Avbryter." -ForegroundColor Red
    exit
}

# Säkerhetskontroll: Verifiera att Development-filen INTE är med i publish
Write-Host "1b. Verifierar att appsettings.Development.json inte är med i publish..." -ForegroundColor Yellow
if (Test-Path "$LocalPublishPath\appsettings.Development.json") {
    Write-Host "VARNING: appsettings.Development.json finns i publish-mappen! Detta bör inte hända." -ForegroundColor Red
    Write-Host "Installationen avbryts för säkerhet." -ForegroundColor Red
    exit
} else {
    Write-Host "✓ Bekräftat: appsettings.Development.json är korrekt exkluderad från publish" -ForegroundColor Green
}

# 2. Stoppa ChargeMaster på rasp5
Write-Host "2. Stoppar tjänsten på Raspberry Pi..." -ForegroundColor Yellow
ssh $PiUser@$PiHost "sudo systemctl stop chargemaster.service"

# 3 & 4. Rensa gamla filer och kopiera nya
Write-Host "3 & 4. Rensar och kopierar filer..." -ForegroundColor Yellow
ssh $PiUser@$PiHost "mkdir -p $RemotePath && rm -rf $RemotePath/*"
scp -q -r "$LocalPublishPath\*" "${PiUser}@${PiHost}:$RemotePath/"

# 4b. Ställ in korrekt behörigheter för filerna
Write-Host "4b. Ställer in behörigheter (filer: 664, kataloger: 775)..." -ForegroundColor Yellow
ssh $PiUser@$PiHost "find $RemotePath -type f -exec chmod 664 {} \; && find $RemotePath -type d -exec chmod 775 {} \; && mkdir -p $RemotePath/logs"

# 5. Starta ChargeMaster
Write-Host "5. Startar tjänsten igen..." -ForegroundColor Green
ssh $PiUser@$PiHost "sudo systemctl start chargemaster.service"

# Kontrollera status
Write-Host "--- Deploy klar! Kontrollerar status ---" -ForegroundColor Cyan
ssh $PiUser@$PiHost "systemctl is-active chargemaster.service"
Write-Host "Loggar (senaste 5 raderna):" -ForegroundColor Gray
ssh $PiUser@$PiHost "journalctl -u chargemaster.service -n 5 --no-pager"