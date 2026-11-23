# HAI-3.3: dependency and secret checks

This PR adds two independent read-only checks; it does not deploy or modify branch rules.
NuGet and npm include transitive/development dependencies. High/Critical findings fail CI.
Unknown severities, missing reports and audit-service failures also fail; they cannot produce a clean result.
Moderate/Low findings remain visible in dependency artifacts. No advisory suppression is added.

The [gate](../scripts/check-dependency-audit.mjs) has positive and negative regression fixtures;
run `node --test scripts/check-dependency-audit.test.mjs`. NuGet JSON v1 is selected explicitly.
The npm command's exit status is checked as well as its JSON, so a failed request cannot be ignored.

Gitleaks 8.30.1 is pinned to its official Linux archive SHA256 and scans all available Git refs.
It uses full redaction and does not upload secret findings. Findings must be investigated privately;
do not copy a matching value into an issue. Rotate an actual exposed secret before history cleanup.
An allowlist, if needed, must identify an exact confirmed test fixture and be reviewed separately.
No blanket path/history exclusions or baseline bypass is included.

The existing Vitest 3 dependency graph included vulnerable tinypool versions. Upgrade only the
test runner to Vitest 4.1.12 and regenerate the npm lock file; no production UI/API code is changed.
Local evidence: 71/71 frontend tests, production build with refresh enabled, formatting and generated
client checks pass; lint has 0 errors and 25 existing warnings. npm audit now reports 0 High/Critical
(7 Moderate remain); NuGet lists 50 projects and 0 findings. The gate's 13 regression fixtures pass.
Gitleaks scanned 108 available commits with no finding before this commit. CI must verify the pushed
revision independently. This is a package/security check, not proof that staging has been upgraded.

Hiếu reviews the workflow; module owners review any dependency upgrade it requires. The agent does
not merge. Once accepted, the repo owner may add both check names to branch protection. A new
workflow cannot be considered enforced before it is merged and the ruleset is updated by the owner.

Runtime acceptance still needs HTTPS/HSTS, untrusted-forwarded-header rejection, per-source login
rate limiting, no direct API/SQL host ports, and an explicit-origin CORS policy. Existing evidence
is in the week-one test strategy; a green dependency scan does not certify those controls.

Command references: [NuGet JSON](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list),
[npm exit threshold](https://docs.npmjs.com/cli/commands/npm-audit/),
[Gitleaks CLI](https://github.com/gitleaks/gitleaks),
[pinned release](https://github.com/gitleaks/gitleaks/releases/tag/v8.30.1).
