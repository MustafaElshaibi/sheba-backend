> [!WARNING]
> **ARCHIVED — SUPERSEDED (2026-07-16).** This document is retained for history only.
> The authoritative architecture & implementation plan is now [`docs/sheba.md`](../sheba.md),
> with focused extracts in the sibling docs under `docs/`. Where this file conflicts with
> `sheba.md` (notably: JSend API standard, AES-256-GCM credential encryption superseding
> ADR-011, single-track modular-monolith design), **`sheba.md` wins**. Do not implement from
> this file.

# Sheba — e-Government Platform Architecture Plan

> **e-Government IAM & Service Portal for Yemen — Graduation Project**
> Stack: ASP.NET Core 9 · C# 13 · PostgreSQL · Redis · MinIO · OpenIddict · MediatR · Hangfire · Serilog
>
> **Two-track document:**
> - Sections 1–23 describe the **full production-grade architecture** — the long-term vision and design decisions
> - **Section 24** is the **Graduation Project Implementation Guide** — what you actually build and run now (modular monolith, ~1,000 test users, Docker Compose)

---

## Table of Contents

1. [System Overview & Design Philosophy](#1-system-overview--design-philosophy)
2. [Architecture Patterns & Principles](#2-architecture-patterns--principles)
3. [High-Level Architecture Diagram](#3-high-level-architecture-diagram)
4. [Microservices Breakdown](#4-microservices-breakdown)
5. [Identity Service — Deep Dive (IAM / OIDC)](#5-identity-service--deep-dive-iam--oidc)
6. [Citizen Authentication Workflow](#6-citizen-authentication-workflow)
7. [Ministry Service — Deep Dive](#7-ministry-service--deep-dive)
8. [Government Service Catalog — Deep Dive](#8-government-service-catalog--deep-dive)
9. [Pluggable National ID Connector](#9-pluggable-national-id-connector)
10. [Pluggable OTP System](#10-pluggable-otp-system)
11. [Wallet Service (W3C Verifiable Credentials)](#11-wallet-service-w3c-verifiable-credentials)
12. [Notification Service](#12-notification-service)
13. [Document Service](#13-document-service)
14. [Payment Service](#14-payment-service)
15. [Audit Service](#15-audit-service)
16. [API Gateway](#16-api-gateway)
17. [Normalized Database Design](#17-normalized-database-design)
18. [Event-Driven Architecture (RabbitMQ + MassTransit)](#18-event-driven-architecture-rabbitmq--masstransit)
19. [Security Architecture](#19-security-architecture)
20. [Observability Stack](#20-observability-stack)
21. [Deployment Architecture](#21-deployment-architecture)
22. [Development Roadmap & Phasing](#22-development-roadmap--phasing)
23. [Admin API Service](#23-admin-api-service-backend-only)
24. **[Graduation Project Implementation Guide ← START HERE](#24-graduation-project-implementation-guide)**

---

## 1. System Overview & Design Philosophy

### What Sheba Is

Sheba is Yemen's national e-Government platform — a single digital identity layer and service gateway that:

- Gives every citizen and resident a **verified, sovereign digital identity** (like UAE Pass)
- Acts as a **national OAuth 2.1 / OIDC Identity Provider** for all government systems (like Estonia's X-Road + Keycloak)
- Provides a **federated service catalog** where citizens request services across ministries through one portal
- Allows any ministry or external system to **register as a Relying Party** and authenticate their users via Sheba SSO
- Acts as a **trusted broker** between citizens and ministry systems — Sheba authenticates the citizen, the ministry trusts Sheba's assertion

### Analogues & Lessons Learned

| Platform | Lesson Applied |
|----------|---------------|
| **UAE Pass** | Single national digital ID; OIDC provider for all government apps; eKYC with liveness check |
| **Absher (Saudi)** | Ministry integration hub; citizen service requests; SMS OTP on every action |
| **Estonia X-Road** | Secure data exchange fabric; every ministry exposes standard APIs; no direct DB sharing |
| **GOV.UK Verify** | Identity levels of assurance (LoA 1/2/3); admin approval gating account activation |
| **Singapore Singpass** | Myinfo pattern: citizen controls what data is shared with relying parties |
| **OpenIddict** | Production-grade OAuth 2.1/OIDC on ASP.NET Core; used instead of IdentityServer (licensing) |

### Core Tenets

1. **Identity is the source of truth** — no service operates without a verified digital identity
2. **Zero direct database coupling** — services communicate via events and APIs, never shared databases
3. **Pluggability by design** — every external system (National ID DB, OTP provider, ministry) is behind an adapter interface
4. **Event sourcing for audit** — every state change emits a domain event; the audit trail is immutable
5. **Defense in depth** — mTLS between services, short-lived JWTs, hardware-bound refresh tokens, rate limiting at every layer
6. **Normalized data, denormalized reads** — 3NF+ relational schema for writes; Redis/materialized views for reads

---

## 2. Architecture Patterns & Principles

### Pattern Stack

```
┌─────────────────────────────────────────────────────────────┐
│                     PATTERNS IN USE                          │
├──────────────────────────┬──────────────────────────────────┤
│ Structural               │ Behavioral                        │
│ • Microservices          │ • CQRS (Command/Query separation) │
│ • Clean Architecture     │ • Event Sourcing (Audit log)      │
│ • Hexagonal (Ports &     │ • Outbox Pattern (reliable events)│
│   Adapters) per service  │ • Saga / Process Manager          │
│ • API Gateway            │ • Domain Events                   │
│ • Backend for Frontend   │ • Mediator (MediatR)              │
│   (BFF)                  │ • Strategy (pluggable adapters)   │
│ • Repository + UoW       │ • Chain of Responsibility         │
│ • Anti-Corruption Layer  │ • Observer (webhooks)             │
│   (ministry integrations)│ • Decorator (pipeline behaviors)  │
└──────────────────────────┴──────────────────────────────────┘
```

### Clean Architecture per Service

Each microservice follows this internal layering:

```
src/
├── Sheba.<Service>.Api/           # HTTP entry point (controllers, minimal API)
├── Sheba.<Service>.Application/   # Use cases: Commands, Queries, Handlers, DTOs
│   ├── Commands/
│   ├── Queries/
│   ├── Events/                    # Domain event handlers
│   └── Behaviors/                 # Cross-cutting: validation, logging, auth
├── Sheba.<Service>.Domain/        # Entities, value objects, domain services, domain events
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Enums/
│   ├── DomainEvents/
│   └── Interfaces/                # Ports (repository contracts, adapter contracts)
└── Sheba.<Service>.Infrastructure/# Adapters: EF Core, Redis, RabbitMQ, HTTP clients
    ├── Persistence/
    │   ├── Configurations/        # EF Fluent API
    │   ├── Repositories/
    │   └── Migrations/
    ├── Messaging/                 # MassTransit consumers and publishers
    ├── Adapters/                  # External system adapters (ministry, OTP, NID)
    └── BackgroundJobs/            # Hangfire jobs
```

### CQRS with MediatR

```csharp
// Command side
public record CreateDigitalIdentityCommand(string NationalId, string PhoneNumber, 
    string Email, string Username, string Password) : ICommand<DigitalIdentityId>;

// Query side  
public record GetCitizenProfileQuery(Guid CitizenId) : IQuery<CitizenProfileDto>;

// Pipeline behaviors (applied in order)
1. LoggingBehavior<TRequest, TResponse>
2. ValidationBehavior<TRequest, TResponse>       // FluentValidation
3. AuthorizationBehavior<TRequest, TResponse>
4. CachingBehavior<TRequest, TResponse>          // For queries
5. TransactionBehavior<TRequest, TResponse>      // For commands
6. OutboxBehavior<TRequest, TResponse>           // Domain events → outbox
```

### Outbox Pattern (Guaranteed Event Delivery)

Every command that changes state writes domain events to an **outbox table** in the same database transaction. A background job (Hangfire) publishes them to RabbitMQ. This eliminates dual-write failures between the database and the message broker.

```
[Command Handler]
    → Save aggregate to DB
    → Save OutboxMessages to DB
    → Commit transaction
    
[Outbox Publisher Job (every 5s)]
    → Read unpublished OutboxMessages
    → Publish to RabbitMQ
    → Mark as published
```

---

## 3. High-Level Architecture Diagram

```
                          ┌─────────────────────────────────────────────────────┐
                          │              EXTERNAL CONSUMERS                       │
                          │  Citizens  │  Ministries  │  Organizations  │  Devs  │
                          └────────────────────────┬────────────────────────────┘
                                                   │ HTTPS
                                    ┌──────────────▼──────────────┐
                                    │       API GATEWAY            │
                                    │   (YARP / Ocelot)            │
                                    │  • Rate limiting              │
                                    │  • JWT validation             │
                                    │  • mTLS termination          │
                                    │  • Request routing            │
                                    │  • Correlation IDs            │
                                    └──┬──────┬──────┬──────┬─────┘
                                       │      │      │      │
              ┌────────────────────────▼──┐   │   ┌──▼──────────────────────┐
              │    IDENTITY SERVICE        │   │   │   CITIZEN SERVICE        │
              │  ┌──────────────────────┐ │   │   │  • Citizen profile        │
              │  │ OpenIddict (OIDC 2.1)│ │   │   │  • KYC documents          │
              │  │ • Authorization server│ │   │   │  • Account management     │
              │  │ • Token endpoint      │ │   │   │  • Organization accounts  │
              │  │ • Userinfo endpoint   │ │   │   └─────────────┬────────────┘
              │  │ • JWKS endpoint       │ │   │                 │
              │  │ • Discovery endpoint  │ │   │   ┌─────────────▼────────────┐
              │  └──────────────────────┘ │   │   │  MINISTRY SERVICE         │
              │  • eKYC approval workflow  │   │   │  • Ministry registry       │
              │  • Relying Party mgmt      │   │   │  • Sub-ministry tree       │
              │  • NID adapter (pluggable) │   │   │  • Auth credentials mgmt   │
              │  • OTP adapter (pluggable) │   │   │  • Endpoint registry       │
              │  • Admin approval queue    │   │   │  • Webhook receivers        │
              └───────────┬───────────────┘   │   └─────────────┬────────────┘
                          │                   │                 │
        ┌─────────────────┼───────────────────┼─────────────────┼──────────────┐
        │                 │   MESSAGE BUS      │                 │              │
        │         ┌───────▼───────────────────▼─────────────────▼──────┐       │
        │         │              RabbitMQ + MassTransit                  │       │
        │         │   Topics: identity.* | citizen.* | ministry.*        │       │
        │         │            service.* | payment.* | notification.*    │       │
        │         │            document.* | wallet.* | audit.*           │       │
        │         └──┬─────────┬────────────┬──────────┬────────────────┘       │
        │            │         │            │          │                         │
        │   ┌────────▼──┐  ┌───▼──────┐ ┌──▼───────┐ ┌▼─────────────┐          │
        │   │ SERVICE    │  │ NOTIFIC. │ │ PAYMENT  │ │  AUDIT       │          │
        │   │ REQUEST    │  │ SERVICE  │ │ SERVICE  │ │  SERVICE     │          │
        │   │ • Catalog  │  │ • Email  │ │ • Fees   │ │  • Immutable │          │
        │   │ • Workflow │  │ • SMS    │ │ • Wallet │ │    log       │          │
        │   │ • Ministry │  │ • Push   │ │ • Refund │ │  • SIEM feed │          │
        │   │   proxy   │  └──────────┘ └──────────┘ └──────────────┘          │
        │   └────────┬──┘                                                        │
        │            │                                                            │
        │   ┌────────▼──────────────────┐   ┌─────────────────────────┐         │
        │   │   DOCUMENT SERVICE         │   │   WALLET SERVICE         │         │
        │   │  • Upload / store (MinIO) │   │  • W3C VC issuance       │         │
        │   │  • Encryption at rest      │   │  • VC verification       │         │
        │   │  • Access control          │   │  • DID management        │         │
        │   │  • Virus scan              │   │  • Credential schemas    │         │
        │   └───────────────────────────┘   └─────────────────────────┘         │
        └───────────────────────────────────────────────────────────────────────┘
                          │                              │
              ┌───────────▼────────────┐    ┌───────────▼────────────┐
              │   POSTGRESQL CLUSTER   │    │   REDIS CLUSTER         │
              │  (one DB per service)  │    │  • Sessions             │
              │  • identity_db         │    │  • OTP cache            │
              │  • citizen_db          │    │  • Token blacklist      │
              │  • ministry_db         │    │  • Rate limit counters  │
              │  • service_request_db  │    │  • Query result cache   │
              │  • payment_db          │    └────────────────────────┘
              │  • document_db         │
              │  • wallet_db           │    ┌────────────────────────┐
              │  • notification_db     │    │   MINIO                │
              │  • audit_db            │    │  • Citizen documents    │
              └────────────────────────┘    │  • KYC evidence        │
                                            │  • Service attachments  │
              ┌────────────────────────┐    └────────────────────────┘
              │   EXTERNAL SYSTEMS     │
              │  • Civil Registry (NID)│    ┌────────────────────────┐
              │  • OpenCRVS            │    │  OBSERVABILITY          │
              │  • Passport System     │    │  • OpenTelemetry        │
              │  • Traffic System      │    │  • Prometheus           │
              │  • Any Ministry API    │    │  • Grafana              │
              └────────────────────────┘    │  • Jaeger (tracing)    │
                                            └────────────────────────┘
```

---

## 4. Microservices Breakdown

| Service | Responsibility | DB | Port |
|---------|---------------|-----|------|
| `Identity` | OIDC provider, auth, eKYC workflow, admin approval, RP management | `identity_db` | 5001 |
| `Citizen` | Citizen/resident/org profiles, KYC documents, account settings | `citizen_db` | 5002 |
| `Ministry` | Ministry registry, sub-ministries, auth credentials, endpoint registry, webhooks | `ministry_db` | 5003 |
| `ServiceRequest` | Service catalog, request lifecycle, ministry proxy, webhooks | `service_request_db` | 5004 |
| `Document` | Document upload, storage (MinIO), encryption, access control | `document_db` | 5005 |
| `Wallet` | W3C Verifiable Credentials, DID, credential schema registry | `wallet_db` | 5006 |
| `Payment` | Fee collection, payment gateway integration, refunds | `payment_db` | 5007 |
| `Notification` | Email/SMS/Push delivery, templates, delivery tracking | `notification_db` | 5008 |
| `Audit` | Immutable event log, compliance queries, SIEM export | `audit_db` | 5009 |
| `Gateway` | YARP reverse proxy, JWT validation, rate limiting, routing | — | 443/80 |

---

## 5. Identity Service — Deep Dive (IAM / OIDC)

### Responsibilities

The Identity Service is the **crown jewel** — it owns:

1. The **OpenIddict** authorization server (OAuth 2.1 + OIDC)
2. The **Relying Party** (application/client) registry
3. The **citizen account** lifecycle (registration → eKYC → admin approval → active)
4. The **admin approval workflow** for digital identity requests
5. The **pluggable NID connector** (civil registry lookup)
6. The **pluggable OTP provider**
7. Ministry-facing **machine credentials** (client_credentials grant for ministry systems)

### OpenIddict Configuration

```csharp
// Supported grant types
• authorization_code        (citizens, web apps — with PKCE required)
• client_credentials        (ministries, machine-to-machine)
• refresh_token             (session extension)
• urn:ietf:params:oauth:grant-type:device_code  (TV / IoT)
• Custom: urn:sheba:grant:national_id_otp       (NID + password + OTP)

// Supported flows
• OIDC Discovery            /.well-known/openid-configuration
• JWKS                      /.well-known/jwks
• Authorization endpoint    /connect/authorize
• Token endpoint            /connect/token
• Userinfo endpoint         /connect/userinfo
• Introspection endpoint    /connect/introspect
• Revocation endpoint       /connect/revoke
• End-session endpoint      /connect/endsession
• Registration endpoint     /connect/register   (dynamic client registration)
```

### Identity Database Schema (identity_db)

```sql
-- Core account table (normalized)
accounts
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  national_id         VARCHAR(20) NOT NULL UNIQUE          -- from civil registry
  username            VARCHAR(100) NOT NULL UNIQUE
  email               VARCHAR(254) NOT NULL UNIQUE
  email_verified_at   TIMESTAMPTZ
  phone_number        VARCHAR(20) NOT NULL                 -- from civil registry
  phone_verified_at   TIMESTAMPTZ
  password_hash       TEXT NOT NULL                        -- Argon2id
  status              account_status NOT NULL              -- ENUM below
  identity_level      SMALLINT NOT NULL DEFAULT 1          -- LoA 1/2/3
  failed_login_count  SMALLINT NOT NULL DEFAULT 0
  locked_until        TIMESTAMPTZ
  last_login_at       TIMESTAMPTZ
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
  updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()

CREATE TYPE account_status AS ENUM (
  'PENDING_VERIFICATION',   -- just registered; NID check done; awaiting admin
  'PENDING_ADMIN_APPROVAL', -- NID verified; admin notified; awaiting review
  'APPROVED',               -- admin approved; account active
  'REJECTED',               -- admin rejected; citizen notified
  'SUSPENDED',              -- active but temporarily disabled
  'DEACTIVATED'             -- permanently closed
);

-- Digital identity requests (eKYC workflow)
identity_requests
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  account_id          UUID NOT NULL REFERENCES accounts(id)
  request_type        identity_request_type NOT NULL       -- OPEN_ACCOUNT, UPGRADE_LOA, etc.
  status              request_status NOT NULL              -- PENDING, UNDER_REVIEW, APPROVED, REJECTED
  submitted_at        TIMESTAMPTZ NOT NULL DEFAULT now()
  reviewed_at         TIMESTAMPTZ
  reviewed_by         UUID REFERENCES admin_users(id)
  rejection_reason    TEXT
  citizen_snapshot    JSONB NOT NULL                       -- snapshot of civil registry data at time of request
  notes               TEXT                                 -- admin notes

CREATE TYPE identity_request_type AS ENUM (
  'OPEN_ACCOUNT', 'UPGRADE_LOA2', 'UPGRADE_LOA3', 'REOPEN_ACCOUNT'
);

CREATE TYPE request_status AS ENUM (
  'PENDING', 'UNDER_REVIEW', 'APPROVED', 'REJECTED', 'CANCELLED'
);

-- KYC documents attached to requests
identity_request_documents
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  request_id          UUID NOT NULL REFERENCES identity_requests(id)
  document_type       VARCHAR(50) NOT NULL                 -- NATIONAL_ID_PHOTO, SELFIE, LIVENESS_VIDEO
  document_service_id UUID NOT NULL                        -- reference to Document Service
  uploaded_at         TIMESTAMPTZ NOT NULL DEFAULT now()

-- Admin users (separate table, never mixed with citizen accounts)
admin_users
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  employee_id         VARCHAR(50) NOT NULL UNIQUE
  email               VARCHAR(254) NOT NULL UNIQUE
  full_name           VARCHAR(200) NOT NULL
  role                admin_role NOT NULL
  department          VARCHAR(100)
  status              VARCHAR(20) NOT NULL DEFAULT 'ACTIVE'
  password_hash       TEXT NOT NULL
  mfa_secret          TEXT                                 -- TOTP secret (encrypted)
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
  last_login_at       TIMESTAMPTZ

CREATE TYPE admin_role AS ENUM (
  'SUPER_ADMIN', 'IDENTITY_REVIEWER', 'MINISTRY_MANAGER', 'AUDITOR', 'SUPPORT'
);

-- Relying Parties (OAuth clients / OIDC applications)
relying_parties
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  client_id           VARCHAR(100) NOT NULL UNIQUE         -- OpenIddict application ID
  name                VARCHAR(200) NOT NULL
  name_ar             VARCHAR(200)                         -- Arabic name
  description         TEXT
  logo_url            TEXT
  client_type         rp_client_type NOT NULL              -- CONFIDENTIAL, PUBLIC
  party_type          rp_party_type NOT NULL               -- MINISTRY, ORGANIZATION, INTERNAL
  ministry_id         UUID REFERENCES ministries(id)       -- in ministry_db (logical FK)
  organization_id     UUID                                 -- if organization
  status              VARCHAR(20) NOT NULL DEFAULT 'ACTIVE'
  registered_at       TIMESTAMPTZ NOT NULL DEFAULT now()
  registered_by       UUID NOT NULL REFERENCES admin_users(id)
  metadata            JSONB                                -- additional config

CREATE TYPE rp_client_type AS ENUM ('CONFIDENTIAL', 'PUBLIC');
CREATE TYPE rp_party_type AS ENUM ('MINISTRY', 'ORGANIZATION', 'INTERNAL', 'DEVELOPER');

-- RP allowed redirect URIs (normalized — one row per URI)
rp_redirect_uris
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  relying_party_id    UUID NOT NULL REFERENCES relying_parties(id)
  uri                 TEXT NOT NULL
  uri_type            VARCHAR(20) NOT NULL DEFAULT 'REDIRECT'  -- REDIRECT, POST_LOGOUT

-- RP allowed scopes
rp_scopes
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  relying_party_id    UUID NOT NULL REFERENCES relying_parties(id)
  scope_name          VARCHAR(100) NOT NULL
  UNIQUE(relying_party_id, scope_name)

-- OTP records (short-lived; purged after use/expiry)
otp_records
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  account_id          UUID NOT NULL REFERENCES accounts(id)
  purpose             otp_purpose NOT NULL
  code_hash           TEXT NOT NULL                        -- hashed; never store plaintext
  channel             VARCHAR(20) NOT NULL DEFAULT 'SMS'   -- SMS, EMAIL, TOTP
  expires_at          TIMESTAMPTZ NOT NULL
  used_at             TIMESTAMPTZ
  attempt_count       SMALLINT NOT NULL DEFAULT 0
  ip_address          INET
  user_agent          TEXT

CREATE TYPE otp_purpose AS ENUM (
  'LOGIN', 'REGISTRATION', 'PASSWORD_RESET', 'EMAIL_VERIFY',
  'PHONE_VERIFY', 'SENSITIVE_ACTION', 'TRANSACTION_CONFIRM'
);

-- Refresh token families (rotation with family invalidation on reuse)
refresh_token_families
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  account_id          UUID NOT NULL REFERENCES accounts(id)
  client_id           VARCHAR(100) NOT NULL
  family_id           UUID NOT NULL UNIQUE
  current_token_hash  TEXT NOT NULL
  issued_at           TIMESTAMPTZ NOT NULL DEFAULT now()
  expires_at          TIMESTAMPTZ NOT NULL
  revoked_at          TIMESTAMPTZ
  revocation_reason   VARCHAR(50)
  device_fingerprint  TEXT
  ip_address          INET

-- Scopes definition registry (what scopes exist and what claims they expose)
scope_definitions
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  name                VARCHAR(100) NOT NULL UNIQUE         -- e.g. 'openid', 'profile', 'civil_data'
  display_name        VARCHAR(200) NOT NULL
  display_name_ar     VARCHAR(200)
  description         TEXT
  claims              TEXT[] NOT NULL                      -- list of claim names included
  is_system           BOOLEAN NOT NULL DEFAULT FALSE
  requires_loa        SMALLINT NOT NULL DEFAULT 1

-- Outbox (guaranteed event delivery)
outbox_messages
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  aggregate_type      VARCHAR(100) NOT NULL
  aggregate_id        UUID NOT NULL
  event_type          VARCHAR(200) NOT NULL
  payload             JSONB NOT NULL
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
  published_at        TIMESTAMPTZ
  error               TEXT
  retry_count         SMALLINT NOT NULL DEFAULT 0
```

---

## 6. Citizen Authentication Workflow

### A. Registration (Open Digital Identity Account)

```
STEP 1 — Enter National ID + Phone
  ┌─────────────────────────────────────────────────────────────────────┐
  │ Citizen submits: national_id + phone_number                          │
  │                                                                       │
  │ System → NID Adapter → Civil Registry                                 │
  │   ✓ National ID exists                                               │
  │   ✓ Phone matches record in civil registry                           │
  │   ✓ Not already registered in Sheba                                  │
  │   ✓ Not deceased / not suspended in civil registry                   │
  │                                                                       │
  │ If any check fails → return generic error (no information leakage)   │
  └─────────────────────────────────────────────────────────────────────┘

STEP 2 — OTP Verification
  ┌─────────────────────────────────────────────────────────────────────┐
  │ System sends OTP to phone via OTP Adapter                            │
  │ Citizen submits OTP                                                  │
  │ Validated server-side (max 3 attempts; 5-min TTL)                   │
  └─────────────────────────────────────────────────────────────────────┘

STEP 3 — Account Details
  ┌─────────────────────────────────────────────────────────────────────┐
  │ Citizen submits:                                                      │
  │   • email (will be verified)                                         │
  │   • username (unique; validated for format)                          │
  │   • password (Argon2id; zxcvbn strength check)                      │
  │   • uploaded KYC documents (if LOA2 required)                       │
  └─────────────────────────────────────────────────────────────────────┘

STEP 4 — Email Verification
  ┌─────────────────────────────────────────────────────────────────────┐
  │ Verification email sent via Notification Service                     │
  │ Link is time-limited (15 min) and single-use                        │
  │ Account status → PENDING_ADMIN_APPROVAL                              │
  └─────────────────────────────────────────────────────────────────────┘

STEP 5 — Admin Notification & Approval
  ┌─────────────────────────────────────────────────────────────────────┐
  │ Event published: IdentityRequestSubmitted                            │
  │ Notification Service → Email to all IDENTITY_REVIEWER admins        │
  │ Admin dashboard shows new request with:                              │
  │   • Civil registry data snapshot                                     │
  │   • Uploaded documents                                               │
  │   • Account details entered by citizen                               │
  │   • Risk score (automated pre-screening)                             │
  │                                                                       │
  │ Admin actions:                                                        │
  │   APPROVE → account_status = APPROVED                                │
  │   REJECT  → account_status = REJECTED + rejection_reason            │
  │   REQUEST_MORE_INFO → status = UNDER_REVIEW + message to citizen    │
  └─────────────────────────────────────────────────────────────────────┘

STEP 6 — Citizen Notification
  ┌─────────────────────────────────────────────────────────────────────┐
  │ Event: IdentityRequestDecided                                        │
  │ Notification Service → Email to citizen with result                  │
  │   APPROVED: "Your Sheba digital identity is now active"             │
  │   REJECTED: "Your request was not approved" + reason (if shareable) │
  └─────────────────────────────────────────────────────────────────────┘
```

### B. Login Flow (After Account is Active)

```
STEP 1 — Credential Entry (choose either)
  Option A: national_id  + password
  Option B: username     + password

STEP 2 — Credential Validation
  • Lookup account by national_id OR username
  • Verify account status = APPROVED (reject all others with specific messages)
  • Verify password (Argon2id)
  • Increment failed_login_count; lock after 5 failures (exponential backoff)

STEP 3 — OTP Dispatch
  • Generate OTP (6 digits; Argon2id hash stored)
  • Send via OTP Adapter to citizen's verified phone
  • Store: account_id, purpose=LOGIN, expires_at=now()+5min

STEP 4 — OTP Validation
  • Citizen submits OTP
  • Validate: not expired, not used, code_hash matches
  • Mark as used; reset failed_login_count

STEP 5 — Token Issuance
  • Issue access_token (JWT; 15-min TTL; signed RS256)
  • Issue refresh_token (opaque; 30-day TTL; stored hashed)
  • Issue id_token (OIDC; contains sub, national_id_hash, loa)
  
STEP 6 — Refresh Token Rotation
  • Each refresh returns new access_token + new refresh_token
  • Old refresh_token invalidated (family-based; reuse = revoke entire family)
```

### C. Level of Assurance (LoA)

```
LoA 1 — Password + OTP            (basic services: inquiry, document view)
LoA 2 — LoA1 + KYC documents      (high-value services: passport, license)
LoA 3 — LoA2 + biometric/in-person(critical: property, legal transactions)
```

---

## 7. Ministry Service — Deep Dive

### Concept

The Ministry Service is the **integration registry** — it does not itself call ministries, but it stores all the metadata needed for the ServiceRequest Service to call them. Think of it as a **ministry API registry with credential vault**.

### Ministry Data Model

```
Ministry (top-level entity)
  └── Sub-Ministry (recursive; unlimited depth)
        └── Ministry Endpoint (specific API endpoint for a service)
              └── Ministry Auth Credential (how to authenticate to this endpoint)
                    └── Endpoint Header Template (custom headers per call)
```

### Database Schema (ministry_db)

```sql
-- Core ministry entity
ministries
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  parent_ministry_id    UUID REFERENCES ministries(id)          -- NULL = top-level ministry
  code                  VARCHAR(20) NOT NULL UNIQUE              -- e.g. 'MOI', 'MOJ', 'MOH'
  name_ar               VARCHAR(300) NOT NULL
  name_en               VARCHAR(300) NOT NULL
  description_ar        TEXT
  description_en        TEXT
  logo_url              TEXT
  website_url           TEXT
  contact_email         VARCHAR(254)
  contact_phone         VARCHAR(20)
  address_ar            TEXT
  address_en            TEXT
  is_active             BOOLEAN NOT NULL DEFAULT TRUE
  depth_level           SMALLINT NOT NULL DEFAULT 0              -- 0=ministry, 1=department, 2=division
  display_order         SMALLINT NOT NULL DEFAULT 0
  metadata              JSONB                                    -- flexible extra fields
  created_at            TIMESTAMPTZ NOT NULL DEFAULT now()
  updated_at            TIMESTAMPTZ NOT NULL DEFAULT now()

-- Auth configuration for connecting TO the ministry external system
-- Each ministry system may require a different auth method
ministry_auth_configs
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  ministry_id           UUID NOT NULL REFERENCES ministries(id)
  name                  VARCHAR(100) NOT NULL                    -- 'Civil Registry Production', 'Traffic API'
  auth_type             ministry_auth_type NOT NULL
  base_url              TEXT NOT NULL                            -- base URL of the ministry's API
  is_active             BOOLEAN NOT NULL DEFAULT TRUE
  is_default            BOOLEAN NOT NULL DEFAULT FALSE           -- default auth for this ministry
  health_check_path     TEXT                                     -- e.g. '/health' or '/ping'
  timeout_seconds       SMALLINT NOT NULL DEFAULT 30
  retry_count           SMALLINT NOT NULL DEFAULT 3
  created_at            TIMESTAMPTZ NOT NULL DEFAULT now()
  updated_at            TIMESTAMPTZ NOT NULL DEFAULT now()

CREATE TYPE ministry_auth_type AS ENUM (
  'OIDC',           -- OIDC client_credentials against ministry's OIDC server
  'OAUTH2',         -- OAuth 2.0 client_credentials
  'API_KEY',        -- Static API key in header/query
  'BEARER_TOKEN',   -- Static bearer token
  'BASIC_AUTH',     -- Username + password (HTTP Basic)
  'SAML',           -- SAML service provider integration
  'CUSTOM',         -- Custom plugin adapter
  'NONE'            -- Public API; no auth required
);

-- Auth credentials (all sensitive fields encrypted at rest with RSA-256 / OAEP)
-- Stored separately from config for access control
ministry_auth_credentials
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  auth_config_id        UUID NOT NULL REFERENCES ministry_auth_configs(id)
  -- OIDC / OAuth2 fields
  oidc_token_endpoint   TEXT                                     -- e.g. 'https://ministry.gov.ye/connect/token'
  oidc_client_id        TEXT                                     -- encrypted
  oidc_client_secret    TEXT                                     -- encrypted
  oidc_scope            TEXT                                     -- e.g. 'api openid'
  -- API Key fields
  api_key_header_name   TEXT                                     -- e.g. 'X-Api-Key'
  api_key_value         TEXT                                     -- encrypted
  api_key_placement     VARCHAR(20)                              -- HEADER, QUERY, COOKIE
  -- Bearer Token
  bearer_token          TEXT                                     -- encrypted; static token
  -- Basic Auth
  basic_username        TEXT                                     -- encrypted
  basic_password        TEXT                                     -- encrypted
  -- SAML
  saml_entity_id        TEXT
  saml_sso_url          TEXT
  saml_certificate      TEXT                                     -- encrypted
  -- Token cache (for OIDC/OAuth2)
  cached_access_token   TEXT                                     -- encrypted; refreshed automatically
  token_expires_at      TIMESTAMPTZ
  -- Audit
  last_verified_at      TIMESTAMPTZ
  last_used_at          TIMESTAMPTZ
  created_by            UUID NOT NULL
  updated_by            UUID
  created_at            TIMESTAMPTZ NOT NULL DEFAULT now()
  updated_at            TIMESTAMPTZ NOT NULL DEFAULT now()

-- Ministry API endpoints (what APIs the ministry exposes that Sheba can call)
ministry_endpoints
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  ministry_id           UUID NOT NULL REFERENCES ministries(id)
  auth_config_id        UUID REFERENCES ministry_auth_configs(id) -- which auth to use
  code                  VARCHAR(100) NOT NULL                    -- unique within ministry
  name_ar               VARCHAR(300) NOT NULL
  name_en               VARCHAR(300) NOT NULL
  description_ar        TEXT
  description_en        TEXT
  http_method           VARCHAR(10) NOT NULL                     -- GET, POST, PUT, DELETE, PATCH
  path_template         TEXT NOT NULL                            -- e.g. '/citizens/{nationalId}/verify'
  request_schema        JSONB                                    -- JSON Schema for request body
  response_schema       JSONB                                    -- JSON Schema for expected response
  endpoint_type         endpoint_type NOT NULL
  is_active             BOOLEAN NOT NULL DEFAULT TRUE
  timeout_seconds       SMALLINT NOT NULL DEFAULT 30
  rate_limit_per_minute SMALLINT                                 -- optional per-endpoint rate limit
  requires_citizen_consent BOOLEAN NOT NULL DEFAULT FALSE        -- citizen must consent before call
  UNIQUE(ministry_id, code)

CREATE TYPE endpoint_type AS ENUM (
  'DATA_QUERY',         -- read-only data fetch (citizen lookup, record check)
  'SERVICE_ACTION',     -- triggers an action in ministry system (issue doc, register)
  'WEBHOOK_REGISTER',   -- registers Sheba as webhook receiver
  'STATUS_CHECK',       -- poll for async operation result
  'DOCUMENT_FETCH',     -- fetch a document/file
  'HEALTH'              -- health probe
);

-- Custom headers to send with endpoint calls (e.g. X-Ministry-Source: SHEBA)
endpoint_header_templates
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  endpoint_id           UUID NOT NULL REFERENCES ministry_endpoints(id)
  header_name           TEXT NOT NULL
  header_value          TEXT NOT NULL                            -- may contain {variables}
  is_encrypted          BOOLEAN NOT NULL DEFAULT FALSE

-- Webhook registrations (ministry systems calling SHEBA back)
ministry_webhooks
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  ministry_id           UUID NOT NULL REFERENCES ministries(id)
  endpoint_id           UUID REFERENCES ministry_endpoints(id)   -- the action that triggers this webhook
  event_type            VARCHAR(100) NOT NULL                    -- e.g. 'service_request.completed'
  sheba_webhook_path    TEXT NOT NULL                            -- the path on Sheba's side
  signing_secret       TEXT NOT NULL                            -- encrypted; used to verify HMAC signature
  is_active             BOOLEAN NOT NULL DEFAULT TRUE
  last_received_at      TIMESTAMPTZ
  created_at            TIMESTAMPTZ NOT NULL DEFAULT now()

-- Ministry connection log (health, auth token refresh, errors)
ministry_connection_logs
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  auth_config_id        UUID NOT NULL REFERENCES ministry_auth_configs(id)
  endpoint_id           UUID REFERENCES ministry_endpoints(id)
  event_type            VARCHAR(50) NOT NULL                     -- AUTH_SUCCESS, AUTH_FAIL, CALL_SUCCESS, CALL_ERROR
  status_code           SMALLINT
  duration_ms           INTEGER
  error_message         TEXT
  logged_at             TIMESTAMPTZ NOT NULL DEFAULT now()

-- Relying Party registration for ministry SSO (logical link to identity_db)
ministry_relying_parties
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  ministry_id           UUID NOT NULL REFERENCES ministries(id)
  rp_client_id          VARCHAR(100) NOT NULL                    -- matches identity_db.relying_parties.client_id
  purpose               TEXT                                     -- 'Staff SSO', 'Citizen portal SSO'
  created_at            TIMESTAMPTZ NOT NULL DEFAULT now()

-- Outbox
outbox_messages
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  aggregate_type        VARCHAR(100) NOT NULL
  aggregate_id          UUID NOT NULL
  event_type            VARCHAR(200) NOT NULL
  payload               JSONB NOT NULL
  created_at            TIMESTAMPTZ NOT NULL DEFAULT now()
  published_at          TIMESTAMPTZ
  error                 TEXT
  retry_count           SMALLINT NOT NULL DEFAULT 0
```

### Ministry Authentication Flow (Sheba → Ministry System)

```
When ServiceRequest Service needs to call a ministry endpoint:

1. Fetch ministry_endpoint + auth_config from Ministry Service (cached in Redis)

2. Check cached_access_token (for OIDC/OAuth2 types):
   • If valid → use it
   • If expired → refresh via token endpoint → store encrypted in DB + cache

3. Build request with auth adapter:
   OIDC/OAuth2  → Authorization: Bearer <token>
   API_KEY      → X-Api-Key: <key>  (or ?api_key=<key>)
   BEARER_TOKEN → Authorization: Bearer <static_token>
   BASIC_AUTH   → Authorization: Basic base64(user:pass)
   SAML         → POST SAML assertion (via SAML middleware)
   CUSTOM       → ICustomMinistryAuthAdapter.AuthenticateAsync()

4. Add endpoint_header_templates to request

5. Execute HTTP call with Polly:
   • Retry (exponential backoff, max retry_count)
   • Circuit breaker (open after 5 failures; half-open after 30s)
   • Timeout (endpoint.timeout_seconds)

6. Log result to ministry_connection_logs

7. Return response to ServiceRequest Service
```

---

## 8. Government Service Catalog — Deep Dive

### Concept

Services are **definitions** of what a citizen can request. Each service is linked to:
- A ministry (owner)
- One or more ministry endpoints (how to fulfill it)
- A form schema (what the citizen fills out)
- A workflow definition (the steps to complete it)
- A fee schedule (what it costs)
- Required LoA (minimum identity assurance level)

### Database Schema (service_request_db)

```sql
-- Service category (hierarchical: Education → School Enrollment → Transfer)
service_categories
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  parent_id             UUID REFERENCES service_categories(id)
  name_ar               VARCHAR(200) NOT NULL
  name_en               VARCHAR(200) NOT NULL
  icon_url              TEXT
  display_order         SMALLINT NOT NULL DEFAULT 0
  is_active             BOOLEAN NOT NULL DEFAULT TRUE

-- Government service definition
services
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  category_id           UUID NOT NULL REFERENCES service_categories(id)
  ministry_id           UUID NOT NULL                            -- logical FK to ministry_db
  code                  VARCHAR(100) NOT NULL UNIQUE             -- e.g. 'PASSPORT_RENEW'
  name_ar               VARCHAR(300) NOT NULL
  name_en               VARCHAR(300) NOT NULL
  description_ar        TEXT
  description_en        TEXT
  eligibility_rules     JSONB                                    -- JSON Logic rules
  required_loa          SMALLINT NOT NULL DEFAULT 1
  requires_appointment  BOOLEAN NOT NULL DEFAULT FALSE
  is_active             BOOLEAN NOT NULL DEFAULT TRUE
  is_online             BOOLEAN NOT NULL DEFAULT TRUE            -- can be submitted online
  average_days          SMALLINT                                 -- expected processing days
  display_order         SMALLINT NOT NULL DEFAULT 0
  tags                  TEXT[]
  metadata              JSONB
  created_at            TIMESTAMPTZ NOT NULL DEFAULT now()
  updated_at            TIMESTAMPTZ NOT NULL DEFAULT now()

-- Service form schema (JSON Schema defining what the citizen must fill in)
service_forms
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  service_id            UUID NOT NULL REFERENCES services(id) UNIQUE
  schema_version        VARCHAR(20) NOT NULL DEFAULT '1.0'
  form_schema           JSONB NOT NULL                          -- JSON Schema (form fields)
  ui_schema             JSONB                                   -- UI rendering hints
  validation_rules      JSONB                                   -- additional server-side rules

-- Required documents for each service
service_required_documents
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  service_id            UUID NOT NULL REFERENCES services(id)
  document_type         VARCHAR(100) NOT NULL                   -- 'NATIONAL_ID', 'BIRTH_CERT', etc.
  name_ar               VARCHAR(200) NOT NULL
  name_en               VARCHAR(200) NOT NULL
  is_mandatory          BOOLEAN NOT NULL DEFAULT TRUE
  max_size_mb           SMALLINT NOT NULL DEFAULT 5
  allowed_mime_types    TEXT[]                                  -- ['image/jpeg', 'application/pdf']

-- Fee schedule for services
service_fees
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  service_id            UUID NOT NULL REFERENCES services(id)
  fee_type              VARCHAR(50) NOT NULL                    -- BASE, EXPEDITE, DELIVERY
  name_ar               VARCHAR(200) NOT NULL
  name_en               VARCHAR(200) NOT NULL
  amount                NUMERIC(10,2) NOT NULL
  currency              CHAR(3) NOT NULL DEFAULT 'YER'
  is_mandatory          BOOLEAN NOT NULL DEFAULT TRUE
  valid_from            DATE NOT NULL
  valid_until           DATE

-- Service workflow steps
service_workflow_steps
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  service_id            UUID NOT NULL REFERENCES services(id)
  step_order            SMALLINT NOT NULL
  name_ar               VARCHAR(200) NOT NULL
  name_en               VARCHAR(200) NOT NULL
  step_type             workflow_step_type NOT NULL
  actor                 workflow_actor NOT NULL                 -- CITIZEN, SYSTEM, MINISTRY, ADMIN
  ministry_endpoint_id  UUID                                    -- logical FK to ministry_db endpoint
  timeout_hours         INTEGER
  is_automated          BOOLEAN NOT NULL DEFAULT FALSE
  on_success_step       SMALLINT                               -- next step number on success
  on_failure_step       SMALLINT                               -- next step number on failure
  config                JSONB                                   -- step-specific configuration

CREATE TYPE workflow_step_type AS ENUM (
  'CITIZEN_SUBMIT',       -- citizen submits form/docs
  'PAYMENT',              -- payment required
  'MINISTRY_API_CALL',    -- automated call to ministry endpoint
  'MINISTRY_REVIEW',      -- human review in ministry
  'ADMIN_REVIEW',         -- Sheba admin review
  'DOCUMENT_ISSUE',       -- issue a document/credential
  'NOTIFICATION',         -- send notification to citizen
  'WEBHOOK_WAIT',         -- wait for ministry webhook callback
  'APPOINTMENT'           -- book appointment
);

CREATE TYPE workflow_actor AS ENUM (
  'CITIZEN', 'SYSTEM', 'MINISTRY', 'SHEBA_ADMIN'
);

-- ─── CITIZEN SERVICE REQUESTS (runtime) ───────────────────────────────────

-- A citizen's actual request for a service
service_requests
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  reference_number      VARCHAR(20) NOT NULL UNIQUE             -- human-readable: SHB-2024-000001
  service_id            UUID NOT NULL REFERENCES services(id)
  citizen_id            UUID NOT NULL                           -- logical FK to citizen_db
  status                request_lifecycle_status NOT NULL
  current_step          SMALLINT NOT NULL DEFAULT 1
  form_data             JSONB                                   -- citizen's submitted form data (encrypted PII)
  priority              VARCHAR(20) NOT NULL DEFAULT 'NORMAL'   -- NORMAL, EXPRESS, URGENT
  submitted_at          TIMESTAMPTZ NOT NULL DEFAULT now()
  updated_at            TIMESTAMPTZ NOT NULL DEFAULT now()
  completed_at          TIMESTAMPTZ
  due_date              TIMESTAMPTZ                             -- SLA deadline
  rejection_reason      TEXT
  notes                 TEXT                                    -- internal notes

CREATE TYPE request_lifecycle_status AS ENUM (
  'DRAFT',              -- citizen started but not submitted
  'SUBMITTED',          -- citizen submitted
  'PAYMENT_PENDING',    -- awaiting payment
  'UNDER_REVIEW',       -- being reviewed
  'PROCESSING',         -- automated processing
  'AWAITING_MINISTRY',  -- waiting for ministry webhook/response
  'ACTION_REQUIRED',    -- citizen needs to provide more info
  'COMPLETED',          -- successfully completed
  'REJECTED',           -- rejected
  'CANCELLED',          -- cancelled by citizen
  'EXPIRED'             -- expired (not completed within SLA)
);

-- Documents attached to a specific request
request_documents
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  request_id            UUID NOT NULL REFERENCES service_requests(id)
  required_document_id  UUID REFERENCES service_required_documents(id)
  document_service_id   UUID NOT NULL                           -- FK to Document Service
  uploaded_at           TIMESTAMPTZ NOT NULL DEFAULT now()
  verified_at           TIMESTAMPTZ
  verified_by           UUID                                    -- admin who verified

-- Step execution log (audit trail of workflow progress)
request_step_executions
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  request_id            UUID NOT NULL REFERENCES service_requests(id)
  step_id               UUID NOT NULL REFERENCES service_workflow_steps(id)
  step_order            SMALLINT NOT NULL
  status                VARCHAR(30) NOT NULL                    -- PENDING, RUNNING, COMPLETED, FAILED, SKIPPED
  started_at            TIMESTAMPTZ NOT NULL DEFAULT now()
  completed_at          TIMESTAMPTZ
  actor_id              UUID                                    -- who/what executed this step
  actor_type            VARCHAR(30)                             -- CITIZEN, SYSTEM, MINISTRY, ADMIN
  result                JSONB                                   -- ministry API response, decision, etc.
  error_message         TEXT

-- Ministry API call log (specific calls made during service processing)
ministry_api_call_logs
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  request_id            UUID NOT NULL REFERENCES service_requests(id)
  step_execution_id     UUID REFERENCES request_step_executions(id)
  ministry_endpoint_id  UUID NOT NULL                          -- logical FK to ministry_db
  http_method           VARCHAR(10) NOT NULL
  request_path          TEXT NOT NULL                          -- path called (no base URL for security)
  request_body_hash     TEXT                                   -- hash only; no PII stored
  response_status       SMALLINT
  response_body         JSONB                                  -- sanitized response
  duration_ms           INTEGER
  called_at             TIMESTAMPTZ NOT NULL DEFAULT now()
  error                 TEXT

-- Webhook receipts from ministries
ministry_webhook_receipts
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  request_id            UUID REFERENCES service_requests(id)
  webhook_id            UUID NOT NULL                          -- logical FK to ministry_db.ministry_webhooks
  event_type            VARCHAR(100) NOT NULL
  payload               JSONB NOT NULL
  signature_valid       BOOLEAN NOT NULL
  processed_at          TIMESTAMPTZ
  processing_error      TEXT
  received_at           TIMESTAMPTZ NOT NULL DEFAULT now()

-- Citizen comments / communications on a request
request_communications
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  request_id            UUID NOT NULL REFERENCES service_requests(id)
  sender_type           VARCHAR(20) NOT NULL                   -- CITIZEN, ADMIN, MINISTRY, SYSTEM
  sender_id             UUID
  message               TEXT NOT NULL
  attachments           UUID[]                                 -- document_service_ids
  sent_at               TIMESTAMPTZ NOT NULL DEFAULT now()

-- Outbox
outbox_messages
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid()
  aggregate_type        VARCHAR(100) NOT NULL
  aggregate_id          UUID NOT NULL
  event_type            VARCHAR(200) NOT NULL
  payload               JSONB NOT NULL
  created_at            TIMESTAMPTZ NOT NULL DEFAULT now()
  published_at          TIMESTAMPTZ
  error                 TEXT
  retry_count           SMALLINT NOT NULL DEFAULT 0
```

---

## 9. Pluggable National ID Connector

### Design

The NID connector is an **adapter interface** with a mock implementation for development and pluggable real implementations per country/system.

```csharp
// Domain port (in Identity.Domain)
public interface INationalIdProvider
{
    string ProviderCode { get; }  // 'MOCK', 'CIVIL_REGISTRY_V1', 'OPENCRVS'
    
    Task<NidVerificationResult> VerifyCitizenAsync(
        string nationalId, 
        string phoneNumber, 
        CancellationToken ct);
    
    Task<CitizenNidRecord?> GetCitizenDataAsync(
        string nationalId, 
        CancellationToken ct);
    
    Task<bool> IsAliveAsync(CancellationToken ct);  // health check
}

public record NidVerificationResult(
    bool IsVerified,
    NidVerificationFailureReason? FailureReason,
    string? FailureMessage  // logged internally, not returned to client
);

public enum NidVerificationFailureReason
{
    NotFound, PhoneMismatch, Deceased, Suspended, InvalidFormat
}

public record CitizenNidRecord(
    string NationalId,
    string FullNameAr,
    string FullNameEn,
    DateOnly DateOfBirth,
    string Gender,            // M / F
    string Governorate,
    string District,
    string PhoneNumber,
    string? MotherNationalId,
    string? FatherNationalId,
    DateTime? ExpiryDate,     // NID expiry
    bool IsActive
);
```

### Adapter Implementations

```
INationalIdProvider
├── MockNationalIdProvider           (development; hardcoded test citizens)
├── CivilRegistryHttpProvider        (HTTP REST adapter to civil registry API)
├── OpenCrvsProvider                 (OpenCRVS GraphQL/REST adapter)
└── DatabaseDirectProvider           (direct DB query; when Sheba hosts the data)
```

### Mock Provider — Development Database

```sql
-- mock_civil_registry.mock_citizens (in development only; separate DB/schema)
CREATE TABLE mock_citizens (
  national_id         VARCHAR(20) PRIMARY KEY,
  full_name_ar        VARCHAR(300) NOT NULL,
  full_name_en        VARCHAR(300) NOT NULL,
  date_of_birth       DATE NOT NULL,
  gender              CHAR(1) NOT NULL,
  phone_number        VARCHAR(20) NOT NULL,
  governorate         VARCHAR(100),
  district            VARCHAR(100),
  mother_national_id  VARCHAR(20),
  father_national_id  VARCHAR(20),
  expiry_date         DATE,
  is_active           BOOLEAN NOT NULL DEFAULT TRUE,
  is_deceased         BOOLEAN NOT NULL DEFAULT FALSE,
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Pre-seeded test citizens for all development scenarios:
-- 1000000001 — valid active citizen
-- 1000000002 — deceased citizen (test rejection)
-- 1000000003 — suspended citizen (test rejection)
-- 1000000004 — expired NID (test rejection)
-- 1000000005 — valid; phone mismatch scenario
```

### Configuration

```json
{
  "NationalIdProvider": {
    "ActiveProvider": "CivilRegistryHttp",   // switch via config / feature flag
    "Providers": {
      "Mock": { "Enabled": true },
      "CivilRegistryHttp": {
        "BaseUrl": "https://nid.gov.ye/api/v1",
        "ApiKey": "<from secrets>",
        "TimeoutSeconds": 10
      },
      "OpenCrvs": {
        "BaseUrl": "https://opencrvs.gov.ye/graphql",
        "ClientId": "<from secrets>",
        "ClientSecret": "<from secrets>"
      }
    }
  }
}
```

---

## 10. Pluggable OTP System

### Design

```csharp
// Domain port
public interface IOtpProvider
{
    string ProviderCode { get; }
    
    Task<OtpSendResult> SendOtpAsync(
        string phoneNumber,
        string code,
        OtpPurpose purpose,
        CancellationToken ct);
    
    Task<bool> IsHealthyAsync(CancellationToken ct);
}

public record OtpSendResult(
    bool Succeeded,
    string? MessageId,       // provider's message ID for tracking
    string? FailureReason
);
```

### Implementations

```
IOtpProvider
├── ConsoleSmsProvider           (development: logs to console)
├── TwilioSmsProvider            (Twilio Verify API)
├── NexmoSmsProvider             (Vonage SMS)
├── LocalYemeniSmsProvider       (direct gateway to local telcos: MTN, Y)
├── FirebasePushProvider         (push notification for mobile apps)
└── EmailOtpProvider             (fallback: send code via email)
```

### OTP Generation Security

```
• 6-digit numeric code
• Generated using: RandomNumberGenerator.GetInt32(100000, 999999)
• Stored as: Argon2id hash (never plaintext)
• TTL: 5 minutes (configurable per purpose)
• Max attempts: 3 (then code invalidated; must request new one)
• Rate limiting: max 3 OTP requests per 15 minutes per account
• The OTP code is NEVER logged anywhere
```

---

## 11. Wallet Service (W3C Verifiable Credentials)

### Concept

Citizens can hold **digital credentials** in their Sheba wallet — verifiable proof of facts about themselves, issued by trusted authorities (Sheba itself, or connected ministries).

```
Examples of Verifiable Credentials:
• Proof of Digital Identity (issued by Sheba)
• Birth Certificate (issued via OpenCRVS)
• Driver's License (issued by Traffic Ministry)
• Educational Certificate (issued by Ministry of Education)
• Property Ownership (issued by Ministry of Housing)
```

### Database Schema (wallet_db)

```sql
-- DID document registry (Decentralized Identifiers per citizen)
did_documents
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  citizen_id          UUID NOT NULL UNIQUE                     -- logical FK to citizen_db
  did                 TEXT NOT NULL UNIQUE                     -- did:sheba:xxxxxxx
  did_document        JSONB NOT NULL                          -- W3C DID Document
  public_key_jwk      JSONB NOT NULL                          -- citizen's public key
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
  updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()

-- Credential schema registry (what types of VCs exist)
credential_schemas
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  schema_id           TEXT NOT NULL UNIQUE                     -- URI: https://sheba.gov.ye/schemas/v1/identity
  name                VARCHAR(200) NOT NULL
  version             VARCHAR(20) NOT NULL
  issuer_did          TEXT NOT NULL
  schema_definition   JSONB NOT NULL                          -- JSON Schema for the VC
  is_active           BOOLEAN NOT NULL DEFAULT TRUE
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()

-- Issued credentials (in citizen's wallet)
verifiable_credentials
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  citizen_id          UUID NOT NULL                           -- holder
  schema_id           UUID NOT NULL REFERENCES credential_schemas(id)
  issuer_ministry_id  UUID                                    -- NULL = issued by Sheba
  credential_id       TEXT NOT NULL UNIQUE                    -- UUID URI per W3C spec
  credential_jwt      TEXT NOT NULL                          -- signed JWT-VC (encrypted at rest)
  status              vc_status NOT NULL
  issued_at           TIMESTAMPTZ NOT NULL DEFAULT now()
  expires_at          TIMESTAMPTZ
  revoked_at          TIMESTAMPTZ
  revocation_reason   TEXT
  service_request_id  UUID                                    -- which service request triggered issuance

CREATE TYPE vc_status AS ENUM ('ACTIVE', 'REVOKED', 'EXPIRED', 'SUSPENDED');

-- Credential presentation history (who the citizen shared credentials with)
credential_presentations
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  credential_id       UUID NOT NULL REFERENCES verifiable_credentials(id)
  citizen_id          UUID NOT NULL
  verifier_rp_id      VARCHAR(100)                            -- relying party that requested
  presented_at        TIMESTAMPTZ NOT NULL DEFAULT now()
  presentation_purpose TEXT
  citizen_consented   BOOLEAN NOT NULL DEFAULT TRUE
```

---

## 12. Notification Service

### Architecture

```
NotificationService receives events from RabbitMQ and delivers via:
• Email     → SMTP / SendGrid / AWS SES
• SMS       → Pluggable (same interface as OTP provider)
• Push      → Firebase Cloud Messaging
• In-App    → stored in DB; citizen polls via API
```

### Database Schema (notification_db)

```sql
-- Notification templates (multi-language)
notification_templates
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  code                VARCHAR(100) NOT NULL UNIQUE             -- e.g. 'IDENTITY_REQUEST_APPROVED'
  channel             notification_channel NOT NULL
  language            CHAR(2) NOT NULL DEFAULT 'ar'
  subject_template    TEXT                                     -- for email; supports Liquid/Handlebars
  body_template       TEXT NOT NULL
  is_active           BOOLEAN NOT NULL DEFAULT TRUE
  UNIQUE(code, channel, language)

CREATE TYPE notification_channel AS ENUM ('EMAIL', 'SMS', 'PUSH', 'IN_APP');

-- Notification records (every notification attempt)
notifications
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  recipient_id        UUID NOT NULL                           -- citizen or admin
  recipient_type      VARCHAR(20) NOT NULL                   -- CITIZEN, ADMIN
  recipient_address   TEXT NOT NULL                          -- email / phone / device_token (encrypted)
  template_id         UUID REFERENCES notification_templates(id)
  channel             notification_channel NOT NULL
  subject             TEXT
  body                TEXT NOT NULL
  status              notification_status NOT NULL
  reference_type      VARCHAR(100)                           -- 'identity_request', 'service_request', etc.
  reference_id        UUID
  sent_at             TIMESTAMPTZ
  delivered_at        TIMESTAMPTZ
  failed_at           TIMESTAMPTZ
  failure_reason      TEXT
  retry_count         SMALLINT NOT NULL DEFAULT 0
  provider_message_id TEXT                                   -- provider's tracking ID
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()

CREATE TYPE notification_status AS ENUM (
  'PENDING', 'SENDING', 'SENT', 'DELIVERED', 'FAILED', 'BOUNCED', 'READ'
);

-- In-app notifications (for citizen portal)
in_app_notifications
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  citizen_id          UUID NOT NULL
  title_ar            TEXT NOT NULL
  title_en            TEXT NOT NULL
  body_ar             TEXT NOT NULL
  body_en             TEXT NOT NULL
  action_url          TEXT
  icon_type           VARCHAR(50)                            -- INFO, SUCCESS, WARNING, ERROR
  is_read             BOOLEAN NOT NULL DEFAULT FALSE
  read_at             TIMESTAMPTZ
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
  expires_at          TIMESTAMPTZ
```

---

## 13. Document Service

### Database Schema (document_db)

```sql
-- Document metadata (actual files stored in MinIO)
documents
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  owner_id            UUID NOT NULL                          -- citizen or admin who owns it
  owner_type          VARCHAR(20) NOT NULL                   -- CITIZEN, ADMIN, SYSTEM
  document_type       VARCHAR(100) NOT NULL                  -- NATIONAL_ID, SELFIE, BIRTH_CERT, etc.
  category            VARCHAR(50) NOT NULL                   -- KYC, SERVICE_REQUEST, ISSUED, WALLET
  original_filename   TEXT NOT NULL
  content_type        VARCHAR(100) NOT NULL                  -- MIME type
  size_bytes          BIGINT NOT NULL
  minio_bucket        VARCHAR(100) NOT NULL
  minio_object_key    TEXT NOT NULL                         -- encrypted path
  checksum_sha256     TEXT NOT NULL                         -- integrity check
  encryption_key_id   UUID NOT NULL                         -- reference to key in KMS
  is_virus_clean      BOOLEAN                               -- null = pending scan
  virus_scan_at       TIMESTAMPTZ
  access_level        VARCHAR(20) NOT NULL DEFAULT 'PRIVATE' -- PRIVATE, RESTRICTED, PUBLIC
  expires_at          TIMESTAMPTZ
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
  deleted_at          TIMESTAMPTZ                           -- soft delete

-- Document access grants (who can access which document)
document_access_grants
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  document_id         UUID NOT NULL REFERENCES documents(id)
  grantee_id          UUID NOT NULL
  grantee_type        VARCHAR(20) NOT NULL                  -- ADMIN, MINISTRY, SYSTEM
  permission          VARCHAR(20) NOT NULL                  -- READ, DOWNLOAD
  granted_at          TIMESTAMPTZ NOT NULL DEFAULT now()
  expires_at          TIMESTAMPTZ
  granted_by          UUID NOT NULL

-- Presigned URL log (track who generated download links)
presigned_url_logs
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  document_id         UUID NOT NULL REFERENCES documents(id)
  generated_by        UUID NOT NULL
  expires_at          TIMESTAMPTZ NOT NULL
  used_at             TIMESTAMPTZ
  ip_address          INET
  generated_at        TIMESTAMPTZ NOT NULL DEFAULT now()
```

---

## 14. Payment Service

### Database Schema (payment_db)

```sql
-- Payment providers configuration
payment_providers
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  code                VARCHAR(50) NOT NULL UNIQUE            -- 'INTERNAL_WALLET', 'BANK_TRANSFER', etc.
  name                VARCHAR(200) NOT NULL
  is_active           BOOLEAN NOT NULL DEFAULT TRUE
  config              JSONB                                  -- encrypted provider config

-- Payment orders (created per service request that needs payment)
payment_orders
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  reference_number    VARCHAR(30) NOT NULL UNIQUE            -- PAY-2024-000001
  service_request_id  UUID NOT NULL                         -- logical FK to service_request_db
  citizen_id          UUID NOT NULL
  status              payment_order_status NOT NULL
  subtotal            NUMERIC(10,2) NOT NULL
  discount            NUMERIC(10,2) NOT NULL DEFAULT 0
  total               NUMERIC(10,2) NOT NULL
  currency            CHAR(3) NOT NULL DEFAULT 'YER'
  provider_id         UUID REFERENCES payment_providers(id)
  provider_order_id   TEXT                                  -- external provider's order ID
  paid_at             TIMESTAMPTZ
  refunded_at         TIMESTAMPTZ
  refund_amount       NUMERIC(10,2)
  expires_at          TIMESTAMPTZ NOT NULL
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
  updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()

CREATE TYPE payment_order_status AS ENUM (
  'PENDING', 'AWAITING_PAYMENT', 'PAID', 'FAILED', 'CANCELLED', 'REFUNDED', 'PARTIALLY_REFUNDED'
);

-- Line items on a payment order
payment_order_items
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  order_id            UUID NOT NULL REFERENCES payment_orders(id)
  fee_id              UUID NOT NULL                         -- logical FK to service_fees
  description_ar      TEXT NOT NULL
  description_en      TEXT NOT NULL
  amount              NUMERIC(10,2) NOT NULL
  quantity            SMALLINT NOT NULL DEFAULT 1

-- Payment transactions (actual money movements)
payment_transactions
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  order_id            UUID NOT NULL REFERENCES payment_orders(id)
  type                VARCHAR(30) NOT NULL                  -- CHARGE, REFUND, REVERSAL
  amount              NUMERIC(10,2) NOT NULL
  currency            CHAR(3) NOT NULL
  provider_tx_id      TEXT                                  -- external transaction ID
  status              VARCHAR(30) NOT NULL
  gateway_response    JSONB
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
```

---

## 15. Audit Service

### Principles

- **Immutable** — no UPDATE or DELETE on audit_events (enforced at DB user level)
- **Append-only** — only INSERT is granted to the audit writer user
- Subscribes to ALL domain events across ALL services
- Partitioned by month for performance

### Database Schema (audit_db)

```sql
-- All audit events (append-only; partitioned by month)
audit_events (PARTITION BY RANGE (occurred_at))
  id                  UUID NOT NULL DEFAULT gen_random_uuid()
  event_id            UUID NOT NULL                          -- source domain event ID
  event_type          VARCHAR(200) NOT NULL                  -- e.g. 'identity.account.approved'
  aggregate_type      VARCHAR(100) NOT NULL                  -- 'Account', 'ServiceRequest', etc.
  aggregate_id        UUID NOT NULL
  actor_id            UUID                                   -- who triggered it
  actor_type          VARCHAR(30)                            -- CITIZEN, ADMIN, SYSTEM, MINISTRY
  actor_ip            INET
  actor_user_agent    TEXT
  service_name        VARCHAR(100) NOT NULL                  -- source microservice
  payload             JSONB NOT NULL                        -- sanitized event payload
  correlation_id      UUID                                  -- request correlation ID
  occurred_at         TIMESTAMPTZ NOT NULL                  -- event time (partition key)
  recorded_at         TIMESTAMPTZ NOT NULL DEFAULT now()    -- when audit received it
  
-- Monthly partitions created automatically via pg_partman
-- Example: audit_events_2024_01, audit_events_2024_02, ...

-- Audit queries / reports (pre-computed for compliance)
audit_reports
  id                  UUID PRIMARY KEY DEFAULT gen_random_uuid()
  report_type         VARCHAR(100) NOT NULL
  generated_at        TIMESTAMPTZ NOT NULL DEFAULT now()
  generated_by        UUID NOT NULL
  parameters          JSONB
  result_file_id      UUID                                  -- document service reference
  row_count           INTEGER
```

---

## 16. API Gateway

### Responsibilities

- Single entry point for all external traffic
- mTLS termination for service-to-service
- JWT validation and forwarding of claims as headers
- Rate limiting (per IP, per user, per client)
- Request/Response logging (correlation IDs)
- Routing to microservices
- CORS policy

### Routing Table

```
/api/identity/*         → Identity Service   :5001
/api/citizen/*          → Citizen Service    :5002
/api/ministry/*         → Ministry Service   :5003
/api/services/*         → ServiceRequest     :5004
/api/documents/*        → Document Service   :5005
/api/wallet/*           → Wallet Service     :5006
/api/payments/*         → Payment Service    :5007
/api/notifications/*    → Notification       :5008
/api/audit/*            → Audit Service      :5009
/connect/*              → Identity Service   :5001  (OIDC endpoints)
/.well-known/*          → Identity Service   :5001
/admin/*                → Admin BFF          :5010
```

---

## 17. Normalized Database Design

### Normalization Strategy

| Level | Applied To | Reasoning |
|-------|-----------|-----------|
| 1NF | All tables | Atomic values; no repeating groups (arrays used only for simple scalar lists like tags, not for entity relationships) |
| 2NF | All tables | No partial dependencies; all non-key columns depend on the full primary key |
| 3NF | All tables | No transitive dependencies; reference data stored in lookup tables |
| BCNF | Critical entities | Account, IdentityRequest, ServiceRequest — all determinants are candidate keys |
| Denormalization | Read models only | Materialized views and Redis caches for reporting and high-read paths |

### Key Normalization Decisions

```
1. Person name data stays in citizen_db (Citizen Service) — Identity Service
   only holds authentication data (national_id, username, email, password).
   No duplication of citizen profile across services.

2. Ministry hierarchy uses adjacency list (parent_ministry_id self-reference)
   rather than storing path/level redundantly. Path queries use recursive CTEs.

3. Service fees normalized into service_fees table — not embedded in services.
   Supports historical fee records (valid_from/valid_until) without data duplication.

4. Auth credentials split across two tables (ministry_auth_configs + 
   ministry_auth_credentials) — separating connection config from secrets
   enables different access control levels on each table.

5. Outbox pattern: each service has its own outbox_messages table to avoid
   distributed transactions. Never a shared outbox.

6. OTP codes: only the hash is stored, never the plaintext code. TTL enforced
   by expires_at column + scheduled cleanup job (every 1 minute).

7. Audit events partitioned by month — prevents single-table bloat and enables
   efficient time-range queries without full-table scans.
```

### Cross-Service Data References

Since each service owns its own database, cross-service references are **logical foreign keys** (UUIDs that reference another service's entities, without DB-level constraints):

```
Pattern: Event-carried state transfer
• When ServiceRequest needs citizen data → publishes request event → Citizen Service 
  responds with data → stored in request snapshot
• For frequent reads: API call with Redis caching (TTL 5 min) in calling service
• Never query another service's database directly — always via API or events
```

---

## 18. Event-Driven Architecture (RabbitMQ + MassTransit)

### Topic / Exchange Design

```
Exchange: sheba.events (topic type)
Routing key pattern: <service>.<aggregate>.<event>

Examples:
identity.account.registered
identity.account.approved
identity.account.rejected
identity.account.login_success
identity.account.login_failed
identity.account.locked
identity.otp.sent
identity.otp.verified
citizen.profile.updated
citizen.document.uploaded
ministry.created
ministry.auth_config.connected
ministry.auth_config.failed
service_request.submitted
service_request.step_completed
service_request.completed
service_request.rejected
service_request.ministry_api_called
service_request.webhook_received
payment.order.created
payment.order.paid
payment.order.failed
document.uploaded
document.virus_scanned
wallet.credential.issued
wallet.credential.revoked
notification.sent
notification.failed
```

### Key Sagas (long-running workflows)

```csharp
// Identity Request Saga
IdentityRequestSaga
  State: PENDING → NID_VERIFYING → OTP_PENDING → DETAILS_SUBMITTED 
       → EMAIL_VERIFYING → ADMIN_QUEUE → ADMIN_REVIEWING → COMPLETED
  
  Compensations:
  • NID verification failed → notify citizen; delete draft account
  • OTP expired → allow resend (max 3 times)
  • Email verification expired → resend (max 3 times)
  • Admin rejected → notify citizen; mark account rejected

// Service Request Saga
ServiceRequestSaga
  State: SUBMITTED → PAYMENT_PENDING → MINISTRY_CALLING → WEBHOOK_WAITING
       → REVIEW_PENDING → CREDENTIAL_ISSUING → COMPLETED
  
  Compensations:
  • Payment failed → notify citizen; return to PAYMENT_PENDING
  • Ministry API error → retry (Polly) → escalate to admin
  • Webhook timeout → poll ministry endpoint → escalate
  • SLA breach → notify admin; escalate priority
```

---

## 19. Security Architecture

### Defense in Depth

```
Layer 1 — Network
  • Cloudflare / WAF in front of gateway
  • DDoS protection
  • IP allowlisting for ministry-to-Sheba calls (where possible)

Layer 2 — Transport
  • TLS 1.3 minimum externally
  • mTLS for all service-to-service communication
  • Certificate rotation via Vault / cert-manager

Layer 3 — API Gateway
  • JWT validation on every request
  • Rate limiting (sliding window): 
    - Anonymous: 10 req/min
    - Authenticated: 100 req/min
    - Admin: 300 req/min
    - Ministry M2M: 1000 req/min per client
  • Request size limits (1MB for API; 10MB for document upload)
  • CORS policy (allowlisted origins only)

Layer 4 — Authentication
  • Argon2id for passwords (min cost: memory=64MB, iterations=3, parallelism=4)
  • OTP required on every login
  • Refresh token rotation (family-based reuse detection)
  • Session invalidation on password change / account suspension
  • Admin: TOTP (Google Authenticator compatible) mandatory

Layer 5 — Authorization
  • Role-based (RBAC) for admin operations
  • Scope-based for OIDC relying parties
  • Resource-based (citizen can only access own data)
  • Claims forwarded from gateway as X-User-Id, X-User-Role, X-User-Loa headers (internal only)

Layer 6 — Data
  • Encryption at rest: RSA-256 (OAEP) for PII fields and credential secrets
  • JWT signing: RS256 (RSA-2048 + SHA-256) via OpenIddict
  • Key management: RSA private keys in Vault/HSM; public keys exposed via JWKS endpoint
  • PostgreSQL: row-level security where applicable
  • MinIO: server-side encryption (SSE-S3)
  • Soft deletes only — no hard deletes for compliance

Layer 7 — Audit
  • Every authentication event logged
  • Every admin action logged with full before/after state
  • Every ministry API call logged (request hash, response status)
  • Logs shipped to SIEM (Elasticsearch / OpenSearch)
  • Tamper-evident: append-only audit DB user
```

### OWASP Top 10 Mitigations

| Risk | Mitigation |
|------|-----------|
| Injection | EF Core parameterized queries only; no raw SQL with user input |
| Broken Auth | Argon2id + OTP + refresh token rotation + account lockout |
| IDOR | Resource ownership check in every handler; never expose internal IDs in URLs |
| Security Misconfiguration | Secrets in Vault/environment; no config in code; TLS enforced |
| Sensitive Data Exposure | PII encrypted at field level; presigned URLs for documents; no logging of sensitive data |
| SSRF | Ministry endpoint URLs allowlisted; no user-supplied URLs executed |
| XSS | API-only responses (no HTML rendering in backend); CSP headers |

---

## 20. Observability Stack

### Three Pillars

```
METRICS (Prometheus + Grafana)
  • Request rate, error rate, latency (P50/P95/P99) per service
  • Ministry API call success rate and latency per ministry
  • OTP delivery success rate per provider
  • Identity request approval/rejection rate
  • Active sessions, token issuance rate
  • Message queue depth and consumer lag per topic
  • Database connection pool utilization
  
TRACING (OpenTelemetry → Jaeger)
  • Distributed traces across all services (correlation_id propagated)
  • Ministry API calls annotated with ministry name + endpoint
  • Saga state transitions traced
  • Slow query detection (trace DB queries > 100ms)
  
LOGGING (Serilog → Elasticsearch → Kibana)
  • Structured JSON logs; never log PII
  • Log levels per service; dynamic adjustment via feature flags
  • Correlation ID on every log entry
  • Alert on error rate spike; on circuit breaker open; on OTP provider down
```

### Dashboards

```
1. Platform Health Dashboard
   • All services green/red
   • RabbitMQ queue depths
   • Error rates (last 1h/24h/7d)

2. Identity & Auth Dashboard
   • Login success/failure rates
   • Account registrations per day
   • Admin approval queue depth and average review time
   • OTP delivery rates per provider

3. Ministry Integration Dashboard
   • Per-ministry: call volume, success rate, avg latency
   • Circuit breaker states
   • Credential token refresh events

4. Service Request Dashboard
   • Requests per service type
   • Average completion time per service
   • SLA breach rate
   • Payment conversion rate

5. Security Dashboard
   • Failed login attempts by IP
   • Account lockout events
   • Suspicious activity alerts
```

---

## 21. Deployment Architecture

> For the graduation project deployment setup, see **Section 24**.

### Production Container Strategy (Future / Full Scale)

```
Kubernetes (production — full microservices)
  • Separate namespace per service
  • Horizontal Pod Autoscaler on Identity, Gateway, ServiceRequest
  • Secrets via Kubernetes Secrets + external-secrets-operator (Vault)
  • Ingress: NGINX + cert-manager (Let's Encrypt)
  • Service mesh: Istio (mTLS, traffic policies, circuit breaking at infra level)
  • One shared PostgreSQL cluster (Patroni HA) — one schema per module
  • Redis Cluster (3 nodes minimum)
  • RabbitMQ cluster (3 nodes)
  • MinIO distributed (4 nodes)
```

### Database Strategy — All Environments

**One PostgreSQL database, one schema per module.** This applies in development, staging, and production.

```
All environments:
  Single PostgreSQL instance (or Patroni HA cluster in production)
  ├── schema: identity      ← accounts, identity_requests, otp_records, relying_parties
  ├── schema: citizen       ← citizen_profiles, kyc_documents
  ├── schema: ministry      ← ministries, auth_configs, credentials, endpoints
  ├── schema: service_req   ← services, requests, workflow_steps
  ├── schema: document      ← documents, access_grants
  ├── schema: wallet        ← did_documents, verifiable_credentials
  ├── schema: payment       ← payment_orders, transactions
  ├── schema: notification  ← templates, notification_log
  ├── schema: audit         ← audit_events (INSERT only)
  └── schema: admin_data    ← analytics snapshots, report_jobs

Production additions:
  • Patroni HA cluster (primary + 2 standbys)
  • PgBouncer connection pooling
  • Read replica for audit/reporting queries
  • Continuous WAL archiving + daily base backups (30-day retention)
  • RSA-256 encrypted credential fields; encryption keys in Vault
```

---

## 22. Development Roadmap & Phasing

> **Graduation project build order is in Section 24.** The phases below represent the full production roadmap if Sheba were deployed nationally.

### Phase 0 — Foundation (Weeks 1–4)
- [ ] Solution structure (`Sheba.sln` — modular monolith with module boundaries)
- [ ] `Sheba.Shared.Kernel` (base entities, value objects, domain events, interfaces)
- [ ] Docker Compose: PostgreSQL, Redis, MinIO, Mailhog, Seq
- [ ] CI pipeline (build, test, lint)
- [ ] Mock NID provider + seeded test citizens

### Phase 1 — Identity Core (Weeks 5–10)
- [ ] Identity module: OpenIddict setup (OIDC discovery, JWKS, token endpoints)
- [ ] Identity module: Admin authentication + TOTP
- [ ] Identity module: Citizen registration workflow (NID check → OTP → details → email verify)
- [ ] Identity module: Admin approval queue
- [ ] Identity module: Citizen login (NID/username + password + OTP)
- [ ] Identity module: Refresh token rotation
- [ ] Notification module: Email (Mailhog dev) + SMS (console adapter)

### Phase 2 — Ministry & Citizen (Weeks 11–16)
- [ ] Citizen module: Profile management
- [ ] Ministry module: Ministry CRUD + hierarchical sub-ministry tree
- [ ] Ministry module: Auth config + encrypted credential vault
- [ ] Ministry module: Endpoint registry + webhook registration
- [ ] Ministry auth adapters: OIDC, OAuth2, API Key, Basic Auth, Bearer
- [ ] Relying Party management + OIDC dynamic client registration
- [ ] Pluggable NID HTTP adapter (connects to real or mock civil registry)

### Phase 3 — Service Catalog & Requests (Weeks 17–24)
- [ ] ServiceRequest module: categories + service definitions + JSON Schema forms
- [ ] ServiceRequest module: workflow engine (step-by-step execution)
- [ ] ServiceRequest module: ministry endpoint integration (outbound API calls)
- [ ] ServiceRequest module: webhook receiver (ministry callbacks)
- [ ] Payment module: fee tracking + full payment gateway adapter (mock provider implements real interface)
- [ ] Document module: upload + MinIO + presigned URLs
- [ ] Admin module: full analytics read model + report generation (PDF, Excel, CSV)

### Phase 4 — Wallet, Analytics & Hardening (Weeks 25–32)
- [ ] Wallet module: DID management + W3C VC issuance + verification endpoint
- [ ] Audit module: append-only event log + compliance export
- [ ] Scheduled reports via Hangfire + email delivery
- [ ] Seq structured logging (dev) → migrate to OpenTelemetry (production)
- [ ] Security hardening: rate limiting, IP allowlist for admin, anomaly detection

### Phase 5 — Production Migration (Post-graduation, if deployed)
- [ ] Split modular monolith into true microservices (one module → one service)
- [ ] Introduce RabbitMQ + MassTransit (replace in-process MediatR events)
- [ ] Kubernetes + Helm charts (PostgreSQL remains one shared cluster — schemas unchanged)
- [ ] Redis cluster, MinIO distributed
- [ ] Istio service mesh (mTLS between services)
- [ ] Patroni HA cluster for PostgreSQL (primary + standbys + PgBouncer)
- [ ] OpenTelemetry → Jaeger, Prometheus → Grafana, SIEM

---

---

## 23. Admin API Service (Backend Only)

### What It Is

`Sheba.Admin.Api` is a **dedicated backend service** — your frontend connects to it directly. It is not a domain service (it owns no citizen or ministry business logic), but it is far more than a passthrough. It owns:

- **Admin authentication** — completely separate from citizen auth; TOTP mandatory
- **Admin user management** — CRUD for admin accounts and roles
- **Cross-service aggregation** — assembles multi-service data into single admin-view responses
- **Approval workflow orchestration** — executes approval/rejection actions across services
- **Analytics read model** — its own denormalized `admin_db` populated by consuming domain events; never queries other service databases directly
- **Report generation** — PDF, Excel, CSV; on-demand and scheduled via Hangfire
- **Report scheduling and delivery** — scheduled reports emailed automatically

```
                         YOUR FRONTEND
                               │ HTTPS + Admin JWT
        ┌──────────────────────▼─────────────────────────────────────┐
        │                  Sheba.Admin.Api                             │
        │  ASP.NET Core 9 · Minimal API · Hangfire · QuestPDF         │
        │                                                              │
        │  ┌──────────────┐  ┌───────────────┐  ┌─────────────────┐  │
        │  │ Admin Auth   │  │  Aggregators  │  │ Report Engine   │  │
        │  │ • Password + │  │  • Identity   │  │ • PDF (QuestPDF)│  │
        │  │   TOTP       │  │    request    │  │ • Excel         │  │
        │  │ • Short TTL  │  │    detail     │  │   (ClosedXML)   │  │
        │  │   JWT        │  │  • Service    │  │ • CSV streaming │  │
        │  │ • Role RBAC  │  │    request    │  │ • Scheduled     │  │
        │  └──────────────┘  │    timeline   │  │   delivery      │  │
        │                    │  • Dashboard  │  └─────────────────┘  │
        │  ┌──────────────┐  │    KPIs       │  ┌─────────────────┐  │
        │  │ Service      │  └───────────────┘  │ Analytics       │  │
        │  │ Clients      │                     │ Read Model      │  │
        │  │ (typed HTTP) │  ┌───────────────┐  │ (admin_db)      │  │
        │  │ per service  │  │ Hangfire Jobs │  │ Populated from  │  │
        │  └──────────────┘  │ • Report gen  │  │ domain events   │  │
        │                    │ • Scheduled   │  │ via RabbitMQ    │  │
        │                    │   reports     │  └─────────────────┘  │
        │                    │ • Cleanup     │                        │
        │                    └───────────────┘                        │
        └──┬──────────┬──────────┬──────────┬──────────┬─────────────┘
           │          │          │          │          │
        Identity   Citizen   Ministry  Service    Audit +
        Service    Service   Service   Request    Payment
                                        Service   Services
                                        
        RabbitMQ ──(events)──► admin_db consumers (analytics read model)
```

---

### Admin Roles and Permissions

```
SUPER_ADMIN         — full access to everything; manage admin users
IDENTITY_REVIEWER   — review and approve/reject identity requests only
MINISTRY_MANAGER    — manage ministries, endpoints, auth configs, relying parties
SERVICE_MANAGER     — manage service catalog, workflow definitions, fee schedules
AUDITOR             — read-only: audit log, reports, analytics (no write access)
SUPPORT             — view citizen accounts and service requests; cannot approve/reject
```

Role is embedded in the admin JWT claim `role`. Every endpoint checks it via policy.

---

### Internal Project Structure

```
Sheba.Admin.Api/
├── Endpoints/
│   ├── AuthEndpoints.cs                  # admin login, TOTP verify, logout
│   ├── AdminUserEndpoints.cs             # CRUD admin users (SUPER_ADMIN only)
│   ├── IdentityRequestEndpoints.cs       # review queue, approve, reject
│   ├── AccountEndpoints.cs               # citizen accounts: search, suspend, etc.
│   ├── MinistryEndpoints.cs              # ministry tree, auth configs, endpoints
│   ├── RelyingPartyEndpoints.cs          # OIDC client management
│   ├── ServiceCatalogEndpoints.cs        # service definitions, form schemas, fees
│   ├── ServiceRequestEndpoints.cs        # monitor all citizen requests
│   ├── AnalyticsEndpoints.cs             # KPI data, trends, breakdowns
│   ├── ReportEndpoints.cs                # generate, schedule, download reports
│   └── AuditEndpoints.cs                 # audit log search
│
├── Aggregators/                          # multi-service data assembly
│   ├── IdentityRequestAggregator.cs      # request + citizen snapshot + docs + risk
│   ├── ServiceRequestDetailAggregator.cs # request + step log + ministry calls + comms
│   └── DashboardKpiAggregator.cs         # live KPIs from analytics read model
│
├── Clients/                              # typed HttpClient per service (Polly + mTLS)
│   ├── IdentityServiceClient.cs
│   ├── CitizenServiceClient.cs
│   ├── MinistryServiceClient.cs
│   ├── ServiceRequestServiceClient.cs
│   ├── DocumentServiceClient.cs
│   ├── PaymentServiceClient.cs
│   └── AuditServiceClient.cs
│
├── Analytics/
│   ├── Consumers/                        # MassTransit event consumers → write to admin_db
│   │   ├── AccountRegisteredConsumer.cs
│   │   ├── IdentityRequestDecidedConsumer.cs
│   │   ├── ServiceRequestStatusConsumer.cs
│   │   ├── PaymentOrderPaidConsumer.cs
│   │   └── MinistryApiCallConsumer.cs
│   ├── ReadModel/                        # EF Core for admin_db
│   └── Queries/                          # analytics query handlers
│
├── Reports/
│   ├── Generators/
│   │   ├── PdfReportGenerator.cs         # QuestPDF
│   │   ├── ExcelReportGenerator.cs       # ClosedXML
│   │   └── CsvReportGenerator.cs         # streaming CSV
│   ├── Templates/                        # QuestPDF document templates
│   │   ├── IdentityRequestsReportDocument.cs
│   │   ├── ServiceRequestsReportDocument.cs
│   │   ├── PaymentSummaryReportDocument.cs
│   │   ├── MinistryHealthReportDocument.cs
│   │   ├── AuditComplianceReportDocument.cs
│   │   └── CitizenAnalyticsReportDocument.cs
│   └── Scheduler/
│       ├── ReportSchedulerJob.cs          # Hangfire recurring job
│       └── ScheduledReportConfig.cs
│
├── Authorization/
│   ├── AdminJwtValidator.cs
│   └── Policies/                         # one policy per role
│
└── Program.cs
```

---

### Full API Endpoint Reference

#### Authentication (no prior auth needed)
```
POST /admin/auth/login                    — password + TOTP step 1 (returns challenge token)
POST /admin/auth/totp/verify              — TOTP code + challenge token → admin JWT
POST /admin/auth/refresh                  — refresh admin access token
POST /admin/auth/logout                   — revoke refresh token
GET  /admin/auth/me                       — current admin user profile
```

#### Admin User Management (SUPER_ADMIN only)
```
GET    /admin/users                       — paginated list with role filter
POST   /admin/users                       — create admin user (triggers email with temp password)
GET    /admin/users/{id}                  — profile + login history + activity
PUT    /admin/users/{id}                  — update name, department
PUT    /admin/users/{id}/role             — change role
POST   /admin/users/{id}/deactivate       — deactivate (cannot delete)
POST   /admin/users/{id}/reactivate       — reactivate
POST   /admin/users/{id}/reset-totp       — reset TOTP secret (user re-enrolls on next login)
```

#### Identity Request Queue (IDENTITY_REVIEWER, SUPER_ADMIN)
```
GET  /admin/identity/requests             — paginated queue; filter: status, date, assignee
GET  /admin/identity/requests/{id}        — full detail: civil registry snapshot + KYC docs
                                            + account info + previous requests + risk score
POST /admin/identity/requests/{id}/approve          — approve; triggers citizen notification
POST /admin/identity/requests/{id}/reject            — reject with reason; triggers notification
POST /admin/identity/requests/{id}/request-info      — ask citizen for more info
POST /admin/identity/requests/{id}/assign            — assign request to specific reviewer
GET  /admin/identity/requests/stats                  — queue depth, avg review time, today's count
```

#### Citizen Account Management (SUPPORT+)
```
GET  /admin/accounts                      — search: NID, username, email, phone, status
GET  /admin/accounts/{id}                 — profile + status + LoA + login history + sessions
GET  /admin/accounts/{id}/requests        — all identity requests by this citizen
GET  /admin/accounts/{id}/service-requests — all service requests by this citizen
POST /admin/accounts/{id}/suspend         — suspend with reason (IDENTITY_REVIEWER+)
POST /admin/accounts/{id}/reactivate      — reactivate (IDENTITY_REVIEWER+)
POST /admin/accounts/{id}/force-password-reset — citizen must reset on next login (SUPER_ADMIN)
POST /admin/accounts/{id}/revoke-sessions — invalidate all active sessions (SUPER_ADMIN)
```

#### Ministry Management (MINISTRY_MANAGER+)
```
GET    /admin/ministries                                — full tree (all depths)
GET    /admin/ministries/flat                           — flat paginated list
POST   /admin/ministries                                — create ministry/sub-ministry
GET    /admin/ministries/{id}                           — detail + sub-ministries + stats
PUT    /admin/ministries/{id}                           — update metadata
DELETE /admin/ministries/{id}                           — soft delete (only if no active endpoints)
PATCH  /admin/ministries/{id}/activate                  — reactivate

GET    /admin/ministries/{id}/auth-configs              — list auth configurations
POST   /admin/ministries/{id}/auth-configs              — add auth config (no credentials yet)
GET    /admin/ministries/{id}/auth-configs/{cid}        — config detail (no secrets returned)
PUT    /admin/ministries/{id}/auth-configs/{cid}        — update config metadata
POST   /admin/ministries/{id}/auth-configs/{cid}/credentials — set credentials (write-only; never read back)
POST   /admin/ministries/{id}/auth-configs/{cid}/test   — test connection right now → returns latency + status
GET    /admin/ministries/{id}/auth-configs/{cid}/logs   — recent connection log

GET    /admin/ministries/{id}/endpoints                 — list endpoints
POST   /admin/ministries/{id}/endpoints                 — register endpoint
GET    /admin/ministries/{id}/endpoints/{eid}           — endpoint detail + headers + schema
PUT    /admin/ministries/{id}/endpoints/{eid}           — update endpoint
PATCH  /admin/ministries/{id}/endpoints/{eid}/toggle    — activate/deactivate

GET    /admin/ministries/{id}/webhooks                  — list webhook registrations
POST   /admin/ministries/{id}/webhooks                  — register webhook + generate signing secret
DELETE /admin/ministries/{id}/webhooks/{wid}            — deactivate webhook
GET    /admin/ministries/{id}/webhooks/{wid}/receipts   — recent webhook receipt log
```

#### Relying Party Management (MINISTRY_MANAGER+)
```
GET    /admin/relying-parties                           — all RPs with status + last used
POST   /admin/relying-parties                           — register new RP in OpenIddict
GET    /admin/relying-parties/{id}                      — detail: scopes, redirect URIs, stats
PUT    /admin/relying-parties/{id}                      — update metadata
POST   /admin/relying-parties/{id}/rotate-secret        — rotate client secret (returns new secret once)
POST   /admin/relying-parties/{id}/revoke               — revoke RP (blocks all future tokens)
GET    /admin/relying-parties/{id}/tokens               — active token count (not the tokens themselves)
```

#### Service Catalog (SERVICE_MANAGER+)
```
GET    /admin/service-catalog/categories                — full category tree
POST   /admin/service-catalog/categories                — create category
PUT    /admin/service-catalog/categories/{id}           — update category

GET    /admin/service-catalog/services                  — paginated list with stats (requests/day, SLA rate)
POST   /admin/service-catalog/services                  — create service definition
GET    /admin/service-catalog/services/{id}             — full definition: form, workflow, fees, docs
PUT    /admin/service-catalog/services/{id}             — update service metadata
PATCH  /admin/service-catalog/services/{id}/toggle      — activate/deactivate
PUT    /admin/service-catalog/services/{id}/form        — update JSON Schema form definition
PUT    /admin/service-catalog/services/{id}/workflow    — update workflow steps
GET    /admin/service-catalog/services/{id}/fees        — fee schedules
POST   /admin/service-catalog/services/{id}/fees        — add fee
PUT    /admin/service-catalog/services/{id}/fees/{fid}  — update fee (creates new version)
GET    /admin/service-catalog/services/{id}/required-docs — required document list
```

#### Service Request Monitor (SUPPORT+)
```
GET  /admin/service-requests                    — all citizen requests; filter: status, service, ministry, date, citizen
GET  /admin/service-requests/{id}               — full detail: form data + step log + API calls + comms + payments
POST /admin/service-requests/{id}/escalate      — bump to higher priority (SERVICE_MANAGER+)
POST /admin/service-requests/{id}/cancel        — cancel request with reason (SERVICE_MANAGER+)
POST /admin/service-requests/{id}/override-step — manually advance/skip a step (SUPER_ADMIN only)
GET  /admin/service-requests/{id}/ministry-calls — log of all ministry API calls made for this request
GET  /admin/service-requests/{id}/webhooks       — webhook receipts for this request
POST /admin/service-requests/{id}/message        — send message to citizen about their request
```

#### Audit Log (AUDITOR+)
```
GET  /admin/audit/events                        — paginated search; filter: actor, event_type, service, date range, NID
GET  /admin/audit/events/{id}                   — single event detail with full payload
```

---

### Analytics Endpoints

The analytics read model (`admin_db`) is populated by consuming domain events from all services via RabbitMQ. All analytics queries hit `admin_db` only — never production service databases.

#### Platform KPI Dashboard
```
GET  /admin/analytics/kpis                      — live snapshot:
                                                    • total_accounts, active_accounts
                                                    • pending_identity_requests
                                                    • active_service_requests, completed_today
                                                    • payment_volume_today (amount + count)
                                                    • sla_breach_count
                                                    • avg_identity_approval_hours (last 30 days)

GET  /admin/analytics/kpis/trends               — query params: metric, period (daily/weekly/monthly), from, to
                                                  returns: time series array for charts
```

#### Identity & Account Analytics
```
GET  /admin/analytics/identity/registrations    — registrations over time; breakdown: approved/rejected/pending
GET  /admin/analytics/identity/approval-times   — avg, P50, P95 approval duration; by reviewer; by period
GET  /admin/analytics/identity/rejection-reasons — top rejection reasons with counts
GET  /admin/analytics/identity/loa-distribution — count of accounts per LoA level
GET  /admin/analytics/identity/geographic        — accounts by governorate (from civil registry snapshot)
GET  /admin/analytics/accounts/activity          — daily/monthly active accounts (login events)
GET  /admin/analytics/accounts/login-patterns    — login times heatmap; OTP channel usage; failure rates
```

#### Ministry & Integration Analytics
```
GET  /admin/analytics/ministries/health          — per-ministry: uptime %, avg latency, error rate (last 7/30 days)
GET  /admin/analytics/ministries/call-volume     — API call volume per ministry per day
GET  /admin/analytics/ministries/{id}/trends     — call success/failure trend for specific ministry
GET  /admin/analytics/ministries/slowest         — ranked list: slowest ministry endpoints (P95 latency)
GET  /admin/analytics/webhooks/delivery-rate     — webhook receipt + processing success rate per ministry
```

#### Service Request Analytics
```
GET  /admin/analytics/services/volume            — requests per service; daily/weekly/monthly
GET  /admin/analytics/services/completion-times  — avg completion time per service; by period
GET  /admin/analytics/services/sla               — SLA compliance rate per service (% completed within SLA)
GET  /admin/analytics/services/funnel            — step-by-step funnel: how many requests reach each step
GET  /admin/analytics/services/drop-off          — where requests commonly stall or get cancelled
GET  /admin/analytics/services/{id}/stats        — per-service: volume, completion, avg time, fee revenue
```

#### Payment Analytics
```
GET  /admin/analytics/payments/summary           — total revenue, count, avg fee; by period
GET  /admin/analytics/payments/by-service        — revenue breakdown per service
GET  /admin/analytics/payments/by-ministry       — revenue breakdown per ministry
GET  /admin/analytics/payments/failure-rate      — failed payment attempts over time
GET  /admin/analytics/payments/refunds           — refund volume and rate
```

#### Security Analytics (AUDITOR+)
```
GET  /admin/analytics/security/failed-logins     — failed login count over time; grouped by IP
GET  /admin/analytics/security/lockouts          — account lockout events; identify brute-force patterns
GET  /admin/analytics/security/suspicious-ips    — IPs with anomalously high request rates
GET  /admin/analytics/security/token-revocations — token revocation events by reason
```

---

### Report Generation

#### Report Types

| Report Code | Description | Formats | Audience |
|-------------|-------------|---------|----------|
| `IDENTITY_REQUESTS` | All identity requests in period: status, decision time, reviewer, rejection reason | PDF, Excel, CSV | SUPER_ADMIN, AUDITOR |
| `CITIZEN_ACCOUNTS` | Account counts, status distribution, LoA breakdown, geographic distribution | PDF, Excel | SUPER_ADMIN, AUDITOR |
| `SERVICE_REQUESTS` | Requests by service/ministry/status; SLA compliance; avg completion times | PDF, Excel, CSV | SERVICE_MANAGER, AUDITOR |
| `PAYMENT_SUMMARY` | Revenue by service and ministry; payment volumes; refunds | PDF, Excel | SUPER_ADMIN |
| `MINISTRY_HEALTH` | Per-ministry API uptime, latency, error rates, circuit-breaker events | PDF, Excel | MINISTRY_MANAGER |
| `AUDIT_COMPLIANCE` | Full audit event log export for compliance; admin actions; sensitive operations | PDF, CSV | AUDITOR, SUPER_ADMIN |
| `SECURITY_SUMMARY` | Failed logins, lockouts, suspicious IPs, token anomalies | PDF | SUPER_ADMIN |
| `RELYING_PARTY_USAGE` | Token issuance per RP, active users per RP, scope usage | Excel | SUPER_ADMIN |
| `PLATFORM_OVERVIEW` | Executive summary: all key metrics in one page | PDF | SUPER_ADMIN |

#### On-Demand Report Endpoints
```
POST /admin/reports/generate              — trigger report generation (async)
  body: { report_type, format, filters: { from, to, ministry_id?, service_id?, ... } }
  returns: { job_id }                     — Hangfire job ID

GET  /admin/reports/jobs/{job_id}         — check generation status: QUEUED | RUNNING | DONE | FAILED
GET  /admin/reports                       — list all generated reports (paginated)
GET  /admin/reports/{id}                  — report metadata
GET  /admin/reports/{id}/download         — stream the file (presigned MinIO URL redirect)
DELETE /admin/reports/{id}               — delete generated report file
```

#### Scheduled Report Endpoints
```
GET    /admin/reports/schedules                   — list all scheduled report configs
POST   /admin/reports/schedules                   — create schedule
  body: {
    report_type, format,
    cron_expression,          — e.g. "0 8 * * 1" (every Monday at 8am)
    filters,
    recipients: [             — admin user IDs to email the report to
      { admin_user_id, include_inline: false }
    ]
  }
PUT    /admin/reports/schedules/{id}              — update schedule
DELETE /admin/reports/schedules/{id}              — delete schedule
PATCH  /admin/reports/schedules/{id}/toggle       — enable/disable without deleting
POST   /admin/reports/schedules/{id}/run-now      — trigger immediately (test run)
```

#### Report Generation Flow

```
1. Admin calls POST /admin/reports/generate
2. Admin API creates report_jobs record (status=QUEUED)
3. Hangfire picks up job
4. Job queries admin_db (analytics read model) for data
5. Job calls appropriate generator:
   • PdfReportGenerator → QuestPDF document template → byte[]
   • ExcelReportGenerator → ClosedXML workbook → byte[]
   • CsvReportGenerator → IAsyncEnumerable rows → stream
6. Generated file uploaded to MinIO (reports/ bucket; 30-day retention)
7. report_jobs status = DONE; stores MinIO object key
8. If scheduled: Notification Service sends email to recipients with download link
9. Admin polls GET /admin/reports/jobs/{job_id} or receives webhook notification
```

---

### Analytics Read Model (admin_db)

```sql
-- Populated purely by consuming domain events. Never written to by the Admin API's
-- command handlers. Separate DB user for reads vs. the consumer writer.

-- Daily identity metrics snapshot (materialized daily by consumer)
analytics_identity_daily
  date                    DATE NOT NULL
  total_registrations     INTEGER NOT NULL DEFAULT 0
  approved                INTEGER NOT NULL DEFAULT 0
  rejected                INTEGER NOT NULL DEFAULT 0
  pending_eod             INTEGER NOT NULL DEFAULT 0
  avg_approval_hours      NUMERIC(6,2)
  PRIMARY KEY (date)

-- Per-reviewer performance
analytics_reviewer_stats
  date                    DATE NOT NULL
  reviewer_id             UUID NOT NULL
  reviewed_count          INTEGER NOT NULL DEFAULT 0
  approved_count          INTEGER NOT NULL DEFAULT 0
  rejected_count          INTEGER NOT NULL DEFAULT 0
  avg_review_minutes      NUMERIC(6,2)
  PRIMARY KEY (date, reviewer_id)

-- Daily account activity
analytics_accounts_daily
  date                    DATE NOT NULL
  new_accounts            INTEGER NOT NULL DEFAULT 0
  active_logins           INTEGER NOT NULL DEFAULT 0
  failed_logins           INTEGER NOT NULL DEFAULT 0
  lockouts                INTEGER NOT NULL DEFAULT 0
  loa1_count              INTEGER NOT NULL DEFAULT 0
  loa2_count              INTEGER NOT NULL DEFAULT 0
  loa3_count              INTEGER NOT NULL DEFAULT 0
  PRIMARY KEY (date)

-- Per-ministry API call stats (daily)
analytics_ministry_calls_daily
  date                    DATE NOT NULL
  ministry_id             UUID NOT NULL
  endpoint_id             UUID NOT NULL
  total_calls             INTEGER NOT NULL DEFAULT 0
  success_calls           INTEGER NOT NULL DEFAULT 0
  error_calls             INTEGER NOT NULL DEFAULT 0
  avg_latency_ms          INTEGER
  p95_latency_ms          INTEGER
  circuit_open_events     SMALLINT NOT NULL DEFAULT 0
  PRIMARY KEY (date, ministry_id, endpoint_id)

-- Per-service request stats (daily)
analytics_service_requests_daily
  date                    DATE NOT NULL
  service_id              UUID NOT NULL
  ministry_id             UUID NOT NULL
  submitted               INTEGER NOT NULL DEFAULT 0
  completed               INTEGER NOT NULL DEFAULT 0
  rejected                INTEGER NOT NULL DEFAULT 0
  cancelled               INTEGER NOT NULL DEFAULT 0
  sla_breached            INTEGER NOT NULL DEFAULT 0
  avg_completion_hours    NUMERIC(8,2)
  PRIMARY KEY (date, service_id)

-- Payment daily summary
analytics_payments_daily
  date                    DATE NOT NULL
  service_id              UUID NOT NULL
  currency                CHAR(3) NOT NULL
  total_orders            INTEGER NOT NULL DEFAULT 0
  paid_orders             INTEGER NOT NULL DEFAULT 0
  failed_orders           INTEGER NOT NULL DEFAULT 0
  refunded_orders         INTEGER NOT NULL DEFAULT 0
  gross_amount            NUMERIC(14,2) NOT NULL DEFAULT 0
  refund_amount           NUMERIC(14,2) NOT NULL DEFAULT 0
  PRIMARY KEY (date, service_id, currency)

-- Security events daily
analytics_security_daily
  date                    DATE NOT NULL
  failed_logins           INTEGER NOT NULL DEFAULT 0
  account_lockouts        INTEGER NOT NULL DEFAULT 0
  token_revocations       INTEGER NOT NULL DEFAULT 0
  suspicious_ip_alerts    INTEGER NOT NULL DEFAULT 0
  PRIMARY KEY (date)

-- Generated reports registry
report_jobs
  id                      UUID PRIMARY KEY DEFAULT gen_random_uuid()
  report_type             VARCHAR(50) NOT NULL
  format                  VARCHAR(10) NOT NULL              -- PDF, EXCEL, CSV
  status                  VARCHAR(20) NOT NULL DEFAULT 'QUEUED'
  filters                 JSONB
  requested_by            UUID NOT NULL                     -- admin user
  hangfire_job_id         VARCHAR(100)
  minio_object_key        TEXT
  file_size_bytes         BIGINT
  row_count               INTEGER
  error_message           TEXT
  started_at              TIMESTAMPTZ
  completed_at            TIMESTAMPTZ
  expires_at              TIMESTAMPTZ                       -- file auto-deleted after this
  created_at              TIMESTAMPTZ NOT NULL DEFAULT now()

-- Scheduled report configurations
report_schedules
  id                      UUID PRIMARY KEY DEFAULT gen_random_uuid()
  report_type             VARCHAR(50) NOT NULL
  format                  VARCHAR(10) NOT NULL
  cron_expression         TEXT NOT NULL
  filters                 JSONB
  is_active               BOOLEAN NOT NULL DEFAULT TRUE
  created_by              UUID NOT NULL
  last_run_at             TIMESTAMPTZ
  next_run_at             TIMESTAMPTZ
  created_at              TIMESTAMPTZ NOT NULL DEFAULT now()

-- Scheduled report recipient list
report_schedule_recipients
  id                      UUID PRIMARY KEY DEFAULT gen_random_uuid()
  schedule_id             UUID NOT NULL REFERENCES report_schedules(id)
  admin_user_id           UUID NOT NULL
  include_inline          BOOLEAN NOT NULL DEFAULT FALSE    -- attach file vs link only

-- Admin users (owned by this service — not shared with Identity Service)
admin_users
  id                      UUID PRIMARY KEY DEFAULT gen_random_uuid()
  employee_id             VARCHAR(50) NOT NULL UNIQUE
  full_name               VARCHAR(200) NOT NULL
  email                   VARCHAR(254) NOT NULL UNIQUE
  password_hash           TEXT NOT NULL                    -- Argon2id
  totp_secret             TEXT                             -- encrypted; null until enrolled
  totp_enrolled           BOOLEAN NOT NULL DEFAULT FALSE
  role                    admin_role NOT NULL
  department              VARCHAR(100)
  status                  VARCHAR(20) NOT NULL DEFAULT 'ACTIVE'
  failed_login_count      SMALLINT NOT NULL DEFAULT 0
  locked_until            TIMESTAMPTZ
  last_login_at           TIMESTAMPTZ
  created_by              UUID
  created_at              TIMESTAMPTZ NOT NULL DEFAULT now()
  updated_at              TIMESTAMPTZ NOT NULL DEFAULT now()

CREATE TYPE admin_role AS ENUM (
  'SUPER_ADMIN', 'IDENTITY_REVIEWER', 'MINISTRY_MANAGER',
  'SERVICE_MANAGER', 'AUDITOR', 'SUPPORT'
);

-- Admin session / refresh tokens
admin_refresh_tokens
  id                      UUID PRIMARY KEY DEFAULT gen_random_uuid()
  admin_user_id           UUID NOT NULL REFERENCES admin_users(id)
  token_hash              TEXT NOT NULL
  issued_at               TIMESTAMPTZ NOT NULL DEFAULT now()
  expires_at              TIMESTAMPTZ NOT NULL
  revoked_at              TIMESTAMPTZ
  ip_address              INET
  user_agent              TEXT
```

---

### Admin Authentication Flow

```
STEP 1 — Password
  POST /admin/auth/login  { employee_id, password }
  • Lookup admin_users by employee_id
  • Verify Argon2id password hash
  • Check status = ACTIVE and not locked
  • Issue short-lived challenge_token (JWT; 3-min TTL; not an access token)
  • Return { challenge_token, totp_required: true }

STEP 2 — TOTP
  POST /admin/auth/totp/verify  { challenge_token, totp_code }
  • Validate challenge_token signature + expiry
  • Validate TOTP code (RFC 6238; 30-second window; ±1 step tolerance)
  • Issue admin access_token (JWT; 8-min TTL)
  • Issue admin refresh_token (opaque; 8-hour TTL; stored hashed)
  • Log successful login to audit

ACCESS TOKEN CLAIMS
  sub         — admin_user.id
  role        — admin_role value
  employee_id — for display
  jti         — unique token ID (for revocation check)
  
SECURITY RULES
  • access_token TTL = 8 minutes (shorter than citizen 15 min)
  • idle timeout enforced client-side (backend stateless)
  • After 5 failed password attempts: lock for 30 minutes
  • TOTP secret stored encrypted (RSA-256/OAEP); never returned via API
  • Every admin action writes to Audit Service via event
  • Destructive actions (suspend account, reject request) require
    X-Admin-Reason header — logged verbatim in audit event
```

---

### NuGet Packages (Admin API specific)

```xml
<PackageReference Include="QuestPDF" />                    <!-- PDF generation -->
<PackageReference Include="ClosedXML" />                   <!-- Excel generation -->
<PackageReference Include="CsvHelper" />                   <!-- CSV streaming -->
<PackageReference Include="Hangfire.AspNetCore" />          <!-- background jobs -->
<PackageReference Include="Hangfire.PostgreSql" />          <!-- Hangfire storage -->
<PackageReference Include="Otp.NET" />                     <!-- TOTP validation (RFC 6238) -->
<PackageReference Include="MassTransit.RabbitMQ" />         <!-- event consumers for analytics -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
<PackageReference Include="MediatR" />
<PackageReference Include="FluentValidation.AspNetCore" />
<PackageReference Include="Polly.Extensions.Http" />        <!-- resilient service clients -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
<PackageReference Include="Mapster" />
<PackageReference Include="Serilog.AspNetCore" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" />
```

---

---

## 24. Graduation Project Implementation Guide

> **This is the section you implement.** Sections 1–23 are the production vision and full design reference. This section tells you exactly what to build, how to structure it, what to skip, and in what order — for a graduation project with ~1,000 test users.

---

### Implementation Strategy: Modular Monolith

One ASP.NET Core 9 application. One PostgreSQL database with one schema per module. Modules are internally structured like microservices (Clean Architecture, CQRS, domain events via MediatR) but deployed as a single process.

**Why this is the right choice for your situation:**
- Full microservices = 6+ months just on infrastructure. You would never finish the features.
- Modular monolith = all the architectural concepts demonstrated, all features working, runs on a laptop.
- The architecture is designed so each module can be extracted into a real microservice later without rewriting business logic — just changing how they communicate.

---

### Solution Structure

```
Sheba.sln
│
├── src/
│   ├── Sheba.Api/                          ← Single ASP.NET Core 9 entry point
│   │   ├── Program.cs                      ← registers all modules, middleware, OpenIddict
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   └── Dockerfile
│   │
│   ├── Sheba.Shared.Kernel/                ← No dependencies on other modules
│   │   ├── Entities/
│   │   │   └── BaseEntity.cs               ← Id, CreatedAt, UpdatedAt
│   │   ├── Events/
│   │   │   └── IDomainEvent.cs             ← marker interface (INotification for MediatR)
│   │   ├── Interfaces/
│   │   │   ├── IRepository.cs
│   │   │   └── IUnitOfWork.cs
│   │   ├── ValueObjects/
│   │   │   ├── NationalId.cs
│   │   │   ├── PhoneNumber.cs
│   │   │   └── Email.cs
│   │   └── Exceptions/
│   │       ├── DomainException.cs
│   │       ├── NotFoundException.cs
│   │       └── ValidationException.cs
│   │
│   └── Modules/
│       │
│       ├── Identity/                       ← AUTH, OIDC, registration, login, admin approval
│       │   ├── Sheba.Identity.Domain/
│       │   │   ├── Entities/
│       │   │   │   ├── Account.cs
│       │   │   │   ├── IdentityRequest.cs
│       │   │   │   └── OtpRecord.cs
│       │   │   ├── Enums/
│       │   │   │   ├── AccountStatus.cs
│       │   │   │   └── RequestStatus.cs
│       │   │   ├── DomainEvents/
│       │   │   │   ├── AccountRegisteredEvent.cs
│       │   │   │   ├── IdentityRequestSubmittedEvent.cs
│       │   │   │   └── IdentityRequestDecidedEvent.cs
│       │   │   └── Interfaces/
│       │   │       ├── INationalIdProvider.cs
│       │   │       └── IOtpProvider.cs
│       │   ├── Sheba.Identity.Application/
│       │   │   ├── Commands/
│       │   │   │   ├── RegisterCitizen/
│       │   │   │   ├── VerifyOtp/
│       │   │   │   ├── CompleteRegistration/
│       │   │   │   ├── ApproveIdentityRequest/
│       │   │   │   ├── RejectIdentityRequest/
│       │   │   │   └── LoginCitizen/
│       │   │   ├── Queries/
│       │   │   │   ├── GetIdentityRequests/
│       │   │   │   └── GetAccountById/
│       │   │   └── EventHandlers/
│       │   │       ├── SendApprovalEmailHandler.cs
│       │   │       └── SendRejectionEmailHandler.cs
│       │   └── Sheba.Identity.Infrastructure/
│       │       ├── Persistence/
│       │       │   ├── IdentityDbContext.cs
│       │       │   └── Repositories/
│       │       ├── Adapters/
│       │       │   ├── MockNationalIdProvider.cs
│       │       │   ├── HttpNationalIdProvider.cs
│       │       │   ├── ConsoleOtpProvider.cs
│       │       │   └── TwilioOtpProvider.cs
│       │       └── IdentityModule.cs       ← IServiceCollection extension; registers everything
│       │
│       ├── Citizen/                        ← Citizen profiles, KYC data
│       │   ├── Sheba.Citizen.Domain/
│       │   ├── Sheba.Citizen.Application/
│       │   └── Sheba.Citizen.Infrastructure/
│       │       └── CitizenModule.cs
│       │
│       ├── Ministry/                       ← Ministry registry, auth configs, endpoints, webhooks
│       │   ├── Sheba.Ministry.Domain/
│       │   │   ├── Entities/
│       │   │   │   ├── Ministry.cs
│       │   │   │   ├── MinistryAuthConfig.cs
│       │   │   │   ├── MinistryAuthCredential.cs
│       │   │   │   ├── MinistryEndpoint.cs
│       │   │   │   └── MinistryWebhook.cs
│       │   │   └── Interfaces/
│       │   │       └── IMinistryAuthAdapter.cs
│       │   ├── Sheba.Ministry.Application/
│       │   └── Sheba.Ministry.Infrastructure/
│       │       ├── Adapters/
│       │       │   ├── OidcMinistryAuthAdapter.cs
│       │       │   ├── ApiKeyMinistryAuthAdapter.cs
│       │       │   ├── BasicAuthMinistryAuthAdapter.cs
│       │       │   ├── BearerTokenMinistryAuthAdapter.cs
│       │       │   └── OAuth2MinistryAuthAdapter.cs
│       │       └── MinistryModule.cs
│       │
│       ├── ServiceRequest/                 ← Service catalog, citizen requests, workflow
│       │   ├── Sheba.ServiceRequest.Domain/
│       │   ├── Sheba.ServiceRequest.Application/
│       │   └── Sheba.ServiceRequest.Infrastructure/
│       │       └── ServiceRequestModule.cs
│       │
│       ├── Document/                       ← File upload, MinIO, presigned URLs
│       │   ├── Sheba.Document.Domain/
│       │   ├── Sheba.Document.Application/
│       │   └── Sheba.Document.Infrastructure/
│       │       └── DocumentModule.cs
│       │
│       ├── Wallet/                         ← W3C Verifiable Credentials, DID
│       │   ├── Sheba.Wallet.Domain/
│       │   ├── Sheba.Wallet.Application/
│       │   └── Sheba.Wallet.Infrastructure/
│       │       └── WalletModule.cs
│       │
│       ├── Notification/                   ← Email, SMS, in-app
│       │   ├── Sheba.Notification.Domain/
│       │   ├── Sheba.Notification.Application/
│       │   └── Sheba.Notification.Infrastructure/
│       │       ├── Adapters/
│       │       │   ├── SmtpEmailProvider.cs
│       │       │   ├── MailhogEmailProvider.cs  ← dev
│       │       │   ├── ConsoleOtpProvider.cs    ← dev SMS
│       │       │   └── TwilioSmsProvider.cs
│       │       └── NotificationModule.cs
│       │
│       ├── Payment/                        ← Fee tracking, full payment gateway adapter
│       │   ├── Sheba.Payment.Domain/
│       │   ├── Sheba.Payment.Application/
│       │   └── Sheba.Payment.Infrastructure/
│       │       └── PaymentModule.cs
│       │
│       ├── Audit/                          ← Append-only event log
│       │   ├── Sheba.Audit.Domain/
│       │   ├── Sheba.Audit.Application/
│       │   └── Sheba.Audit.Infrastructure/
│       │       └── AuditModule.cs
│       │
│       └── Admin/                          ← Admin API, analytics, reports
│           ├── Sheba.Admin.Domain/
│           ├── Sheba.Admin.Application/
│           │   ├── Analytics/
│           │   └── Reports/
│           └── Sheba.Admin.Infrastructure/
│               ├── Reports/
│               │   ├── PdfReportGenerator.cs
│               │   ├── ExcelReportGenerator.cs
│               │   └── CsvReportGenerator.cs
│               └── AdminModule.cs
│
└── tests/
    ├── Sheba.Identity.Tests/
    ├── Sheba.Ministry.Tests/
    ├── Sheba.ServiceRequest.Tests/
    └── Sheba.Integration.Tests/
```

---

### One Database, One Schema Per Module

```sql
-- All modules share one PostgreSQL instance
-- Each module gets its own schema — no cross-schema JOINs allowed

CREATE SCHEMA identity;      -- accounts, identity_requests, otp_records, relying_parties
CREATE SCHEMA citizen;       -- citizen_profiles, kyc_documents
CREATE SCHEMA ministry;      -- ministries, auth_configs, credentials, endpoints, webhooks
CREATE SCHEMA service_req;   -- services, categories, requests, workflow_steps
CREATE SCHEMA document;      -- documents, access_grants
CREATE SCHEMA wallet;        -- did_documents, verifiable_credentials
CREATE SCHEMA payment;       -- payment_orders, transactions
CREATE SCHEMA notification;  -- templates, notifications
CREATE SCHEMA audit;         -- audit_events (append-only)
CREATE SCHEMA admin_data;    -- analytics snapshots, report_jobs, admin_users

-- Each module's DbContext only maps to its own schema:
-- IdentityDbContext   → search_path = identity
-- CitizenDbContext    → search_path = citizen
-- MinistryDbContext   → search_path = ministry
-- ... etc.

-- Cross-module "foreign keys" are just UUID columns — no DB-level FK constraints.
-- Referential integrity is enforced in application code, not the database.
```

---

### Module Communication Rules

```
RULE: A module MUST NOT import another module's DbContext or Entity classes.

Allowed cross-module communication:
  1. In-process domain events via MediatR (INotification + INotificationHandler)
     Example: Identity publishes IdentityRequestApprovedEvent
              Notification module handles it → sends approval email

  2. Direct service interface injection (for simple queries)
     Example: ServiceRequest module needs citizen name
              → ICitizenQueryService interface defined in Sheba.Shared.Kernel
              → Citizen module provides the implementation
              → ServiceRequest module injects ICitizenQueryService (not CitizenDbContext)

NOT allowed:
  ✗ MinistryDbContext injected into ServiceRequestModule
  ✗ Direct SQL across schemas from application code
  ✗ Shared Entity classes between modules
```

---

### Module = Full Microservice Boundary

> **The modular monolith changes the deployment topology, not the feature set.**
>
> Each module in the monolith is the **complete, production-equivalent implementation** of its corresponding microservice. Same entities, same database schema, same endpoints, same business logic. The only difference is it runs in the same process instead of a separate one.

When implementing any module, **Sections 3–23 of this document are your exact specification.** Read the relevant section for a module before implementing it. Do not cut any endpoint, entity, or feature — if it is in the spec, it is in the module.

| Module | Spec Section(s) | DB Schema | All Endpoint Groups |
|--------|----------------|-----------|---------------------|
| **Identity** | §3 (IAM), §5 (OIDC) | `identity.*` | `/api/identity/*`, `/connect/*`, `/.well-known/*` |
| **Citizen** | §6 (Citizen Profile) | `citizen.*` | `/api/citizen/*` |
| **Ministry** | §7 (Ministry Integration Hub) | `ministry.*` | `/api/ministry/*`, `/api/admin/relying-parties` |
| **ServiceRequest** | §8 (Service Portal), §9 (Workflow Engine) | `service_req.*` | `/api/services/*`, `/api/services/requests/*`, `/api/webhooks/*` |
| **Payment** | §14 (Payment Service) | `payment.*` | `/api/payments/*` — orders, providers, transactions, refunds |
| **Document** | §10 (Document Management) | `document.*` | `/api/documents/*` |
| **Wallet** | §11 (Digital Wallet), §12 (DID) | `wallet.*` | `/api/wallet/*` |
| **Notification** | §12 (Notification Service) | `notification.*` | `/api/notifications/*` (admin) |
| **Audit** | §15 (Audit Service) | `audit.*` | `/api/admin/audit/*` |
| **Admin** | §13 (Admin BFF), §17 (Analytics) | `admin_data.*` | `/admin/*`, `/api/admin/analytics/*`, `/api/admin/reports/*` |

---

### What to Keep vs. What Changes

The graduation version implements **every feature fully** — nothing is mocked or simplified except the deployment topology (modular monolith instead of separate processes) and infrastructure scale (single-node instead of clustered).

| Component | Full Production | Graduation Version |
|-----------|----------------|-------------------|
| **Architecture** | True microservices (10 processes) | Modular monolith (1 process, 10 modules) ✅ |
| **Database** | 1 shared PostgreSQL cluster, 10 schemas | 1 PostgreSQL, 10 schemas — **identical** ✅ |
| **Messaging** | RabbitMQ + MassTransit | MediatR in-process events ✅ |
| **OpenIddict** | Full OAuth 2.1 + OIDC | Full OAuth 2.1 + OIDC — **identical** ✅ |
| **Redis** | Redis Cluster (3 nodes) | Redis single instance ✅ |
| **MinIO** | MinIO distributed (4 nodes) | MinIO single instance ✅ |
| **Hangfire** | Hangfire + PostgreSql storage | Hangfire + PostgreSql storage — **identical** ✅ |
| **OTP** | Twilio / local telco | Console adapter (dev) + Twilio wired and ready ✅ |
| **NID Adapter** | Real civil registry HTTP adapter | Mock provider (dev) + HTTP adapter ready ✅ |
| **Payment** | Full gateway adapter + provider interface | Full gateway adapter — mock provider implements real interface ✅ |
| **W3C VC** | Full JWT-VC issuance + RSA signing | Full JWT-VC issuance + RSA signing — **identical** ✅ |
| **Credential Encryption** | RSA-256 (OAEP) | RSA-256 (OAEP) — **identical** ✅ |
| **API Docs** | Swagger UI | Swagger UI — **identical** ✅ |
| **API Gateway** | YARP reverse proxy | ASP.NET Core routing (built-in) ✅ |
| **Observability** | OpenTelemetry + Jaeger + Grafana | Seq (structured logs, 1 container) ✅ |
| **mTLS** | Between all services (Istio) | Not needed (single process) ✅ |
| **Kubernetes** | Full cluster + Helm charts | Docker Compose ✅ |
| **SAML** | Ministry auth adapter | Skip — OIDC/API Key/Basic/Bearer covers all demo cases ✅ |

---

### Docker Compose Setup

```yaml
# docker-compose.yml

version: "3.9"

services:

  sheba-api:
    build:
      context: .
      dockerfile: src/Sheba.Api/Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Default=Host=postgres;Port=5432;Database=sheba;Username=sheba;Password=sheba_dev
      - Redis__ConnectionString=redis:6379
      - Minio__Endpoint=minio:9000
      - Minio__AccessKey=minioadmin
      - Minio__SecretKey=minioadmin
      - Mail__Host=mailhog
      - Mail__Port=1025
      - Seq__ServerUrl=http://seq:5341
      - NationalId__ActiveProvider=Mock
      - Otp__ActiveProvider=Console
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_started
      minio:
        condition: service_started

  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: sheba
      POSTGRES_USER: sheba
      POSTGRES_PASSWORD: sheba_dev
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U sheba"]
      interval: 5s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data

  minio:
    image: minio/minio:latest
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: minioadmin
    ports:
      - "9000:9000"   # API
      - "9001:9001"   # Console UI
    volumes:
      - minio_data:/data

  mailhog:
    image: mailhog/mailhog:latest
    ports:
      - "1025:1025"   # SMTP
      - "8025:8025"   # Web UI — view sent emails at http://localhost:8025

  seq:
    image: datalust/seq:latest
    environment:
      ACCEPT_EULA: Y
    ports:
      - "5341:5341"   # Ingestion
      - "8081:80"     # Web UI — view logs at http://localhost:8081

volumes:
  postgres_data:
  redis_data:
  minio_data:
```

**6 containers total. Runs on any laptop with Docker Desktop.**

Local URLs when running:
```
Sheba API          http://localhost:5000
OIDC Discovery     http://localhost:5000/.well-known/openid-configuration
Admin API          http://localhost:5000/api/admin/...
Mailhog (emails)   http://localhost:8025
Seq (logs)         http://localhost:8081
MinIO Console      http://localhost:9001
```

---

### Mock Civil Registry — Test Citizens

Seed these into `identity.mock_citizens` on startup (only in Development environment):

```csharp
// Seeded automatically by IdentityModule on app startup in Development
// Use these national IDs to test all registration scenarios

| National ID  | Name (EN)           | Phone        | Scenario                    |
|--------------|---------------------|-------------|------------------------------|
| 1000000001   | Ahmed Al-Yemeni     | 0777000001  | ✅ Valid — happy path         |
| 1000000002   | Fatima Al-Sana'a    | 0777000002  | ✅ Valid — second test user   |
| 1000000003   | Omar Al-Hadhrami    | 0777000003  | ✅ Valid — for LoA2 testing   |
| 1000000004   | Sara Al-Aden        | 0777000004  | ✅ Valid — for service tests  |
| 1000000099   | Deceased Citizen    | 0777000099  | ❌ Deceased — test rejection  |
| 1000000098   | Suspended Citizen   | 0777000098  | ❌ Suspended — test rejection |
| 1000000097   | Expired NID         | 0777000097  | ❌ Expired NID — test rejection|
| 1000000096   | Phone Mismatch Test | 0777000001  | ❌ Wrong phone registered     |
```

---

### Program.cs — Module Registration Pattern

```csharp
// src/Sheba.Api/Program.cs

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]!));

// Register all modules (each module registers its own DbContext, handlers, adapters)
builder.Services
    .AddIdentityModule(builder.Configuration)
    .AddCitizenModule(builder.Configuration)
    .AddMinistryModule(builder.Configuration)
    .AddServiceRequestModule(builder.Configuration)
    .AddDocumentModule(builder.Configuration)
    .AddWalletModule(builder.Configuration)
    .AddPaymentModule(builder.Configuration)
    .AddNotificationModule(builder.Configuration)
    .AddAuditModule(builder.Configuration)
    .AddAdminModule(builder.Configuration);

// MediatR — discovers handlers across all module assemblies
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
    typeof(IdentityModule).Assembly,
    typeof(CitizenModule).Assembly,
    typeof(MinistryModule).Assembly,
    typeof(ServiceRequestModule).Assembly,
    typeof(DocumentModule).Assembly,
    typeof(WalletModule).Assembly,
    typeof(PaymentModule).Assembly,
    typeof(NotificationModule).Assembly,
    typeof(AuditModule).Assembly,
    typeof(AdminModule).Assembly
));

// Pipeline behaviors (order matters)
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

// OpenIddict (registered inside IdentityModule.cs, exposed here for clarity)
// Hangfire (registered inside AdminModule.cs)
// Serilog + Seq sink

var app = builder.Build();

// Run all module migrations on startup
await app.MigrateAllModulesAsync();

// Route groups per module
app.MapIdentityEndpoints();      // /api/identity/..., /connect/...
app.MapCitizenEndpoints();       // /api/citizen/...
app.MapMinistryEndpoints();      // /api/ministry/...
app.MapServiceRequestEndpoints();// /api/services/...
app.MapDocumentEndpoints();      // /api/documents/...
app.MapWalletEndpoints();        // /api/wallet/...
app.MapPaymentEndpoints();       // /api/payments/...
app.MapNotificationEndpoints();  // /api/notifications/...
app.MapAdminEndpoints();         // /api/admin/...
app.MapAuditEndpoints();         // /api/audit/...

app.Run();
```

---

### Module Registration Pattern (example: IdentityModule)

```csharp
// Modules/Identity/Sheba.Identity.Infrastructure/IdentityModule.cs

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext — scoped to identity schema
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", "identity")
            ));

        // Repositories
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IIdentityRequestRepository, IdentityRequestRepository>();

        // NID adapter — switch via config
        var nidProvider = configuration["NationalId:ActiveProvider"];
        if (nidProvider == "Mock")
            services.AddScoped<INationalIdProvider, MockNationalIdProvider>();
        else
            services.AddScoped<INationalIdProvider, HttpNationalIdProvider>();

        // OTP adapter — switch via config
        var otpProvider = configuration["Otp:ActiveProvider"];
        if (otpProvider == "Console")
            services.AddScoped<IOtpProvider, ConsoleOtpProvider>();
        else
            services.AddScoped<IOtpProvider, TwilioOtpProvider>();

        // OpenIddict
        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore()
                .UseDbContext<IdentityDbContext>())
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize")
                       .SetTokenEndpointUris("/connect/token")
                       .SetUserinfoEndpointUris("/connect/userinfo")
                       .SetIntrospectionEndpointUris("/connect/introspect")
                       .SetRevocationEndpointUris("/connect/revoke")
                       .SetEndSessionEndpointUris("/connect/endsession");

                options.AllowAuthorizationCodeFlow()
                       .RequireProofKeyForCodeExchange();
                options.AllowClientCredentialsFlow(); // ministries
                options.AllowRefreshTokenFlow();

                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();

                options.UseAspNetCore()
                       .EnableAuthorizationEndpointPassthrough()
                       .EnableTokenEndpointPassthrough()
                       .EnableUserinfoEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }
}
```

---

### Graduation Build Order (Recommended)

Work in this order — each phase delivers something you can demo:

```
WEEK 1–2 — Foundation
  ✅ Solution structure + Shared.Kernel
  ✅ Docker Compose up and running
  ✅ IdentityDbContext + schema migrations
  ✅ Mock NID provider + test citizens seeded
  ✅ Serilog → Seq logging working

WEEK 3–4 — Identity Core (most important)
  ✅ OpenIddict installed and OIDC discovery endpoint live
  ✅ Admin user seeded (employee_id: ADMIN001 / password: Admin@123)
  ✅ Citizen registration: NID check → OTP → email → admin queue
  ✅ Admin approval: approve/reject + email notifications via Mailhog
  ✅ Citizen login: NID/username + password → OTP → JWT issued
  ✅ DEMO: full registration + login + OTP flow working end-to-end

WEEK 5 — Ministry Module
  ✅ Ministry CRUD + sub-ministry tree
  ✅ Auth config: OIDC + API Key adapters (most common)
  ✅ Test connection endpoint
  ✅ Endpoint registry
  ✅ Relying Party registration (admin creates RP → returns client_id/secret)
  ✅ DEMO: add Ministry of Interior with API Key auth, register its SSO RP

WEEK 6 — Service Catalog
  ✅ Service categories + 3–5 demo services
  ✅ JSON Schema form definition per service
  ✅ Workflow steps (at least: SUBMIT → PAYMENT → MINISTRY_CALL → COMPLETE)
  ✅ Fee schedule per service

WEEK 7 — Service Requests
  ✅ Citizen submits request → form data stored → step engine runs
  ✅ Ministry endpoint called (use a public mock API or httpbin.org for demo)
  ✅ Webhook receiver (POST endpoint ministry calls back)
  ✅ Payment: create order → mock-pay → mark paid → continue workflow
  ✅ DEMO: citizen submits passport application → system calls ministry API → completes

WEEK 8 — Document + Wallet
  ✅ Document upload → MinIO → presigned download URL
  ✅ W3C VC: issue "Digital Identity Credential" JWT after account approved
  ✅ Wallet: citizen can view their credentials via API

WEEK 9 — Admin Analytics + Reports
  ✅ Analytics read model: daily snapshots populated by MediatR event handlers
  ✅ KPI endpoint: total accounts, pending requests, today's completions
  ✅ Trend endpoint: registrations per day (last 30 days) — for charts
  ✅ PDF report: Identity Requests Report (QuestPDF)
  ✅ Excel report: Service Requests Summary (ClosedXML)
  ✅ Scheduled report: Hangfire job runs every Monday, emails PDF to admin
  ✅ DEMO: admin triggers report → downloads PDF → shows data

WEEK 10 — Polish + Demo Prep
  ✅ Audit log: all events recorded, searchable via admin API
  ✅ Postman collection covering all flows
  ✅ README with setup instructions
  ✅ Seed 5–10 test citizens and 3 demo services
  ✅ SSO demo: build a tiny sample RP app that logs in via Sheba OIDC
      (a plain HTML page + 50 lines of JS is enough to impress examiners)
```

---

### What Impresses Examiners Most

These are the things that will make your committee stop and take notes:

1. **OIDC discovery endpoint live** — open `/.well-known/openid-configuration` in a browser and show the full JSON. Most students have never built a real OIDC server. This alone is impressive.

2. **The SSO demo** — open Ministry Portal (your tiny sample RP), click "Login with Sheba", get redirected to Sheba's auth, log in as a citizen, get redirected back with an access token. This is what UAE Pass does. Showing it live is powerful.

3. **Admin approval workflow end-to-end** — register as a citizen, check Mailhog for the admin notification email, approve in the admin API, check Mailhog for the citizen approval email. The whole loop visible.

4. **Ministry auth adapters** — show the admin adding a ministry with OIDC credentials, clicking "Test Connection", and getting back `{ status: connected, latency_ms: 142 }`. Then switch to API Key and test again.

5. **PDF report download** — admin triggers an Identity Requests Report, it generates in the background (Hangfire), admin downloads the PDF. Real, formatted, data-driven.

6. **Verifiable Credential** — after account approval, call `GET /api/wallet/credentials` and show the JWT-VC returned. Paste it into jwt.io and show the claims.

---

### NuGet Packages — Graduation Version

```xml
<!-- API Docs -->
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.*" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.*" />   <!-- Swagger UI -->

<!-- OpenIddict — OIDC server -->
<PackageReference Include="OpenIddict.AspNetCore" Version="5.*" />
<PackageReference Include="OpenIddict.EntityFrameworkCore" Version="5.*" />

<!-- CQRS -->
<PackageReference Include="MediatR" Version="12.*" />
<PackageReference Include="FluentValidation.AspNetCore" Version="11.*" />

<!-- Database -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.*" />
<PackageReference Include="EFCore.NamingConventions" Version="9.*" />  <!-- snake_case columns -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.*" />

<!-- Cache & Session -->
<PackageReference Include="StackExchange.Redis" Version="2.*" />
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="9.*" />

<!-- File Storage -->
<PackageReference Include="Minio" Version="6.*" />

<!-- Background Jobs -->
<PackageReference Include="Hangfire.AspNetCore" Version="1.*" />
<PackageReference Include="Hangfire.PostgreSql" Version="1.*" />

<!-- Report Generation -->
<PackageReference Include="QuestPDF" Version="2024.*" />              <!-- PDF -->
<PackageReference Include="ClosedXML" Version="0.*" />                <!-- Excel -->
<PackageReference Include="CsvHelper" Version="33.*" />               <!-- CSV -->

<!-- Security — Passwords -->
<PackageReference Include="Isopoh.Cryptography.Argon2" Version="2.*" />  <!-- Argon2id password hash -->
<PackageReference Include="Otp.NET" Version="1.*" />                      <!-- TOTP for admin 2FA -->

<!-- Security — RSA-256 Encryption (built into .NET, no extra package needed) -->
<!-- System.Security.Cryptography.RSA is in the BCL (netstandard2.1+) -->
<!-- For X.509 cert management in dev: -->
<PackageReference Include="Microsoft.AspNetCore.DataProtection" Version="9.*" />

<!-- JSON Schema Validation (service forms) -->
<PackageReference Include="JsonSchema.Net" Version="7.*" />

<!-- HTTP Resilience (ministry calls) -->
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />

<!-- Logging -->
<PackageReference Include="Serilog.AspNetCore" Version="8.*" />
<PackageReference Include="Serilog.Sinks.Seq" Version="8.*" />

<!-- Mapping -->
<PackageReference Include="Mapster" Version="7.*" />

<!-- API Versioning -->
<PackageReference Include="Asp.Versioning.Http" Version="8.*" />

<!-- Notifications -->
<PackageReference Include="MailKit" Version="4.*" />                  <!-- SMTP email -->
<PackageReference Include="Twilio" Version="7.*" />                   <!-- SMS -->
```

---

## Appendix A — Recommended NuGet Packages

```xml
<!-- Per service -->
<PackageReference Include="OpenIddict.AspNetCore" />           <!-- Identity only -->
<PackageReference Include="OpenIddict.EntityFrameworkCore" />   <!-- Identity only -->
<PackageReference Include="MediatR" />
<PackageReference Include="FluentValidation.AspNetCore" />
<PackageReference Include="MassTransit.RabbitMQ" />
<PackageReference Include="Microsoft.EntityFrameworkCore" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
<PackageReference Include="StackExchange.Redis" />
<PackageReference Include="Minio" />
<PackageReference Include="Hangfire.AspNetCore" />
<PackageReference Include="Hangfire.PostgreSql" />
<PackageReference Include="Polly.Extensions.Http" />
<PackageReference Include="Serilog.AspNetCore" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" />
<PackageReference Include="OpenTelemetry.Exporter.Jaeger" />
<PackageReference Include="prometheus-net.AspNetCore" />
<PackageReference Include="Isopoh.Cryptography.Argon2" />    <!-- Argon2id passwords -->
<PackageReference Include="Microsoft.IdentityModel.JsonWebTokens" />
<PackageReference Include="Swashbuckle.AspNetCore" />         <!-- Swagger UI -->
<PackageReference Include="Asp.Versioning.Mvc" />
<PackageReference Include="Mapster" />
<PackageReference Include="JsonSchema.Net" />                 <!-- service form validation -->
<PackageReference Include="Quartz.AspNetCore" />              <!-- alternative to Hangfire -->
```

---

## Appendix B — Key Design Decisions (ADRs)

| # | Decision | Rationale |
|---|---------|-----------|
| ADR-001 | OpenIddict over IdentityServer | IdentityServer requires a commercial license for production; OpenIddict is MIT-licensed, actively maintained, fully supports OAuth 2.1 + OIDC Core 1.0 |
| ADR-002 | **Modular monolith now; microservices later** | Graduation project has ~1,000 test users and a single developer. Full microservices = 90% infrastructure work, 10% features. Modular monolith delivers all architecture concepts with module boundaries that map 1:1 to future microservices when needed |
| ADR-003 | **One PostgreSQL, one schema per module — all environments** | A single shared PostgreSQL cluster with per-module schemas provides bounded context isolation without the operational cost of 10 separate database servers. Schema-level isolation enforced in application code (no cross-DbContext access). Applies in dev, staging, and production. When microservices are extracted later, schemas can be moved to dedicated servers without changing application code |
| ADR-004 | MediatR in-process events (not RabbitMQ for graduation) | For a single-process deployment, MediatR domain events are reliable and observable via Seq logs; RabbitMQ adds broker operational complexity with no benefit at this scale; the interface contract is identical so switching later requires only changing the publisher implementation |
| ADR-005 | Outbox pattern for all events | Eliminates dual-write problem; guarantees at-least-once delivery even when in-process; same pattern works unchanged if RabbitMQ is introduced later |
| ADR-006 | Argon2id for passwords | OWASP recommended; memory-hard (resistant to GPU cracking); Argon2id (hybrid) preferred over Argon2i/2d for general use. RSA-256 is for data encryption and JWT signing — NOT for passwords |
| ADR-007 | Pluggable adapters via interface | Civil registry systems differ; OTP providers differ; ministerial APIs differ — Strategy pattern allows swapping providers without touching business logic; config-driven selection (Mock in dev, real in prod) |
| ADR-008 | MediatR step-based workflow engine (not Saga for graduation) | Sagas (MassTransit) add broker dependency and distributed state complexity. For graduation, a step-execution table + MediatR command per step gives durable, resumable workflows without that overhead; the step model maps directly to a saga when migrating |
| ADR-009 | Append-only audit schema | Compliance requirement; prevents tampered evidence; enforced by granting only INSERT to the audit schema's DB user — even the application cannot UPDATE or DELETE audit rows |
| ADR-010 | JWT access tokens (15-min TTL) + opaque refresh | Short access token TTL limits breach exposure; opaque refresh enables server-side revocation; family-based rotation detects token theft |
| ADR-011 | **RSA-256 (OAEP) for credential and sensitive field encryption** | Ministry credentials, TOTP secrets, and sensitive PII fields are encrypted using RSA-2048 with OAEP padding (SHA-256). RSA-256 allows key-based encryption without a shared secret; the public key encrypts, only the private key decrypts. In dev, a self-signed RSA certificate is generated on startup. In production, the private key lives in Vault/HSM — the application never holds it directly |
| ADR-012 | RS256 (RSA + SHA-256) for JWT signing | OpenIddict signs all JWTs (access tokens, id tokens, VCs) with an RSA-2048 key using the RS256 algorithm. RSA signatures are asymmetric — any party can verify the token using the public JWKS endpoint without needing the private key |
| ADR-013 | JSON Schema for service forms | Dynamic services without code changes; validated server-side with JsonSchema.Net; versioned per service; decouples form definition from application code |
| ADR-014 | Admin analytics read model (separate schema, event-sourced) | Analytics queries must never hit production write tables; daily snapshot tables in `admin_data` schema are populated by MediatR event handlers asynchronously; reporting is always fast regardless of transaction volume |
| ADR-015 | QuestPDF for report generation | Open-source (community license free); fluent C# API; produces professional PDF layouts; no external dependencies or runtime license servers unlike SSRS or Crystal Reports |
| ADR-016 | **Swagger UI (Swashbuckle) over Scalar** | Swagger UI is the industry standard, universally recognised by examiners and API consumers; Swashbuckle.AspNetCore integrates seamlessly with ASP.NET Core 9 minimal APIs and supports XML doc comments, JWT bearer authentication headers, and grouped tag display per module |
| ADR-017 | Full feature implementation in modular form | Every feature (payment gateway adapter, full W3C VC issuance, full DID management, real ministry auth adapters) is implemented completely — the only graduation simplification is deployment topology (1 process, Docker Compose) not feature scope |

---

*Document version: 3.0 — July 2026*
*Prepared for Sheba e-Government Platform — Yemen (Graduation Project)*
