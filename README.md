# Vores familie

Familieapp (Android, installeret PWA): fælles kalender, lektier, opgaver, madplan m.m. Se [CLAUDE.md](CLAUDE.md) for mål, stack og faser.

**Status: fase 4** – login, familier og rettigheder (fase 1), kalendere med tovejs-synk til Google Kalender og lektier (fase 2), rutiner, daglige opgaver og ToDo (fase 3) samt madplan, indkøbsliste og rengøringsplan (fase 4).

## Arkitektur

```
Browser ─▶ Caddy (:8080) ─┬─ /api/*, /healthz ─▶ backend  (ASP.NET Core, Identity-cookie, EF Core)
                          └─ alt andet        ─▶ frontend (nginx med Blazor WASM)
```

- `src/VoresFamilie.Domain` – `Family` (aggregate root), medlemmer, invitationer, rettigheder. Ingen afhængigheder.
- `src/VoresFamilie.Application` – use cases (`FamilyService`), DTO'er, interfaces.
- `src/VoresFamilie.Infrastructure` – EF Core (SQLite lokalt, SQL Server/Azure SQL i skyen) og Identity.
- `src/VoresFamilie.Api` – WebAPI. OpenAPI-kontrakten skrives til `contracts/openapi.json` ved build.
- `src/VoresFamilie.Web` – Blazor WASM. Kender kun den genererede klient, aldrig domænet.
- `tests/` – xUnit: domæne, application og API-integrationstests.

Frontend-klienten (`src/VoresFamilie.Web/Generated/ApiClient.g.cs`) genereres af NSwag fra `contracts/openapi.json` ved build. Ændrer du API'et, så commit den opdaterede `contracts/openapi.json` (CI fejler ellers).

## Kør lokalt

```bash
docker compose up --build   # åbn http://localhost:8080
```

Valgfrit Google-login: kopiér `.env.example` til `.env` og udfyld `GOOGLE_CLIENT_ID`/`GOOGLE_CLIENT_SECRET` (redirect-URI: `http://localhost:8080/api/auth/google-signin`). Uden dem skjules Google-knappen. Hemmeligheder må aldrig ind i repoet.

Uden Docker: `dotnet user-secrets` i `src/VoresFamilie.Api` og `dotnet run`.

Vil du prøve appen på en telefon, så se [docs/dev-tunnel.md](docs/dev-tunnel.md).

## Kalendere og Google

Se [docs/google-setup.md](docs/google-setup.md). I Development kører API'et som standard med en indbygget demo-Google (`Google:Mode=Fake`), så kalender og lektier kan prøves uden Google-konto. `https://localhost:7222/scalar` viser og tester API'et.

## Rutiner, opgaver og ToDo

- **Min side** er den daglige oversigt for ét familiemedlem: morgenrutine, aftenrutine, dagens opgaver, ToDo og kalender for en dag. Man kan gå en dag frem eller tilbage; kun i dag og i går kan krydses af, og alt nulstilles hver dag.
- **Rutiner** (menuen "Rutiner", kræver redigeringsret til området Rutiner): sæt punkter op pr. barn og husopgaver med ugedage og ansvarlig.
- **ToDo**: fælles liste med ansvarlig og frist.

## Madplan, indkøb og rengøring

- **Madplan:** aftensmad pr. dag med ingredienser. Retter kan gemmes og genbruges, og en dag eller hele ugen kan lægges på indkøbslisten (uden dubletter).
- **Indkøb:** liste grupperet efter butikkens afdelinger (foreslås automatisk ud fra navnet), mængde, faste varer og "Ryd købte".
- **Rengøring:** opgaver pr. rum med interval og "sidst gjort". Forfaldne opgaver er markeret, og Min side viser dem for den, de hører til.
- Rettighederne styres pr. område (Madplan, Indkøb, Rengøring) på Admin-siden.

## Test

```bash
dotnet test VoresFamilie.slnx
```

## Database

- Lokalt/test: SQLite (`Database:Provider=Sqlite`), oprettes automatisk.
- Azure SQL: `Database:Provider=SqlServer` og `ConnectionStrings:Default`; migrations køres ved opstart. Ny migration:
  `dotnet ef migrations add <Navn> -p src/VoresFamilie.Infrastructure -s src/VoresFamilie.Infrastructure -o Migrations`

## Sikkerhed

Cookie-login (HttpOnly, SameSite=Lax). API'et accepterer kun JSON og har ingen CORS, så andre sites ikke kan sende state-ændrende kald; yderligere antiforgery kan tilføjes senere.




