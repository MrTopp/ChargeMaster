# SQL-skript för ShellyTemperature-tabellen

Dessa SQL-skript används för att hantera `ShellyTemperature`-tabellen i PostgreSQL 17 när migrations inte kan köras i produktionsmiljö.

## Skript

### 001_CreateShellyTemperatureTable.sql
Skapar tabellen och dess index.

**Vad det gör:**
- Skapar tabellen `ShellyTemperature` med kolumner för DeviceId, TemperatureCelsius och Timestamp
- Skapar 3 index för optimal prestanda:
  - Composite index på (DeviceId, Timestamp DESC) för snabb sökning av senaste värden
  - Index på DeviceId för filtrering per enhet
  - Index på Timestamp för tidsbaserad sortering
- Lägger till dokumentation (comments) på tabellen

**Körning:**
```bash
psql -U username -d chargemaster -f 001_CreateShellyTemperatureTable.sql
```

Eller direkt i psql:
```sql
\i 001_CreateShellyTemperatureTable.sql
```

## Databasanslutning (från Program.cs)

```
Host: localhost (eller din server)
Port: 5432
Database: chargemaster_db
User: chargemasterapp
```

## Schema

| Kolumn | Typ | Beskrivning |
|--------|-----|-------------|
| Id | integer (PK) | Primärnyckel, auto-increment |
| DeviceId | text | Enhets-ID (sovrum, hall, arbetsrum) |
| TemperatureCelsius | double precision | Temperaturvärde |
| Timestamp | timestamp without time zone | Mätningstidpunkt |

## Index

| Namn | Kolumner | Beskrivning |
|------|----------|-------------|
| IX_ShellyTemperature_DeviceId_Timestamp | DeviceId ASC, Timestamp DESC | Snabb sökning av senaste värden per enhet |
| IX_ShellyTemperature_DeviceId | DeviceId | Filtrering per enhet |
| IX_ShellyTemperature_Timestamp | Timestamp DESC | Tidsbaserad sortering |

## Felsökning

**Tabellen skapas inte:**
- Verifiera att du är ansluten till rätt databas
- Kontrollera att du har `CREATE TABLE`-behörigheter

**Indexskapning misslyckas:**
- Kontrollera att inga kolumner saknas
- Verifiera PostgreSQL-versionen (kräver 9.0+)

**Permission denied:**
- Säkerställ att användaren har rätt privilegier:
```sql
GRANT ALL PRIVILEGES ON TABLE public."ShellyTemperature" TO chargemasterapp;
GRANT ALL PRIVILEGES ON SEQUENCE "ShellyTemperature_Id_seq" TO chargemasterapp;
```

## Rollback (om något går fel)

```sql
DROP TABLE IF EXISTS public."ShellyTemperature" CASCADE;
```
