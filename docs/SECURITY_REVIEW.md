# Initial security review

This is a planning baseline, not a completed security audit.

## Must-haves before client data is enabled
- No Supabase service-role key or other privileged secret in the desktop binary.
- Use the public/publishable project key only with server-side authorization and RLS.
- Verify active status and role for every authenticated session.
- Enforce staff-to-client assignment on the server, not only in UI filters.
- Review every Edge Function, including functions configured with JWT verification disabled.
- Preserve existing OTP/MFA and forced-password-change requirements; do not treat password success alone as proof of MFA.
- Handle refresh, expiry, revocation, sign-out, and restored sessions safely.
- Avoid logging passwords, access/refresh tokens, client documents, or sensitive personal data.
- Validate upload type, size, ownership, and destination on the server.
- Use TLS for all backend calls and protect local session material using Windows user-scoped protection.
- Tax-portal credentials are scoped by family account, member profile, tax system and GSTIN (where applicable) and stored through Windows Credential Manager; verify save/read/remove behavior on a dedicated Windows test account before production use.
- Add authorization tests for inactive users, clients, unassigned clients, staff, and admins.

## Current status
- Repository is public; avoid committing secrets, real client data, or environment-specific credentials.
- No production portal or Supabase resources have been changed.
- Desktop sign-in/OTP flow is implemented against the portal's existing OTP function, but live acceptance testing and session refresh/revocation handling remain incomplete; not production-approved.
- Windows CI build, XAML/XML validation, 15 dummy-only integration checks, startup smoke test, and Windows UI Automation navigation/safety checks have passed on the hosted Windows runner. This does not replace live acceptance testing or a dedicated Windows pilot.
- No sensitive modules should be enabled until the requirements above pass review.

## Open questions
- Confirm the actual profiles schema and canonical display-name column.
- Confirm the required Supabase MFA/AAL policy for staff and admins.
- Inventory document/query endpoints and their current authorization behavior.
- Confirm which due-date source/page should be treated as authoritative and its parsing contract.
