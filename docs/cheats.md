# Cheats — desktop preview 0.3

During the game, press **F1** if the menu bar is hidden, then open **Cheats**.

- **Infinite health** maintains 16 HP during active play. Falls and instant death still apply.
- **Infinite hearts** maintains 99 hearts during active play.
- **Infinite lives** maintains at least five lives without reducing a higher count.
- **Disable all** stops further cheat writes immediately. It does not rewind values already granted.

All options start disabled when the game process starts and apply only for that session.
They are shared by the Windows, macOS and Linux game builds. The launcher stays simple.

## Implementation and validation

Addresses for USA SLUS-01384: HP byte `8005A822`, hearts byte `8005A838`, lives byte `80059292`.
The original code leads are credited to floydfan in the [GameHacking.org USA database](https://gamehacking.org/game/88579).
The generated game corroborates HP initialization to 16 in `80017170`, heart consumption
in `8001BC44`, and life decrement in `80017274`. Player base is `8005A808`.

The stage-one state dispatch table maps state 15 to normal play and 20 to boss play.
The helper also checks the latest overlay against the current stage index before any write.
It skips other states, preventing heart replenishment from stalling the heart-to-score
countdown in `8001686C`/`80016A7C`. Only byte writes are used; adjacent player fields stay intact.

UI toggles update an atomic bitmask. The existing VSync pre-hook applies changes on the
game thread. This avoids racing the renderer against game RAM. No disc or code patch is made.

Run `scripts/dotnet.sh run --project tests/CheatChecks -c Release` for eight synthetic
checks covering disabled behavior, independent toggles, write width, death, all stage
identities, transition states, overlay mismatch, preserving earned lives and disable-all.
These checks do not establish full-game compatibility; gameplay testing remains necessary.

## 0.3.6: contact invincibility

The independent **Invincibility** toggle extends the existing immunity branch at `func_8001D458` / `8001D528`, before HP loss and hit reaction, while preserving enemy bookkeeping. It applies in gameplay states 15/20, stages 1–8, and only to living players. No timer, movement, death-state or HP writes are added. Both Original and Arrange share this handler. Disabling takes effect on the next contact; no artificial timer remains. Falls and instant-death paths retain their original behavior.

`scripts/patch-contact-invincibility.py` applies and verifies the narrow generated-code transform at every build; unexpected regenerated instructions fail the build. CheatChecks executes the generated contact handler in both mode branches and all stages, verifies player state and enemy bookkeeping, and checks immediate damage restoration.
