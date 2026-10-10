# Launcher Auto-Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Automatically update the ROM selector and bundled game from complete normal GitHub releases, preserving user data and recovering failed installations.

**Architecture:** LauncherCore owns discovery, verified staging and transactional installation. DesktopLauncher presents an asynchronous update state machine and dispatches an early headless helper mode. A copied single-file launcher performs installation after the original process exits; game processes coordinate through an installation lease.

**Tech Stack:** C#/.NET 10, HttpClient, System.Text.Json, System.IO.Compression, System.Formats.Tar, existing ImGui launcher, Python packaging, GitHub releases.

**Spec:** `docs/superpowers/specs/2026-10-10-launcher-auto-update-design.md` (approved by user on 2026-10-10).

## Global Constraints

- Windows x64, macOS arm64 and Linux x64; interface remains English.
- Fixed repository: Spicywonda/castlevania-chronicles-recomp. No embedded credentials.
- Only newer normal releases; no drafts, prereleases or downgrades.
- Download automatically; Later postpones installation for this session.
- Install only when the launcher is idle and no game process owns an installation lease.
- Preserve disc images, saves, settings, bindings, packs and CHRONICLES_DATA_DIR.
- Existing 0.3.9 users require an initial manual download.
- Planned version 0.3.10; check remote tags before assigning it.
- Publish all assets as a draft first, verify, then expose a normal latest release.
- Do not overwrite existing unrelated changes or claim the boss/orb sound bug fixed.
- Native Windows/Linux installation checks are required before publishing enabled update flows.

## Review Focus

1. Installation paths contain spaces, Unicode, percent signs or shell characters: argument passing and restart must preserve them (Tasks 4–5).
2. A user placed a ROM/save/custom directory next to Play, inside runtime, or inside the app: preserve it or refuse safely (Tasks 2, 4).
3. Two launchers, direct game execution and an updater race: no replacement while a game starts or runs (Tasks 3–5).
4. Process/OS interruption happens between replacement and acknowledgement: launcher remains recoverable and old files are restored (Task 4).
5. A redirect, cache entry, release edit or reused version supplies unexpected bytes: reject incorrect origin/identity/digest rather than installing stale content (Tasks 1–2).

## File map

- `Directory.Build.props`: one application version, inherited by root/launcher projects.
- `launcher/LauncherCore/Updates/UpdateModels.cs`: immutable release, package, progress and state records.
- `launcher/LauncherCore/Updates/ReleaseClient.cs`: GitHub requests, conditional cache, selection.
- `launcher/LauncherCore/Updates/PackageStager.cs`: streamed download, hashes, bounded extraction.
- `launcher/LauncherCore/Updates/InstallManifest.cs`: owned paths, platform/version/layout checks.
- `launcher/LauncherCore/Updates/InstallationLease.cs`: installation-specific process coordination.
- `launcher/LauncherCore/Updates/UpdateTransaction.cs`: durable journal and Windows/Linux managed-file replacement.
- `launcher/LauncherCore/Updates/MacAppSwap.cs`: signed bundle validation and atomic exchange.
- `launcher/LauncherCore/Updates/UpdateHelper.cs`: wait/install/restart/acknowledge/recover lifecycle.
- `launcher/LauncherCore/Updates/UpdateCoordinator.cs`: UI-independent session state machine.
- `launcher/DesktopLauncher/UpdatePanel.cs`: English status, progress and actions.
- `launcher/DesktopLauncher/Program.cs`: early helper dispatch and launcher lifecycle integration.
- `Program.cs`: direct game lease and shared build version.
- `scripts/package-desktop.py`: manifests, version metadata and managed layout.
- `scripts/publish-release.sh`: complete draft upload, digest verification, normal publication.
- `tests/UpdaterChecks/`: deterministic checks using fake HTTP responses/disposable installs.
- `tests/UpdaterProcessChecks/`: disposable native process/restart/locking fixtures.
- `.github/workflows/updater-checks.yml`: Windows, macOS and Linux native checks.
- `docs/auto-updates.md`, `README.md`: player instructions, limitations and maintenance.

