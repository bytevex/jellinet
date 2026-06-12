# Jellyfin Netflix Rows

> ## ⚠️ VIBE CODED — LEES DIT EERST
>
> Dit project is **volledig "vibe coded"** door een AI (Claude), als experiment
> om te testen hoe ver AI-gegenereerde code komt voor een niet-trivale
> Jellyfin server plugin (C#, ASP.NET Core, Jellyfin SDK, web-injectie in de
> Jellyfin web client).
>
> Dat betekent concreet:
> - De code is **niet getest tegen een echte Jellyfin server** (geen build,
>   geen runtime-test tegen `Jellyfin.Controller`).
> - API-namen, types en signatures (bv. `InternalItemsQuery`, `IDtoService`,
>   `ItemSortBy`, `IHomeScreenManager`) zijn gebaseerd op publieke Jellyfin SDK
>   documentatie/voorbeelden en kunnen per Jellyfin-versie licht afwijken.
> - Er kunnen compile-errors, kleine API-mismatches of edge-cases zitten die
>   eerst opgelost moeten worden voordat dit in productie draait.
> - Gebruik dit als **goed startpunt / architectuur / referentie-implementatie**,
>   niet als kant-en-klaar, blind te installeren productiepakket.
>
> Test altijd eerst op een **test-/staging Jellyfin server**, niet direct op
> je hoofdserver met je echte bibliotheek.

---

## 1. Onderzoek: kan een server plugin homepage-rijen injecteren?

**Kort antwoord: niet rechtstreeks via de officiële, stabiele Jellyfin
plugin-API.**

