# Income Tax & GST Automation — Workflow, Procedures & Rules

**Product:** KKA Desk (private Windows desktop app)  
**Scope:** Admin-only Income Tax and GST document retrieval and filing (“Punch Documents”).  
**Out of scope:** Any change to the public website or client-side portal; return preparation/submission; e-verification; DSC signing; acceptance of CA assignments; alteration of tax records; bypassing OTP, CAPTCHA, MFA, or other portal controls.

## 1. Operating principles

1. This feature exists only inside KKA Desk. The existing client portal and its schema/functions are not modified as part of this work.
2. Admins alone may edit portal IDs, credentials, tax profile settings, start download jobs, retry jobs, and run Punch Documents. The app must check admin authorization at the command/service boundary, not merely hide controls.
3. Client profile settings determine which documents and periods are expected. Admin can override a period for a one-off run, but the override must be visible and logged.
4. Retrieval is read-only. The app must never submit, amend, file, verify, sign, or pay anything on either government portal.
5. Use documented/authorized APIs only where available and approved. Otherwise use a controlled browser automation worker. Never defeat OTP/CAPTCHA/MFA or other access controls; pause for the admin to complete the challenge in the official portal session.
6. Do not report a document as downloaded until its bytes are saved, file type is plausible, and the file is readable/non-empty. Do not silently claim a document exists if the portal does not expose it.
7. No secrets in logs, telemetry, source code, screenshots, exception messages, or job names. Credentials must be encrypted at rest using Windows-protected key material (DPAPI/Windows Credential Manager or an approved vault), excluded from sync/backup unless separately encrypted, and never returned to UI after save. The UI may show only configured/not configured and last-tested time.
8. Keep the downloaded source file unchanged. If a normalized name is needed, copy it; do not alter the original content.

## 2. Admin client tax profile

The profile is maintained in KKA Desk only for this feature; it does not write to the client portal.

### Income Tax
- Income Tax enabled: yes/no
- Income Tax portal user ID: admin-managed; password/secret saved separately in protected credential store
- PAN / tax identifier: validated format; stored as sensitive data
- Relevant financial year and assessment year / tax year model
- Audit case: yes/no/needs confirmation
- Expected audit form family is tax-year-aware; do not hard-code legacy forms. For FY 2025–26 / AY 2026–27 use applicable legacy forms where relevant; for Tax Year 2026–27, new Form 26 may apply. The final form set must be confirmed against the portal's live form availability and case facts.
- ITR document retrieval enabled: yes/no
- Tax audit report/attachments retrieval enabled: yes/no
- Credentials status and last successful portal test (metadata only)

### GST
- GST enabled: yes/no
- GSTIN: validated GSTIN format
- GST portal user ID: optional if required by the actual supported login flow; password/secret saved in protected credential store
- Filing frequency: **Monthly** or **Quarterly (QRMP)**, per GSTIN and effective date
- Effective-from period and, where needed, effective-to period for a frequency change
- GST return period selection and optional quarter/month override
- Credential status and last successful portal test (metadata only)

### Profile validation
- Do not start a job if required identifiers are missing/invalid, the profile is inactive, or credential setup is incomplete.
- GST filing frequency is explicit per GST registration; do not infer it from client type, turnover, or another GSTIN.
- Changes to GST frequency are effective-dated. Never reclassify historical periods using today's setting.
- The same client can have different Income Tax and GST settings; GST frequency does not affect Income Tax retrieval.

## 3. Period-selection rules

### GST frequency
- **Monthly:** one period per month. A batch for a selected FY enumerates all months in that FY, but the admin can choose a subset.
- **Quarterly / QRMP:** enumerate quarters (Apr–Jun, Jul–Sep, Oct–Dec, Jan–Mar) only for periods where the profile says quarterly. Generate GSTR-1 and GSTR-3B tasks for each selected quarter; where the portal's available return workflow requires a specific monthly action or period-specific document, show that requirement rather than guessing.
- **Frequency transition:** split requested periods at the effective date and apply the correct frequency to each period. If the exact effective period cannot be resolved, stop and ask the admin to confirm.
- Never create duplicate jobs for the same client + GSTIN + return type + period while a prior job is active. Completed periods are skipped by default; admin may choose “recheck/replace” with confirmation.

### Income Tax
- Use an explicitly selected FY / AY or Tax Year. Do not infer an assessment year from the current date alone.
- For ITR retrieval, only request documents that are available for the selected taxpayer and filing status.
- For tax audit cases, require the admin to confirm audit applicability and the relevant form family before retrieval. Document availability depends on portal status, CA assignment/authorization, and the particular case.
- Never treat a balance sheet or P&L as guaranteed portal downloads. Include them only if they are exposed as attachments/documents or supplied by an explicitly configured approved source. Otherwise mark “not available from portal” and leave a placeholder for manual filing.

## 4. Document catalogue and destination rules

All paths are generated from a stable internal client ID, not client-supplied free text. Sanitize display names and prevent path traversal. Store downloaded files in the app's configured protected document root; a local path or existing document-store integration must be explicitly configured and tested before real downloads are enabled.

Suggested folder layout:

- `<ClientID>/Income Tax/<FY> - <AY or Tax Year>/ITR/`
  - ITR acknowledgement / ITR-V (if available)
  - Filed ITR PDF / return copy (if available)
  - Intimation/order (optional document type, only if requested and available)
- `<ClientID>/Income Tax/<FY> - <AY or Tax Year>/Tax Audit/`
  - Applicable audit report and portal-provided attachments (case-dependent)
- `<ClientID>/GST/<FY>/<Monthly or Quarterly>/<Period>/GSTR-1/`
- `<ClientID>/GST/<FY>/<Monthly or Quarterly>/<Period>/GSTR-3B/`