## Shared contracts

Define these in Task 1; later tasks consume these exact names:

- `UpdateRelease(Version Version, string Tag, string Rid, Uri PackageUrl, long Size, string Sha256, Uri ReleaseUrl)`.
- `UpdateProgress(string Phase, long Completed, long? Total)`.
- `StagedUpdate(UpdateRelease Release, string Root, InstallManifest Manifest)` (completed in Task 2).
- `UpdatePhase`: Idle, Checking, Downloading, Ready, Deferred, Installing, Failed, UpToDate.
- `UpdateStatus(UpdatePhase Phase, string Message, UpdateProgress? Progress)`.
- `InstallManifest(int Schema, string Version, string Rid, IReadOnlyList<OwnedFile> Files)` and `OwnedFile(string Path, long Size, string Sha256, int UnixMode)` (Task 2).
- `UpdateRequest(string InstallationRoot, string StagingRoot, string DataDirectory, string TransactionDirectory, int ParentPid, DateTime ParentStartUtc, string Version, string Nonce)` (Task 4).

## Task 1: Version identity and stable-release discovery

**Files:** Create Directory.Build.props, UpdateModels.cs, ReleaseClient.cs, tests/UpdaterChecks/{UpdaterChecks.csproj,Program.cs,ReleaseChecks.cs}; modify launcher/game version display and package version reading.

**Interfaces:** `ReleaseClient(HttpClient client, string cacheDirectory)`; `Task<UpdateRelease?> CheckAsync(Version current, string rid, CancellationToken token)`; `BuildVersion.Current` sourced from assembly version metadata.

- [ ] Write fake-HTTP checks: select newer v0.3.10 for win-x64; return null for equal/older versions; reject draft/prerelease, missing/duplicate/wrong-RID assets, missing digest and unuploaded assets. Assert numeric 0.3.10 > 0.3.9.
- [ ] Run `scripts/dotnet.sh run --project tests/UpdaterChecks -- release`; observe failure before implementation.
- [ ] Implement latest-release lookup with fixed repository URLs, GitHub headers, 8-second discovery timeout and 1 MiB metadata limit. Validate exact archive names and HTTPS download origins; allow only GitHub's required asset CDN redirects, never arbitrary hosts. Cache ETag and validated metadata atomically; 304 requires a usable matching cache. Reject redirects to unapproved origins, inconsistent tag/version and a reused version with changed identity. Retry remains user-driven after errors.
- [ ] Add checks for 304 without cache, corrupt cache, 404/no stable release, 403/429, oversized JSON, cancellation and timeout. All leave the installed version usable.
- [ ] Run release checks and existing LauncherChecks; verify success. Commit only Task 1 files.

## Task 2: Verified packages and staging

**Files:** Create InstallManifest.cs, PackageStager.cs, tests/UpdaterChecks/PackageChecks.cs; modify scripts/package-desktop.py.

**Interfaces:** `Task<StagedUpdate> PackageStager.StageAsync(UpdateRelease release, string cacheDirectory, IProgress<UpdateProgress> progress, CancellationToken token)`; `InstallManifest.ReadAndValidate(string root, string rid, Version version)`.

- [ ] Write checks asserting a valid miniature package stages correctly, wrong digest/length never reaches extraction, and cancellation leaves no completed download. Assert cache reuse requires the same verified digest.
- [ ] Run `scripts/dotnet.sh run --project tests/UpdaterChecks -- package`; observe failure.
- [ ] Implement partial download with a 1 GiB download cap, 30-second stalled-read timeout and cancellation. Check actual length and SHA-256 before accepting it. Limit extraction to 4 GiB, 20,000 entries and 512 MiB per regular file; reject excess before allocating/writing.
- [ ] Reject traversal, absolute/drive paths, links, devices, duplicate paths and case collisions. Permit only the expected top-level package; ignore only recognized __MACOSX metadata. Preserve safe executable modes; prohibit setuid/setgid bits.
- [ ] Generate and validate manifest version/RID, owned paths, file sizes and hashes. On macOS generate before signing, exclude the manifest's own hash and signer-generated _CodeSignature entries from its list; verify the complete staged signature separately. All other packaged regular files must be listed. Never list user data as managed assets.
- [ ] Add malformed/archive-bomb/path-confusion tests and legitimate ZIP/tar.gz tests, including Unix modes and unknown files. Run package checks, inspect newly generated manifests, commit Task 2 files.

