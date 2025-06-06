# MagentaTV API

**MagentaTV API** je otevřená brána k televiznímu obsahu MagentaTV. Díky této ASP.NET Core aplikaci můžete pohodlně obsloužit oficiální API, bezpečně spravovat relace a vytvářet vlastní playlisty či integrační služby. Projekt míří na vývojáře a nadšence, kteří chtějí využívat MagentaTV ve svých aplikacích bez složité konfigurace.

Rychlé starty, přehledné rozhraní a možnost libovolného rozšíření – to je MagentaTV API v kostce. Ať už stavíte domácí IPTV řešení, nebo chcete pouze automatizovat přístup k živému vysílání, tato aplikace vám poskytne všechny potřebné nástroje.

## Požadavky
- .NET SDK 9.0 (preview)
- Volitelně `curl` pro testování endpointů

## Struktura projektu
- `MagentaTV/` – zdrojové kódy aplikace
- `MagentaTV.sln` – solution soubor
- `appsettings.Production.json` – ukázková konfigurace

## Co získáte
- **Okamžitou správu relací a přihlášení** – API obstará autentizaci a správu uživatelských relací za vás.
- **Přístup ke kanálům, EPG a streamům** – vše přehledně na jednom místě díky koncovým bodům `MagentaController`.
- **Generování M3U a XMLTV** – připravte svým přehrávačům dokonalý playlist jedním požadavkem.
- **SignalR notifikace** – sledujte v reálném čase přihlášení uživatelů i dokončení FFmpeg úloh přes `/hubs/notifications`.
- **Health checks a barevná konzole** – kontrolujte stav služeb a užijte si přehledné výpisy díky [Spectre.Console](https://spectreconsole.net).
- **Jednotný formát chyb** – vlastní middleware vrací `ApiResponse` s identifikátorem chyby, takže se v logu neztratíte.

## Spuštění v režimu vývoje
```bash
# obnova závislostí
 dotnet restore MagentaTV.sln

# spuštění aplikace
 dotnet run --project MagentaTV/MagentaTV.csproj
```
Aplikace standardně poslouchá na portu `5000` (HTTP) a `5001` (HTTPS).

## Konfigurace
Nastavení se provádí pomocí souborů `appsettings.json` (případně variant `Development`, `Production` atd.). Klíčové sekce:
- `MagentaTV` – URL API, identifikace zařízení a další parametry
- `Session` – délka platnosti session, maximální počet přihlášení
- `Cache` – expirace jednotlivých položek v paměťové cache
- `RateLimit` – omezení počtu požadavků

V produkčním prostředí je nutné nastavit proměnnou `SESSION_ENCRYPTION_KEY` pro šifrování session tokenů.

## Poznámky
Projekt momentálně cílí na .NET 9.0, který může vyžadovat preview verzi SDK. Pokud SDK není dostupné, kompilace se nemusí podařit.