File naming format: `<ClientCode>_<TaxType>_<DocumentType>_<Period>_<DownloadedUTC>.<ext>`; preserve original filename as metadata. Do not overwrite silently. Deduplicate using client ID, tax identifier, document type, period, file size, and SHA-256 hash. If same logical document differs, retain both versions and flag for admin review.

“Punch Documents” means file/move the verified downloaded file into the configured destination folder or document store. It is a separate explicit action from download. Show a preview of all destination paths before punching. Never delete the source download on failure. A punch is successful only after destination write is verified by size/hash; retries must be idempotent.

## 5. End-to-end workflow

1. **Admin sign-in gate:** verify current KKA Desk identity, active account, and admin role. Deny access by default if the role cannot be confirmed.
2. **Choose client:** search/select a client by stable ID and display name; show Income Tax and GST configuration statuses.
3. **Configure profile:** admin sets IDs, audit applicability, FY/AY/tax year, GSTIN, monthly/quarterly frequency, effective dates, and secret credentials. Save secrets into the protected vault, never ordinary profile text fields.
4. **Test access:** open the official portal using the approved automation path. Admin completes OTP/CAPTCHA/MFA manually when prompted. Do not log secret values or reuse a stale authenticated session without checking its state.
5. **Build job preview:** list each requested document type, period, and destination. Explain skipped/unsupported/unavailable document types before starting.
6. **Run download job:** a worker processes one portal session at a time per account. Record queued, authenticating, awaiting admin action, downloading, validating, downloaded, punching, completed, skipped, failed, or cancelled states. Support cancellation and bounded retries.
7. **Validate files:** verify successful download response, expected extension/content type, non-zero size, readable PDF where applicable, and hash. Quarantine unexpected/HTML/login/error responses rather than filing them as documents.
8. **Punch preview:** show selected verified files, client identity, periods, and destination folders. Admin confirms once for the batch.
9. **Punch:** copy into configured destination, verify the copy, and record the destination, hash, and outcome. If destination is unavailable, retain local download and mark “punch pending.”
10. **Summary:** report counts for downloaded, punched, skipped, awaiting admin, and failed. Show exact actionable reasons without secrets.
11. **Audit trail:** append actor, client ID, job ID, action, tax type, period, timestamps, file hash, destination and result. Never log passwords, OTPs, cookies, auth tokens, full PAN/GSTIN where avoidable, or document contents.

## 6. Job and failure rules

- Job states: Draft → Previewed → Queued → Running ↔ Awaiting Admin → Downloaded/Partially Completed → Punch Pending → Completed; terminal alternatives: Failed, Cancelled, Skipped.
- One worker lock per portal account/session. Do not run concurrent sessions for the same credential set.
- Retry transient network/timeouts with a small bounded retry count and backoff. Do not automatically retry wrong-password, locked-account, CAPTCHA/OTP, access-denied, or ambiguous portal-state errors.
- If the portal changes layout or the page cannot be confidently identified, stop safely and ask for admin intervention.
- Session timeout means re-authentication, not reuse of stale cookies. Never store cookies in logs or unprotected files.
- An “already exists” document is skipped unless the admin explicitly selects recheck/replace. Replacements are versioned; never destructive overwrite.
- A missing document is reported as “not available / not exposed for this case” only after the worker can distinguish it from login failure or selector breakage.
- Do not run jobs when the client identity, tax period, or GST frequency is ambiguous.

## 7. Security and privacy controls

- Admin-only controls are enforced in application services and the job worker, not just UI visibility.
- Credential secrets are write-only from the UI; replace/clear/test, never reveal.
- Use Windows DPAPI/Credential Manager or an approved vault, with separate protected key material. Do not put secrets in Supabase profile columns, GitHub, app settings, plain JSON, or logs.
- Encrypt transport; validate TLS. Restrict the download root to authorized Windows users and use least-privilege filesystem permissions.
- Minimize stored identifiers and redact them in logs. Audit access to credentials and downloaded tax documents.
- No public/client portal access, endpoints, migrations, policies, or Edge Functions are created/changed by this module.
- Keep browser automation and credential handling separate from UI. The UI receives job progress and sanitized results only.

## 8. Build and release phases

1. **Design + local admin UI:** profile settings, monthly/quarterly rules, job preview, progress states, Punch Documents preview; use mock data only.
2. **Local persistence and protected credentials:** admin role integration and Windows protected vault, still no government portal automation.
3. **Portal compatibility proof:** one test account and one document type at a time; manually assisted OTP/CAPTCHA; verify selectors and official download formats.
4. **GST retrieval:** one monthly period, then one QRMP quarter; validate both GSTR-1 and GSTR-3B workflow behavior before batch mode.
5. **Income Tax retrieval:** ITR acknowledgement/return first; audit forms/attachments only after case-specific availability is proven.
6. **Punch destination adapter:** configured local/approved document destination; preview, hash verification, idempotency and retry.
7. **Pilot and hardening:** tests for frequency transitions, duplicates, wrong credentials, session expiry, invalid downloads, path safety, role enforcement and audit logs.

## 9. Acceptance criteria

- Client-side portal is unchanged.
- A non-admin cannot open, invoke, or queue admin tax automation services.
- Monthly profile creates monthly GST period tasks; quarterly profile creates quarter tasks; effective-dated changes do not rewrite historical periods.
- Job preview shows all document types and destination paths before a job starts.
- OTP/CAPTCHA/MFA prompts pause for the admin; no bypass is attempted.
- Invalid/login HTML responses never enter document folders as successful downloads.
- Punching is separate, confirmed, verified by hash/size, and safe to retry.
- No credential values or session tokens appear in logs, config files, source control, or UI after saving.
- Every action has a sanitized audit record and a clear outcome.
- The app never submits or signs returns.
