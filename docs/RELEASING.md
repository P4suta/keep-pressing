# Releasing

Conventional Commits on `main` drive release-please's version proposal.
The PR title becomes the squash commit message.
The existing [release design](adr/0001-automated-release-please.md) and the [independent approval check](adr/independent-release-approval-check.md) describe the decisions.

## Release flow

1. [release-please](../.github/workflows/release-please.yml) opens or updates a Release PR.
   It changes `KeepPressing/KeepPressing.csproj` and `CHANGELOG.md` together.
   Keep the `x-release-please-version` marker on the `<Version>` line.
2. Review the proposed version, changes, and unsigned portable CI artifact.
   The version manifest tracks the last release.
   The config omits the `release-as` override.
3. The maintainer adds `release: approved` to approve that exact release.
   The `release-gate` check rejects an `autorelease: pending` PR without that approval label.
4. The maintainer merges the Release PR manually.
   release-please creates a draft release and its `vX.Y.Z` tag through `force-tag-creation`, then dispatches [release.yml](../.github/workflows/release.yml).
5. The workflow builds the portable bundle and CycloneDX SBOM.
   Its `sign` job needs approval in the `release` environment before it uses eSigner credentials.
6. The `publish` job needs a separate approval in that environment.
   It verifies signatures, prepares the ZIP and checksum, adds provenance and SBOM attestations, attaches the assets, and publishes the immutable release.

Reviewing or merging an ordinary maintenance PR authorizes none of those release approvals.
Automated tooling needs an explicit, version-named instruction before merging a Release PR, creating a release tag, signing artifacts, approving the release environment, or publishing.

## Required checks

`main` requires both `required` and `release-gate` from GitHub Actions.
Product [CI](../.github/workflows/ci.yml) runs on opened, synchronized, and reopened PRs.
It builds and tests the app, audits dependencies during restore, and checks the dependency diff and PR title.
Portable candidate packaging runs for release-please branches.
CodeQL still runs on `main` and its schedule.

The separate [Release approval workflow](../.github/workflows/release-gate.yml) runs on those source events and on label changes.
It reads PR labels with a read-only token and never checks out PR code.
Label changes update `release-gate` without repeating the Windows build or replacing the product check result.
Keep both check names required in the main ruleset.

## Release safeguards

- `release-label-guard.yml` restores `autorelease: pending` if someone removes it from an open release-please PR.
  Removing `release: approved` remains a valid way to withdraw approval.
- `no-automerge-on-release-pr.yml` disables automatic merging on release-please branches.
- `.github/labels.json` defines the labels, and `labels-sync.yml` creates or updates them without deleting other labels.
- `release.yml` accepts explicit dispatches.
  Tag pushes don't start it.
- Both signing and publication use the protected `release` environment.
- Publication verifies the signer, certificate chain, and timestamp before uploading release assets.

## Credential setup

The `release-please` environment accepts only `main` and requires no reviewers.
Its `RELEASE_PLEASE_CLIENT_ID` and `RELEASE_PLEASE_PRIVATE_KEY` secrets identify a GitHub App installed on this repository.
The App token requests only Contents and Pull requests write access.
The job uses its own Actions write permission to dispatch the release build.
Missing App secrets stop release proposals.
The workflow emits a notice instead.

The protected `release` environment holds `ES_USERNAME`, `ES_PASSWORD`, `CREDENTIAL_ID`, and `ES_TOTP_SECRET` for the existing eSigner setup.
Keep its maintainer reviewer and approval boundary for both `sign` and `publish`.
Keep these credentials out of repository-wide secrets.
