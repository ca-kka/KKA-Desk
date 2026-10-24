# KKA Desk

Private Windows-first desktop workspace for Kamal Kuldeep & Associates (KKA).

## Current milestone

The repository contains the initial WPF solution and KKA-branded workspace shell, plus architecture, phased delivery, and security guardrail documents. The shell is still a **non-production foundation preview**. A portal-sync window supports the existing KKA sign-in/OTP gate and loads active family/profile associations for administrators. Selected profiles can be passed into the tax preview, where profile-specific Income Tax and GST credentials can be saved to Windows Credential Manager; government portal downloads and document punching remain disabled.

## Planned first release

- Dashboard with automatically synchronized important due dates from ca-kka.com
- Portal-compatible sign-in/OTP and administrator-only profile synchronization (initial integration; requires live acceptance testing)
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

GitHub Actions builds on Windows with warnings-as-errors, XAML/XML parsing, merge-conflict checks, and CodeQL C# analysis.

## Not production-ready yet

The application compiles in GitHub's Windows CI and validates XAML/XML. The portal-sync path has not yet passed live credential/OTP acceptance testing on KKA's actual account, and credential-vault save/remove operations still need an authorized Windows pilot. Due-date synchronization, document handling, tax automation execution, and the remaining modules are not yet connected. Do not use it with production client data until security review and acceptance testing are complete.
