# SQL-skript för databasenuppdateringar

Dessa SQL-skript används för att hantera databastabeller i PostgreSQL 17 när migrations inte kan köras i produktionsmiljö.

## Skript

### 001_CreateShellyTemperatureTable.sql
Skapar tabellen och dess index för temperaturmätningar från Shelly-enheter.

### 002_CreateElectricityPricesTable.sql
Skapar tabellen för elpriser från API:et.

**Vad det gör:**
- Skapar tabellen `ElectricityPrices` med priser i SEK och EUR
- Lagrar prisperioder med start- och sluttider
- Skapar 3 index för optimal prestanda

### 003_CreateChargeSessionTable.sql
Skapar tabellen för Wallbox laddningssessiondata.

**Vad det gör:**
- Skapar tabellen `ChargeSessions` med sessionsdata från Wallbox-laddaren
- Lagrar information om varje laddningssession inkl. energi, laddningsnivå och status
- Skapar 2 index för optimal prestanda

### 004_CreateWallboxMeterReadingsTable.sql
Skapar tabellen för Wallbox mätningsdata.

**Vad det gör:**
- Skapar tabellen `WallboxMeterReadings` med elmätningsdata från Wallbox-laddaren
- Lagrar mätningar av energiförbrukning, effekt och rådata från API
- Skapar 3 index för optimal prestanda:
  - Index på ReadAt DESC för snabb sökning av mätningar över tid
  - Index på AccEnergy DESC för att hitta ändringar i energi
  - Composite index på (ReadAt DESC, AccEnergy DESC) för períodsökning

## Körning av SQL-skript

```bash
# Körning med psql från command line
psql -U chargemasterapp -d chargemaster_db -f 002_CreateElectricityPricesTable.sql
psql -U chargemasterapp -d chargemaster_db -f 003_CreateChargeSessionTable.sql
psql -U chargemasterapp -d chargemaster_db -f 004_CreateWallboxMeterReadingsTable.sql
```

Eller direkt i psql-sessionen:
```sql
\i 002_CreateElectricityPricesTable.sql
\i 003_CreateChargeSessionTable.sql
\i 004_CreateWallboxMeterReadingsTable.sql
```

## Databasanslutning

```
Host: localhost (eller din server)
Port: 5432
Database: chargemaster_db
User: chargemasterapp
```

## Schema ElectricityPrices

| Kolumn | Typ | Beskrivning |
|--------|-----|-------------|
| Id | integer (PK) | Primärnyckel, auto-increment |
| SekPerKwh | numeric(18,2) | Elpris i SEK per kWh |
| EurPerKwh | numeric(18,2) | Elpris i EUR per kWh |
| ExchangeRate | numeric(18,2) | Växelkurs SEK/EUR vid registrering |
| TimeStart | timestamp without time zone | Starttid för prisperioden (UTC) |
| TimeEnd | timestamp without time zone | Sluttid för prisperioden (UTC) |

## Index ElectricityPrices

| Namn | Kolumner | Beskrivning |
|------|----------|-------------|
| IX_ElectricityPrices_TimeStart | TimeStart DESC | Snabb sökning av senaste priser |
| IX_ElectricityPrices_TimeEnd | TimeEnd DESC | Sökning av prisperioder |
| IX_ElectricityPrices_TimeStart_TimeEnd | TimeStart ASC, TimeEnd ASC | Periodsökning |

## Schema ChargeSessions

| Kolumn | Typ | Beskrivning |
|--------|-----|-------------|
| Id | bigint (PK) | Primärnyckel, auto-increment |
| Timestamp | timestamp without time zone | Tidpunkt då data spelades in |
| SessionEnergy | integer | Energi förbrukad under sessionen (Wh) |
| SessionStartValue | bigint | Startvärde för energi (Wh) |
| SessionStartTime | bigint | Unix timestamp när sessionen startade (sekunder) |
| ChargeLevel | integer | Aktuell laddningsnivå (%) |
| ChargeTarget | integer | Målnivå för laddning (%) |
| ChargeState | text | Status för laddningssessionen |

## Schema WallboxMeterReadings

