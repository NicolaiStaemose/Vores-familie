# Opsætning af Google Kalender

Appen læser og skriver Google Kalender på vegne af hvert familiemedlem. Hver person forbinder sin egen Google-konto. Du skal bruge ét Google Cloud-projekt med en OAuth-klient. Det er gratis og tager ca. 15 minutter.

> Uden opsætning kan du stadig prøve alle kalenderfunktioner med demodata, se "Prøv uden Google" nederst.

## 1. Opret projekt og slå Calendar API til

1. Gå til <https://console.cloud.google.com/> og log ind med en Google-konto (fx din egen).
2. Øverst: **Vælg et projekt → Nyt projekt**. Navn: `Vores familie`. Opret.
3. Menu **API'er og tjenester → Bibliotek**, søg efter **Google Calendar API** og tryk **Aktivér**.

## 2. Samtykkeskærm (OAuth consent screen)

1. **API'er og tjenester → OAuth-samtykkeskærm** (kan hedde *Google Auth Platform → Branding/Målgruppe*).
2. Brugertype: **Ekstern**. Appnavn: `Vores familie`, support-mail og udviklerkontakt: din mail.
3. **Områder (scopes):** tilføj `https://www.googleapis.com/auth/calendar`.
4. **Vigtigt:** sæt publiceringsstatus til **I produktion** (*In production*). Står den på *Testing*, udløber refresh-tokens efter 7 dage, og synken stopper.
   Uden Googles verificering viser Google en advarselsside ved første login ("Google har ikke bekræftet denne app"). Tryk **Avanceret → Gå til Vores familie (usikker)**. Det er fint til en privat familieapp, og hver person gør det én gang.

## 3. Opret OAuth-klient

1. **Legitimationsoplysninger → Opret legitimationsoplysninger → OAuth-klient-id**.
2. Programtype: **Webapplikation**.
3. **Autoriserede omdirigerings-URI'er** (præcis disse, API-adressen + `/api/google/callback`):
   - Lokalt i Visual Studio: `http://localhost:5156/api/google/callback` og `https://localhost:7222/api/google/callback`
   - Lokalt med Docker: `http://localhost:8080/api/google/callback`
   - Azure senere: `https://<jeres-domæne>/api/google/callback`
4. Gem. Kopiér **Klient-id** og **Klienthemmelighed**.

## 4. Læg hemmelighederne i konfigurationen (aldrig i repoet)

### Visual Studio / `dotnet run`

```bash
cd src/VoresFamilie.Api
dotnet user-secrets set "Google:Calendar:ClientId" "<klient-id>"
dotnet user-secrets set "Google:Calendar:ClientSecret" "<klienthemmelighed>"
dotnet user-secrets set "Google:Mode" "Real"
```

`Google:Mode=Real` slår demodata fra (i Development er standarden `Fake`). Genstart API'et.

### Docker Compose

Kopiér `.env.example` til `.env` og udfyld `GOOGLE_CALENDAR_CLIENT_ID` og `GOOGLE_CALENDAR_CLIENT_SECRET`. Tilføj redirect-URI'en `http://localhost:8080/api/google/callback`.

### Azure (senere)

Gem værdierne som Container Apps-secrets/Key Vault og sæt `Google__Calendar__ClientId` og `Google__Calendar__ClientSecret`.

## 5. Forbind i appen

1. Log ind som admin → **Google** i menuen → **Forbind Google Kalender** → vælg din konto → giv adgang.
2. Under **Tilføj kalender** vælger du for hver kalender, hvad den er:
   - **Fælles:** familiens kalender.
   - **Lektier:** børnenes lektiekalendere (fx "Emma lektier", "Noah lektier"). Vælg barnet under "Tilhører".
   - **Skema:** et barns skole-skema (fx "Emma skema"). Vælg barnet under "Tilhører". Skemaet vises på barnets **Min side** og kan kun rettes af voksne.
   - **Personlig:** hvert medlems egen kalender. Børn og voksne forbinder selv deres Google-konto og tilføjer deres egen personlige kalender.
3. Åbn **Kalender** og **Lektier**.

Lektier er begivenheder, hvis titel indeholder "lektie" (uanset store/små bogstaver). Fag og barn læses fra linjerne `Fag:` og `Barn:` i beskrivelsen. Afkrydsning gemmes kun i appen; Google-begivenhederne ændres ikke.

## Fejlfinding

| Symptom | Løsning |
| --- | --- |
| `redirect_uri_mismatch` hos Google | Redirect-URI'en i Google Cloud skal være præcis API-adressen + `/api/google/callback` (http/https og port tæller). |
| "Google gav ingen langtidsadgang" | Fjern appens adgang på <https://myaccount.google.com/permissions> og forbind igen. |
| Synken stopper efter en uge | Projektet står stadig på *Testing*. Sæt det til *I produktion* og forbind igen. |
| "Google-forbindelsen er udløbet" | Adgangen er trukket tilbage. Forbind igen under Google-indstillinger. |

## Prøv uden Google

Sæt `Google:Mode` til `Fake` (standard i Development, eller `GOOGLE_MODE=Fake` i Docker). Appen bruger så en indbygget demo-Google med en fælles kalender og lektiekalendere, så kalender, lektier og afkrydsning kan prøves uden konto. Data nulstilles, når API'et genstartes.

