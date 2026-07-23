# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Unity 6000.4.10f1 (Unity 6) 2D game project built for GMTK Jam 2026. Uses URP (Universal Render Pipeline, 2D renderer), the new Input System, TextMesh Pro, and LeanTween for tweening.

There is no CLI build/test pipeline in this repo — this is a Unity Editor project. Building, running, and testing happen inside the Unity Editor (Play mode) or via the Unity command line (`-batchmode -executeMethod ...` / `-runTests`) if you have the Editor installed; there are no package.json/npm scripts, Makefiles, or CI config checked in.

Scenes (`Assets/Scenes/`): `Title`, `Level`, `Game`, `Procedural`. `Title` is the entry scene (main menu, settings, tutorial). `Procedural` hosts the runtime-built match-3 fighter minigame.

## Architecture

### Bootstrap / persistent systems

The game bootstraps itself independent of which scene is opened first:

- `Bootstrapper` (`Assets/Scripts/Utilities/Boostrapper.cs`) — a `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` static method that instantiates `Resources/GameInitializer.prefab` and marks it `DontDestroyOnLoad`.
- `GameInitializer` (`Assets/Scripts/GameInitializer.cs`) — on `Awake`, instantiates the `AudioManager`, `EffectManager`, and `SceneLoader` prefabs (from `Assets/Resources/systems/`) and calls `Utility.Setup(this)`.
- `GameFontController` (`Assets/Scripts/GameFontController.cs`) — separately runs at `BeforeSceneLoad`/`AfterSceneLoad`, loads the `FreeCheeseR` TTF from Resources, builds a runtime TMP font asset, and force-applies it to every `TMP_Text` in every scene on load (including inactive objects). Any new TMP label doesn't need manual font assignment — this happens automatically, but hand-built TMP text (e.g. spawned at runtime, see `ProceduralMatchFighter`) should still set `.font = GameFontController.Font` for consistency before the next scene-load pass.

### Singletons

`Singleton<T>` (`Assets/Scripts/Utilities/Singleton.cs`) is a generic MonoBehaviour singleton base (auto-creates or finds an instance, `DontDestroyOnLoad`, guards against duplicates/quitting). `AudioManager` and `EffectManager` derive from it. `SceneLoader` implements its own hand-rolled singleton instead (not derived from `Singleton<T>`) — keep that inconsistency in mind, don't assume all "manager" classes share the same base.

- `AudioManager` — sound/music library backed by a `Sound[]` array plus auto-discovered clips from `Resources/sounds`, an SFX source pool (`POOL_SIZE = 10`), named loop channels (`PlayLoop`/`StopLoop` keyed by `channelKey`), and master/music/sfx volume controls.
- `EffectManager` — pooled particle-effect playback (`Effect[]` library, per-effect object pools, auto pool expansion, auto-return based on `ParticleSystem` duration or a fallback lifetime).
- `SceneLoader` — fade-transition scene loading (`LoadScene(sceneName)`), optional loading-bar/progress UI, `FadeTransition()` for a fade without a scene load.

### Title scene UI flow

`Title.cs` wires three UI panel components together by toggling `gameObject.SetActive`, no separate state machine:
- `UIMainMenu` — Play / Settings / Tutorial / version text; `Setup(Param)` takes a struct of callbacks (`onPlay`, `onSetting`, `onTutorial`, `onExit`).
- `UISetting` — volume sliders bound directly to `AudioManager` getters/setters via callbacks.
- `UITutorial` — sprite-based paginated tutorial (`nextButton`/`prevButton`/`backButton`).

This `Setup(Param)`-with-callback-struct pattern is the convention for UI panel components in this project — follow it for new panels rather than having panels reach into game state directly.

`ButtonAnimation` (`Assets/Scripts/UI/Components/ButtonAnimation.cs`) is a reusable hover/press LeanTween scale+rotation effect attached to buttons; it also fires `AudioManager.Instance.PlaySFXOneShot("pop")` on press.

### Procedural match-3 fighter (`Assets/Scripts/ProceduralMatchFighter.cs`)

The largest and most self-contained script. It builds its entire battle HUD and 6x6 orb board at runtime via code (no prefabs/scene-authored UI) — `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` auto-spawns itself into the `Procedural` scene if not already present. Key points for anyone touching this file:

- All UI (canvas, board grid, health bars, HUD text, selection-cursor frame) is constructed procedurally in `BuildInterface()`/`CreateOrb()`/etc. — there's no corresponding scene hierarchy to inspect; read the code to understand layout.
- Match-3 orb types (`Red/Blue/Green/Yellow/Purple`) map to combat resources: Attack, Mana, Heal, Shield, Special. Matches queue into a `Fighter.Pending[]` array; the queue resolves into damage/heal/shield/mana on a fixed-length turn timer (`TurnDuration = 10s`, see `EndTurn()`).
- Supports both single-player (vs. a heuristic CPU, `FindBestCpuMove()`/`ScoreCpuMove()`) and local 2-player (`playerVsPlayer` flag) with separate keyboard bindings (P1: WASD+Enter, P2: numpad 1/2/3/5+0) and per-player gamepad (`Gamepad.all[humanIndex]`).
- Has both `ENABLE_INPUT_SYSTEM` (new Input System) and legacy `Input` code paths behind `#if` — when changing input handling, update both branches.
- `elementMap` (`panelPlayer`/`panelEnemy` transforms) is resolved by `GameObject.Find("PanelPlayer")`/`"Panelenemy"` if not wired in the inspector — note the inconsistent casing of `"Panelenemy"` when searching/renaming scene objects.

## Working in this codebase

- Scripts are split into `Assets/Scripts/` (game/menu logic), `Assets/Scripts/UI/` (panel components), `Assets/Scripts/UI/Components/` (reusable widgets), and `Assets/Scripts/Utilities/` (cross-scene managers/singletons/helpers).
- Runtime-loaded assets (sounds, sprites, fonts, prefabs for the bootstrap systems) live under `Assets/Resources/` and are referenced by string path/name (e.g. `Resources.Load<Font>("fonts/FreeCheeseR")`, `Resources.LoadAll<Sprite>("sprites/circle")`) — renaming or moving files there breaks lookups silently (falls back to warnings/null, not compile errors).
- `Assets/LeanTween/` is a vendored third-party tweening library (with its own examples/tests) — treat it as external, don't modify it for feature work.
