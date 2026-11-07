# ACEO Franchise Filter

Source for [Steam Workshop item 3832500668](https://steamcommunity.com/sharedfiles/filedetails/?id=3832500668).

Filters franchise contract offers by stars and current room eligibility, mixes shelf products, aligns them with shelves, and optionally hides default franchises. It does not change franchise prices, sales calculations or ratings.

## Install and use

Install [AirportCEO Mod Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3109136766), completing its [external installation guide](https://github.com/Humoresque811/AirportCEO-Mod-Loader/wiki), then subscribe to the Workshop item and restart. Subscribing to the Loader alone is insufficient. No other Aeroportos BR pack is required.

Open a shop or food room's contracts panel. Use the star buttons and Fits/Cabem filter. Settings are in `BepInEx/config/br.eduardocampos.aceo.filtrofranquias.cfg`.

## Build from source

Requires the .NET SDK, .NET Framework 4.6 targeting/reference assemblies, your own Airport CEO installation and BepInEx/Harmony from a compatible Loader installation. External DLLs are intentionally excluded.

```powershell
dotnet build FiltroFranquias.csproj -c Release -p:GameDir="D:/SteamLibrary/steamapps/common/Airport CEO"
```

Override `Managed` and `BepInExCore` if your installation uses different paths. On macOS use the managed assemblies from your own installation. Output: `bin/Release/FiltroFranquias.dll`. With the game closed, install one copy under `BepInEx/plugins/`; avoid duplicate Workshop/manual copies. Restart after removal.

The plugin assembly reports 0.3.2; the Workshop metadata is 0.3.3 (description/package revision). This export preserves the existing plugin source and GUID.

## Português

Código do Filtro de Franquias, publicado para revisão e contribuições. Requer BepInEx/Harmony e a instalação completa do AirportCEO Mod Loader. Desenvolvimento acelerado por IA, com revisão humana informada pelo autor. Compile com suas próprias bibliotecas do jogo; não há DLLs do jogo neste repositório.

## Development and contributions

AI assisted development; the author reports human review of the code and personal use alongside other mods before publication. This is not a guarantee of compatibility with every mod or game version.

Source is public so others can inspect it, learn from it and build on it. Forks and pull requests are welcome. Only the maintainer can accept changes into this repository; public visibility does not grant write access.

Contact: [Eduardo Campos on Steam](https://steamcommunity.com/profiles/76561198373672598). You can also open a GitHub issue with reproduction steps, game/mod versions and a redacted log. Never include credentials or private saves in public reports.

## Credits and license

Original code and authored configuration are MIT-licensed; see LICENSE and THIRD_PARTY_NOTICES.md. Airport CEO, Unity, third-party libraries, company names, logos and official emblems are not relicensed by this repository. This is an unofficial community project.