## Task 3: Game/update exclusion

**Files:** Create InstallationLease.cs and tests/UpdaterProcessChecks/{UpdaterProcessChecks.csproj,Program.cs,LeaseChecks.cs}; modify root Program.cs and game-start integration.

**Interfaces:** `IDisposable? InstallationLease.TryAcquire(string installationRoot)`; `string InstallationLease.CanonicalRoot(string executablePath)`.

- [ ] Add two-process checks: second lease acquisition fails until the first process exits; killed owner releases the lock; aliases of the same install share a lock; separate installations do not block each other.
- [ ] Run process lease checks and observe failure.
- [ ] Implement an exclusive OS-backed file lease keyed by canonical installation identity in the user's update state directory. Windows/Linux runtime executable resolves to its parent install; macOS resolves to enclosing app bundle. Resolve existing symlinks before deriving identity. Launcher validation/game child and direct game execution must hold the same lease during execution; helper needs it before mutation. Avoid double-acquiring in parent and child.
- [ ] Confirm game-start failure produces a clear English message and no partial child process. Serialize launcher UI transitions so installation cannot race Play.
- [ ] Run native process lease checks and LauncherChecks; commit Task 3 files.

## Task 4: Transaction, helper and recovery

**Files:** Create UpdateTransaction.cs, MacAppSwap.cs, UpdateHelper.cs, tests/UpdaterChecks/TransactionChecks.cs and tests/UpdaterProcessChecks/RecoveryChecks.cs; add an early headless entry to DesktopLauncher/Program.cs.

**Interfaces:** `Task<int> UpdateHelper.RunAsync(UpdateRequest request, CancellationToken token)`; `UpdateTransaction.Prepare(UpdateRequest request)`; `Apply()`, `Rollback()`, `Commit()`; `Task RecoverPendingAsync(string installationRoot, string dataDirectory)`; `MacAppSwap.Exchange(string installedApp, string stagedApp)`.

- [ ] Write disposable-install tests for success, unmanaged-file preservation, conflicting custom data, read-only paths, insufficient space, failure after every journal boundary and corrupted requests. Original bytes must be recoverable after every injected failure.
- [ ] Run transaction checks and observe failure.
- [ ] Prepare staging/backup on the installation's filesystem. Validate canonical roots, version, ownership, manifest and data-path overlap in helper. Copy the self-contained Play to a private transaction directory; use --apply-update with an argument-list request path, not shell text. Bind request to installation, expected parent start time and random nonce. Verify a real headless copied Play starts without graphics libraries before relying on this design.
- [ ] Persist journal before each mutation, flush it, and replace files with temporary siblings plus atomic overwrite; backup by copy so the installed launcher always exists. Replace Play last. Delete only obsolete manifest-owned files. Reject existing symlink ancestors. Abort and preserve unknown files that collide with new owned paths.
- [ ] On macOS verify codesign, refuse translocated/read-only locations and unmanaged app content, stage as sibling, then atomically exchange app directories with renameatx_np(RENAME_SWAP). Verify support/failure without moving the original aside first. Old app remains available at the staging path for rollback.
- [ ] Wait up to 30 seconds for the original launcher process identity to exit; take the lease, apply, relaunch from the original path and carry CHRONICLES_DATA_DIR. New launcher performs startup recovery before graphics unless launched for the matching transaction acknowledgement. Send nonce/version acknowledgement only after the window initializes. Wait up to 30 seconds; terminate only the verified new child if needed, restore and relaunch old version. Persist failed version to stop retry loops.
- [ ] On interrupted startup use the copied recovery helper before native graphics loading. Retain the helper/journal until transaction completion and expose its recovery invocation in logs. Cleanup only verified transaction-owned paths; cleanup failure is nonfatal.
- [ ] Run transaction and native helper/rollback checks with spaces/Unicode/shell characters. Confirm no shell interpolation and no user data mutation. Commit Task 4 files.

