# Sheba — Frontend Code Examples (React · Next.js · Flutter)

> Copy-paste starting points for the real Sheba API. Contracts: [frontend-api.md](frontend-api.md);
> flows: [frontend-auth.md](frontend-auth.md); patterns: [frontend-integration.md](frontend-integration.md).
>
> Set `BASE_URL` to your Sheba host (dev: `https://localhost:7001`). `sheba-portal` is a **public**
> PKCE client (no secret); `sheba-admin` is confidential.

---

## Contents

- [1. Shared: JSend parsing](#1-shared-jsend-parsing)
- [2. React — Axios client + interceptors](#2-react--axios-client)
- [3. React — Fetch API variant](#3-react--fetch-api-variant)
- [4. React — automatic retry](#4-react--automatic-retry)
- [5. React — OTP login flow](#5-react--otp-login-flow)
- [6. React — multipart upload](#6-react--multipart-upload)
- [7. React — document download](#7-react--document-download)
- [8. React — pagination](#8-react--pagination)
- [9. React — service request submission](#9-react--service-request-submission)
- [10. React — wallet](#10-react--wallet)
- [11. Next.js — BFF token handling (httpOnly cookies)](#11-nextjs--bff-token-handling)
- [12. Next.js — server-side data fetching](#12-nextjs--server-side-fetch)
- [13. Flutter — Dio client + interceptors](#13-flutter--dio-client)
- [14. Flutter — refresh interceptor](#14-flutter--refresh-interceptor)
- [15. Flutter — OTP + admin MFA](#15-flutter--otp--mfa)
- [16. Flutter — multipart upload & download](#16-flutter--upload--download)
- [17. Flutter — service request + notifications](#17-flutter--service-request--notifications)

---

## 1. Shared: JSend parsing

```ts
// jsend.ts — one parser for every non-OIDC response
export type JSend<T> =
  | { status: 'success'; data: T }
  | { status: 'fail'; data: Record<string, string> }
  | { status: 'error'; message: string; code?: number; data?: { correlation_id?: string } };

export class ApiError extends Error {
  constructor(
    public kind: 'fail' | 'error' | 'oauth',
    public httpStatus: number,
    public fields?: Record<string, string>,
    public code?: number,
    public correlationId?: string,
    message?: string,
  ) { super(message ?? 'Request failed'); }
}

export function unwrap<T>(httpStatus: number, body: any): T {
  // OIDC error shape ({error, error_description}) — /connect/* only
  if (body && body.error && !body.status) {
    throw new ApiError('oauth', httpStatus, undefined, undefined, undefined, body.error_description || body.error);
  }
  if (body?.status === 'success') return body.data as T;
  if (body?.status === 'fail')    throw new ApiError('fail', httpStatus, body.data);
  if (body?.status === 'error')   throw new ApiError('error', httpStatus, undefined, body.code, body.data?.correlation_id, body.message);
  throw new ApiError('error', httpStatus, undefined, undefined, undefined, 'Unrecognized response');
}
```

---

## 2. React — Axios client

```ts
// api.ts
import axios from 'axios';
import { unwrap } from './jsend';
import { tokenStore } from './tokens';
import { refreshTokens } from './auth';

export const BASE_URL = 'https://localhost:7001';

export const api = axios.create({ baseURL: BASE_URL });

// ── Request interceptor: bearer + correlation id ──────────────────────────
api.interceptors.request.use((config) => {
  const token = tokenStore.access;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  config.headers['X-Correlation-Id'] = crypto.randomUUID();
  return config;
});

// ── Response interceptor: unwrap JSend, single-flight refresh on 401 ──────
let refreshing: Promise<void> | null = null;

api.interceptors.response.use(
  (res) => { res.data = unwrap(res.status, res.data); return res; },
  async (error) => {
    const { response, config } = error;
    if (!response) throw error;                      // network error
    // 401 → refresh once, then replay once
    if (response.status === 401 && !config.__retried) {
      config.__retried = true;
      try {
        refreshing ??= refreshTokens().finally(() => { refreshing = null; });
        await refreshing;
        config.headers.Authorization = `Bearer ${tokenStore.access}`;
        return api(config);
      } catch { tokenStore.clear(); location.assign('/login'); }
    }
    // hand the JSend error to callers
    throw unwrapError(response);
  },
);

function unwrapError(response: any) {
  try { unwrap(response.status, response.data); } catch (e) { return e; }
  return new Error(`HTTP ${response.status}`);
}
```

```ts
// tokens.ts
export const tokenStore = {
  get access() { return sessionStorage.getItem('at') ?? undefined; },   // in-memory-ish; prefer a JS var
  get refresh() { return localStorage.getItem('rt') ?? undefined; },    // XSS caveat — see integration §5
  set(at: string, rt?: string) { sessionStorage.setItem('at', at); if (rt) localStorage.setItem('rt', rt); },
  clear() { sessionStorage.removeItem('at'); localStorage.removeItem('rt'); },
};
```

```ts
// auth.ts — token endpoint helpers (form-urlencoded; /connect/* is NOT JSend)
import axios from 'axios';
import { BASE_URL } from './api';
import { tokenStore } from './tokens';

const form = (o: Record<string, string>) => new URLSearchParams(o).toString();

export async function citizenTokenFromOtp(accountId: string, otp: string) {
  const { data } = await axios.post(`${BASE_URL}/connect/token`, form({
    grant_type: 'urn:sheba:grant:national_id_otp',
    account_id: accountId, otp, client_id: 'sheba-portal',
    scope: 'openid profile email offline_access',
  }), { headers: { 'Content-Type': 'application/x-www-form-urlencoded' } });
  tokenStore.set(data.access_token, data.refresh_token);
  return data;
}

export async function refreshTokens() {
  const rt = tokenStore.refresh;
  if (!rt) throw new Error('no refresh token');
  const { data } = await axios.post(`${BASE_URL}/connect/token`, form({
    grant_type: 'refresh_token', refresh_token: rt, client_id: 'sheba-portal',
  }), { headers: { 'Content-Type': 'application/x-www-form-urlencoded' } });
  tokenStore.set(data.access_token, data.refresh_token); // rotate BOTH
}

export async function adminTokenFromPassword(id: string, password: string, mfaCode?: string) {
  const body: Record<string, string> = {
    grant_type: 'urn:sheba:grant:admin_password',
    employee_id_or_email: id, password,
    client_id: 'sheba-admin', client_secret: 'sheba-admin-dev-secret',
    scope: 'openid profile admin_api',
  };
  if (mfaCode) body.mfa_code = mfaCode;
  const { data } = await axios.post(`${BASE_URL}/connect/token`, form(body),
    { headers: { 'Content-Type': 'application/x-www-form-urlencoded' } });
  tokenStore.set(data.access_token, data.refresh_token);
  return data;
}
```

---

## 3. React — Fetch API variant

```ts
// fetchClient.ts
import { unwrap } from './jsend';
import { tokenStore } from './tokens';

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  if (tokenStore.access) headers.set('Authorization', `Bearer ${tokenStore.access}`);
  headers.set('X-Correlation-Id', crypto.randomUUID());
  if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json');

  const res = await fetch(`${BASE_URL}${path}`, { ...init, headers });
  const body = res.status === 204 ? null : await res.json().catch(() => null);
  return unwrap<T>(res.status, body);   // throws ApiError on fail/error
}

// usage
// const cats = await apiFetch<ServiceCatalog>('/api/services');
```

---

## 4. React — automatic retry

```ts
// retry.ts — idempotent GETs only; honor Retry-After on 429
import { ApiError } from './jsend';

export async function withRetry<T>(fn: () => Promise<T>, max = 3): Promise<T> {
  let attempt = 0;
  for (;;) {
    try { return await fn(); }
    catch (e) {
      const err = e as ApiError;
      const retriable = err.httpStatus >= 500 || err.httpStatus === 429;
      if (!retriable || attempt >= max) throw e;
      const wait = err.httpStatus === 429 ? 2000 : (2 ** attempt) * 300 + Math.random() * 200;
      await new Promise((r) => setTimeout(r, wait));
      attempt++;
    }
  }
}
// Never wrap non-idempotent POSTs (register, submit request, payment) in withRetry.
```

---

## 5. React — OTP login flow

```tsx
// LoginFlow.tsx
import { useState } from 'react';
import { api } from './api';
import { citizenTokenFromOtp } from './auth';
import { ApiError } from './jsend';

export function LoginFlow() {
  const [step, setStep] = useState<'creds' | 'otp'>('creds');
  const [accountId, setAccountId] = useState('');
  const [maskedPhone, setMaskedPhone] = useState('');
  const [err, setErr] = useState('');

  async function submitCreds(usernameOrNid: string, password: string) {
    setErr('');
    try {
      const res = await api.post('/api/identity/login', { usernameOrNid, password });
      setAccountId(res.data.accountId); setMaskedPhone(res.data.maskedPhone); setStep('otp');
    } catch (e) {
      const a = e as ApiError;
      setErr(a.fields?.credentials ?? a.fields?.domain ?? a.message); // 400 generic / 422 not-approved
    }
  }

  async function submitOtp(otp: string) {
    setErr('');
    try {
      await citizenTokenFromOtp(accountId, otp);  // hits /connect/token, stores tokens
      location.assign('/dashboard');
    } catch (e) { setErr((e as ApiError).message || 'Invalid or expired code'); }
  }

  return step === 'creds'
    ? <CredForm onSubmit={submitCreds} error={err} />
    : <OtpForm phone={maskedPhone} onSubmit={submitOtp} error={err} />;
}
```

---

## 6. React — multipart upload

```tsx
// UploadDocument.tsx
import { api } from './api';

const ALLOWED = ['image/jpeg', 'image/png', 'image/webp', 'application/pdf'];
const MAX = 10 * 1024 * 1024;

export async function uploadDocument(file: File, documentType = 'GENERAL') {
  if (!ALLOWED.includes(file.type)) throw new Error('Only JPEG, PNG, WebP, or PDF allowed.');
  if (file.size > MAX) throw new Error('File must be 10 MB or smaller.');

  const fd = new FormData();
  fd.append('file', file);
  fd.append('documentType', documentType);
  // Do NOT set Content-Type — the browser sets the multipart boundary.
  const res = await api.post('/api/documents', fd);
  return res.data; // { documentId, fileName, sizeBytes, message }
}
```

---

## 7. React — document download

```ts
// download.ts
import { api } from './api';

export async function downloadDocument(documentId: string) {
  const { data } = await api.get(`/api/documents/${documentId}/download-url`);
  // data: { downloadUrl, fileName, contentType, expiresAt }
  const a = document.createElement('a');
  a.href = data.downloadUrl;          // presigned MinIO URL — no auth header needed
  a.download = data.fileName;
  document.body.appendChild(a); a.click(); a.remove();
}

// Admin report (direct authenticated file stream):
export async function downloadReport(from: string, to: string, format: 'pdf' | 'excel' = 'pdf') {
  const res = await api.get(`/api/admin/reports/identity-requests?from=${from}&to=${to}&format=${format}`,
    { responseType: 'blob' });
  const url = URL.createObjectURL(res.data as Blob);
  const a = document.createElement('a');
  a.href = url; a.download = `identity-requests-${from}-${to}.${format === 'excel' ? 'xlsx' : 'pdf'}`;
  a.click(); URL.revokeObjectURL(url);
}
```

> Note: for `responseType: 'blob'` reports the Axios response interceptor's `unwrap` should skip
> blobs — guard it with `if (res.config.responseType !== 'blob')` before unwrapping.

---

## 8. React — pagination

```tsx
// useIdentityRequests.ts
import { useEffect, useState } from 'react';
import { api } from './api';

export function useIdentityRequests(status?: string) {
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [data, setData] = useState<{ items: any[]; totalCount: number } | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    const q = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (status) q.set('status', status);
    api.get(`/api/admin/identity-requests?${q}`)
      .then((r) => setData(r.data))
      .finally(() => setLoading(false));
  }, [page, pageSize, status]);

  const hasNext = data ? page * pageSize < data.totalCount : false;
  return { data, loading, page, setPage, hasNext };
}
```

---

## 9. React — service request submission

```ts
// submitRequest.ts — form data validated server-side against the service's JSON Schema
import { api } from './api';
import { ApiError } from './jsend';

export async function submitServiceRequest(serviceId: string, formData: object, priority = 'NORMAL') {
  try {
    const res = await api.post('/api/requests', {
      serviceId,
      formDataJson: JSON.stringify(formData), // NOTE: string, not an object
      priority,
    });
    return res.data; // { requestId, referenceNumber, status, message } — first step auto-executes
  } catch (e) {
    const a = e as ApiError;
    if (a.kind === 'fail') {
      // schema violations keyed as formData.<field>; strip the prefix for your form
      const fieldErrors = Object.fromEntries(
        Object.entries(a.fields ?? {}).map(([k, v]) => [k.replace(/^formData\.?/, ''), v]));
      throw { fieldErrors };
    }
    throw e;
  }
}

// Poll status:
export async function pollRequest(id: string) {
  const res = await api.get(`/api/requests/${id}`);
  return res.data; // { status, currentStep, steps: [...] }  — stop on Completed/Rejected/Cancelled/Expired
}
```

---

## 10. React — wallet

```ts
import { api } from './api';
export async function getMyCredentials() {
  const res = await api.get('/api/wallet/credentials');
  return res.data as Array<{
    id: string; credentialType: string; issuerDid: string; subjectDid: string;
    jwt: string; claims: Record<string, unknown>; issuedAt: string; expiresAt?: string; isRevoked: boolean;
  }>;
}
```

> **Notifications:** there is **no** notifications API today (delivered via email/SMS). A
> `GET /api/notifications/{accountId}` is planned but unmapped — do not build against it yet.

---

## 11. Next.js — BFF token handling

Keep tokens in **httpOnly cookies** set by Next route handlers; the browser never sees them.

```ts
// app/api/auth/login/route.ts  (Route Handler)
import { NextRequest, NextResponse } from 'next/server';

const BASE_URL = process.env.SHEBA_BASE_URL!; // server-only

export async function POST(req: NextRequest) {
  const { accountId, otp } = await req.json();
  const tokenRes = await fetch(`${BASE_URL}/connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'urn:sheba:grant:national_id_otp',
      account_id: accountId, otp, client_id: 'sheba-portal',
      scope: 'openid profile email offline_access',
    }),
  });
  if (!tokenRes.ok) {
    const err = await tokenRes.json().catch(() => ({}));
    return NextResponse.json({ error: err.error_description ?? 'login_failed' }, { status: 401 });
  }
  const tok = await tokenRes.json();
  const res = NextResponse.json({ ok: true });
  res.cookies.set('sheba_at', tok.access_token, { httpOnly: true, secure: true, sameSite: 'lax', maxAge: 900, path: '/' });
  res.cookies.set('sheba_rt', tok.refresh_token, { httpOnly: true, secure: true, sameSite: 'lax', maxAge: 60 * 60 * 24 * 30, path: '/' });
  return res;
}
```

```ts
// lib/server-api.ts — call Sheba from Server Components / actions with the cookie token
import { cookies } from 'next/headers';
import { unwrap } from './jsend';

export async function serverApi<T>(path: string, init: RequestInit = {}): Promise<T> {
  const at = cookies().get('sheba_at')?.value;
  const headers = new Headers(init.headers);
  if (at) headers.set('Authorization', `Bearer ${at}`);
  headers.set('X-Correlation-Id', crypto.randomUUID());
  const res = await fetch(`${process.env.SHEBA_BASE_URL}${path}`, { ...init, headers, cache: 'no-store' });
  return unwrap<T>(res.status, res.status === 204 ? null : await res.json());
}
```

```ts
// app/api/auth/refresh/route.ts — rotate refresh cookie (call from middleware or on 401)
import { NextResponse } from 'next/server';
import { cookies } from 'next/headers';
export async function POST() {
  const rt = cookies().get('sheba_rt')?.value;
  if (!rt) return NextResponse.json({ error: 'no_session' }, { status: 401 });
  const r = await fetch(`${process.env.SHEBA_BASE_URL}/connect/token`, {
    method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ grant_type: 'refresh_token', refresh_token: rt, client_id: 'sheba-portal' }),
  });
  if (!r.ok) return NextResponse.json({ error: 'refresh_failed' }, { status: 401 });
  const tok = await r.json();
  const res = NextResponse.json({ ok: true });
  res.cookies.set('sheba_at', tok.access_token, { httpOnly: true, secure: true, sameSite: 'lax', maxAge: 900, path: '/' });
  res.cookies.set('sheba_rt', tok.refresh_token, { httpOnly: true, secure: true, sameSite: 'lax', maxAge: 60*60*24*30, path: '/' });
  return res;
}
```

---

## 12. Next.js — server-side fetch

```tsx
// app/services/page.tsx — Server Component
import { serverApi } from '@/lib/server-api';

export default async function ServicesPage() {
  const catalog = await serverApi<{ categories: any[] }>('/api/services');
  return (
    <ul>
      {catalog.categories.map((c) => (
        <li key={c.id}>{c.nameEn} ({c.services.length})</li>
      ))}
    </ul>
  );
}
```

---

## 13. Flutter — Dio client

```dart
// api_client.dart
import 'package:dio/dio.dart';
import 'package:uuid/uuid.dart';
import 'token_store.dart';

const baseUrl = 'https://localhost:7001';

class ApiException implements Exception {
  final String kind; // 'fail' | 'error' | 'oauth'
  final int httpStatus;
  final Map<String, dynamic>? fields;
  final int? code;
  final String? correlationId;
  final String message;
  ApiException(this.kind, this.httpStatus, {this.fields, this.code, this.correlationId, this.message = 'Request failed'});
}

/// Unwraps a JSend body; throws ApiException on fail/error/oauth.
dynamic unwrap(int status, dynamic body) {
  if (body is Map && body['error'] != null && body['status'] == null) {
    throw ApiException('oauth', status, message: body['error_description'] ?? body['error']);
  }
  if (body is Map && body['status'] == 'success') return body['data'];
  if (body is Map && body['status'] == 'fail') {
    throw ApiException('fail', status, fields: Map<String, dynamic>.from(body['data'] ?? {}));
  }
  if (body is Map && body['status'] == 'error') {
    throw ApiException('error', status,
        code: body['code'], correlationId: body['data']?['correlation_id'], message: body['message'] ?? 'Server error');
  }
  throw ApiException('error', status, message: 'Unrecognized response');
}

final dio = Dio(BaseOptions(baseUrl: baseUrl))
  ..interceptors.add(InterceptorsWrapper(
    onRequest: (options, handler) async {
      final at = await TokenStore.access;
      if (at != null) options.headers['Authorization'] = 'Bearer $at';
      options.headers['X-Correlation-Id'] = const Uuid().v4();
      handler.next(options);
    },
  ));

Future<T> apiGet<T>(String path, {Map<String, dynamic>? query}) async {
  final res = await dio.get(path, queryParameters: query);
  return unwrap(res.statusCode ?? 0, res.data) as T;
}

Future<T> apiPost<T>(String path, {Object? data}) async {
  final res = await dio.post(path, data: data);
  return unwrap(res.statusCode ?? 0, res.data) as T;
}
```

```dart
// token_store.dart — Keychain/Keystore via flutter_secure_storage
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
class TokenStore {
  static const _s = FlutterSecureStorage();
  static Future<String?> get access => _s.read(key: 'at');
  static Future<String?> get refresh => _s.read(key: 'rt');
  static Future<void> set(String at, [String? rt]) async {
    await _s.write(key: 'at', value: at);
    if (rt != null) await _s.write(key: 'rt', value: rt);
  }
  static Future<void> clear() async { await _s.delete(key: 'at'); await _s.delete(key: 'rt'); }
}
```

```dart
// auth.dart — /connect/token (form-urlencoded)
import 'package:dio/dio.dart';
import 'api_client.dart';
import 'token_store.dart';

Future<Map> citizenTokenFromOtp(String accountId, String otp) async {
  final res = await dio.post('/connect/token',
    options: Options(contentType: Headers.formUrlEncodedContentType),
    data: {
      'grant_type': 'urn:sheba:grant:national_id_otp',
      'account_id': accountId, 'otp': otp, 'client_id': 'sheba-portal',
      'scope': 'openid profile email offline_access',
    });
  await TokenStore.set(res.data['access_token'], res.data['refresh_token']);
  return res.data;
}
```

---

## 14. Flutter — refresh interceptor

```dart
// refresh_interceptor.dart — single-flight refresh on 401
import 'package:dio/dio.dart';
import 'api_client.dart';
import 'token_store.dart';

Future<void>? _refreshing;

Future<void> _refresh() async {
  final rt = await TokenStore.refresh;
  if (rt == null) throw Exception('no refresh token');
  final res = await Dio(BaseOptions(baseUrl: baseUrl)).post('/connect/token',
    options: Options(contentType: Headers.formUrlEncodedContentType),
    data: {'grant_type': 'refresh_token', 'refresh_token': rt, 'client_id': 'sheba-portal'});
  await TokenStore.set(res.data['access_token'], res.data['refresh_token']); // rotate both
}

final refreshInterceptor = QueuedInterceptorsWrapper(
  onError: (e, handler) async {
    if (e.response?.statusCode == 401 && e.requestOptions.extra['retried'] != true) {
      try {
        _refreshing ??= _refresh();
        await _refreshing;
        _refreshing = null;
        final at = await TokenStore.access;
        final req = e.requestOptions;
        req.extra['retried'] = true;
        req.headers['Authorization'] = 'Bearer $at';
        final clone = await dio.fetch(req);
        return handler.resolve(clone);
      } catch (_) {
        await TokenStore.clear();
        // navigate to login (via your router)
      }
    }
    handler.next(e);
  },
);
// dio.interceptors.add(refreshInterceptor);
```

---

## 15. Flutter — OTP + admin MFA

```dart
// login: step 1 dispatches OTP, then token grant
Future<void> citizenLogin(String usernameOrNid, String password) async {
  final data = await apiPost('/api/identity/login',
      data: {'usernameOrNid': usernameOrNid, 'password': password});
  // data: { accountId, maskedPhone } — show OTP screen
}

Future<void> completeCitizenLogin(String accountId, String otp) async {
  await citizenTokenFromOtp(accountId, otp); // stores tokens
}

// admin login with optional MFA code (TOTP or recovery code)
Future<Map> adminLogin(String idOrEmail, String password, {String? mfaCode}) async {
  final body = {
    'grant_type': 'urn:sheba:grant:admin_password',
    'employee_id_or_email': idOrEmail, 'password': password,
    'client_id': 'sheba-admin', 'client_secret': 'sheba-admin-dev-secret',
    'scope': 'openid profile admin_api',
    if (mfaCode != null) 'mfa_code': mfaCode,
  };
  final res = await dio.post('/connect/token',
      options: Options(contentType: Headers.formUrlEncodedContentType), data: body);
  await TokenStore.set(res.data['access_token'], res.data['refresh_token']);
  return res.data;
  // On DioException 400 with error_description 'mfa_required' → prompt for code and resubmit.
}

// MFA enrollment
Future<Map> enrollMfa() => apiPost('/api/admin/mfa/enroll');            // { secret, provisioningUri }
Future<Map> verifyMfa(String totpCode) =>
    apiPost('/api/admin/mfa/verify', data: {'totpCode': totpCode});     // { recoveryCodes: [...] }
```

---

## 16. Flutter — upload & download

```dart
import 'package:dio/dio.dart';
import 'api_client.dart';

Future<Map> uploadDocument(String filePath, {String documentType = 'GENERAL'}) async {
  final form = FormData.fromMap({
    'file': await MultipartFile.fromFile(filePath),
    'documentType': documentType,
  });
  final res = await dio.post('/api/documents', data: form);
  return unwrap(res.statusCode ?? 0, res.data) as Map; // { documentId, fileName, sizeBytes }
}

Future<String> documentDownloadUrl(String documentId) async {
  final data = await apiGet<Map>('/api/documents/$documentId/download-url');
  return data['downloadUrl'] as String; // presigned MinIO URL, valid 15 min — open with url_launcher / download directly
}
```

---

## 17. Flutter — service request + notifications

```dart
import 'dart:convert';
import 'api_client.dart';

Future<Map> submitRequest(String serviceId, Map<String, dynamic> formData, {String priority = 'NORMAL'}) async {
  try {
    return await apiPost<Map>('/api/requests', data: {
      'serviceId': serviceId,
      'formDataJson': jsonEncode(formData), // string, not object
      'priority': priority,
    });
  } on ApiException catch (e) {
    if (e.kind == 'fail') {
      // schema errors keyed formData.<field>
      final errors = e.fields!.map((k, v) => MapEntry(k.replaceFirst(RegExp(r'^formData\.?'), ''), v));
      throw errors;
    }
    rethrow;
  }
}

Future<Map> requestStatus(String id) => apiGet<Map>('/api/requests/$id');
Future<List> myRequests() => apiGet<List>('/api/requests/mine');

// NOTE: no notifications API exists yet — Sheba delivers notifications by email/SMS.
// Poll requestStatus / myRequests for progress; there is no WebSocket/SSE channel.
```

---

*Generated from source on 2026-07-18. Client-side examples target the real endpoints; adjust
`BASE_URL`, storage strategy, and your router/state library as needed.*
