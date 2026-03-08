# Kopierar en dump av chargemaster_db från produktionsservern till utvecklingsservern och återställer den där.
# Filerna raderas inte efter användandet
ssh thomas@192.168.1.10 "pg_dump -h localhost -U admin_user -d chargemaster_db -Fc -f /home/thomas/backup/chargemaster_$(Get-Date -Format 'yyyyMMdd').dump"
scp thomas@192.168.1.10:/home/thomas/backup/chargemaster_$(Get-Date -Format 'yyyyMMdd').dump G:\rasp5\backup\chargemaster_$(Get-Date -Format 'yyyyMMdd').dump
pg_restore -U postgres -h localhost --clean -d chargemaster_db --clean --if-exists --no-owner --no-privileges G:\rasp5\backup\chargemaster_$(Get-Date -Format 'yyyyMMdd').dump
