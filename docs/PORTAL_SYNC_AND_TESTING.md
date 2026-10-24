# Portal sync adapter and testing boundaries

KKA Desk now has a read-only adapter for the existing KKA Supabase project. It queries the portal's existing `profiles` and `client_account_members` relations, including the nested client profile fields used by the family-management UI. Requests use a user access token, the publishable key, HTTPS, and the portal's existing RLS policies. No service-role key is used.

## Important limitation

The desktop UI now calls the portal's existing `client-login-otp` function and supports its email OTP challenge, then uses the returned user session for a read-only profile sync. Session refresh/revocation handling and live acceptance testing remain outstanding. Expired sessions and authorization failures must fail closed.

## Family model

- `account_id` is the family/account grouping key.
- `client_id` is the individual member profile key.
- Each active association remains a separate profile row; account ID is never substituted for profile ID.
- Tax settings and documents must be scoped to the selected member profile and later to each GST registration.
- The adapter is read-only and does not alter the portal or production database.

## CI safeguards

The Windows workflow treats compiler warnings as errors, parses XAML/project XML, rejects merge-conflict markers, and runs CodeQL C# security analysis. This reduces syntax and static-analysis regressions but cannot guarantee zero runtime defects. CodeQL is configured for local analysis because GitHub code-scanning upload is not enabled for this private repository. Sentry/CodeAnt is not connected; no Sentry DSN or CodeAnt app integration was available in the connected workspace.


## Current UI hand-off

After a successful administrator sign-in and sync, select one row in the profile grid and choose **Use selected profile for tax automation**. The tax preview is prefilled with the family/account ID, member profile ID, name, PAN and the profile's current GSTIN. These identity fields are read-only when loaded from portal sync. Tax settings and period selections are still preview-only; no government portal downloads or document punching are enabled.

The current client schema has one GSTIN field per client record. Multiple GST registrations per profile require a later, explicit registration-level model; do not infer or copy registrations from another family member.

## Acceptance checklist before enabling any download

1. Sign in with an active administrator and complete the portal's existing email OTP challenge if required.
2. Confirm inactive accounts and inactive member profiles are excluded.
3. Compare family/account ID, member profile ID, display/legal name, PAN and GSTIN with the portal admin family screen.
4. Open tax preview from two different family members and confirm IDs and tax identifiers do not cross over.
5. Confirm a member without PAN has Income Tax disabled and a member without GSTIN has GST disabled by default.
6. Confirm expired/invalid tokens and non-admin roles fail closed.
7. Confirm no database rows are created or modified by sync or preview.
8. Run the Windows build/XAML validation and local CodeQL analysis after every change.

Live sign-in/OTP acceptance testing still requires a human operator with authorized KKA credentials. Do not commit credentials, OTP values, access/refresh tokens, or Sentry DSNs.


## Local credential vault

The tax profile UI now offers separate Income Tax and GST credential save/remove actions. Credentials are keyed by family/account ID, member profile ID, tax system and GSTIN for GST, then stored as a Windows Credential Manager generic credential under the signed-in Windows user. The app does not send these secrets to Supabase and does not write them to application files or logs. Credential save/remove is enabled only when the tax window was opened from a portal-synced profile with the corresponding PAN/GSTIN. This is an initial storage layer only: automated government-portal login/download has not been enabled, and the vault still requires a Windows pilot and security review.
