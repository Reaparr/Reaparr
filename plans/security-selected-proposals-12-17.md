# Selected security proposals — 2026-11-16

## Decisions and boundaries

- Push completed: `origin/secruity-audit` includes `8c91c1998`, `af7ea88ba`, `3d00bc2c7`.
- Completed fixes: 12, 13, and 16. Proposal 14 was subsequently declined; do not implement or repropose it. Proposal 17 remains validation-only.
- Completed validation-only proposal 17 with dummy-proxy hosted evidence. No production authentication policy was changed; a deployed external-authorization bypass remains unconfirmed.
- Declined: 8, 9, 10, 11, 14, 15, 18, 19, 20. Recorded in IMPROVEMENTS.MD; do not reopen.
- Latest user instruction: finish the remaining selected implementation and validation here. Use normal local project tests and dummy-user/browser verification; permission granted explicitly on 2026-11-17. No automatic push, live identity-provider changes, external security probes, or releases.
- The previously staged genre-migration test was committed unchanged as `26cf5f38b` and pushed before transfer.
- Do not change README or CHANGELOG files.
- Completed the remaining approved scope on Unraid in `/workspaces/Reaparr-secruity-audit`, branch `secruity-audit`: fix 16 and validation-only 17. Do not implement declined proposal 14 or change production proxy authentication without further approval.

## Verification prerequisites

For proposal 12, use existing Actionlint and a focused independent source review; do not expand verification tooling. Actionlint passed for both changed release workflows. The earlier capability-only sandbox attempt was blocked before execution; no target code ran and no permission configuration changed. Live GitHub publication and end-to-end release authentication are not exercised by this check.

## 12 — Scope publishing credentials

Replace publishing-token checkout authentication with the job token and `persist-credentials: false`; restrict checkout token permission to read. Preserve full history, refs, package lifecycle hooks, and existing semantic-release GH_TOKEN authentication at its release step. Resolve exact action-token permission syntax from primary docs. Verify changed workflows with maintained Actionlint route, source/primary library auth trace, and bounded nonpublishing authentication proof where available. Obtain independent security review before separate commit.

Completed as local commit `83e14ea82`. Both release workflows use read-only job-token checkout with `persist-credentials: false`; publishing `GH_TOKEN` remains scoped to semantic-release. Actionlint passed for both workflows, and independent review found no required corrections. No live release or end-to-end GitHub authentication was exercised. No additional push occurred.

## 13 — Pin vpk CLI

Choose one reviewed, compatible exact vpk version and invoke the same version in packaging and upload, preserving existing tools/Build PATH contract. Prefer the smallest existing-compatible pin over an unnecessary second invocation convention. Verify workflow syntax and pin resolution using existing local tools/dependencies without installing or fetching dependencies. Report unavailable platform checks; resolve required verification before editing. Separate review and commit.

Completed as local commit `82b3e5e3e`: both global vpk install steps pin version `1.2.161`, matching the existing local tool. NuGet confirms the stable listed package; metadata confirms .NET 9 support matching CI setup. Actionlint passed and independent review found no required corrections. No installation, package build, upload, or publication was run for this configuration-only fix.

## 14 — Require fresh authentication for credential replacement

Map backend contract, user service, frontend form/store/generated callers, and real-auth regressions. Require current local password or a specifically approved fresh-authentication route before either username/password change. Resolve the proxy-user flow before editing. Preserve normal authenticated operations and existing security-stamp revocation. Required proof: wrong/missing step-up leaves credentials unchanged; valid step-up changes intended fields; stale-cookie behavior remains correct; actual browser form behavior. Independent authentication review and separate commit.

Declined before implementation on 2026-11-17. After learning that verified external step-up needs a real provider-specific integration, the user asked to skip this fix because its scope had grown beyond their expectations. No credential request, service, frontend, or test code was changed for this proposal.

## 16 — Private thumbnail responses

