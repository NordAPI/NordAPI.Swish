# SwishSample.Web – Local Webhook Test

This is a minimal ASP.NET Core app for testing the webhook endpoint in **NordAPI.Swish SDK**.

## Features

- Swish client registered via Dependency Injection (mocked in dev)
- Webhook endpoint `/webhook/swish` with verification:
  - Timestamp validation (±5 min)
  - HMAC-SHA256 signature check
  - Replay protection with nonce store (returns 401 if reused)
 - Additional endpoints for health:
  - `/health`
  - `/ping`
  - `/di-check`

---

## System requirements

- .NET 8 SDK
- PowerShell 5.1 or later
- `curl` (located in `C:\Windows\System32`)

---

## Environment variables for local development

Set the following variables before running:

```powershell
$env:ASPNETCORE_ENVIRONMENT   = "Development"
$env:SWISH_WEBHOOK_SECRET     = "dev_secret"
$env:SWISH_DEBUG              = "1"
$env:SWISH_ALLOW_OLD_TS       = "1"
$env:SWISH_REQUIRE_NONCE      = "0"
$env:SWISH_NONCE_TTL_SECONDS  = "600"
```

🔒 NOTE! SWISH_WEBHOOK_SECRET="dev_secret" may only be used for local development.
In test and production environments, set a real secret value via
environment variables or KeyVault – never hardcode or commit it in the repo.

---

## Start the server
```powershell
dotnet watch --project samples/SwishSample.Web run
```

---

## Send test webhook in PowerShell
# Parameters
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
$key    = [Text.Encoding]::UTF8.GetBytes ($secret)
$hmac   = [System.Security.Cryptography.HMACSHA256]::new($key)
$sigB64 = [Convert]::ToBase64String($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($message)))
```

# Send
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

# Send the same again => should get 401 replay-detected
```powershell
& $curl -i -X POST $uri `
  -H "Content-Type: application/json" `
  -H "X-Swish-Timestamp: $ts" `
  -H "X-Swish-Nonce: $nonce" `
  -H "X-Swish-Signature: $sigB64" `
  -d $bodyJson
  ```
  - If the same nonce is reused, the server will respond with '401 Replay detected.'

### Webhook behavior

When the webhook receives a valid payload, it responds with:

```json
{ "received": true }

- If a replay or invalid signature is detected, it responds with:
{ "error": "unauthorized" }
```

---

## QuickStart
```powershell
git clone https://github.com/NordAPI/NordAPI.SwishSdk.git
cd NordAPI.SwishSdk
```

# Set environment variables
```powershell
$env:ASPNETCORE_ENVIRONMENT   = "Development"
$env:SWISH_WEBHOOK_SECRET     = "dev_secret"
$env:SWISH_DEBUG              = "1"
$env:SWISH_ALLOW_OLD_TS       = "1"
$env:SWISH_REQUIRE_NONCE      = "0"
$env:SWISH_NONCE_TTL_SECONDS  = "600"
```
# Start the server
```powershell
dotnet watch --project samples/SwishSample.Web run
```

---

## Alternative: ISO-8601 timestamp
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
### Advanced: Named HttpClient with mTLS

Set `SWISH_USE_NAMED_CLIENT=1` in the example to register the named HttpClient "Swish".
If `SWISH_PFX_PATH` or `SWISH_PFX_BASE64` and `SWISH_PFX_PASSWORD|PASS` are set, the SDK will attach a client certificate via its `MtlsHttpHandler`.

- **Default/dev:** No environment variable → unchanged behavior (no mTLS).
- **Optional:**   `SWISH_USE_NAMED_CLIENT=1` + cert variables → named pipeline with mTLS is used.
- **Security:**    Relaxed certificate chain applies only in `DEBUG`; `Release` is strict. Never commit certificates/keys; use environment variables or `KeyVault`.


Example (PowerShell):
```powershell
$env:SWISH_USE_NAMED_CLIENT="1"
$env:SWISH_PFX_PATH="C:\path\client.pfx"
$env:SWISH_PFX_PASSWORD="secret"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj
```

---

### Environment selection for BaseAddress

The example selects the Swish base address from environment variables:

1. `SWISH_BASE_URL` (absolute override, if set)
2. `SWISH_ENV=TEST|PROD`:
   - `SWISH_BASE_URL_TEST` when `SWISH_ENV=TEST`
   - `SWISH_BASE_URL_PROD` when `SWISH_ENV=PROD`
3. Fallback: `https://example.invalid`

Upon startup, the example logs the selected environment and URL:
```
[Swish] Environment: 'TEST' | BaseAddress: https://your-test-url
```

**Example (PowerShell):**
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

# Absolute override
$env:SWISH_BASE_URL="https://override.example"
dotnet run --project .\samples\SwishSample.Web\SwishSample.Web.csproj
```

---

## Troubleshooting

- Common issues and how to resolve them when running or testing the webhook locally.

| Problem | Cause | Solution |
|----------|--------|-----------|
| `401 Unauthorized (replay-detected)` | Same nonce reused | Generate a new GUID for `$nonce` before retrying |
| `401 Invalid signature` | Canonical string or secret mismatch | Compare your canonical message with the server log and recompute HMAC |
| `400 Missing header` | One or more Swish headers missing | Ensure `X-Swish-Timestamp`, `X-Swish-Nonce`, and `X-Swish-Signature` are present |
| Server won’t start | Port already in use | Stop any previous `dotnet run` instance or change the port |

---

## See also

- [NordAPI.Swish SDK – Main README](../../src/NordAPI.Swish/README.md)
- [Project repository on GitHub](https://github.com/NordAPI/NordAPI.SwishSdk)

---



