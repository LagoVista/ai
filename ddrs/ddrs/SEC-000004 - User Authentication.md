# Metadata
ID: SEC-000004
Title: User Authentication
Type: Policy / Rules / Governance
Summary: Defines the unified authentication policy and enrollment flow for NuvOS across password, native providers (Apple/Google), external OAuth logins, and future passkeys using a PendingIdentity “airlock” to safely resolve identity, verify email, and apply invites.
Status: Approved
Creator: Kevin D. Wolf
References: 
Creation Date: 2026-01-31T10:34:54.959Z
Last Updated Date: 2026-01-31T10:34:54.959Z
Last Updated By: Kevin D. Wolf

# Body
## 1 - Overview
Summary: Establishes a single, consistent authentication architecture that supports legacy password sign-in while enabling low-friction provider-first onboarding and future passkeys without creating duplicate “ghost” accounts.

This DDR defines how users authenticate into NuvOS (web and native clients) and how the system safely transitions a user from “proved a credential” to “fully resolved account with permissions”.

Key design goal: treat authentication proofs (Apple/Google/OAuth/passkey/password) as inputs into a controlled resolution funnel that prevents unsafe auto-merges and minimizes support load.

## 2 - Goals and Non-Goals
Summary: Clarifies what this DDR is optimizing for and what is intentionally deferred to later work.

### Goals
- Low-friction onboarding with **native providers first** (Apple/Google) while still allowing email/password signup.
- One unified resolution mechanism for: Password, Native Provider, OAuth External Login, and future Passkeys.
- Avoid creation of durable “real users” before the system can confidently resolve identity and verify a recovery/linking anchor.
- Support multi-device, multi-provider usage by allowing multiple login methods to attach to the same canonical user (GUID).
- Safely carry **invite context** through onboarding until a user is resolved, then apply it atomically.

### Non-Goals (for now)
- Full self-serve account merge UX and automated merge heuristics beyond verified-email-based linking.
- Removal of password sign-in for existing users.
- Passkey implementation details (captured separately when passkeys are prioritized).

## 3 - Identity and Account Model
Summary: Defines the canonical user identity model and separates “account” from “login methods”.

### 3.1 Canonical user identity
- The canonical identifier for a user account is a **normalized GUID** (UserId).
- A user may have a system username, but username is not the canonical identity.

### 3.2 Email uniqueness and role
- Email must be **unique** in the system.
- Email is used for **lookup**, recovery, and cross-device account linking.
- Email becomes a trusted anchor only after **verification** (OTP/magic-link style).

### 3.3 Login methods are additive
A single user may have multiple login methods attached over time:
- Password (hashed)
- Native providers: Apple, Google
- OAuth external logins (provider-defined)
- Passkeys (future)

The system must treat “login method” as separate from “user account”, and attach/detach methods without creating duplicate users.

## 4 - PendingIdentity Airlock
Summary: Introduces PendingIdentity as the unified provisional container used to safely resolve identity for all supported sign-in methods.

### 4.1 Concept
`PendingIdentity` is a first-class object representing a provisional authentication session:
- The user has proven control of **exactly one** authentication proof (“auth bucket”).
- The session is allowed to access only registration/linking endpoints.
- The session has **no app capabilities** (authorization is restricted until resolution).

### 4.2 Auth buckets (exactly one)
A PendingIdentity MUST contain exactly one of the following auth buckets, aligned with `FlowType`:
- **Password**: email + password (new user creation path); email is untrusted until verified.
- **Native Provider**: Apple/Google identifiers (e.g., provider subject id).
- **OAuth External Login**: external provider subject/issuer identifiers.
- **Passkey** (future): credential id + public key + RP context and verification artifacts.

Invariant: exactly one auth bucket populated, and it MUST match `FlowType`.

### 4.3 Invite attachment (optional)
PendingIdentity MAY include an **Invite** attachment when the user arrives with an invite id.

Invite attachment purpose:
- Preserve onboarding destination context (org/customer/role) until identity is resolved.

Invite attachment rules:
- Invite MUST NOT be applied to any durable user until resolution.
- Invite may influence onboarding UX (“you were invited to …”) but MUST NOT drive identity linking.
- Invite email (if present) may be advisory only; it MUST NOT be used for linking without proof.
- Invite email and verified email are allowed to differ (revisit later if business rules change).

