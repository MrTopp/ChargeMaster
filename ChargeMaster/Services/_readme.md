# ChargeMaster/Services

Katalogen innehåller tjänster som tillhandahåller interface mot olika externa tjänster.

## Daikin

Tjänsten `DaikinService` tillhandahåller interface mot Daikin luftvärmepumpar. Den använder luftvärmepumpens lokala API
för att hämta information om luftvärmepumpens status och för att styra den.

## ElectricityPrice

Tjänsten `ElectricityPriceService` tillhandahåller interface mot elprisdata från Nord Pool.

## SMHI

Tjänsten tillhandahåller interface mot SMHI:s väderdata. Den hämtar väderprognoser för Strömtorp och gör det möjligt att
få aktuell väderinformation och prognoser för de kommande dagarna

## TibberPulse

Kommunicerar med tibber pulse och hämtar information om elförbrukning.

## TibberVehicle

Hämtar information om bilens laddstatus.

## Wallbox

Tjänsten `WallboxService` tillhandahåller interface mot GARO ladbox. Den kommunicerar lokalt via http och använder samma
API som den lokala webbsidan.  
Interafacet har refaktoriserats genom att läsa http-trafiken mellan webbsidan och laddboxen.
