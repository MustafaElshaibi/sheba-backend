# Performance & Scalability

> Extract of [sheba.md](sheba.md) §12, §14; sheba.md wins conflicts.

## 1. Targets (pilot scale, assumption A2)

| Metric | Target |
|--------|--------|
| p95 API latency (non-ministry endpoints) | < 300 ms |
| p95 login (password step, excl. SMS delivery) | < 500 ms |
| Ministry-call endpoints | bounded by per-endpoint `timeout_seconds` (default 30 s); async beyond that via WebhookWait |
| Sustained load | 10 RPS with headroom ×10 on one host |
| BI staleness | seconds (event-fed projections); daily snapshots at EOD |

Verify with a k6/NBomber smoke suite in Phase 4 ([roadmap.md](roadmap.md)).

## 2. Caching

| What | Where | TTL / invalidation |
|------|-------|--------------------|
| OTP issuance/IP counters | Redis | window-scoped (15 min) |
| Ministry endpoint + auth-config lookups | Redis (via `IDistributedCache`) | 5 min or bust-on-update |
| Cached ministry OAuth tokens | DB (encrypted) + in-memory | until `token_expires_at` − skew |
| Service catalog (public browse) | Redis | 10 min, bust on publish/depublish |
| JWKS (RP side) | RP-cached | standard `kid`-based rollover |
| Query results (selected admin lists) | MediatR CachingBehavior (target) | short TTL; only where measured |

Rule: cache only what a measurement justifies; every cache entry has an owner and an invalidation
story.

## 3. Read models over cross-schema queries

The BI/dashboard backend is an **event-fed read model** in `admin_data`
([sheba.md §12](sheba.md#12-dashboard--bi--reporting-backend)) — chosen over materialized views
because matviews would re-couple module schemas at the SQL layer and die at extraction time.
Reports (PDF/Excel/CSV) run in Hangfire, never on the request path; files stored on `ReportJob`.

## 4. Expected bottlenecks & responses

| Bottleneck | Signal | Response |
|------------|--------|----------|
| External ministry latency | step durations, circuit-open counts | Async WebhookWait pattern; per-endpoint timeouts; circuit breaker already in the resilience pipeline |
| SMS delivery latency/cost | OTP send failures, spend | Provider failover order (T-INT-2); queue + retry |
| Argon2id CPU under login bursts | CPU saturation | It's intentional (security); rate limiter shields; scale vertically first |
| Outbox dispatcher lag | unpublished-row age | Tune poll interval/batch; partial index on `published_at IS NULL` |
| Report generation | Hangfire queue depth | Already off-request-path; cap concurrent report workers |
| Postgres single instance | connection saturation | Pooling (Npgsql), then read replica, then per-module DB split (§5) |

## 5. Scaling path

Ladder (details [sheba.md §14.2](sheba.md#142-single-server-today-split-tomorrow)):
vertical → YARP fronting + horizontal API replicas (stateless host; Redis-backed shared state;
Hangfire handles distributed locking) → module extraction in the §3.4 order with RabbitMQ behind
the same outbox → K8s only when container count demands it. The design goal is that no rung
requires rewriting module internals.
