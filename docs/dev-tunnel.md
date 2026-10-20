# Prøv appen på en telefon med en Dev Tunnel

En Dev Tunnel giver din lokale app en https-adresse, som telefonen kan åbne over wifi eller 4G. Du behøver hverken kabel eller udviklerindstillinger på telefonen.

## Sådan hænger det sammen

Lokalt kører Web (port 5134) og API (port 5156) hver for sig. En tunnel kan kun pege på ét sted, så i udvikling sender API'et alt, der ikke er `/api`, videre til Web-serveren (`DevFrontend__Url` i `launchSettings.json`). Dermed er hele appen tilgængelig på API'ets adresse, og **én tunnel til port 5156 er nok**. Det er kun slået til i Development.

Du kan også åbne appen lokalt på `http://localhost:5156` i stedet for `http://localhost:5134`. Begge dele virker.

## Engangsopsætning

```bash
winget install Microsoft.devtunnel
devtunnel user login
devtunnel create vores-familie
devtunnel port create vores-familie -p 5156
```

`user login` åbner et vindue, hvor du logger ind med en Microsoft- eller GitHub-konto. Tunnelen er privat og kan kun åbnes af den, der er logget ind med samme konto.

## Hver gang du vil teste

1. Start API og Web i Visual Studio med **http-profilerne** (F5). Begge projekter skal køre.
2. Start tunnelen i en terminal, og lad den køre:

   ```bash
   devtunnel host vores-familie
   ```

   Den skriver en adresse som `https://<id>-5156.euw.devtunnels.ms`. Adressen er den samme hver gang.
3. Åbn adressen i Chrome på telefonen. Første gang beder Microsoft dig logge ind med samme konto og trykke **Continue**.
4. Første indlæsning er langsom (debug-udgaven af appen er stor). Brug wifi.

Vil du lade andre i familien prøve uden at logge ind, kan du tilføje `--allow-anonymous` til `devtunnel create`. Så kan **alle med adressen** åbne appen og oprette brugere, så sluk tunnelen (Ctrl+C), når du er færdig.

## Google Kalender over tunnelen

Google skal kende tunnelens adresse. Tilføj `https://<id>-5156.euw.devtunnels.ms/api/google/callback` under **Autoriserede omdirigerings-URI'er** på OAuth-klienten (se [google-setup.md](google-setup.md)). Uden det virker alt andet, men "Forbind Google Kalender" gør ikke.

## Fejlfinding

| Symptom | Løsning |
| --- | --- |
| Siden viser "Web-projektet svarer ikke på http://localhost:5134" | Web-projektet kører ikke. Start det (F5), og opdater siden. |
| Tunnelen svarer 404 eller tom side | Tjek, at API'et kører på port 5156 og at `devtunnel port create … -p 5156` er kørt. |
| Telefonen vil ikke logge ind | Åbn adressen i Chrome (ikke en indbygget browser i en anden app), og tjek, at du bruger https-adressen. |
| `devtunnel` findes ikke | Luk og åbn terminalen igen efter installationen. |
