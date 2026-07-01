# Releasing

keep-pressing versions itself from Conventional Commits — there is no manual
version bump. This page covers how a release happens and how to **activate** the
automation. Design rationale: [ADR-0001](adr/0001-automated-release-please.md).

keep-pressing is a single C# solution, so the setup is deliberately simple: one
csproj holds the version, and release-please bumps it. (There is no separate
engine, lockfile, or build channel to keep in sync.)

## How a release happens (once activated)

1. Conventional Commits land on `main` (squash-merged PRs; the PR title is the commit).
2. [`release-please`](../.github/workflows/release-please.yml) keeps a **Release PR**
   open that bumps `KeepPressing/KeepPressing.csproj`'s `<Version>` (the `x-release-please-version`
   line) and updates [`CHANGELOG.md`](../CHANGELOG.md). The version is derived from the
   commits: `feat:` → minor, `fix:`/`perf:` → patch, `!` / `BREAKING CHANGE:` → major.
3. **Add the `release: approved` label** to the Release PR. Until it's there the
   `release-gate` check fails the PR (so a release is never an accidental merge).
4. **Merge the Release PR.** release-please creates the GitHub Release as a **draft**
   (config `"draft": true`) and **materializes the `vX.Y.Z` git tag at the release
   commit** (`"force-tag-creation": true`), then dispatches
   [`release.yml`](../.github/workflows/release.yml). The tag is forced because a draft
   release otherwise has no git tag, so in the same run release-please can't see the
   just-cut release and would open a spurious "next" Release PR re-listing already-shipped
   commits.
5. `release.yml` runs: build → **sign (approve in the `release` environment)** →
   **publish (approve again)**. The publish step attaches the signed `keep-pressing-<version>-win-x64.zip`
   + its `.sha256` + the CycloneDX SBOM to the draft (which already carries the tag from
   step 4) and **publishes it**. Assets land *before* publish, the order
   [immutable releases](https://docs.github.com/code-security/concepts/supply-chain-security/immutable-releases)
   require (a published immutable release can't gain assets afterward).

You never hand-pick or hand-edit a version. The Release PR diff *is* the preview.

## Release safety (defence in depth)

Cutting a real, immutable release is deliberately gated by several independent steps
(ADR-0001), so an ambiguous instruction can't ship one by accident:

- **Label gate** — the Release PR can't merge until you add `release: approved` (`release-gate`
  in `ci.yml`). Adding or removing the label re-evaluates the gate automatically (CI runs
  on `labeled`/`unlabeled` events), so the check flips within seconds of approval. Make
  `release-gate` a **required status check** in branch protection for the gate to be binding.
- **Label guard** — `autorelease: pending` is release-please's tracking label; without it the
  merged PR is never tagged/released. It can't be locked in GitHub, so
  `release-label-guard.yml` reinstates it if it's removed from a release-please PR. (The human
  `release: approved` label is deliberately *not* guarded — removing it to un-approve is fine.)
  Labels themselves are declared in `.github/labels.json` (synced by `labels-sync.yml`).
- **Manual merge** — the Release PR is never auto-merged. `no-automerge-on-release-pr.yml`
  turns auto-merge back off if it's ever armed on a release-please PR (normal PRs unaffected).
- **No tag-triggered cascade** — `release.yml` is started only by an explicit dispatch from
  `release-please.yml`, never by a tag push, so a stray or manual `vX.Y.Z` tag starts nothing.
- **Two environment approvals** — both the `sign` and `publish` jobs pause on the `release`
  environment (reviewer = the maintainer); the irreversible publish has its own approval.
- **Agent contract** — automated tooling (incl. the AI assistant) will not merge the Release PR,
  push a `v*` tag, approve the `release` environment, or run `release.yml` with `publish=true`
  without an explicit, version-named instruction.

## Activation (one-time)

release-please ships **dormant**: with the App secrets unset, `release-please.yml` runs
green and no-ops. It runs as a **GitHub App** because a tag pushed by the default
`GITHUB_TOKEN` does **not** trigger `release.yml` (GitHub's workflow-recursion guard) — so
the tag must be pushed by a different identity. The workflow mints a short-lived
installation token at runtime via `actions/create-github-app-token`.

1. **Create a GitHub App** (org or personal). Repository permissions: **Contents:
   Read & write** and **Pull requests: Read & write**. No webhook needed.
2. **Install** the App on the `keep-pressing` repo.
3. Generate a **private key** (`.pem`) for the App and note its **Client ID** (shown
   at the top of the App's **General** settings page, e.g. `Iv…`).
4. **Create an environment** for the credential:
   Settings → Environments → **New environment** → name it **`release-please`**.
   - **Deployment branches and tags** → **Selected branches** → add **`main`** only.
   - **Do NOT add required reviewers** (release-please must run unattended).
5. In that environment's **Environment secrets**, add:
   - `RELEASE_PLEASE_CLIENT_ID` = the App's Client ID
   - `RELEASE_PLEASE_PRIVATE_KEY` = the full `.pem` contents (paste the whole file,
     `-----BEGIN…` through `…END-----`; multi-line is fine)
6. Ensure the **`release`** environment already holds the SSL.com eSigner signing secrets
   (`ES_USERNAME` / `ES_PASSWORD` / `CREDENTIAL_ID` / `ES_TOTP_SECRET`) — the `sign` and
   `publish` jobs use it as their approval gate (reviewer = the maintainer).

### Why an environment, not a repository secret

A **repository** secret is readable by a workflow run on *any* branch. The App private key
carries `contents: write` + `pull-requests: write`, so we scope it: the `release-please` job
declares `environment: release-please`, and the environment's branch policy (`main` only)
means only the main-branch release-please run can read the key. This mirrors the signing
secrets in the `release` environment, with one deliberate difference: **release-please's
environment has no required reviewers** (the human gate is merging the Release PR).

> A fine-grained **PAT** with the same two permissions also works — drop the
> `create-github-app-token` step and pass the PAT directly as `token:`. The App is
> preferred (no human-tied credential; the token is short-lived and repo-scoped).

### First release: the `release-as` pin

`release-please-config.json` sets `"release-as": "0.1.0"` to pin the **first** release to
`0.1.0` — without it, release-please treats a first release from a `0.0.0` manifest as the
initial `1.0.0`, wrong for a pre-1.0 project. **After the first release is cut, remove
`"release-as"`** in a follow-up PR (leaving it pins every future release to 0.1.0). From then
the manifest tracks the real version: a `feat:` proposes the next minor, a `fix:` the next patch.

### Verify the first Release PR

On the **first** Release PR, confirm the diff bumps both:

- `KeepPressing/KeepPressing.csproj` `<Version>` → `0.1.0` (the `x-release-please-version` line)
- `CHANGELOG.md`

If `<Version>` doesn't move, check that the marker comment `<!-- x-release-please-version -->`
is still on the same line as `<Version>…</Version>` (the `generic` updater keys on it).
