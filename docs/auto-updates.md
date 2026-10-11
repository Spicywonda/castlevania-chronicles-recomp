# Launcher updates

The updater targets Windows x64, macOS Apple Silicon and Linux x64. Install its
first release manually; the older 0.3.9 launcher cannot add this feature itself.

The ROM selector checks this repository's latest normal release when opened.
Checks and downloads run in the background. You can play without Internet or
while a download is in progress. **Later** postpones installation for this session.
After a verified download, an idle launcher shows a ten-second restart countdown.
An active game prevents installation. **Retry** explicitly retries a failed update.

Downloads must match their GitHub SHA-256 digest, platform, version and installation
manifest. The updater rejects drafts, prereleases, older versions and incomplete
packages. SHA-256 checks integrity; it is not an independent publisher signature.

Install the application in a writable location. Read-only folders and translocated
macOS apps cannot be updated in place. Keep your ROM, packs and saves outside the
signed macOS app bundle. Windows/Linux unknown files are retained; conflicting
custom files stop the update with an error rather than being overwritten.

The updater backs up managed files or exchanges the complete signed macOS bundle.
The new launcher must acknowledge startup; a failed start restores the previous
installation. This acknowledgement does not certify a full game playthrough.
**Open logs** opens the data folder containing `update.log`, game logs and settings.

## Maintenance

Application version is defined in `Directory.Build.props`. Every package has a
manifest; macOS stores it in `Contents/Resources` before code signing. Build the
three platform archives, run the unit and native process suites, and publish using
`scripts/publish-release.sh PACKAGE_DIRECTORY RELEASE_NOTES.md` after the reviewed
commit and matching tag exist on GitHub. The script verifies the publisher, stages
a draft release, uploads and checks all assets, then publishes a normal latest
release. Do not edit assets of an existing released version; publish a new version.

Native tests use disposable, self-contained fake applications and never include
a ROM or real save. The process suite exercises helper execution, locking, signed
macOS bundle exchange and startup-failure rollback. `.github/workflows/updater-checks.yml`
runs it on all three operating systems. Cross-compiling alone does not verify
Windows/Linux replacement behavior.
