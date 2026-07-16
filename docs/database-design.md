# Database Design

> Extract of [sheba.md](sheba.md) §8; sheba.md wins conflicts. Diagram sources live in
> [diagrams/](diagrams/) as `.mmd` files and are rendered inline below.

## 1. Physical layout

One PostgreSQL 16 database (`sheba`), **one schema per module** — the microservice
database-per-service rule expressed as schemas so extraction is a schema move, not a redesign:

| Schema | Module | Schema | Module |
|--------|--------|--------|--------|
| `identity` | Identity | `wallet` | Wallet |
| `citizen` | Citizen | `payment` | Payment |
| `ministry` | Ministry | `notification` | Notification |
| `service_req` | ServiceRequest | `audit` | Audit |
| `document` | Document | `admin_data` | Admin |

Gateway has no schema. Conventions: UUID PKs (`gen_random_uuid()` semantics via app-side Guids),
`created_at`/`updated_at` timestamptz on every table, snake_case names, enums stored as ints,
JSON payloads as `jsonb` with `_json` suffix.

## 2. Normalization

Write model is 3NF — child tables instead of arrays/CSV for anything queried or constrained
(fees, redirect URIs, scopes, required documents, workflow steps). Justified denormalizations are
listed in [sheba.md §8.1](sheba.md#81-normalization-approach): immutable evidence snapshots,
per-service dynamic form payloads, VC claim read-copies, cross-context read copies, and the BI
read model.

## 3. Cross-context references

**IDs only — no FK constraints, joins, or queries across schemas.** Consistency across contexts is
event-driven with idempotent consumers. Inside a schema, real FKs and unique constraints apply.
Every logical reference is annotated in the ERDs below (`"logical ref to …"`).

## 4. Migrations

EF Core migrations per module, applied independently at startup (`MigrateAllModulesAsync`).
**Current gap:** only Identity has a migrations folder; the other nine contexts use an
`EnsureCreated()` fallback that cannot evolve schemas — closing this is **T-DB-1** in
[TASKS.md](../TASKS.md), scheduled before any real data exists. Rules once closed: never edit an
applied migration; squash only pre-production; `EnsureCreated` banned.

## 5. PII & encryption map

| Table / store | PII / secret | Protection & retention |
|---------------|--------------|------------------------|
| `identity.accounts` | NID, phone, names, email, password hash | Argon2id (password); NID never in tokens (SHA-256 hash claim); volume encryption T-SEC-6. Retained while account exists |
| `identity.identity_requests` | Full registry snapshot (jsonb) | Immutable evidence; purge 10 years after account closure (assumption A3) |
| `identity.otp_records` | OTP hash, IP, UA | Argon2id; purged on use/expiry (Hangfire job) |
| `identity.refresh_token_families` | Token hash, device fingerprint, IP | Hashed tokens only; rows expire with token |
| `ministry.ministry_auth_credentials` | Ministry client secrets, API keys, passwords | **AES-256-GCM** app-level encryption (nonce‖ct‖tag, base64); decrypt only in auth adapters; key-id rotation T-SEC-3 |
| `ministry.ministry_webhooks.signing_secret` | HMAC secrets | Same AES-256-GCM |
| `service_req.service_requests.form_data_json` | Citizen submissions | Ownership-policy access; column encryption T-SEC-7 |
| `document.*` + MinIO objects | KYC images/files | Presigned URLs (short TTL); soft delete; MinIO SSE T-DOC-2 |
| `audit.audit_events` snapshots | Command payloads | Sanitized (no secrets); INSERT-only + hash chain T-AUD-1/2 |
| `notification.notification_records` | Email/phone + message body | Append-only; body templates avoid embedding NIDs |
| Seq logs | — | No-PII logging rule ([coding-standards.md §7](coding-standards.md)) |

> Superseded decision: old ADR-011 prescribed RSA-256/OAEP for credential field encryption.
> RSA-OAEP is a key-wrap primitive (size-limited, unauthenticated for this use); the implemented
> **AES-256-GCM** is correct and is the standard ([sheba.md §7.1](sheba.md#71-data-model)).

## 6. ERDs per bounded context

### 6.1 Identity (`identity`) — [source](diagrams/erd-identity.mmd)

```mermaid
erDiagram
    ACCOUNTS ||--o{ IDENTITY_REQUESTS : "submits"
    ACCOUNTS ||--o{ OTP_RECORDS : "receives"
    ACCOUNTS ||--o{ REFRESH_TOKEN_FAMILIES : "holds"
    IDENTITY_REQUESTS ||--o{ IDENTITY_REQUEST_DOCUMENTS : "attaches"
    ADMIN_USERS ||--o{ IDENTITY_REQUESTS : "reviews"
    ADMIN_USERS ||--o{ RELYING_PARTIES : "registers"
    RELYING_PARTIES ||--o{ RP_REDIRECT_URIS : "allows"
    RELYING_PARTIES ||--o{ RP_SCOPES : "grants"

    ACCOUNTS {
        uuid id PK
        varchar national_id UK "from civil registry; unique"
        varchar phone_number "from civil registry"
        varchar full_name_ar "registry snapshot"
        varchar full_name_en "registry snapshot"
        varchar username UK "citizen-chosen"
        varchar email UK "citizen-chosen"
        timestamptz email_verified_at "nullable"
        timestamptz phone_verified_at "nullable"
        text password_hash "Argon2id"
        int status "AccountStatus enum"
        int identity_level "LoA 1-3"
        int failed_login_count
        timestamptz locked_until "nullable; exponential backoff"
        timestamptz last_login_at "nullable"
        timestamptz created_at
        timestamptz updated_at
    }

    IDENTITY_REQUESTS {
        uuid id PK
        uuid account_id FK
        int request_type "OpenAccount|UpgradeLoa2|UpgradeLoa3|ReopenAccount"
        int status "Pending|UnderReview|Approved|Rejected|Cancelled"
        timestamptz submitted_at
        timestamptz reviewed_at "nullable"
        uuid reviewed_by_admin_id FK "nullable"
        text rejection_reason "nullable"
        text admin_notes "nullable"
        jsonb citizen_snapshot_json "immutable registry snapshot"
    }

    IDENTITY_REQUEST_DOCUMENTS {
        uuid id PK
        uuid request_id FK
        varchar document_type "NATIONAL_ID_PHOTO|SELFIE|..."
        uuid document_service_id "logical ref to document.documents"
        timestamptz uploaded_at
    }

    ADMIN_USERS {
        uuid id PK
        varchar employee_id UK
        varchar email UK
        varchar full_name
        int role "SuperAdmin|IdentityReviewer|MinistryManager|Auditor|Support"
        varchar department "nullable"
        varchar status "ACTIVE|SUSPENDED|DEACTIVATED"
        text password_hash "Argon2id"
        text mfa_secret "encrypted TOTP secret; nullable"
        timestamptz last_login_at "nullable"
    }

    RELYING_PARTIES {
        uuid id PK
        varchar client_id UK "OpenIddict application id"
        varchar name
        varchar name_ar "nullable"
        text description "nullable"
        text logo_url "nullable"
        int client_type "Confidential|Public"
        int party_type "Ministry|Organization|Internal|Developer"
        uuid ministry_id "logical ref to ministry.ministries; nullable"
        uuid organization_id "nullable"
        varchar status "ACTIVE|INACTIVE"
        timestamptz registered_at
        uuid registered_by FK
        jsonb metadata "nullable"
    }

    RP_REDIRECT_URIS {
        uuid id PK
        uuid relying_party_id FK
        text uri
        varchar uri_type "REDIRECT|POST_LOGOUT"
    }

    RP_SCOPES {
        uuid id PK
        uuid relying_party_id FK
        varchar scope_name "unique per RP"
    }

    OTP_RECORDS {
        uuid id PK
        uuid account_id FK
        int purpose "Login|Registration|PasswordReset|..."
        int channel "Sms|Email|Totp"
        text code_hash "Argon2id; plaintext never stored"
        timestamptz expires_at "5-min TTL"
        timestamptz used_at "nullable; single-use"
        int attempt_count "max 3"
        inet ip_address "nullable"
        text user_agent "nullable"
    }

    REFRESH_TOKEN_FAMILIES {
        uuid id PK
        uuid account_id FK
        varchar client_id "RP client"
        uuid family_id UK "reuse = revoke whole family"
        text current_token_hash
        timestamptz issued_at
        timestamptz expires_at "30 days"
        timestamptz revoked_at "nullable"
        varchar revocation_reason "nullable"
        text device_fingerprint "nullable"
        inet ip_address "nullable"
    }

    SCOPE_DEFINITIONS {
        uuid id PK
        varchar name UK "openid|profile|civil_data|..."
        varchar display_name
        varchar display_name_ar "nullable"
        text description "nullable"
        text_array claims "claim names exposed"
        boolean is_system
        int requires_loa "minimum LoA"
    }

    OUTBOX_MESSAGES {
        uuid id PK
        varchar aggregate_type
        uuid aggregate_id
        varchar event_type
        jsonb payload
        timestamptz created_at
        timestamptz published_at "nullable; null = pending"
        text error "nullable"
        int retry_count
    }
```

### 6.2 Citizen (`citizen`) — [source](diagrams/erd-citizen.mmd)

```mermaid
erDiagram
    CITIZEN_PROFILES {
        uuid id PK
        uuid account_id UK "logical 1:1 ref to identity.accounts"
        varchar national_id "denormalized read copy"
        varchar full_name_ar
        varchar full_name_en
        varchar email "nullable; profile contact"
        varchar phone_number "nullable"
        date date_of_birth "nullable"
        text address "nullable"
        varchar city "nullable"
        varchar governorate "nullable"
        timestamptz created_at
        timestamptz updated_at
    }
```

### 6.3 Ministry (`ministry`) — [source](diagrams/erd-ministry.mmd)

```mermaid
erDiagram
    MINISTRIES ||--o{ MINISTRIES : "parent of (sub-ministries)"
    MINISTRIES ||--o{ MINISTRY_AUTH_CONFIGS : "authenticates via"
    MINISTRIES ||--o{ MINISTRY_ENDPOINTS : "exposes"
    MINISTRIES ||--o{ MINISTRY_WEBHOOKS : "calls back via"
    MINISTRY_AUTH_CONFIGS ||--o| MINISTRY_AUTH_CREDENTIALS : "secured by"
    MINISTRY_AUTH_CONFIGS ||--o{ MINISTRY_ENDPOINTS : "used by"

    MINISTRIES {
        uuid id PK
        uuid parent_ministry_id FK "null = top-level"
        varchar code UK "MOI|MOJ|MOH..."
        varchar name_ar
        varchar name_en
        text description_ar "nullable"
        text description_en "nullable"
        text logo_url "nullable"
        text website_url "nullable"
        varchar contact_email "nullable"
        varchar contact_phone "nullable"
        text address_ar "nullable"
        text address_en "nullable"
        boolean is_active
        int depth_level "0=ministry 1=dept 2=division"
        int display_order
        jsonb metadata_json "nullable"
    }

    MINISTRY_AUTH_CONFIGS {
        uuid id PK
        uuid ministry_id FK
        varchar name "e.g. Civil Registry Production"
        int auth_type "Oidc|OAuth2|ApiKey|BearerToken|BasicAuth|Saml|Custom|None"
        text base_url
        boolean is_active
        boolean is_default
        text health_check_path "nullable"
        int timeout_seconds "default 30"
        int retry_count "default 3"
    }

    MINISTRY_AUTH_CREDENTIALS {
        uuid id PK
        uuid auth_config_id FK "1:1"
        text oidc_token_endpoint "nullable"
        text oidc_client_id "encrypted; nullable"
        text oidc_client_secret "encrypted; nullable"
        text oidc_scope "nullable"
        text api_key_header_name "nullable"
        text api_key_value "encrypted; nullable"
        int api_key_placement "Header|Query|Cookie; nullable"
        text bearer_token "encrypted; nullable"
        text basic_username "encrypted; nullable"
        text basic_password "encrypted; nullable"
        text cached_access_token "encrypted; refreshed automatically"
        timestamptz token_expires_at "nullable"
        timestamptz last_verified_at "nullable"
        timestamptz last_used_at "nullable"
        uuid created_by "admin user id"
        uuid updated_by "nullable"
    }

    MINISTRY_ENDPOINTS {
        uuid id PK
        uuid ministry_id FK
        uuid auth_config_id FK "nullable; which auth to use"
        varchar code "unique within ministry"
        varchar name_ar
        varchar name_en
        text description_ar "nullable"
        text description_en "nullable"
        varchar http_method "GET|POST|PUT|DELETE|PATCH"
        text path_template "e.g. /citizens/{nationalId}/verify"
        jsonb request_schema_json "JSON Schema; nullable"
        jsonb response_schema_json "JSON Schema; nullable"
        int endpoint_type "DataQuery|ServiceAction|WebhookRegister|StatusCheck|DocumentFetch|Health"
        boolean is_active
        int timeout_seconds
        int rate_limit_per_minute "nullable"
        boolean requires_citizen_consent
    }

    MINISTRY_WEBHOOKS {
        uuid id PK
        uuid ministry_id FK
        uuid endpoint_id FK "nullable; triggering action"
        varchar event_type "e.g. service_request.completed"
        text sheba_webhook_path "receiver path on Sheba"
        text signing_secret "encrypted; HMAC-SHA256 verification"
        boolean is_active
        timestamptz last_received_at "nullable"
    }
```

### 6.4 ServiceRequest (`service_req`) — [source](diagrams/erd-servicerequest.mmd)

```mermaid
erDiagram
    SERVICE_CATEGORIES ||--o{ SERVICE_CATEGORIES : "parent of"
    SERVICE_CATEGORIES ||--o{ SERVICES : "groups"
    SERVICES ||--o| SERVICE_FORM_SCHEMAS : "defines form"
    SERVICES ||--o{ SERVICE_FEES : "charges"
    SERVICES ||--o{ SERVICE_REQUIRED_DOCUMENTS : "requires"
    SERVICES ||--o{ SERVICE_WORKFLOW_STEPS : "executes as"
    SERVICES ||--o{ SERVICE_REQUESTS : "requested as"
    SERVICE_REQUESTS ||--o{ REQUEST_STEP_EXECUTIONS : "progresses through"
    SERVICE_WORKFLOW_STEPS ||--o{ REQUEST_STEP_EXECUTIONS : "instantiated as"

    SERVICE_CATEGORIES {
        uuid id PK
        uuid parent_id FK "nullable; hierarchy"
        varchar name_ar
        varchar name_en
        text icon_url "nullable"
        int display_order
        boolean is_active
    }

    SERVICES {
        uuid id PK
        uuid category_id FK
        uuid ministry_id "logical ref to ministry.ministries"
        varchar code UK "e.g. PASSPORT_RENEW"
        varchar name_ar
        varchar name_en
        text description_ar "nullable"
        text description_en "nullable"
        jsonb eligibility_rules_json "nullable; JSON Logic"
        int required_loa "1-3"
        boolean requires_appointment
        boolean is_active "false until published"
        boolean is_online
        int average_days "nullable; SLA basis"
        int display_order
        text tags_csv "nullable"
        jsonb metadata_json "nullable"
    }

    SERVICE_FORM_SCHEMAS {
        uuid id PK
        uuid service_id FK "1:1"
        varchar schema_version "default 1.0"
        jsonb form_schema_json "JSON Schema"
        jsonb ui_schema_json "nullable; rendering hints"
        jsonb validation_rules_json "nullable; server-side rules"
    }

    SERVICE_FEES {
        uuid id PK
        uuid service_id FK
        varchar fee_type "BASE|EXPEDITE|DELIVERY"
        varchar name_ar
        varchar name_en
        numeric amount
        char currency "YER default"
        boolean is_mandatory
        date valid_from
        date valid_until "nullable"
    }

    SERVICE_REQUIRED_DOCUMENTS {
        uuid id PK
        uuid service_id FK
        varchar document_type "NATIONAL_ID|BIRTH_CERT|..."
        varchar name_ar
        varchar name_en
        boolean is_mandatory
        int max_size_mb "default 5"
        text allowed_mime_types_csv "nullable"
    }

    SERVICE_WORKFLOW_STEPS {
        uuid id PK
        uuid service_id FK
        int step_order
        varchar name_ar
        varchar name_en
        int step_type "CitizenSubmit|Payment|MinistryApiCall|MinistryReview|AdminReview|DocumentIssue|Notification|WebhookWait|Appointment"
        int actor "Citizen|System|Ministry|ShebaAdmin"
        uuid ministry_endpoint_id "logical ref to ministry.ministry_endpoints; nullable"
        int timeout_hours "nullable"
        boolean is_automated
        int on_success_step "nullable; branch target"
        int on_failure_step "nullable; branch target"
        jsonb config_json "nullable; step config"
    }

    SERVICE_REQUESTS {
        uuid id PK
        varchar reference_number UK "SHB-2026-XXXXXX"
        uuid service_id FK
        uuid citizen_id "logical ref to identity.accounts"
        int status "RequestLifecycleStatus enum"
        int current_step
        jsonb form_data_json "citizen submission; PII"
        varchar priority "NORMAL|EXPRESS|URGENT"
        timestamptz submitted_at
        timestamptz completed_at "nullable"
        timestamptz due_date "nullable; SLA deadline"
        text rejection_reason "nullable"
        text notes "nullable; internal"
    }

    REQUEST_STEP_EXECUTIONS {
        uuid id PK
        uuid request_id FK
        uuid step_id FK
        int step_order
        int status "Pending|Running|Completed|Failed|Skipped"
        timestamptz started_at
        timestamptz completed_at "nullable"
        uuid actor_id "nullable"
        varchar actor_type "CITIZEN|SYSTEM|MINISTRY|ADMIN; nullable"
        jsonb result_json "nullable; ministry response, decision"
        text error_message "nullable"
    }
```

### 6.5 Document (`document`) — [source](diagrams/erd-document.mmd)

```mermaid
erDiagram
    DOCUMENTS ||--o{ DOCUMENT_ACCESS_GRANTS : "shared via"

    DOCUMENTS {
        uuid id PK
        uuid owner_id "logical ref to identity.accounts"
        varchar file_name "original client name"
        varchar content_type "MIME"
        bigint size_bytes
        varchar bucket_name
        varchar object_key "MinIO key"
        varchar document_type "NATIONAL_ID_PHOTO|SELFIE|GENERAL|..."
        boolean is_deleted "soft delete only"
        timestamptz deleted_at "nullable"
        timestamptz created_at
        timestamptz updated_at
    }

    DOCUMENT_ACCESS_GRANTS {
        uuid id PK
        uuid document_id FK
        uuid grantee_id "logical ref; e.g. ministry reviewer"
        timestamptz expires_at "time-boxed read access"
    }
```

### 6.6 Wallet (`wallet`) — [source](diagrams/erd-wallet.mmd)

```mermaid
erDiagram
    DID_DOCUMENTS ||--o{ VERIFIABLE_CREDENTIALS : "issues / holds"
    CREDENTIAL_SCHEMAS ||--o{ VERIFIABLE_CREDENTIALS : "typed by"

    DID_DOCUMENTS {
        uuid id PK
        varchar did UK "did:sheba:issuer | did:sheba:citizen:{id}"
        uuid subject_id "logical ref to identity.accounts; null for issuer DID"
        text public_key_pem "RSA public key"
        boolean is_active
    }

    CREDENTIAL_SCHEMAS {
        uuid id PK
        varchar schema_uri UK "https://sheba.gov.ye/schemas/v1/identity"
        varchar name
        varchar version
        varchar issuer_did
        jsonb schema_definition_json "JSON Schema"
        boolean is_active
    }

    VERIFIABLE_CREDENTIALS {
        uuid id PK
        uuid subject_id "logical ref to identity.accounts"
        varchar credential_type "DigitalIdentityCredential|..."
        varchar issuer_did
        varchar subject_did
        text jwt "signed compact JWS"
        jsonb claims_json "decoded claims for quick reads"
        timestamptz issued_at
        timestamptz expires_at "nullable"
        boolean is_revoked
        timestamptz revoked_at "nullable"
    }
```

### 6.7 Payment (`payment`) — [source](diagrams/erd-payment.mmd)

```mermaid
erDiagram
    PAYMENT_ORDERS ||--o{ PAYMENT_TRANSACTIONS : "settled by"

    PAYMENT_ORDERS {
        uuid id PK
        uuid service_request_id "logical ref to service_req.service_requests"
        uuid citizen_id "logical ref to identity.accounts"
        varchar order_number UK "PAY-YYYYMMDD-XXXXXXXX"
        numeric total_amount
        char currency "YER default"
        int status "Pending|Completed|Failed|Refunded|Cancelled"
        text description "nullable"
        text payment_url "nullable; gateway or mock pay URL"
        timestamptz paid_at "nullable"
        varchar gateway_reference "nullable"
        timestamptz created_at
        timestamptz updated_at
    }

    PAYMENT_TRANSACTIONS {
        uuid id PK
        uuid payment_order_id FK
        varchar transaction_type "CHARGE|REFUND|..."
        numeric amount
        jsonb gateway_response "nullable; sanitized"
    }
```

### 6.8 Notification (`notification`) — [source](diagrams/erd-notification.mmd)

```mermaid
erDiagram
    NOTIFICATION_TEMPLATES ||--o{ NOTIFICATION_RECORDS : "rendered as (planned)"

    NOTIFICATION_RECORDS {
        uuid id PK
        uuid recipient_id "logical ref to identity.accounts"
        varchar channel "Email|Sms"
        varchar recipient "email address or phone"
        varchar subject "email subject or SMS tag"
        text body
        boolean succeeded
        text error_message "nullable"
        timestamptz sent_at "immutable once written"
    }

    NOTIFICATION_TEMPLATES {
        uuid id PK
        varchar code UK "IDENTITY_APPROVED|OTP_LOGIN|... (planned)"
        varchar channel "Email|Sms|Push"
        varchar subject_template
        text body_template_ar
        text body_template_en
        boolean is_active
    }
```

### 6.9 Audit (`audit`) — [source](diagrams/erd-audit.mmd)

```mermaid
erDiagram
    AUDIT_EVENTS {
        uuid id PK
        uuid actor_id "JWT sub; Guid.Empty = system/anonymous"
        varchar action "MediatR command name"
        varchar entity_type "nullable; e.g. Account"
        uuid entity_id "nullable"
        timestamptz timestamp
        varchar ip_address "nullable"
        jsonb request_snapshot "nullable; before/input state"
        jsonb response_snapshot "nullable; after/output state"
        boolean succeeded
        text error_message "nullable"
        text prev_hash "target: hash of previous row (chain)"
        text entry_hash "target: SHA-256 of row + prev_hash"
    }
```

Target hardening (T-AUD-1..3): INSERT-only grant for the app role, SHA-256 hash chain
(`entry_hash = H(row ‖ prev_hash)` — any rewrite breaks the chain), monthly partitioning.

### 6.10 Admin (`admin_data`) — [source](diagrams/erd-admin.mmd)

```mermaid
erDiagram
    DAILY_REGISTRATION_SNAPSHOTS {
        uuid id PK
        date date UK
        int total_registrations
        int approved
        int rejected
        int pending_eod "pending at end of day"
        numeric avg_approval_hours "nullable"
    }

    DAILY_SERVICE_REQUEST_SNAPSHOTS {
        uuid id PK
        date date "unique with service_id"
        uuid service_id "logical ref to service_req.services"
        uuid ministry_id "logical ref to ministry.ministries"
        int submitted
        int completed
        int rejected
        int cancelled
        int sla_breach
        numeric avg_completion_hours "nullable"
    }

    REPORT_JOBS {
        uuid id PK
        int report_type "IdentityRequests|ServiceRequests|AuditLog"
        int format "Pdf|Excel|Csv"
        int status "Queued|Running|Done|Failed"
        jsonb filters_json "nullable"
        uuid requested_by "admin user id"
        varchar hangfire_job_id "nullable"
        bytea file_bytes "nullable; generated file"
        varchar file_name "nullable"
        bigint file_size_bytes "nullable"
        int row_count "nullable"
        text error_message "nullable"
        timestamptz started_at "nullable"
        timestamptz completed_at "nullable"
    }
```

Written only by event handlers / Hangfire, never by API command handlers
([sheba.md §12](sheba.md#12-dashboard--bi--reporting-backend)).

## 7. Indexing baseline

Unique: every `UK` above. Hot paths: `otp_records(account_id, purpose, expires_at)`,
`identity_requests(status, submitted_at)`, `service_requests(citizen_id, status)`,
`service_requests(reference_number)`, `outbox_messages(published_at) WHERE published_at IS NULL`
(partial), `audit_events(entity_type, entity_id)`, `audit_events(timestamp)`. Add indexes with
migrations, justified by a query — not speculatively.
