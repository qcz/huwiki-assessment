Ez a repó a magyar Wikipédia cikkértékelő botjának forráskódját tartalmazza.

## Általános tudnivalók
A bot C#-ban íródott, jelenleg .NET 4.0 szükséges a futtatásához. A Wikipédiával való kommunikáláshoz a DotNetWikiBot könyvtárat használja.

A repóban megtalálható a DotNetWikiBot egy módosított változata, amely nem ír a konzolra semmit – a program képes működni a módosítást nem tartalmazó DotNetWikiBottal is, de ekkor a DotNetWikiBot tele fogja szemetelni a kimenetet.

Megjegyzés: a kód legnagyobb része 2008-2009-ben íródott, ez meg is látszódik a használt konstrukciókon.

## Fordítás
A kód fordításához elegendő a Visual Studio 2013 Community vagy 2015 Community verziója.

## Futtatás
A lefordított alkalmazás mappájába (`bin/Debug` vagy `bin/Release`) létre kell hozni egy `Cache` almappát, majd ide készíteni kell egy Defaults.dat fájlt a következő tartalommal:
```
[Wiki elérési útja (HTTPS)]
[bot neve]
[jelszó]
```
Minta:
``` 
https://hu.wikipedia.org/
MyBot
MyPassword
```
Ez az autentikáció a régebbi DotNetWikiBot verziókkal való kompatibilitás miatt maradt meg, ahol a DotNetWikiBot beépítetten ebből a fájlból olvasta fel a bejelentkezési adatokat. A kód egyszerűen átírható, ha erre nincs szükség.

## Tesztelés
Ha módosítunk a kódon, és mindenképp szeretnénk, hogy a bot elmentsen minden oldalt (ha nincs változás, akkor nem ment semmit), akkor a `Main` függvény elején található `forceSave` változó értékét állítsuk át `true`-ra.

## Licenc
A kód GPLv3 licenc alatt van, a repóban megtalálható DotNetWikiBot könyvtár GPLv2 vagy újabb licencet használ.