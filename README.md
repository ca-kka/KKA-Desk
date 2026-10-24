# KKA Desk

## Download KKA Desk for Windows

**[Download the latest tested Windows x64 EXE](https://github.com/ca-kka/KKA-Desk/releases/latest/download/Kka.Desk.exe)** · [View release notes and SHA-256 checksum](https://github.com/ca-kka/KKA-Desk/releases/latest)

This is a self-contained Windows preview. Download the EXE directly from the latest release; you do not need to download source code or install the .NET runtime. Because this repository is private, you must be signed in to GitHub with access to KKA-Desk.

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

GitHub Actions builds on a Windows runner with warnings-as-errors, XAML/XML parsing, merge-conflict checks, Windows Credential Manager isolation/cleanup tests using dummy credentials, mocked OTP and staff-role authorization tests, an executable startup smoke test, and CodeQL C# analysis. Successful main-branch builds publish the tested EXE and checksum as a GitHub Release.

## Not production-ready yet

The application compiles in GitHub's Windows CI and validates XAML/XML. The portal-sync path has not yet passed live credential/OTP acceptance testing on KKA's actual account, and credential-vault save/remove operations still need an authorized Windows pilot. Due-date synchronization, document handling, tax automation execution, and the remaining modules are not yet connected. Do not use it with production client data until security review and acceptance testing are complete.


## Download a ready-to-run Windows preview

Every push to `main` and manual workflow run attempts to create a self-contained Windows x64 EXE package. You do not need to install the .NET runtime to run that package.

1. Open the [latest KKA Desk release](https://github.com/ca-kka/KKA-Desk/releases/latest) while signed in to an account with access to this private repository.
2. Download `Kka.Desk.exe` directly, or download the complete package from the linked GitHub Actions run.
3. Optionally verify the EXE against `SHA256SUMS.txt` from that same release before running it.

GitHub Releases keep the latest published preview available until replaced by a later successful build. If the latest build fails, the release step is skipped; check the [Actions history](https://github.com/ca-kka/KKA-Desk/actions) rather than assuming the older release contains the newest code.

The workflow performs a warnings-as-errors build, XML/XAML validation, conflict-marker checks, dummy-only Windows Credential Manager save/isolation/removal tests, mocked OTP and role-gate tests, a self-contained single-file publish, an 8-second process-startup smoke test, and CodeQL analysis. Release publication waits for both the Windows test job and CodeQL job to pass. These checks do not replace live acceptance testing of portal OTP, Windows Credential Manager behavior, profile authorization, or tax portal downloads.
