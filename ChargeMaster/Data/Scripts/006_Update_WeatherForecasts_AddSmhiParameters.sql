-- Script för att uppdatera WeatherForecasts-tabellen med nya kolumner
-- PostgreSQL
-- Datum: 2026-03-08
-- Beskrivning: Lägger till SMHI-parametrar för bättre väderprognos

-- Lägg till nya kolumner för åskornadsannolikhet
ALTER TABLE "WeatherForecasts" 
ADD COLUMN IF NOT EXISTS "ThunderstormProbability" integer;

-- Lägg till nya kolumner för nederbördsmedianed
ALTER TABLE "WeatherForecasts" 
ADD COLUMN IF NOT EXISTS "PrecipitationMedian" double precision;

-- Lägg till kolumn för nederbördsannolikhet
ALTER TABLE "WeatherForecasts" 
ADD COLUMN IF NOT EXISTS "PrecipitationProbability" integer;

-- Lägg till kolumn för nederbördskategori
ALTER TABLE "WeatherForecasts" 
ADD COLUMN IF NOT EXISTS "PrecipitationCategory" integer;

-- Lägg till kolumn för vädsymbol
ALTER TABLE "WeatherForecasts" 
ADD COLUMN IF NOT EXISTS "WeatherSymbol" integer;

-- Lägg till kolumn för total niederbörd
ALTER TABLE "WeatherForecasts" 
ADD COLUMN IF NOT EXISTS "TotalPrecipitation" double precision;

-- Verifiering
SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'WeatherForecasts' 
ORDER BY ordinal_position;
