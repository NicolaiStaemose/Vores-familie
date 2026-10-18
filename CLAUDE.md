# Vores familie

Familieapp til Android som installeret PWA. GitHub-repo: `Vores-familie`. Ejer: Nicolai. Brugerfladen er på dansk (da-DK, tidszone Europe/Copenhagen). Kode, identifiers og commit-beskeder er på engelsk. Svar til Nicolai på dansk.

## Mål

En fælles app for en familie på fire med Android-telefoner. Børnene har hver deres telefon og egen Email.

Funktioner:
- Fælles kalender, en personlig kalender pr. medlem og børnenes lektie-kalender
- Lektier vises som opgaver, som børnene kan klikke af, når de er udført
- Madplan og indkøbsliste
- Daglige opgaver i hjemmet (lave aftensmad, rydde op i køkkenet, smøre madpakker)
- Daglige rutiner pr. barn, som tjekkes af om morgenen og om aftenen
- Rengøringsplan for huset
- Den store ToDo-liste
- Admin-side, hvor admin kan invitere til familien og styre rettigheder
- Login med Email eller Google-konto
- Synkronisering med familiens Google-kalendere

Forslag, der er aftalt som mulige senere tilføjelser: madplan fylder indkøbslisten, faste favoritvarer og gemte opskrifter, roterende opgaver, stjerner eller lommepenge for rutiner, push kl. 07:00 og 19:00, ugeoverblik på forsiden, rengøringsplan med "sidst gjort", offline indkøbsliste.

## Stack (bekræftet af Nicolai)

- C#, ASP.NET Core WebAPI, Blazor WebAssembly som PWA
- Azure SQL (gratis udgave) i Azure. SQLite kun lokalt til tests og udvikling
- Hosting i Azure Container Apps. Docker Compose lokalt
- Brug .NET 10 (LTS), hvis den er installeret. .NET 8 slutter support i november 2026. Tjek `dotnet --version`
- Installeret PWA, ikke Play Store. En Play-skal (Trusted Web Activity) kan tilføjes senere uden at ændre appen

## Arkitektur

Clean Architecture og DDD. `Family` er aggregate root.

Projekter: `VoresFamilie.Domain`, `.Application`, `.Infrastructure`, `.Api`, `.Web` (Blazor) og testprojekter.

Frontend skal kunne skiftes ud, hvis Blazor ikke fungerer:
- To containere: backend (WebAPI) og frontend (nginx med statiske Blazor-filer)
- Reverse proxy foran (YARP, Caddy eller Traefik): `/api` går til backend, alt andet til frontend, så de har samme origin
- OpenAPI er kontrakten. Frontend-klienten genereres fra specifikationen (NSwag eller Kiota)
- Frontend får kun DTO'er. Del aldrig domæneentiteter med Blazor
- Login (cookies) og Web Push (VAPID) ligger i backend. Frontend har kun service worker og en subscribe-knap
- Brug Blazor WASM, ikke Blazor Server

## Login, familier og rettigheder

- ASP.NET Core Identity med cookie-login. Email og Google som ekstern udbyder
- Admin inviterer via invitationslink
- Roller: Admin, Voksen, Barn
- Børn kan se alt som standard. Admin-siden styrer pr. medlem og pr. område (kalendere, lektier, madplan, indkøb, rutiner, rengøring, ToDo) med tre niveauer: ingen adgang, kan se, kan se og redigere
- Som start må børn afkrydse deres egne rutiner og lektier. Alt andet åbner admin for
- Børnene logger ind med egne konti på egne telefoner, ingen PIN-profiler
- Hvis børnenes Google-konti er Family Link-konti (under 13), kan appen kræve godkendelse fra forælder. Test det tidligt

## Google Kalender-synk

- Google Calendar API med OAuth
- Hold OAuth-appen på "In production" uden verificering. I "Testing" udløber refresh-tokens efter 7 dage, og synken stopper. Der kommer en advarselsside ved første login
- Hemmeligheder (client secret, connection strings, VAPID-nøgler) hører ikke hjemme i repoet. Brug user-secrets lokalt og Key Vault eller Container Apps-secrets i Azure

## Lektier

Appen henter ikke selv fra Aula. En planlagt opgave ("Hent lektier", kører fredage kl. 15:00 og kræver MitID hver gang) synkroniserer MinUddannelses ugeplan til børnenes Google-kalendere, der hedder "Emma lektier" og "Noah lektier". Kalender-id'erne står i den planlagte opgaves prompt og lægges i konfiguration, ikke i koden. Lektier er derfor højst en uge gamle i appen.

Sådan ser begivenhederne ud (tjekket mod rigtige data i uge 43):
- Hele ugeplanen synkroniseres, ikke kun lektier. Eksempler: "99 arter", "Program", "Omfotografering"
- Titel: den oprindelige titel fra ugeplanen, uændret
- Beskrivelse: opgaveteksten, en tom linje, så `Barn: <fulde navn>` og `Fag: <fag>`
- Tidspunkt: fra barnets importerede AULA-skema, ellers ugeplanens tid, ellers 08:00-08:45
- Lektie: titlen indeholder "lektie" (uanset store/små bogstaver, fx "lektie", "Lektie: 5A eller 5B", "LEKTIE: ..."). Så har begivenheden en popup-påmindelse dagen før kl. 15:00
- Opgaven springer dubletter over og opdaterer ikke eksisterende begivenheder

I appen:
- Lektie-parser: summary matcher `lektie` uden hensyn til store/små bogstaver. Fag og barn læses fra beskrivelsens `Fag:`- og `Barn:`-linjer
- Resten af ugeplanen vises som almindelige begivenheder i barnets kalender
- Afkrydsning gemmes i appens egen database med kalender-id og begivenheds-id som nøgle. Google-begivenhederne ændres ikke

## Azure SQL gratis udgave

- Serverless og pauser, når den ikke bruges. Første kald efter pause kan tage op til et minut. Brug retry (`EnableRetryOnFailure`) og rimelig command timeout
- Månedligt gratis kvantum er cirka 100.000 vCore-sekunder. Ved oprettelse vælges, om databasen pauser, når kvantummet er brugt, eller kører videre mod betaling. Tjek de aktuelle grænser i portalen

## Faser

1. Solution med Clean Architecture, de to containere og reverse proxy, OpenAPI, login (Email og Google), familier, invitationer og admin-side med rettigheder
2. Kalendere (fælles, personlige, lektier), Google-synk og lektier som afkrydsbare opgaver
3. Rutiner morgen og aften, daglige opgaver og ToDo
4. Madplan, indkøbsliste og rengøringsplan
5. PWA-polering, push-notifikationer og offline

Byg fase for fase, og få noget brugbart ud efter hver. Skriv tests til domænelogik og API. Kør build og tests, før noget committes.

## Første skridt

1. Tjek `dotnet --version`, `docker --version` og `git --version`
2. Opret repoet `Vores-familie` på GitHub og klon det
3. Start fase 1

## Åbne beslutninger

- Azure-abonnement, resource group og region
- Domænenavn og HTTPS til appen (Container Apps giver HTTPS på standarddomæne)
- Præcis rettighedsmodel for voksne, der ikke er admin
