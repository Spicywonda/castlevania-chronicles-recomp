# Automatic launcher updates

Date: 2026-10-10
Status: Functional flow approved; technical specification awaiting review.
Repository: Spicywonda/castlevania-chronicles-recomp

## Approved outcome

The ROM selector checks GitHub for updates at startup, downloads a newer normal
release automatically, and installs it when no game is running. Show progress
and a Later action. Restart the launcher afterward. Preserve the user's disc,
saves, controls and settings. Offline users can continue playing. The interface
remains English on Windows x64, macOS arm64 and Linux x64.

Future production publications use normal releases, not prereleases. Existing
0.3.9 installations need one manual download to obtain this capability. Changing
an existing release's label does not add the updater to its binaries.

## Approach

Use the existing portable packages with a shared managed updater in LauncherCore.
The single-file Play executable also provides an early, headless updater mode;
this mode runs before importing native graphics or creating a window. Copy that
executable to a private temporary directory before launching it as the helper,
so Windows can replace the installed executable after the launcher exits.

Alternatives considered: opening the release page is simpler but does not meet
automatic installation; adopting a platform-specific updater framework would
add different installers and migration behavior to the existing portable builds.
The shared helper keeps the current archive distribution and application paths.

## Release discovery and versioning

- Use a single checked-in application version shared by the launcher, game build
  banner, package metadata and release tooling. First planned updater release:
  0.3.10, subject to the repository's tags at publication time.
- Query the fixed public repository's latest-release API over HTTPS without a
  token. Require a non-draft, non-prerelease release with a supported numeric
  version tag and strictly newer version. Never downgrade or select another repo.
- Match the exact OS/architecture asset name. Require its uploaded state, size
  and SHA-256 metadata. Reject incomplete releases and unsupported architectures.
- Check asynchronously once per launch with a short timeout and cancellation.
  Cache ETag/last successful metadata for conditional requests. A timeout, rate
  limit, malformed response or offline state leaves Play available.
- Treat release text as plain text. Do not execute commands or follow paths from
  release descriptions. A release-page link is optional and fixed to GitHub.

## User flow

A compact update area in the ROM selector displays current version and status:
Checking for updates, Downloading update (with progress), Update ready,
Installing update, or a concise failure with Retry/Open logs.

Discovery and download do not disable Play. Later suppresses automatic install
for this launcher session. If the user starts the game, installation waits until
the game exits. Once verified and idle, show a short visible restart countdown
with Later; entering installation disables Play and ROM/card-changing actions.
No update UI is added to the in-game F1 menu.

Check for updates supports manual retry. Closing the launcher cancels unfinished
network activity. No detached background download continues after closing.

## Download and archive validation

Download to a unique .partial file under the per-user update cache; verify length
and SHA-256 before renaming it as a completed download. Reject mismatches without
touching the installation. Do not describe SHA-256 as a publisher signature: the
trust source is the fixed GitHub repository over HTTPS.

Extract to staging, never into the installation. Reject absolute paths, parent
traversal, symlinks/hardlinks, device entries, duplicate or case-colliding paths,
and oversized entries/archives. Bound metadata, download and extracted sizes.
Only accept the expected package root and platform layout. Preserve Unix execute
permissions. Ignore known macOS ZIP metadata outside the expected app root.

Each new package includes an installation manifest listing version, RID and
owned files. Cross-check every managed file against the manifest before install.
The manifest must not target saves, settings, disc images or packs. macOS must
also pass verification of the complete staged app's code signature.

## Installation transaction and recovery

Use an installation-specific lock shared by the launcher, direct game executable
and updater. Game execution holds a lease; installation requires exclusive access.
This covers both Play and direct executable launches and multiple launcher
instances. The install phase and game-start action cannot race.

The helper receives a locally generated transaction file, not shell command text.
Use ProcessStartInfo.ArgumentList; validate canonical paths, expected version,
installation identity and staging ownership again inside the helper. Wait for the
original launcher to exit before replacing anything, with a bounded timeout.

Windows/Linux: update only manifest-owned files. Back them up, journal every
replacement/removal and retain unknown user files. Obsolete files can be removed
only if the old manifest owned them. File replacement uses temporary siblings and
renames. Do not remove or recursively overwrite the installation directory.

macOS: replace the complete signed .app from staging using sibling rename/backup,
keeping the existing app path. Do not modify the signed bundle after verification.
If extra user files are detected inside the bundle, stop with a clear relocation
instruction rather than silently deleting them. External per-user data and ROMs
are untouched. Do not attempt privilege elevation or bypass Gatekeeper.

Before mutation check write access, free space for staging/backup and installation
layout. A read-only or translocated app gets an English instruction to move/extract
it to a writable location; the existing game remains usable.

Persist a recovery journal before mutation. Interrupted transactions roll back
before the next normal launch. Keep a runnable recovery helper until completion.
On successful replacement, launch the updated ROM selector and wait for a bounded
startup acknowledgement after its window is initialized. Missing acknowledgement
or launch failure triggers restoration and relaunch of the prior version. Mark
that target version as failed to prevent an automatic retry loop; Retry is explicit.
Do not infer full game compatibility from a successful launcher acknowledgement.

Keep the previous version until acknowledgement, then clean only transaction-owned
backups/cache. A failure to delete an obsolete backup is logged, not treated as a
failed update. update.log is accessible through the existing Open logs action.

## User data and configuration

Continue using LauncherStorage.DataDirectory, including CHRONICLES_DATA_DIR.
Carry the effective data directory to the helper and restarted launcher. Preserve
launcher.json, settings.json, raw memory cards, custom card paths, packs and ROM
paths. If user data overlaps managed installation paths, refuse automatic install
with an actionable message. Never migrate or overwrite user data during updates.

## Publication

Prepare release as a draft, upload all three archives plus SHA256SUMS, verify
uploaded sizes/digests and manifests, then publish as a normal latest release.
This prevents clients from seeing a partly uploaded update. Verify Spicywonda is
the authenticated publisher. Do not embed credentials in the launcher.

The unreleased pause fix remains tracked independently. The reported boss/orb
sound issue is not considered fixed by this feature.

## Verification and acceptance

Automated checks cover newer/equal/older versions, prerelease/draft rejection,
missing/wrong assets, timeouts/rate limits, cancellation, digest failure, hostile
archive paths, permission errors and bounded extraction. Use a local fake HTTP
handler; tests must not depend on a real newly published release.

Transaction tests use disposable fake installations: preserve unknown files and
user data, replace/remove only owned files, recover after each journal boundary,
reject concurrent game/update sessions, rollback on restart failure, and avoid
retry loops. Test arguments with spaces and Unicode paths.

Build and inspect all three platform packages. Exercise a complete local upgrade
and rollback on macOS. Windows must validate locked-executable replacement, and
Linux must validate executable modes and preserved desktop entry paths on native
hosts or equivalent CI before claiming those flows are tested. Do not publish
an auto-update-enabled build with unverified destructive replacement paths.

Success: an older updater-enabled installation receives a complete newer normal
release, restarts at the same path and retains disc selection, cards and controls;
Later/offline/failure cases keep the existing version usable.