- Jellyfin server plugins (C#, `IPlugin` / `BasePlugin<TConfig>`) kunnen:
  - eigen REST API-endpoints toevoegen (`ControllerBase`),
  - scheduled tasks draaien,
  - een eigen configuratiepagina tonen in de Dashboard,
  - achtergrond-services (`IHostedService`) draaien.
- Er is **geen officiële, stabiele "voeg een sectie toe aan de homepage"-API**
  in Jellyfin core (stand van zaken: Jellyfin 10.10.x / 10.11.x,
  medio 2026).
- Er bestaat wél een community-plugin **"Home Screen Sections"**
  ([IAmParadox27/jellyfin-plugin-home-sections](https://github.com/IAmParadox27/jellyfin-plugin-home-sections)),
  die via `IHomeScreenManager.RegisterResultsDelegate(...)` en een
  `PluginDefinedSection` andere plugins toelaat om native rijen te
  registreren op de standaard Jellyfin web client. Dit is echter:
  - een **plugin van een derde partij**, geen Jellyfin-core feature,
  - met een **interne API die tussen versies kan wijzigen** (de README van
    die plugin vermeldt zelf workarounds zoals een `[ModuleInitializer]` voor
    assembly-resolution issues),
  - dus niet geschikt om blind een hard-dependency op te bouwen.
- Er was ook een PR richting Jellyfin core
  ([jellyfin/jellyfin#8039](https://github.com/jellyfin/jellyfin/pull/8039) –
  "Implement a Modular Home screen and User facing plugin pages") die in deze
  richting werkt, maar dit is **geen gegarandeerd onderdeel van een stabiele
  Jellyfin-release**.

### Conclusie / gekozen aanpak

Deze plugin gebruikt daarom een **dubbele aanpak**:

1. **Plan A — optionele integratie met "Home Screen Sections"** (indien je die
   plugin al hebt geïnstalleerd). Deze plugin detecteert of die plugin
   geladen is en logt instructies; de daadwerkelijke registratie-call is
   bewust als duidelijk gemarkeerd uitbreidingspunt achtergelaten
   (`HomeScreen/HomeScreenSectionsIntegration.cs`), omdat de exacte
   methode-signatures per versie kunnen verschillen.
2. **Plan B — eigen REST API + web-injectie (primaire, werkende oplossing)**:
   - De plugin levert een REST API (`/NetflixRows/...`) die per genre-rij de
     bijpassende items teruggeeft, gebaseerd op je echte bibliotheek-metadata.
   - Bij serverstart patcht de plugin éénmalig `index.html` van de Jellyfin
     web client om een klein script + stylesheet te laden
     (`netflixRows.js` / `netflixRows.css`).
   - Dat script bouwt op de homepage automatisch de geconfigureerde
     genre-rijen op (Netflix-stijl horizontale scroll-rijen met posters),
     met data rechtstreeks uit de plugin-API.

Dit geeft je **echte dynamische rijen** (geen handmatige Collections) die
**automatisch meegroeien** met nieuwe content na een library scan, zonder
afhankelijk te zijn van een instabiele core-API.

---

## 2. Architectuur

```
┌─────────────────────────────┐
│        Jellyfin Server       │
│                               │
│  ┌─────────────────────────┐ │
│  │  Jellyfin.Plugin.        │ │
│  │  NetflixRows (.dll)      │ │
│  │                          │ │
│  │  - PluginConfiguration   │ │   admin
│  │  - RowQueryService  ─────┼─┼──► configuratiepagina
│  │  - NetflixRowsController │ │   (Dashboard > Plugins)
│  │  - WebInjectionEntryPoint│ │
│  │  - HomeScreenSections    │ │
│  │    Integration (optional)│ │
│  └───────────┬──────────────┘ │
│              │ ILibraryManager  │
│              │ IDtoService      │
│              ▼                  │
│   Films / Series bibliotheken   │
│   (genre-metadata)               │
└──────────────┬───────────────────┘
               │ REST API: /NetflixRows/Rows, /Rows/{id}/Items, /web/*
               ▼
┌───────────────────────────────────┐
│        Jellyfin Web Client         │
│  index.html (1x gepatcht)          │
│   └─ <script netflixRows.js>       │
│        - haalt rijen + items op    │
│        - injecteert .netflixRows-  │
│          section rijen op homepage │
│        - <link netflixRows.css>    │
└───────────────────────────────────┘
```

### Data-flow

1. Admin configureert in **Dashboard → Plugins → Netflix Rows**:
   - welke bibliotheken meedoen,
   - per rij: titel (incl. emoji), genres, type (films/series/beide),
     sorteervolgorde, min/max aantal items, enabled/disabled.
2. `RowQueryService` vertaalt elke rij naar een `InternalItemsQuery` op
   `ILibraryManager` (filter op `BaseItemKind`, `Genres`, `AncestorIds`,
   `OrderBy`, `Limit`).
3. `NetflixRowsController` exposeert:
   - `GET /NetflixRows/Rows` — lijst van actieve rijen (id + titel) die
     genoeg items hebben (≥ `MinItems`).
   - `GET /NetflixRows/Rows/{id}/Items` — items voor die rij, als
     `BaseItemDto[]` (incl. image tags), voor de ingelogde gebruiker.
   - `GET /NetflixRows/Genres` / `GET /NetflixRows/Libraries` — hulpdata voor
     de configuratiepagina (admin-only).
   - `GET /NetflixRows/web/{file}` — serveert `netflixRows.js` /
     `netflixRows.css` (anoniem toegankelijk, nodig vóór login).
4. `WebInjectionEntryPoint` (een `IHostedService`) patcht bij serverstart
   eenmalig `index.html` zodat die `<script>` + `<link>` laadt. Een backup
   (`index.html.netflixrows.bak`) wordt bewaard.
5. `netflixRows.js` draait in de browser, gebruikt `window.ApiClient`
   (al aanwezig in Jellyfin web) om de plugin-API aan te roepen, en bouwt de
   rijen direct onder de bestaande homepage-secties.

Omdat alles **live queries** op `ILibraryManager` zijn (geen eigen cache van
item-lijsten), verschijnen nieuwe films/series automatisch in de juiste rij
na een normale library scan — er is geen extra scheduled task nodig.

---

## 3. Volledige projectstructuur

```
jellinet/
├── README.md
├── build.yaml
├── repository/
│   └── manifest.json                 # template voor een eigen plugin-repo
└── src/
    └── Jellyfin.Plugin.NetflixRows/
        ├── Jellyfin.Plugin.NetflixRows.csproj
        ├── Plugin.cs
        ├── PluginServiceRegistrator.cs
        ├── Configuration/
        │   ├── PluginConfiguration.cs
        │   ├── configPage.html
        │   └── configPage.js
        ├── Api/
        │   ├── Dtos.cs
        │   └── NetflixRowsController.cs
        ├── Services/
        │   └── RowQueryService.cs
        ├── HomeScreen/
        │   └── HomeScreenSectionsIntegration.cs
        └── WebInjection/
            ├── WebInjectionEntryPoint.cs
            └── wwwroot/
                ├── netflixRows.js
                └── netflixRows.css
```

Alle C#-bestanden staan al op hun plek hierboven (`src/Jellyfin.Plugin.NetflixRows/...`).
De `.js`/`.css`/`.html` bestanden worden als **embedded resources** in de
plugin-DLL gebouwd (zie `.csproj`) — je hoeft ze niet los te kopiëren naar de
Jellyfin web-map.

---

## 4. Build-instructies

### Vereisten

- [.NET SDK 8.0](https://dotnet.microsoft.com/download)
- Git (optioneel, voor versiebeheer)

### Build

```powershell
cd c:\Users\Jesse\Desktop\jellinet\src\Jellyfin.Plugin.NetflixRows
dotnet restore
dotnet build -c Release
```

De gecompileerde plugin-DLL komt in:

```
src\Jellyfin.Plugin.NetflixRows\bin\Release\net8.0\Jellyfin.Plugin.NetflixRows.dll
```

> Als `dotnet build` faalt op de `Jellyfin.Controller` NuGet-versie
> (`10.10.3`), check welke Jellyfin server-versie je draait en pas de
> `<PackageReference Include="Jellyfin.Controller" Version="..." />` in de
> `.csproj` aan naar de bijpassende versie (zie
> [NuGet: Jellyfin.Controller](https://www.nuget.org/packages/Jellyfin.Controller)).

### Releasezip maken (handmatig, lokaal)

```powershell
cd src\Jellyfin.Plugin.NetflixRows\bin\Release\net8.0
Compress-Archive -Path Jellyfin.Plugin.NetflixRows.dll -DestinationPath NetflixRows_1.0.0.0.zip
```

Update daarna `repository/manifest.json` met de echte `sourceUrl` en de
MD5-checksum van de zip (`Get-FileHash -Algorithm MD5 NetflixRows_1.0.0.0.zip`).

> Als je de plugin via GitHub publiceert (zie §9), doet de GitHub Actions
> workflow dit automatisch — dan hoef je dit handmatige stappenplan niet te
> volgen.

---

## 5. Installatie-instructies

### Optie A — Handmatig (zonder eigen plugin-repo)

1. Build de plugin (zie hierboven) of download de `.dll`.
2. Stop de Jellyfin server.
3. Maak een map aan in de Jellyfin **plugins**-map:
   - Linux: `/var/lib/jellyfin/plugins/NetflixRows_1.0.0.0/`
   - Windows: `%ProgramData%\Jellyfin\Server\plugins\NetflixRows_1.0.0.0\`
4. Kopieer `Jellyfin.Plugin.NetflixRows.dll` naar die map.
5. Start Jellyfin opnieuw.
6. Ga naar **Dashboard → Plugins → Netflix Rows** om de configuratiepagina te
   openen en je rijen/bibliotheken in te stellen.
7. Sla op, **herstart Jellyfin nogmaals** zodat de web-injectie (`index.html`
   patch) wordt toegepast.

### Optie B — Via je eigen GitHub plugin-repo (aanbevolen, zie §9)

1. In Jellyfin: **Dashboard → Plugins → Repositories → Add Repository**, voer
   de **raw GitHub-URL** naar `repository/manifest.json` in, bv.:
   `https://raw.githubusercontent.com/<gebruiker>/<repo>/main/repository/manifest.json`
2. Ga naar **Catalog**, zoek "Netflix Rows" en installeer.
3. Herstart Jellyfin twee keer (1x om de plugin te laden, 1x na het opslaan
   van de configuratie zodat de web-injectie wordt toegepast).

### Docker

Als je Jellyfin in Docker draait:

1. Plaats de plugin-map (`NetflixRows_1.0.0.0/Jellyfin.Plugin.NetflixRows.dll`)
   in het **gemounte plugins-volume** van je container, bv.:

   ```yaml
   services:
     jellyfin:
       image: jellyfin/jellyfin
       volumes:
         - ./jellyfin-config:/config
         - ./jellyfin-cache:/cache
         - ./media:/media
   ```

   → de plugin gaat dan in `./jellyfin-config/plugins/NetflixRows_1.0.0.0/`.

2. **Belangrijk voor web-injectie in Docker**: het officiële image bevat de
   web client meestal op `/jellyfin/jellyfin-web` binnen de container
   (`IApplicationPaths.WebPath`). De plugin schrijft daar `index.html` naar
   toe. Zorg dat:
   - de container-user (vaak `root` of `abc`, afhankelijk van het image)
     schrijfrechten heeft op die map, **of**
   - je `JELLYFIN_WEB_DIR` naar een eigen, schrijfbare, gemounte map laat
     wijzen.
3. `docker compose restart jellyfin` na het plaatsen van de plugin én na het
   wijzigen van de configuratie (voor de `index.html`-patch).
4. Bij een **image-update** wordt `jellyfin-web` meestal vervangen → de patch
   verdwijnt, maar wordt bij de volgende start automatisch opnieuw toegepast
   door `WebInjectionEntryPoint` (zolang de plugin geladen is).

---

## 6. Testplan

1. **Plugin laadt correct**
   - Na installatie + restart: Dashboard → Plugins → "Netflix Rows" is
     zichtbaar, geen errors in `Dashboard → Logs`.
2. **Configuratiepagina werkt**
   - Open de configuratiepagina, controleer of bibliotheken en (na een
     scan) genres worden opgehaald (`/NetflixRows/Libraries`,
     `/NetflixRows/Genres`).
   - Voeg/verwijder/wijzig rijen, sla op, herlaad de pagina → wijzigingen
     blijven behouden.
3. **API levert data**
   - Log in op Jellyfin web, open in een nieuw tabblad:
     `https://<server>/NetflixRows/Rows` (met geldige sessie/cookie) → JSON
     met rijen die genoeg items hebben.
   - `https://<server>/NetflixRows/Rows/<id>/Items` → lijst met
     `BaseItemDto`'s (films/series met posters).
4. **Web-injectie**
   - Controleer of `index.html` van de web client de
     `<!-- NetflixRows:start --> ... <!-- NetflixRows:end -->` blok bevat.
   - Open de Jellyfin homepage (hard refresh / cache leeg) → de
     geconfigureerde rijen (🔥 Actie Films, 😂 Komedie Films, ...) verschijnen
     onder de standaard secties, als horizontaal scrollbare posterrijen.
5. **Automatische updates na scan**
   - Voeg een nieuwe film met genre "Horror" toe aan je bibliotheek.
   - Trigger een library scan.
   - Herlaad de homepage → de film verschijnt in de rij "👻 Horror Films"
     (indien `MinItems` gehaald wordt).
6. **Edge cases**
   - Rij met genre dat niet voorkomt → rij wordt niet getoond
     (`MinItems` niet gehaald).
   - `EnableWebInjection` uitzetten → rijen verdwijnen van de homepage, API
     blijft werkbaar (handig als je toch een eigen front-end bouwt).
   - Meerdere gebruikers met verschillende kijkrechten/parental controls →
     `GetItems` gebruikt de ingelogde gebruiker (`InternalItemsQuery(user)`),
     dus per-user filtering zou via Jellyfin's standaard mechanismen moeten
     gelden (controleer dit expliciet, dit is niet end-to-end getest).

---

## 7. Beperkingen & mogelijke problemen bij Jellyfin-updates

- **Geen officiële homepage-rij-API**: deze oplossing leunt op
  *web-injectie* van `index.html`. Een grote herschrijving van de Jellyfin
  web client (nieuwe Vue/React-architectuur, andere DOM-structuur/CSS-classes
  zoals `.homeSectionsContainer`) kan betekenen dat `netflixRows.js` de juiste
  container niet meer vindt en dus niets injecteert. De plugin-API blijft dan
  gewoon werken; alleen de visuele integratie moet bijgewerkt worden
  (selector(s) in `netflixRows.js` aanpassen).
- **`index.html`-patch wordt overschreven bij elke Jellyfin server-update**
  (nieuwe web client wordt uitgepakt). Dit is **opgevangen**: bij elke
  serverstart controleert `WebInjectionEntryPoint` of de marker aanwezig is en
  patcht opnieuw indien nodig. Een backup (`index.html.netflixrows.bak`) wordt
  bewaard voor handmatig herstel.
- **`Jellyfin.Controller` NuGet-versie ↔ server-versie**: de plugin moet
  gebouwd worden tegen een SDK-versie die compatibel is met je
  server-versie (`targetAbi` in `build.yaml`). Bij grote Jellyfin-updates
  (bv. 10.10 → 10.11) kan een rebuild met een nieuwere
  `Jellyfin.Controller`-versie nodig zijn.
- **"Home Screen Sections"-integratie is bewust niet hard-coded**: de interne
  API van die plugin is geen stabiele contract. `HomeScreenSectionsIntegration.cs`
  detecteert alleen of de plugin geladen is en logt instructies — de
  daadwerkelijke `RegisterResultsDelegate`-call moet je zelf invullen tegen de
  versie die je hebt geïnstalleerd (zie code-comments in dat bestand).
- **Genre-namen zijn vrije tekst**: als je metadata-provider andere
  genre-namen gebruikt dan in je rij-configuratie (bv. "Sci-Fi" vs.
  "Science Fiction"), matcht de rij niets. Gebruik de
  `/NetflixRows/Genres`-lijst (zichtbaar via de configuratiepagina) om de
  exacte namen uit je eigen bibliotheek over te nemen.
- **Permissions/parental controls**: `GetItems` geeft de ingelogde gebruiker
  door aan `ILibraryManager`, wat normaal gesproken bibliotheekrechten en
  parental controls respecteert — dit is echter **niet end-to-end getest** in
  dit project en moet gecontroleerd worden voor productiegebruik met
  meerdere accounts/kinderprofielen.

---

## 8. Volgende stappen (aanraders, niet geïmplementeerd)

- Compileren tegen een echte Jellyfin dev-omgeving en API-mismatches
  oplossen (zie disclaimer boven).
- `HomeScreenSectionsIntegration` afronden tegen de daadwerkelijk
  geïnstalleerde versie van de "Home Screen Sections"-plugin, voor een
  native (niet-geïnjecteerde) weergave.
- Eventueel caching toevoegen (`IMemoryCache`) als bibliotheken erg groot
  zijn en `/NetflixRows/Rows/{id}/Items` te traag wordt.
- Unit tests voor `RowQueryService` (sortering, genre-matching, min/max
  logica).

---

## 9. Publiceren op GitHub als Jellyfin plugin-repo

Doel: gebruikers voegen één link toe via
**Dashboard → Plugins → Repositories → Add Repository**, en kunnen daarna
"Netflix Rows" direct installeren via de **Catalog**.

Deze repo is al voorbereid:

- `.gitignore` (build-output wordt niet meegecommit)
- `.github/workflows/release.yml` — GitHub Actions workflow die bij het
  pushen van een **versie-tag** (`v1.0.0.0`) automatisch:
  1. de plugin bouwt (`dotnet build`),
  2. een release-zip maakt met **jprm** (Jellyfin Plugin Repository Manager),
  3. een **GitHub Release** aanmaakt met die zip als asset,
  4. `repository/manifest.json` bijwerkt (versie, checksum, download-URL) en
     terug commit naar `main`.
- `repository/manifest.json` — start leeg (`[]`), wordt door de workflow
  gevuld.

### Stappen

1. **Maak een lege GitHub-repository** aan op github.com (bv.
   `jellyfin-plugin-netflix-rows`), zonder README/license (die heb je al).
2. **Koppel en push** vanuit deze map:

   ```powershell
   cd c:\Users\Jesse\Desktop\jellinet
   git add .
   git commit -m "Initial commit: Netflix Rows plugin"
   git branch -M main
   git remote add origin https://github.com/<gebruiker>/<repo>.git
   git push -u origin main
   ```

3. **Maak een release-tag** (versie moet matchen met `version` in
   `src/Jellyfin.Plugin.NetflixRows/build.yaml`, met een `v`-prefix):

   ```powershell
   git tag v1.0.0.0
   git push origin v1.0.0.0
   ```

4. Wacht tot de **Actions**-run groen is (tab "Actions" op GitHub). Daarna
   bevat `repository/manifest.json` op `main` een geldige entry met
   download-URL + checksum, en staat de `.zip` als asset onder
   **Releases**.
5. **Deel deze URL** met gebruikers:

   ```
   https://raw.githubusercontent.com/<gebruiker>/<repo>/main/repository/manifest.json
   ```

   Zij voegen die toe via **Dashboard → Plugins → Repositories → Add
   Repository** en installeren "Netflix Rows" via **Catalog**.

### Nieuwe versie uitbrengen

1. Verhoog `version` in `src/Jellyfin.Plugin.NetflixRows/build.yaml` (en in
   `Plugin.cs`-assembly-versie indien je die ook gebruikt).
2. Commit, push naar `main`.
3. Tag de nieuwe versie (`git tag v1.0.1.0 && git push origin v1.0.1.0`).
4. De workflow voegt automatisch een nieuwe entry toe aan
   `repository/manifest.json` — bestaande gebruikers zien de update in
   **Dashboard → Plugins → Catalog**.

> **Let op (vibe-coded disclaimer, zie boven):** de `jprm`-commando's in de
> workflow (`jprm plugin build`, `jprm repo add`) zijn gebaseerd op de
> gepubliceerde werking van
> [jprm](https://github.com/oddstr13/jellyfin-plugin-repository-manager) maar
> zijn niet end-to-end getest. Als de workflow faalt, check de exacte
> CLI-flags met `jprm plugin build --help` / `jprm repo add --help` in de
> Actions-log en pas `release.yml` aan.