| Kolumn | Typ | Beskrivning |
|--------|-----|-------------|
| Id | integer (PK) | Primärnyckel, auto-increment |
| ReadAt | timestamp without time zone | Tidpunkt då mätningen lästes (UTC) |
| RawJson | varchar(1000) | Rådata från Wallbox-API i JSON-format |
| AccEnergy | bigint | Ackumulerad energi i Wh sedan installation |
| MeterSerial | varchar(100) | Serienummer på elmätaren (nullable) |
| ApparentPower | bigint | Aktuell skenbar effekt i VA |

## Index WallboxMeterReadings

| Namn | Kolumner | Beskrivning |
|------|----------|-------------|
| IX_WallboxMeterReadings_ReadAt | ReadAt DESC | Snabb sökning av mätningar över tid |
| IX_WallboxMeterReadings_AccEnergy | AccEnergy DESC | Sökning av ändringar i energi |
| IX_WallboxMeterReadings_ReadAt_AccEnergy | ReadAt DESC, AccEnergy DESC | Periodsökning för mätningar |

## Felsökning

**Tabellen skapas inte:**
- Verifiera att du är ansluten till rätt databas
- Kontrollera att du har `CREATE TABLE`-behörigheter

**Permission denied:**
- Säkerställ att användaren har rätt privilegier:
```sql
GRANT ALL PRIVILEGES ON TABLE public."ElectricityPrices" TO chargemasterapp;
GRANT ALL PRIVILEGES ON SEQUENCE "ElectricityPrices_Id_seq" TO chargemasterapp;
GRANT ALL PRIVILEGES ON TABLE public."ChargeSessions" TO chargemasterapp;
GRANT ALL PRIVILEGES ON SEQUENCE "ChargeSession_Id_seq" TO chargemasterapp;
GRANT ALL PRIVILEGES ON TABLE public."WallboxMeterReadings" TO chargemasterapp;
GRANT ALL PRIVILEGES ON SEQUENCE "WallboxMeterReadings_Id_seq" TO chargemasterapp;
```

## Rollback (om något går fel)

```sql
DROP TABLE IF EXISTS public."ElectricityPrices" CASCADE;
DROP TABLE IF EXISTS public."ChargeSessions" CASCADE;
DROP TABLE IF EXISTS public."WallboxMeterReadings" CASCADE;
```

## Deployment på Raspberry Pi

```bash
# Kopiera SQL-skripten
scp 002_CreateElectricityPricesTable.sql chargemasterapp@raspberry-pi:/var/www/ChargeMaster/Data/Scripts/
scp 003_CreateChargeSessionTable.sql chargemasterapp@raspberry-pi:/var/www/ChargeMaster/Data/Scripts/
scp 004_CreateWallboxMeterReadingsTable.sql chargemasterapp@raspberry-pi:/var/www/ChargeMaster/Data/Scripts/

# Anslut och kör skripten
ssh chargemasterapp@raspberry-pi
psql -U chargemasterapp -d chargemaster_db -f /var/www/ChargeMaster/Data/Scripts/002_CreateElectricityPricesTable.sql
psql -U chargemasterapp -d chargemaster_db -f /var/www/ChargeMaster/Data/Scripts/003_CreateChargeSessionTable.sql
psql -U chargemasterapp -d chargemaster_db -f /var/www/ChargeMaster/Data/Scripts/004_CreateWallboxMeterReadingsTable.sql

# Verifiera
\dt
\d "ElectricityPrices"
\d "ChargeSessions"
\d "WallboxMeterReadings"
```

## Maintenans

```sql
-- Ta bort ElectricityPrices data äldre än 1 år
DELETE FROM public."ElectricityPrices" 
WHERE "TimeEnd" < NOW() - INTERVAL '1 year';

-- Ta bort ChargeSessions data äldre än 90 dagar
DELETE FROM public."ChargeSessions" 
WHERE "Timestamp" < NOW() - INTERVAL '90 days';

-- Ta bort WallboxMeterReadings data äldre än 1 år
DELETE FROM public."WallboxMeterReadings" 
WHERE "ReadAt" < NOW() - INTERVAL '1 year';

-- Optimera tabellerna
VACUUM ANALYZE public."ElectricityPrices";
VACUUM ANALYZE public."ChargeSessions";
VACUUM ANALYZE public."WallboxMeterReadings";
```
