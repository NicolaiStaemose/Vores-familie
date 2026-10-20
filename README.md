# Vores familie

Familieapp (Android, installeret PWA): fælles kalender, lektier, opgaver, madplan m.m. Se [CLAUDE.md](CLAUDE.md) for mål, stack og faser.

**Status: fase 2** – login, familier og rettigheder (fase 1) samt kalendere med tovejs-synk til Google Kalender og lektier, der kan afkrydses (fase 2).

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

## Kalendere og Google

Se [docs/google-setup.md](docs/google-setup.md). I Development kører API'et som standard med en indbygget demo-Google (`Google:Mode=Fake`), så kalender og lektier kan prøves uden Google-konto. `https://localhost:7222/scalar` viser og tester API'et.

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

