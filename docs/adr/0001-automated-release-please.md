# ADR-0001: Automated versioning with release-please

Date: 2026-07-01 / Status: Accepted (supersedes the manual `v*`-tag-push release flow)

Until now keep-pressing had no managed version: `KeepPressing.csproj` carried no
`<Version>`, so assemblies were stamped a meaningless `1.0.0.0`, and a release meant a
human hand-pushing a `vX.Y.Z` tag from which `release.yml` derived everything. There was
no CHANGELOG, no machine-checked commit convention, and the version had no single owner.
This ADR adopts the release-please workflow already proven in the sister project
find-my-files, adapted to keep-pressing's single-C#-solution shape.

## Decision

1. **release-please owns the version, CHANGELOG, and a draft release.** Conventional
   Commits on `main` drive a bot (`googleapis/release-please-action`, SHA-pinned) that
   keeps a "Release PR" open; merging it bumps the version, updates `CHANGELOG.md`, and
   creates the GitHub Release as a **draft** (config `"draft": true`). The maintainer never
   hand-picks or hand-edits a number — they merge a PR, and the Release PR diff *is* the
   release preview. A draft release creates **no git tag yet**; `release-please.yml`
   dispatches `release.yml`, which builds + signs, attaches assets to the draft, and
   **publishes it — and publishing is what creates the `vX.Y.Z` tag** (assets before
   publish, the order [immutable releases](https://docs.github.com/code-security/concepts/supply-chain-security/immutable-releases)
   demand). `release-please-config.json` + `.release-please-manifest.json` are the config.

2. **Version stays declared in the file; the bot edits it.** The package is the **repo
   root (`.`)** with `release-type: "simple"` and a single `extra-files` `generic` updater
   keyed on an `x-release-please-version` annotation that sets `KeepPressing/KeepPressing.csproj`
   `<Version>`. The package must be the repo root (not a subdir): `extra-files` paths are
   package-relative and **cannot use `..`**, and it lets `CHANGELOG.md` live at the repo root.
   Because keep-pressing is C#-only there is **no** Rust `toml` updater and **no** `Cargo.lock`
   sync step — the two hardest parts of find-my-files's setup (ADR-0035 there) simply don't
   apply here.

3. **Conventional Commits are enforced.** Locally via a lefthook `commit-msg` hook
   (`committed`, mise-pinned); on PRs via `amannn/action-semantic-pull-request` (the
   `pr-title` gate in `ci.yml` — a squash merge makes the PR title the commit, so the title
   is what release-please reads).

4. **Dormant until a GitHub App is provisioned.** A tag pushed by the default
   `GITHUB_TOKEN` does **not** trigger other workflows (GitHub's recursion guard), so
   release-please's tag would not fire `release.yml`. Activation runs as a **GitHub App**:
   `release-please.yml` mints a short-lived, repo-scoped installation token at runtime
   (`actions/create-github-app-token`), authenticating the App by its **Client ID** from the
   `RELEASE_PLEASE_CLIENT_ID` + `RELEASE_PLEASE_PRIVATE_KEY` secrets. Those secrets live in a
   dedicated **`release-please` environment** with a `main`-only deployment-branch policy (so
   the `contents:write`+`pull-requests:write` App key can't be read by a workflow on another
   branch) and, deliberately, **no required reviewers**. With the secrets unset the job runs
   green and no-ops — the scaffolding ships now, the App lights it up later.

5. **A real release requires multiple deliberate, independent actions — defence in depth.**
   Opening the Release PR does nothing. Cutting a release then takes, in order: (a) **adding
   the `release: approved` label** — the `release-gate` check (required via branch protection)
   fails the Release PR until it's present (non-release PRs pass automatically); (b) **merging
   the Release PR** (manual; `no-automerge-on-release-pr.yml` disarms auto-merge if armed);
   (c) **approving the `sign` job** in the `release` environment; (d) **approving the `publish`
   job**, also in `release` — so the irreversible immutable-Release step has its own approval.
   `release.yml` is **dispatch-triggered** (started only by `release-please.yml` after a
   Release PR merge), never tag-triggered, so a stray/manual `git push origin vX.Y.Z` starts
   nothing — the tag is an *output* of publishing, not a trigger. The label machinery is
   hardened: `release-gate` re-evaluates on `labeled`/`unlabeled` events, `release-label-guard.yml`
   reinstates `autorelease: pending` if stripped, and all labels are declared in
   `.github/labels.json`. Automated tooling (incl. the AI agent) operates under a standing
   contract: it never merges the Release PR, arms auto-merge, pushes a `v*` tag, approves the
   `release` environment, or runs `release.yml` with `publish=true` without an explicit,
   version-named instruction.

The existing supply-chain posture is unchanged: `release.yml` still Authenticode-signs the
first-party binaries with SSL.com eSigner (now via `batch_sign` + a shared `verify-signatures`
composite action + a publish-time unsigned hard-fail gate) and writes keyless build-provenance
+ SBOM attestations.

## Rationale

- **release-please over manual `v*` tags**: the old flow *was* the human-driven shackle —
  someone picks the number and pushes the tag. The Release-PR bot is the lower-friction,
  more-recommended 2024+ workflow and gives a free CHANGELOG.
- **`simple` release-type over `dotnet`/`nuget`**: keep-pressing publishes no NuGet package;
  `simple` + a `generic` csproj updater bumps exactly the one version-bearing line with no
  package-registry assumptions.
- **Declared-and-bot-edited over git-derived (nbgv/GitVersion)**: a height/`git describe`
  version isn't Conventional-Commits-*semantic* (can't turn `feat:` into a minor bump) and
  breaks on `.git`-less source builds. The stored number costs almost nothing.

## Rejected alternatives

- **Keep manual `xtask`/`v*`-tag releases** — simplest diff, but it *is* the human-driven
  shackle this ADR removes. Rejected.
- **Nerdbank.GitVersioning / GitVersion (git-derived)** — no stored number, but
  non-composable with CC-semantic bumps and `.git`-dependent. Rejected; reconsider only if
  the csproj grows multiple version-bearing props.
- **release-please `rust`/`node`/`dotnet` strategies** — carry language/registry assumptions
  keep-pressing doesn't need; `simple` + generic updater is the minimal fit.

## Consequences

- The maintainer's release ritual becomes "write Conventional Commits, merge the Release PR."
- `KeepPressing.csproj` gains a `<Version>` line (owned by release-please); assemblies now
  carry the real version instead of `1.0.0.0`.
- New workflows appear: `release-please.yml` (dormant without the App secrets),
  `no-automerge-on-release-pr.yml`, `release-label-guard.yml`, `labels-sync.yml`; `ci.yml`
  gains `pr-title` + `release-gate`; `release.yml` moves from tag-push to dispatch-only.
- Every contributor commit must be a Conventional Commit (local hook + PR-title gate);
  `--no-verify` is forbidden.
- **First activation must be verified**: the first Release PR should bump the csproj
  `<Version>` to `0.1.0` (pinned once via `"release-as"`, removed afterward) and write
  `CHANGELOG.md`.

## Re-examination triggers

- **NuGet/Store publishing begins** → re-evaluate a real package-publish step and whether a
  registry-aware release-type fits.
- **The csproj version surface grows** (multiple version-bearing props) → reconsider
  Nerdbank.GitVersioning for the .NET side.
- **A second signable language/component is added** → revisit the single-package assumption
  (find-my-files's ADR-0035 dual-language setup is the reference).
