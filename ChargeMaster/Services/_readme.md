# ChargeMaster/Services

Katalogen innehåller tjänster som tillhandahåller interface mot olika externa tjänster.

## Daikin
Tjänsten `DaikinService` tillhandahåller interface mot Daikin luftvärmepumpar. Den använder luftvärmepumpens lokala API för att hämta information om luftvärmepumpens status och för att styra den.

## ElectricityPrice
Tjänsten `ElectricityPriceService` tillhandahåller interface mot elprisdata från Nord Pool.


## SMHI
Tjänsten tillhandahåller interface mot SMHI:s väderdata. Den hämtar väderprognoser för Strömtorp och gör det möjligt att få aktuell väderinformation och prognoser för de kommande dagarna

## Volkswagen
Tjänsten `VolkswagenService` tillhandahåller interface mot Volkswagen:s API för elbilar. Den hämtar information om bilens status, batterinivå och gör det möjligt att styra vissa funktioner på distans.
Kommunikationen med Volkswagens tjänst går en Open Source Python-modul. Kopplingen mot den går via en egen REST API modul som körs lokalt.

## Wallbox
Tjänsten `WallboxService` tillhandahåller interface mot GARO ladbox. Den kommunicerar lokalt via http och använder samma API som den lokala webbsidan.  
Interafacet har refaktoriserats genom att läsa http-trafiken mellan webbsidan och laddboxen.
