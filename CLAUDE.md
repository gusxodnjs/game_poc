# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project context

**TERRA × BIOSPHERE PoC** — a Korean location-based pixel-art mobile game prototype. The PoC validates a single loop: **산책 → 발견 → 안치** (walk → discover → enshrine). Scope is deliberately narrow; see "Out of scope" in `README.md` for what NOT to add. Target platform priority is **iOS first**, Android secondary.

- Display name: `작은정복자들` (set in `PocPostProcessBuild.cs`, not Unity Player Settings)
- Bundle ID: `com.gusxodnjs.terrapoc`
- Unity: `6000.0.75f1` (Unity 6 LTS) — pinned in `ProjectSettings/ProjectVersion.txt`

## Build & run

The Unity Editor exposes a **`TERRA PoC/`** menu (defined in `assets/Editor/PocBuildPipeline.cs`) — these menu items are the canonical build flow, not the generic Unity Build Settings:

1. `TERRA PoC/1. Setup Hello Scene` — recreates `HelloScene.unity` with `HelloRoot` + `GpsRoot` + `DiscoveryRoot` GameObjects wired up.
2. `TERRA PoC/1b. Setup Splash Scene` — recreates `SplashScene.unity` and registers scene build order as `[Splash, Hello]`.
3. `TERRA PoC/2. Configure iOS Player Settings` — applies bundle ID, IL2CPP, iOS 13+, iPhone-only, location usage string, disables Unity splash.
4. `TERRA PoC/3. Build iOS Xcode Project` — emits Xcode project to `build/ios/` and runs the post-process step.

For CLI / CI builds invoke `PocBuildPipeline.DoAll` (runs all four in order):

```bash
/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . -executeMethod PocBuildPipeline.DoAll -logFile -
```

Manual iOS device install (Xcode signing, 7-day Personal Team limit, plist trust flow): see `docs/build_ios.md`. There are no automated test or lint commands in this repo.

## Pixel-art asset generation

Assets under `assets/` (sprites, characters, tiles, world, ui, AppIcon) are generated via the **PixelLab API** by Python scripts in `scripts/` (e.g. `gen_assets.py`, `gen_animations.py`, `gen_cute_walker.py`).

```bash
cp .env.example .env.local       # then fill PIXELLAB_API_KEY
python3 scripts/gen_assets.py
```

Each script calls `https://api.pixellab.ai/v1/generate-image-pixflux`, base64-decodes the PNG, validates the signature, and writes alongside a `*_result.json` log. PixelLab's minimum canvas is **32×32** — smaller targets (e.g. `light_spot_16x16.png`) are generated at 32×32 then nearest-neighbor downscaled. See `assets/README.md` and the per-category READMEs for prompts.

## Architecture (non-obvious bits)

### Discovery loop = grid-cell crossings
`assets/Scripts/CellMapping.cs` maps lat/lon onto a fixed `0.001°` grid (~111m at the equator). `DiscoveryDetection.cs` polls `Input.location.lastData` each `Update`, computes the current cell ID via `CellMapping.ToCellId`, and fires a "발견!" notification **only when the cell ID changes**. Species are picked uniformly at random from a hardcoded 6-name array — the canonical species list lives in `data/species.json` but is not yet wired to gameplay. `GpsCheck.cs` is what actually calls `Input.location.Start(5f, 5f)`; without it on a scene, `DiscoveryDetection` is a no-op.

### IMGUI everywhere, no Canvas/prefabs
All on-screen UI (`HelloWorld`, `SplashScreen`, `GpsCheck`, `DiscoveryDetection`) draws via `OnGUI`. Scenes contain no prefabs, no Canvas, no UI Toolkit — just empty GameObjects with MonoBehaviour components added by `PocBuildPipeline.SetupHelloScene`. Re-running that menu item is the safe way to reset a broken scene.

### Splash → Hello flow
Build scene order is hardcoded in two places that must agree: `EditorBuildSettings.scenes` (set by the setup menu items) and `BuildPlayerOptions.scenes` in `PocBuildPipeline.BuildIOS`. Splash transitions via `SceneManager.LoadScene(nextScene)` after `displayDuration`. Unity's own splash screen is intentionally disabled (`PlayerSettings.SplashScreen.show = false`) — recent fixes (`fix/splash-blackscreen`, `fix/disable-unity-splash`) prevent regressions here; don't re-enable Unity splash without coordinating.

### iOS post-build patches Info.plist + AppIcon
`assets/Editor/PocPostProcessBuild.cs` runs after `BuildIOS` and:
- Sets `CFBundleDisplayName = 작은정복자들` and `NSLocationWhenInUseUsageDescription` in `Info.plist`.
- Copies `Assets/AppIcon/Icon-iPhone-{120,180}.png` directly into Xcode's `Unity-iPhone/Images.xcassets/AppIcon.appiconset/` — Unity's PlayerSettings icon assignment is bypassed. Adding a new icon size means updating the size list in `PocPostProcessBuild.OnPostProcessBuild` AND committing the source PNG under `Assets/AppIcon/`.

### Folder casing caveat
The repo uses `Assets/` (Unity-canonical) but recent commits and `assets/Scripts/*.cs` are referenced with lowercase `assets/`. macOS HFS+/APFS is case-insensitive by default so this works locally; on a case-sensitive filesystem (most Linux CI) this would break. If introducing CI, normalize to `Assets/`.

## Workflow

- PR-driven (currently around PR #29). Feature branches: `feat/...`, `fix/...`, `docs/...`. Merge via GitHub web UI; commit messages are Korean Conventional Commits (e.g. `feat(splash): ...`, `fix(client): ...`).
- `gh` CLI is authenticated against `github.com` (not GHES) — repo is `gusxodnjs/game_poc`.
- Issue numbers in `README.md` (#1 client, #2 backend, #3 tester, #4 designer, #5 합동) map to the four-person, one-week schedule.

## Specialized subagents

Five custom agents in `.claude/agents/` are tuned to this project — prefer them via the Agent tool over the generic `general-purpose`:

| Agent | Use for |
|-------|---------|
| `unity-ios-client-dev` | Unity C# gameplay, iOS build/signing, `OnGUI`/scene wiring, `Input.location` |
| `mobile-game-backend-dev` | BaaS evaluation, API contracts (Firebase / Gamebase / PlayFab comparison per #2) |
| `pixellab-asset-designer` | PixelLab prompt design, new sprite/tile/animation generation via the Python pipeline |
| `game-design-planner` | Game mechanics, species/discovery loop tuning, scope decisions vs. `TERRA_POC_v7_2주.docx` |
| `project-manager-coordinator` | Multi-stream coordination across the D+1 ~ D+5 schedule, status rollups |
