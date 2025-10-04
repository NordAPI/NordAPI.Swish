# SwishSample.Web – Lokal Webhook-Test

Detta är en minimal ASP.NET Core-app för att testa webhook-endpointen i **NordAPI.Swish SDK**.

## Funktioner

- Swish-klient registrerad via Dependency Injection (mockad i dev)
- Webhook-endpoint `/webhook/swish` med verifiering:
  - Timestamp-validering (±5 min)
  - HMAC-SHA256 signaturkontroll
  - Replay-skydd med nonce-store
- Extra endpoints för hälsa:
  - `/health`
  - `/ping`
  - `/di-check`

---

## Systemkrav

- .NET 8 SDK
- PowerShell 5.1 eller senare
- `curl` (finns i `C:\Windows\System32`)

---

## Miljövariabler för lokal utveckling

Ställ in följande variabler innan du kör:

```powershell
$env:ASPNETCORE_ENVIRONMENT   = "Development"
$env:SWISH_WEBHOOK_SECRET     = "dev_secret"
$env:SWISH_DEBUG              = "1"
$env:SWISH_ALLOW_OLD_TS       = "1"
$env:SWISH_REQUIRE_NONCE      = "0"
$env:SWISH_NONCE_TTL_SECONDS  = "600"
```

🔒 OBS! SWISH_WEBHOOK_SECRET="dev_secret" får endast användas för lokal utveckling.
I test- och produktionsmiljöer ska du sätta ett riktigt hemligt värde via
miljövariabler eller KeyVault – aldrig hårdkoda eller committa det i repo

---

## Starta servern
```powershell
dotnet watch --project samples/SwishSample.Web run
```

---

## Skicka test-webhook i PowerShell
# Parametrar
```powershell
$secret   = "dev_secret"
$bodyJson = '{"id":"test-1","amount":100}'
$ts       = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$nonce    = [guid]::NewGuid().ToString("N")
```

# Canonical string: <ts>\n<nonce>\n<body>
```powershell
$message = "{0}`n{1}`n{2}" -f $ts, $nonce, $bodyJson
```

# HMAC-SHA256
```powershell
$key    = [Text.Encoding]::UTF8.GetBytes($secret)
$hmac   = [System.Security.Cryptography.HMACSHA256]::new($key)
$sigB64 = [Convert]::ToBase64String($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($message)))
```

# Skicka
```powershell
$curl = "$env:SystemRoot\System32\curl.exe"
$uri  = "http://localhost:5287/webhook/swish"

& $curl -i -X POST $uri `
  -H "Content-Type: application/json" `
  -H "X-Swish-Timestamp: $ts" `
  -H "X-Swish-Nonce: $nonce" `
  -H "X-Swish-Signature: $sigB64" `
  -d $bodyJson
  ```

# Skicka samma igen => ska få 401 replay-detected
```powershell
& $curl -i -X POST $uri `
  -H "Content-Type: application/json" `
  -H "X-Swish-Timestamp: $ts" `
  -H "X-Swish-Nonce: $nonce" `
  -H "X-Swish-Signature: $sigB64" `
  -d $bodyJson
  ```

  ### Webhook-beteende

När webhooken tar emot en giltig nyttolast svarar den med:

```json
{ ”received”: true }

- Om en repris eller ogiltig signatur upptäcks svarar den med:
{ ”error”: ”unauthorized” }
```

---

## QuickStart
```powershell
git clone https://github.com/NordAPI/NordAPI.SwishSdk.git
cd NordAPI.SwishSdk
```

# Ställ in miljövariabler
```powershell
$env:ASPNETCORE_ENVIRONMENT   = "Development"
$env:SWISH_WEBHOOK_SECRET     = "dev_secret"
$env:SWISH_DEBUG              = "1"
$env:SWISH_ALLOW_OLD_TS       = "1"
$env:SWISH_REQUIRE_NONCE      = "0"
$env:SWISH_NONCE_TTL_SECONDS  = "600"
```
# Starta servern
```powershell
dotnet watch --project samples/SwishSample.Web run
```

---