## Task 5: Launcher experience

**Files:** Create UpdateCoordinator.cs, UpdatePanel.cs and tests/UpdaterChecks/CoordinatorChecks.cs; modify DesktopLauncher/Program.cs.

**Interfaces:** `UpdateCoordinator.StartAsync(CancellationToken token)`, `Defer()`, `RetryAsync(CancellationToken token)`, `SetGameRunning(bool running)`, `Task<bool> InstallIfReadyAsync(CancellationToken token)` and immutable `Status`.

- [ ] Add checks for checking/downloading while Play remains usable, Later, game starting during download, multiple completion events, close cancellation, failed-version suppression and manual Retry.
- [ ] Run coordinator checks and observe failure.
- [ ] Implement the approved automatic flow. Present progress in a compact separate ROM-selector panel with current version, Later, Check for updates/Retry and Open logs. Render plain text only. Ready starts a visible 10-second countdown while idle; Later or game start cancels that countdown. Recheck lease immediately before install. Disable Play and card/ROM changes only after entering installation.
- [ ] Marshal status snapshots to the render thread. Separate cancellation/error copy from game status; retain playability after network failures. Cancel requests when closing. Finish graphics cleanup before helper mutation; ensure disposal occurs with a current GL context so updater restarts do not trigger the previously reported glDeleteBuffers error.
- [ ] Visually verify English layout and countdown on macOS at the existing 960x580 size. Run coordinator/LauncherChecks and headless-mode checks; commit Task 5 files.

## Task 6: Native validation and release tooling

**Files:** Create .github/workflows/updater-checks.yml, scripts/publish-release.sh; extend scripts/package-desktop.py and tests/UpdaterProcessChecks as needed; update README.md and docs/auto-updates.md.

- [ ] Add native CI fixtures for all three OSes using fake game processes and tiny versioned installations (no ROM). Exercise real copied-helper execution, Windows locked files, macOS atomic exchange/signatures, Linux execute bits, startup failure and process termination at journal boundaries.
- [ ] Build production packages sequentially with `scripts/publish-desktop.sh win-x64`, `osx-arm64`, `linux-x64`. Verify helper assets, manifest/version consistency, hashes, managed-code parity and no ROM/save files. Run complete local upgrade/rollback on macOS; obtain successful native Windows/Linux process checks before release.
- [ ] Implement publication script: verify gh account Spicywonda and clean intended commit/tag; create draft; upload all platform archives and SHA256SUMS; compare remote size/digest to local files; only then set prerelease=false, draft=false and latest. On failure retain a draft, never expose an incomplete update. Require explicit publishing invocation; tests use fake command responses and do not create public test releases.
- [ ] Document initial manual download, automatic startup checks, Later/offline/recovery behavior, writable-folder requirements and update.log. Distinguish release classification from full-game compatibility. Record native tests actually run; do not claim missing native coverage.
- [ ] Run all updater suites plus LauncherChecks, ControlsChecks and InputSnapshotChecks. Review cumulative diff against the approved spec; verify unrelated changes remain intact. Commit reviewed Task 6 files.

## Final delivery

- [ ] List verification evidence and remaining native-host limitations, if any.
- [ ] Provide local packages for testing. Publish a normal release only after required native transaction checks pass and the intended release contents are verified.
- [ ] Keep the boss/orb sound report open; do not imply this feature resolves it.

## Self-review

Discovery/version/cache map to Task 1; download/integrity/layout to Task 2;
all entry-point exclusion to Task 3; helper/journal/restart/recovery to Task 4;
UI/offline/cancellation to Task 5; native platform coverage/publication/docs to
Task 6. All five Review Focus items have named tests in their owning tasks.
No new dependency, cloud service, updater credential or ROM-bearing test fixture
is required. Functional approval is preserved; implementation awaits plan review
and execution-method selection.
