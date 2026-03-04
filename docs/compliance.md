# NordAPI.Swish — Security & Compliance Notes

This document describes security-relevant behavior implemented by the NordAPI.Swish SDK.
It is written to support technical reviews and production readiness audits.

## Scope

In scope:
- SDK runtime behavior related to transport security and request integrity
- Request signing / verification logic implemented by the SDK
- Webhook verification behavior implemented by the SDK

Out of scope:
- Merchant agreements, bank onboarding, or “partner/technical supplier” status
- Operational secret management (generation, storage, rotation) in your environment

## Non-negotiable security properties

### 1) Transport security (mTLS)
- The SDK requires mutual TLS (mTLS) by default for outbound API calls.
- Fail-closed behavior: if the client certificate is missing when mTLS is required, SDK configuration fails fast.

### 2) Deterministic request signing
The SDK implements a deterministic signing workflow:
- Canonical input is deterministic (byte-exact).
- Timestamp uses Unix time in seconds.
- HMAC is computed with SHA-256 (HMAC-SHA256). Outbound request signing uses a hex-encoded MAC.
- Strict formatting is enforced to avoid signature drift.

### 3) Webhook verification (fail-closed)
Webhook verification is designed to be deterministic and fail-closed:
- Signature is verified as Base64 HMAC-SHA256 of the canonical string:
  "<timestamp>\n<nonce>\n<body>"
- Timestamp is parsed strictly as Unix seconds.
- Time validation is enforced using:
  - Allowed clock skew (default ±5 minutes)
  - Maximum message age (default 5 minutes)
- Replay protection: nonce reuse is rejected via a nonce store.
- Signature comparison is performed using constant-time equality to mitigate timing attacks.
- Malformed or unverifiable inputs are rejected.

### 4) Idempotency discipline
- Idempotency keys are generated once per logical operation and reused across retries.

## What the SDK intentionally does NOT do

- No proxy / gateway behavior: certificates and keys remain in your environment.
- No persistence layer included: production storage choices (e.g., Redis for nonce storage) are owned by the application.
- No secrets shipped with the SDK: certificates/keys must not be committed or embedded.
- No relaxed “dev-mode” behavior in Release builds.

## Operational guidance (high level)

- Use a durable nonce store in production to make replay protection effective across instances.
- Ensure certificate rotation procedures exist for your environment.
- Treat verification failures as security signals; do not ignore them.
