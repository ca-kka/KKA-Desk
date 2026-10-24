# KKA Desk delivery roadmap

This roadmap follows the security and data dependencies for each module. The work in this branch is the first user-facing slice after the validated portal sign-in/profile sync flow.

## Delivery principles

- Keep the current portal OTP flow and administrator-only, read-only profile sync intact.
- Never change production portal tables, policies, Edge Functions, or data without separate explicit approval.
- Keep client data online-only until authentication lifecycle, RLS, endpoint authorization, and staff assignment checks are reviewed.
- Use synthetic data for tests. Keep government downloads, tax filing, signing, document punching, and OTP/MFA/CAPTCHA bypass disabled.
- Require confirmation before destructive actions and retain useful audit records without copying sensitive reminder text into the audit trail.

## Phases and dependencies

| Phase | Deliverable | Depends on / exit gate |
| --- | --- | --- |
| 1. Desktop foundation and personal work queue | Refined WPF shell, accessible navigation, local personal reminders, atomic local persistence, confirmation for deletion, and unit/integration coverage. Reminders are not linked to clients and must not contain client identifiers. | No server or schema dependency. This is the current implementation slice. |
| 2. Identity lifecycle hardening | Explicit sign-out, expiry handling, refresh/revocation behavior, session cleanup, and MFA/AAL state handling while retaining the existing OTP challenge behavior. | Review actual portal auth contract and policies; test only with authorized non-production accounts. |
| 3. Read-only operational dashboard and directory | Authoritative public due-date source with visible sync freshness, and a directory using approved server-side role/assignment checks. Keep the existing admin profile sync read-only. | Phase 2; verify exact source URL/contract, RLS, active-role checks, and staff assignment behavior. |
| 4. Query and assigned-work modules | Server-backed query inbox, assignment/status actions, and audit history with explicit confirmation where an action changes shared state. | Phase 2 and endpoint-by-endpoint review of authorization, audit behavior, and failure/retry semantics. No schema changes by default. |
| 5. Documents | Authorized listing and download, safe preview, upload validation/progress, version history, and drag/drop. | Phase 2–4 authorization review; confirm storage paths, size/type limits, ownership checks, and server enforcement first. |
| 6. Administration and operational controls | Role-gated user/team settings, diagnostics, audit views, and admin tools. | Verified admin role/MFA, reviewed APIs, and explicit approval for any production write capability. |
| 7. Tax workflow preview and pilot | Profile-scoped planning and human-reviewed processing. Keep downloads, filing, signing, OTP/MFA/CAPTCHA automation, and punching disabled until separately approved. | Phases 2–6 plus documented authorization, credential-vault pilot, file-integrity checks, and explicit business approval. |
| 8. Release and rollout | Signed installer, update notifications requiring user confirmation, accessibility/security review, and limited pilot. | All previous gates, successful Windows CI, authorized acceptance testing, and release approval. |

## Current slice acceptance criteria

- Staff can add, search, filter, complete/reopen, and delete generic personal reminders.
- Reminders stay in the current Windows user's local app-data folder; writes are atomic, malformed data is not silently replaced, and deletion requires confirmation.
- The queue has no client selector, client fields, attachments, or server sync; its UI tells staff not to enter client names, identifiers, or document details.
- The shell provides consistent loading, empty, error, keyboard, focus, and screen-reader states; the existing portal login/sync and tax safety controls remain available.
- Automated tests use isolated temporary files and dummy values only. CI runs the new checks on pull requests as well as main-branch builds.

## Release gates

Do not enable client data modules until authentication lifecycle, MFA, RLS, Edge Function authorization, and staff-client assignment rules have been reviewed and tested. Do not deploy or alter production schema/data without explicit approval. Keep destructive/shared actions auditable and explicitly confirmed.
