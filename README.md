# MagentaTV API

Tento repozitář obsahuje ASP.NET Core aplikaci poskytující REST rozhraní pro služby MagentaTV. Projekt slouží jako "wrapper" nad oficiálním API a přidává správu relací, generování playlistů a další pomocné funkce.

## Požadavky
- .NET SDK 9.0 (preview)
- Volitelně `curl` pro testování endpointů

## Struktura projektu
- `MagentaTV/` – zdrojové kódy aplikace
- `MagentaTV.sln` – solution soubor
- `appsettings.Production.json` – ukázková konfigurace

## Základní funkce
- **Autentizace a správa session** – koncové body v `AuthController` a `SessionController`
- **Získání kanálů, EPG a streamů** – koncové body v `MagentaController`
- **Generování M3U playlistu a XMLTV**
- **SignalR hub** pro zasílání notifikací (`/hubs/notifications`).
  Klienti mohou přijímat události o přihlášení, odhlášení uživatele
  i o dokončení FFmpeg úloh prostřednictvím knihovny SignalR.
- **Health checks** pro kontrolu dostupnosti služeb a background úloh

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

