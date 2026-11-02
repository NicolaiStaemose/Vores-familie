# Notifikationer, installation og offline

## Hvad appen gør

- **Påmindelser kl. 07:00 og 19:00** (dansk tid) til hver person, der har slået notifikationer til på sin telefon.
  - 07:00: dagens aftensmad og det, personen ikke har krydset af endnu (morgenrutine, opgaver).
  - 19:00: aftenrutine, lektier der mangler og forfalden rengøring, som er personens eller ingens.
  - Der sendes ingen besked, hvis der ikke er noget at minde om. Hver person får højst én besked pr. tidspunkt pr. dag.
  - Hver person vælger selv morgen/aften under **Mere → Notifikationer**.
- **Installation:** Chrome på Android tilbyder "Installér app" / "Føj til startskærm". Under **Mere** står også en "Installér appen"-knap, når browseren tilbyder det.
- **Ny version:** når appen er opdateret, vises "Ny version af appen er klar" med en knap, der genindlæser.
- **Offline:** appen åbner uden net, og man forbliver logget ind.
  - **Indkøb** kan ses, afkrydses, udvides og slettes uden net. Ændringerne vises med det samme og sendes i rækkefølge, når nettet er tilbage. Varer tilføjet offline får afdelingen "Andet", til de er sendt. Redigering, faste varer og "Ryd købte" kræver net.
  - **Min side** viser den sidst hentede udgave for i dag (kun visning, ingen afkrydsning).
  - Alt gemt offline fjernes, når man logger ud.

## Sæt notifikationer op (én gang)

Notifikationer bruger Web Push med et VAPID-nøglepar. Uden nøgler er funktionen slået fra, og siden Notifikationer siger det.

1. Lav nøglerne:

   ```bash
   dotnet run --project src/VoresFamilie.Api -- generate-vapid
   ```

2. Gem dem lokalt som user-secrets (i `src/VoresFamilie.Api`):

   ```bash
   dotnet user-secrets set "Push:PublicKey" "<den offentlige nøgle>"
   dotnet user-secrets set "Push:PrivateKey" "<den private nøgle>"
   dotnet user-secrets set "Push:Subject" "mailto:din@email.dk"
   ```

3. I Azure lægges de samme tre værdier som **secrets** i Container Apps (`Push__PublicKey`, `Push__PrivateKey`, `Push__Subject`). Den private nøgle må aldrig i repoet.

Brug **samme nøgler** hele tiden. Skifter man nøgler, skal alle slå notifikationer fra og til igen.

## Azure: API'et skal køre kl. 07 og 19

Påmindelserne sendes af en timer i selve API'et (tjekker hvert minut). Container Apps må derfor **ikke skalere til nul**: sæt *minimum replicas* til 1. Ellers sover API'et, og beskeden udebliver. (Beskeder sendes op til en time efter det planlagte tidspunkt, så en kort genstart taber dem ikke.)

## Prøv på telefonen

Push kræver HTTPS (eller `localhost`). Brug Dev Tunnel som beskrevet i [dev-tunnel.md](dev-tunnel.md):

1. Start API og Web med nøglerne i user-secrets og åbn tunnel-adressen på telefonen i Chrome.
2. Gå til **Mere → Notifikationer → Slå notifikationer til**, og tillad notifikationer.
3. Tryk **Send en testnotifikation**. Beskeden skal komme frem, også med skærmen låst, og et tryk åbner Min side.
4. Til at prøve selve 07:00/19:00 uden at vente: læg en rutine ind for dig selv, og ret `PushSchedule` midlertidigt, eller vent til næste morgen.

Service workeren (der gemmer appens filer til offline brug) er kun aktiv i en **udgivet** udgave (`dotnet publish` eller Docker). Under udvikling hentes alt fra nettet; push virker dog også der.

## Prøv offline

1. Åbn en udgivet udgave på telefonen, log ind og åbn Indkøb og Min side én gang online.
2. Slå flytilstand til. Åbn appen igen: du er stadig logget ind, og listerne vises med en gul "Du er offline"-bjælke.
3. Afkryds en vare og tilføj en ny. Slå flytilstand fra: bjælken forsvinder, og ændringerne er på serveren.