## Alternativ: ISO-8601-timestamp
```powershell
$secret   = "dev_secret"
$bodyJson = '{"id":"test-1","amount":100}'
$tsIso    = [DateTimeOffset]::UtcNow.ToUniversalTime().ToString("o")
$nonce    = [guid]::NewGuid().ToString("N")

$message  = "{0}`n{1}`n{2}" -f $tsIso, $nonce, $bodyJson

$key    = [Text.Encoding]::UTF8.GetBytes($secret)
$hmac   = [System.Security.Cryptography.HMACSHA256]::new($key)
$sigB64 = [Convert]::ToBase64String($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($message)))

$curl = "$env:SystemRoot\System32\curl.exe"
$uri  = "http://localhost:5287/webhook/swish"

& $curl -i -X POST $uri `
  -H "Content-Type: application/json" `
  -H "X-Swish-Timestamp: $tsIso" `
  -H "X-Swish-Nonce: $nonce" `
  -H "X-Swish-Signature: $sigB64" `
  -d $bodyJson
  ```

  ---

### Named client (optional)

Sätt `SWISH_USE_NAMED_CLIENT=1` i exemplet för att registrera den namngivna HttpClienten "Swish".
Om `SWISH_PFX_PATH` eller `SWISH_PFX_BASE64` och `SWISH_PFX_PASSWORD|PASS` är satta, kommer SDK:t att bifoga ett klientcertifikat via sin `MtlsHttpHandler`.

- **Standard/dev:** Ingen miljövariabel → oförändrat beteende (ingen mTLS).
- **Valbart:** - `SWISH_USE_NAMED_CLIENT=1` + cert-variabler → namngiven pipeline med mTLS används.
- **Säkerhet:** - Avslappnad certifikatkedja gäller endast i `DEBUG`; `Release` är strikt. Committa aldrig certifikat/nycklar; använd miljövariabler eller `KeyVault`.


Exempel (PowerShell):
```powershell
$env:SWISH_USE_NAMED_CLIENT="1"
$env:SWISH_PFX_PATH="C:\path\client.pfx"
$env:SWISH_PFX_PASSWORD="secret"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj
```

---

### Val av miljö för BaseAddress

Exemplet väljer Swish-basadressen från miljövariabler:

1. `SWISH_BASE_URL` (absolute override, if set)
2. `SWISH_ENV=TEST|PROD`:
   - `SWISH_BASE_URL_TEST` when `SWISH_ENV=TEST`
   - `SWISH_BASE_URL_PROD` when `SWISH_ENV=PROD`
3. Fallback: `https://example.invalid`


Vid uppstart loggar exemplet vald miljö och URL:
```
[Swish] Environment: 'TEST' | BaseAddress: https://your-test-url
```

**Exempel (PowerShell):**
```powershell
# Dev-standard (fallback)
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj

# TEST
$env:SWISH_ENV="TEST"
$env:SWISH_BASE_URL_TEST="https://your-test-url"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj

# PROD
$env:SWISH_ENV="PROD"
$env:SWISH_BASE_URL_PROD="https://your-prod-url"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj

# Absolut överskrivning
$env:SWISH_BASE_URL="https://override.example"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj
```
---

## Felsökning

- Vanliga problem och hur du löser dem när du kör eller testar webhooken lokalt.

| Problem | Orsak | Lösning |
|----------|--------|-----------|
| `401 Obehörig (replay-detected)` | Samma nonce återanvänds | Generera en ny GUID för `$nonce` innan du försöker igen |
| `401 Ogiltig signatur` | Kanonisk sträng eller hemlighetsmismatch | Jämför ditt kanoniska meddelande med serverloggen och beräkna HMAC på nytt |
| `400 Saknad rubrik` | En eller flera Swish-rubriker saknas | Se till att `X-Swish-Timestamp`, `X-Swish-Nonce` och `X-Swish-Signature` finns |
| Servern startar inte | Porten används redan | Stoppa alla tidigare `dotnet run`-instanser eller ändra porten |

---

## Se även

- [NordAPI.Swish SDK – Huvudsaklig README](../../src/NordAPI.Swish/README.md)
- [Projektarkiv på GitHub](https://github.com/NordAPI/NordAPI.SwishSdk)


---