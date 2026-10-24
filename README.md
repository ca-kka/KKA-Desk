# KKA Desk

Private Windows-first desktop workspace for Kamal Kuldeep & Associates (KKA).

## Current milestone

The repository contains the initial WPF solution and KKA-branded workspace shell, plus architecture, phased delivery, and security guardrail documents. The current screen is a **non-production foundation preview**: modules are navigation placeholders and no client data is fetched.

## Planned first release

- Dashboard with automatically synchronized important due dates from ca-kka.com
- Staff/admin authentication using existing KKA identities
- Documents, queries, directory, queue, and admin modules
- PDF viewing and drag-and-drop
- Windows installer with update notifications and user-confirmed installation

## Security boundaries

- Staff may access only assigned clients; admins may access all clients subject to policy.
- Client data requires an online connection.
- Never ship Supabase service-role credentials in the desktop application.
- Audit RLS, Edge Functions, role/active checks, assignment checks, and MFA/AAL requirements before connecting sensitive modules.
- Do not modify production portal or Supabase resources without explicit approval.

## Build

Open Kka.Desk.sln in Visual Studio on Windows with the .NET 10 SDK and Windows Desktop development components, or run:

```powershell
dotnet restore Kka.Desk.sln
dotnet build Kka.Desk.sln --configuration Release
```

GitHub Actions is configured to build on Windows for pushes and pull requests to main.

## Not production-ready yet

The application has not yet been compiled or tested in a Windows environment. Authentication, MFA, backend authorization, due-date synchronization, document handling, and the remaining modules are not yet connected. Do not use it with production client data until security review and acceptance testing are complete.
