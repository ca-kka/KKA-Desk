# KKA Desk

Private Windows-first desktop workspace for Kamal Kuldeep & Associates (KKA).

## Project status

This repository is the dedicated home for KKA Desk. The initial WPF source scaffold has been prepared separately and is awaiting its full source import and Windows build validation. This repository does not yet contain the application implementation.

## Planned first release

- Dashboard with automatically synchronized important due dates from ca-kka.com
- Staff/admin authentication using existing KKA identities
- Documents, queries, directory, queue, and admin modules
- PDF viewing and drag-and-drop workflows
- Windows installer with update notifications and user-confirmed installation

## Security boundaries

- Staff may access only clients assigned to them; admins may access all clients subject to policy.
- Client data requires an online connection.
- Never ship Supabase service-role credentials in the desktop application.
- Audit RLS, Edge Functions, role/active checks, assignment checks, and MFA/AAL requirements before connecting sensitive modules.
- Do not modify production portal or Supabase resources without explicit approval.

## Build status

Not yet compiled or tested on Windows. Do not use with production client data until authentication, MFA, authorization, and endpoint reviews are complete.
