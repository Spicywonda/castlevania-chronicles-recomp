# Contributing

This project is an early experimental recompilation of the USA SLUS-01384 release. Read the [README](README.md) for current compatibility before reporting a problem or changing behavior.

Use English for documentation, issues, and pull requests. Keep project-owned code free of comments; explain non-obvious behavior in documentation and pull request descriptions, and use clear names in code.

## Repository boundaries

Keep disc images, extracted assets, generated C#, RAM or VRAM dumps, audio captures, local SDKs, dependency caches, and build outputs out of commits and attachments. Use synthetic data for regression checks where possible. Review diagnostic logs before sharing them.

RecompOne is pinned as a submodule. Maintain project runtime changes as patches in `patches/runtime/`, and apply them through `scripts/apply-runtime-patches.sh`. Do not silently update the pinned dependency as part of an unrelated change.

## Reporting a bug

Provide the OS and architecture, hardware, commit, disc edition, game mode, stage, reproduction steps, and expected versus observed behavior. State whether widescreen is enabled and whether the problem also occurs in the default 4:3 mode. Include a minimal relevant text log after reviewing it for game data and personal paths.

The pause-menu crash and widescreen corruption/pop-in are known reports under investigation. Later stages, saving, and Windows runtime behavior still need validation. A build passing does not establish that these features work.

## Proposing a change

Keep each change focused. Describe the concrete failure, the resulting behavior, and the evidence supporting the fix. Preserve the default 4:3 reference while widescreen is experimental. Check patch addresses and instruction sequences against the expected disc revision instead of assuming they apply to other editions.

Run checks relevant to the change:

```sh
./scripts/apply-runtime-patches.sh
./scripts/dotnet.sh run --project tests/RuntimeChecks -c Release
./scripts/dotnet.sh run --project tests/WidescreenChecks -c Release
./scripts/dotnet.sh run --project tests/GpuChecks -c Release
```

GPU checks require a graphical session. Report which checks were run, their results, and any runtime behavior still untested. For game changes, record a repeatable in-game check as well; synthetic checks alone do not prove gameplay compatibility.