Match the maintained photo endpoint's private/client response-cache and Cookie variation contract while preserving existing duration/query variation, passive MIME protection, and ETags. Add meaningful response-level regression proof. Run targeted backend tests and CodeScene review on changed production source. Separate review and commit.

Hosted initial proof found `Private == false` under real-cookie authentication, but primary-source tracing established that every-request security-stamp validation renews cookies and ASP.NET deliberately overrides cache headers with stronger `no-cache,no-store`. This is not a shared-cache leak. The revised hosted cases prove private/max-age/Cookie policy under the existing non-renewing TestScheme and preserve no-store under actual cookie authentication, plus conditional 304 and anonymous rejection. No authentication behavior was weakened. The default build exhausted process/thread resources; bounded compilation succeeded after a one-line prerequisite correction committed as `a4c7c1162`. The test MCP disconnected, so direct native TUnit DLL execution is used. No C# language server is configured here. CodeScene review is unavailable because this machine lacks authentication and its login attempt timed out.

The hosted conditional-response case exposed a second concrete defect: assigning 304 without completing the FastEndpoints response produced 204 on the wire. The thumbnail handler now uses `Send.NotModifiedAsync(ct)` to preserve the intended 304, ETag and cache headers while retaining cookie-renewal no-store protection. Production changes remain limited to thumbnail cache configuration and this conditional response completion.

Completed as local commit `1522d0026`. Final hosted proof: five cases passed, zero failed/skipped, covering nonrenewing private three-day 200/304 responses with Cookie variation, actual-cookie no-store preservation, anonymous rejection, exact upstream bytes/routing, and non-cacheable failures. Independent final review found no findings. No real Plex, CDN, or browser process was required; the actual ASP.NET/FastEndpoints hosted HTTP pipeline and dummy-cookie clients exercised this backend header contract.

## 17 — Validate proxy identity lifecycle

Validation scope: trusted header -> persistent local Identity cookie -> requests without any assertion, with header authentication either enabled or disabled -> local password change -> rejection of the original ticket. The hosted fixture uses loopback as a dummy trusted proxy and actual ASP.NET cookie validation; no production identity provider or proxy is contacted. There is no source-defined per-request external authorization or provider revocation contract, so a deployed external-auth bypass cannot be confirmed from local cookie reuse alone. A future request-bound design would need explicit authorization and a deployment contract; no production authentication change is made here.

Completed local validation: five `HeaderAuthenticationPipelineIntegrationTests` cases passed, zero failed/skipped. Both enabled and disabled header-authentication settings allow the original header-issued persistent cookie to authenticate later cookie-only requests. A local password change preserves user identity, changes its security stamp, and rejects the original ticket for protected reads and credential mutations. Fresh anonymous requests fail; fresh password login succeeds. Existing unknown-user and untrusted-proxy rejection cases also pass. Independent source review found no findings. Actual external-provider revocation, backend reachability, and deployed proxy ACL enforcement remain outside this proof.

Validation coverage committed separately as `ee1468c7a`. Proposal 14's explicit rejection was recorded in `79e0c11c2`; no credential-change implementation was introduced.

Final verification used the repository's compiled TUnit application after the test MCP disconnected. Build concurrency was bounded to avoid the host's process/thread ceiling; no permanent runner settings, package versions, or authentication settings were changed. The two final focused classes each passed five cases. No C# language server is configured; compilation supplied error checking. CodeScene review was attempted but unavailable due to missing authentication and a timed-out login. No automatic push or real release was performed.

## Completion

The approved remaining scope is complete: thumbnail privacy and conditional-response fix `1522d0026`, proxy lifecycle validation `ee1468c7a`, prerequisite compilation correction `a4c7c1162`, and skipped step-up decision `79e0c11c2`. Final focused tests passed ten cases in total and independent review found no findings. These commits remain local; no push was requested after transfer. The remaining external-proxy facts and unavailable CodeScene/C# language-server checks are disclosed above.
