---
date: 2026-10-20
status: proposed
---

# Independent release approval check

## Context

The release approval job shared a workflow with the Windows build, tests, and portable packaging.
Adding or removing a label repeated that product work.
The approval decision only reads the PR labels through the GitHub API.

## Decision

Keep the existing approval script in a separate read-only workflow.
Run it on PR source events and label changes.
Require its `release-gate` check alongside the product `required` check on `main`.
An `autorelease: pending` PR still needs `release: approved`.
Other PRs pass that predicate.
The script has no checkout and runs no PR code.

Run product CI on opened, synchronized, and reopened PRs.
Build unsigned portable candidates for release-please branches, and check ordinary source updates with build, tests, and dependency review.
Keep the existing CodeQL, release label guard, manual release merge, and separate signing and publication approvals.

## Verification boundary

GitHub controls event delivery, label reads, check identities, and enforcement of the ruleset.
Actionlint checks the workflow syntax.
It doesn't prove those hosted effects.
Before merging this change, check the current head's actual jobs and require both check identities in the live ruleset.
Use an unapproved tracking label on the maintenance PR to observe rejection, then remove it and observe success without another product run.
Never grant release approval as part of that check.

The binding-mode annotations preserve WinUI's existing OneTime behavior for immutable lists, constants, and enum values.
Native builds, a generated C# comparison, tests, and a portable startup check cover that external SDK boundary.
Queueing the final close request after service disposal avoids reentering WinUI's closing event when disposal completes synchronously.
Check that native window boundary with one close request on a fresh idle portable process.
NuGet audit and launcher failure propagation use native fault injection rather than a detached model of the tools.
Revisit these checks if GitHub changes its check semantics, the approval predicate changes, or the SDK changes binding or restore behavior.

## Consequences

Label changes need a small metadata job instead of another Windows build and package.
The separate required check keeps release approval binding when product CI doesn't run for labels.
This decision adds no release, tag, signing, or publication authority.
