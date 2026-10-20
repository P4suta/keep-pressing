---
date: 2026-10-20
status: proposed
---

# Windows NuGet dependency snapshots

## Status

Proposed.

## Context

GitHub's automatic NuGet submission stopped after its migration branch was deleted.
Its graph retained old Windows runtime packages after a verified dependency update.
Static project files omit SDK runtime downloads.
CodeQL already builds the solution on Windows for main updates and weekly analysis.

## Decision

Reuse that build to generate SPDX 2.2 metadata with Microsoft SBOM Tool `4.1.5`.
Check the downloaded executable's SHA256 before execution.
Limit component detection to NuGet and scan each package-bearing project separately.
The core library has no external packages.
Project-reference packages are represented by the referenced project's snapshot.

Transfer only the four generated documents to a separate submission job.
Keep product builds read-only.
The submission job has no checkout and executes no product or PR code.
Require all four expected documents to have their project name, SPDX version, and nonempty NuGet package references before making any submission.
SBOM Tool can return success for an empty scan, so its exit code alone is insufficient.

Use the immutable SPDX submission action with each original project file as its manifest path and a stable project correlator.
Its default GitHub context binds submissions to the current workflow's source SHA and ref.
Manual snapshots for those paths replace stale automatic data in GitHub's dependency graph.
Keep native NuGet audit and static PR dependency review.

The component-detection submission action downloads its latest scanner regardless of its action pin.
Use a pinned producer and publisher instead.
A new build just for submission would repeat work already performed by CodeQL.

## Verification boundary

The scanner's NuGet interpretation and GitHub's snapshot ingestion are external semantic boundaries.
Actionlint and Zizmor check the workflow's syntax and permission surface.
Native scans check current runtime downloads, direct and transitive dependencies, and build-only analyzers.
Run the workflow's actual validation block on genuine generated documents, missing input, and an empty scan.
The latter two must fail before the first submission step.
After merge, inspect the real main workflow, exported graph, and security findings.
These checks establish the observed integration behavior rather than a proof of the upstream services.
Revisit the boundary if the project inventory, scanner format, or GitHub submission semantics change.

## Consequences

Each existing Windows CodeQL build gains a bounded metadata scan and one metadata-only submission job.
PRs gain no build campaign or write access.
Maintainers must update the scanner's version and checksum together.
This change grants no release or signing authority.
