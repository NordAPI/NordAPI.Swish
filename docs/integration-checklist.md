# NordAPI Swish SDK — Integration Checklist (v1.1.6)

This is a concise checklist to ensure a production-ready, secure integration with the NordAPI Swish SDK. Verify each box before deployment.

---

## 1) Setup & Configuration

- [ ] **Install the SDK**: `dotnet add package NordAPI.Swish`
- [ ] **Environment variables**:
  - `SWISH_BASE_URL` — Swish API base URL (test or production).
  - `SWISH_API_KEY` — Your HMAC API key.
  - `SWISH_SECRET` — Your shared HMAC secret.
  - `SWISH_WEBHOOK_SECRET` — Secret for the optional NordAPI webhook hardening layer.
- [ ] **Redis Configuration**:
  - [ ] A Redis/DB-backed nonce store is required for replay protection in multi-instance production.
  - [ ] Set `SWISH_REDIS` (aliases `REDIS_URL` and `SWISH_REDIS_CONN` are also supported).
- [ ] **mTLS Certificate** (Enforced fail-closed by default):
  - [ ] `SWISH_PFX_PATH` — Absolute path to your production .pfx/.p12 certificate.
  - [ ] `SWISH_PFX_PASSWORD` — Certificate password.
  - [ ] **Verify fail-fast**: Ensure the application fails to start if certificates are missing when `ASPNETCORE_ENVIRONMENT` is `Production`.
- [ ] **Git Hygiene**: Confirm `.gitignore` blocks all certificate/key files (`*.pfx`, `*.pem`, `*.key`, etc.).

---

## 2) Webhook Configuration

Your Swish callback endpoint must be secure. Note that the `X-Swish-*` headers are **NordAPI-specific hardening extensions** and are not sent by Swish by default.

### Official Swish Constraints (MIG 2.6)
- [ ] Endpoint is served over **HTTPS on port 443**.
- [ ] Server supports connections **without SNI** (Swish callbacks do not support SNI).
- [ ] Endpoint returns **HTTP 200 OK** on success to stop Swish retries (up to 10 attempts).

### NordAPI Hardening (Optional Layer)
- [ ] **Canonical String**: Must be exactly `"<ts>\n<nonce>\n<body>"` using UTF-8.
- [ ] **Body Integrity**: Sign the **raw** request body bytes exactly; do not reformat or prettify JSON.
- [ ] **Verification Logic**: Enforce constant-time signature comparison to prevent timing attacks.

---

## 3) Security Checklist (Enterprise Gate)

- [ ] **Temporal Hard-lock**: Verify that `AllowedClockSkew` and `MaxMessageAge` are not configured to exceed **15 minutes** (SDK enforces this cap at startup).
- [ ] **HTTPS/TLS**: Enforce TLS 1.2+ and enable **HSTS** on your production domain.
- [ ] **Timestamp Tolerance**: Recommended ±5 minutes acceptance window (within the 15-minute hard cap).
- [ ] **Replay Protection**: Ensure a Redis/DB-backed nonce store is active in production.
- [ ] **Secrets Management**: No secrets committed to repo; use Environment Variables, User-Secrets, or a Vault (e.g., Azure Key Vault).
- [ ] **Logging**: Structured logs should not contain PII or raw secrets.

---

## 4) Testing & Verification

- [ ] **Local Smoke Test**:
  - Run the smoke script: `.\scripts\smoke-webhook.ps1 -Secret <your_secret> -Url http://localhost:5000/webhook/swish`.
  - [ ] Verify valid signatures return **HTTP 200**.
  - [ ] Verify replays return a consistent non-200 (commonly 401/403 or 409).
- [ ] **Refund Flow**: Test end-to-end refund logic including callback handling.
- [ ] **Error Scenarios**: Exercise paths for invalid signatures, expired timestamps, and missing mTLS certificates.

---

## 5) Before Go-Live

- [ ] **Production Env**: Ensure `SWISH_DEBUG` and `SWISH_ALLOW_OLD_TS` are disabled/removed.
- [ ] **Persistence**: Clear/flush the Redis nonce store before launch if it was used during staging.
- [ ] **Network**: Allowlist Swish callback IP ranges if your infrastructure uses IP filtering.
- [ ] **Manual Audit**: Perform a final check of `docs/compliance.md` against your implementation.

---

## Notes

- **Fail-Closed Strategy**: NordAPI defaults to "Secure by Default." Any missing security configuration should result in a startup failure rather than a degraded security state.
- **Support**: If signature verification fails, log the server's canonical string and compare it byte-for-byte with the client-side input.

_Last updated: March 2026_
