# KKA Desk delivery plan

## Phase 0 — Foundation
- Establish the private repository and Windows-first WPF solution.
- Create the KKA-branded sign-in and workspace shell.
- Configure Windows CI to build the solution.
- Keep production systems untouched.

## Phase 1 — Identity and authorization
- Verify the actual Supabase profile schema and role values.
- Implement shared-identity sign-in without bypassing the portal's OTP/MFA or password-change gates.
- Support MFA/AAL requirements explicitly; fail closed until verification is complete.
- Confirm active status, staff/admin roles, token refresh, sign-out, and session restoration.
- Review RLS and every required Edge Function before data access.

## Phase 2 — Read-only operational modules
- Dashboard and due-date sync from the firm's public website.
- Client directory, scoped by admin/staff assignment rules.
- Query inbox and queue with auditable authorization.
- Display sync status and stale-data warnings.

## Phase 3 — Documents
- Document listing and secure download using approved endpoints.
- PDF viewer, drag-and-drop upload, upload progress, and version history.
- Validate file type, size, ownership, assignment, and access at the server boundary.

## Phase 4 — Admin and hardening
- Admin-only operational controls and audit views.
- Session expiry, update notifications, diagnostics, logging, and accessibility.
- Threat model, dependency review, permission tests, and security review.

## Phase 5 — Packaging and pilot
- Windows installer and signed release process.
- User-confirmed updates; no silent installation.
- Pilot with test accounts and non-production data.
- Production use only after explicit approval and successful acceptance testing.

## Release gate
Do not connect sensitive client data or deploy backend changes until authentication, MFA, RLS, Edge Function authorization, and staff-client assignment checks have been reviewed and tested.