### 4.4 Lifetime, TTL, and cleanup
- PendingIdentity MUST be TTL-bound and expire automatically (duration configurable).
- Expired PendingIdentity objects MUST be treated as non-authenticated for app access and require restarting the flow.
- Retention of PII in PendingIdentity should be minimized; after expiration, redact/delete PII per operational policy.

## 5 - Resolution Order and Decision Tree
Summary: Defines the strict priority order for resolving provisional identities into durable accounts, including safe linking rules.

### 5.1 Locked resolution order
When Invite and PendingIdentity are present, the system MUST resolve in this order:
1) **Authentication proof** (provider/OAuth/password/passkey) establishes provisional authentication.
2) **Email verification** establishes account ownership anchor for safe linking or creation.
3) **Invite application** applies destination context only after the user is resolved.

### 5.2 Safe linking rules
- The system MUST NOT link a PendingIdentity to an existing account based solely on an unverified email.
- Linking to an existing account is permitted when:
  - the user proves control of the email via OTP/magic-link verification during the flow, OR
  - the auth bucket is already mapped to a user (e.g., provider subject id already linked).

### 5.3 Decision tree (high level)
1) If auth bucket is already linked (e.g., (provider, subjectId) exists) → sign in as that user (no provisional finalize needed).
2) Else create/continue PendingIdentity and collect profile data.
3) Require email verification (unless an alternate “existing-session proof” is used).
4) After verified email:
   - If verified email matches an existing user → link auth bucket to that user and proceed.
   - Else create a new user, attach auth bucket, and proceed.
5) Apply invite (if present) atomically during finalization.
6) Mint a normal user session and end the provisional session.

## 6 - Onboarding UX Requirements
Summary: Specifies UX rules that minimize friction while preventing duplicate accounts and unsafe merges.

### 6.1 Provider-first by default
- Sign-in surfaces SHOULD prioritize: “Continue with Apple/Google” as the primary CTA.
- Email/password remains available for new users (legacy-compatible).

### 6.2 Finalize registration screen
After a new provider/OAuth signup (or password signup), the user is routed to a finalize-registration screen:
- Pre-fill first/last/email where available.
- Present clear guidance that the email is used for recovery and linking on other devices.
- Require email verification before promotion to a fully capable account (unless the product explicitly supports limited-access pre-verification).

### 6.3 Email collision UX
If the user enters an email that already exists:
- Inform the user an account exists.
- Require OTP proof to that email.
- On success, link the auth bucket to the existing account and continue.
- On failure/cancel, do not link; allow the user to provide a different email or restart.

## 7 - Password Policy Going Forward
Summary: Defines how passwords coexist with provider-first onboarding without blocking modernization.

- Password sign-in MUST remain supported for existing users.
- New users MAY create accounts with email/password.
- New users SHOULD NOT be required to set a password when onboarding via native provider/OAuth.
- Users MAY add or remove password credentials later, subject to having at least one other viable recovery/sign-in method.

## 8 - Security, Abuse Controls, and Observability
Summary: Establishes minimum operational safeguards needed to keep auth flows safe and diagnosable.

### 8.1 Abuse controls
- Rate limit OTP send and verify endpoints.
- Enforce retry limits and backoff on OTP verification.
- Ensure provider/OAuth callbacks validate nonce/state and reject replay.

### 8.2 Audit events
The system SHOULD log durable events for:
- PendingIdentity created/continued
- OTP sent / OTP verified / OTP failed
- Resolved via create / resolved via link
- Invite applied / invite consumed
- PendingIdentity expired / canceled

Audit should include correlation ids and reason codes for create vs link decisions.

## 9 - Implementation Notes and Integration Points
Summary: Provides practical guidance for integrating this policy into the existing identity stack without prescribing exact code.

- Treat PendingIdentity as “authenticated but not authorized”: it can obtain a restricted session for only finalization/linking APIs.
- Do not create org/customer memberships until resolution; invite application should be part of the finalization transaction.
- Maintain mappings from auth buckets (provider subject ids, OAuth identifiers, passkey credential ids) to the canonical UserId.
- Username MAY revert to verified email as the system username for new (and optionally existing) accounts, while the canonical identity remains the UserId.

# Approval
Approver: Kevin D. Wolf
Approval Timestamp: 2026-01-31T10:34:54.959Z
