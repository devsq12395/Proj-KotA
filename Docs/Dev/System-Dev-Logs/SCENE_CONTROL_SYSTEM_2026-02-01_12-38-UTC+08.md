# Scene Control System

## Status
- Current: Completed
- Owner: Cascade
- Last Updated: 2026-02-01 13:35 (UTC+08)

## Links
- Proposal: `Docs/Dev/AI-Proposals/SCENE_CONTROL_SYSTEM_PROPOSAL.md`
- Related files:
  - `Scripts/Controllers/GlobalScene/SceneControl.cs`
  - `Scripts/Controllers/GlobalScene/GlobalControl.cs`
  - `Scripts/Data/SaveController.cs`
  - `Shaders/UI_CircleWipeTransition.shader`

## Milestones
- [x] Milestone 1: SceneControl implemented (instant initial load + circle wipe transitions)
- [x] Milestone 2: GlobalControl delegates all scene/transition logic to SceneControl
- [x] Milestone 3: Verify build + confirm editor wiring steps

## Progress Log

### 2026-02-01 12:38 (UTC+08)
- What changed:
  - Created this dev log.
  - Began implementation planning by auditing existing scene transition logic inside `GlobalControl` and identifying call sites.
- Files touched:
  - `Docs/Dev/System-Dev-Logs/SCENE_CONTROL_SYSTEM_2026-02-01_12-38-UTC+08.md`
- Notes:
  - Found existing first-run boot logic inside `BaseLobby.TryHandleFirstRunBoot()` that triggers `GlobalControl.I.ChangeScene("Game")`. This will remain compatible since `GlobalControl.ChangeScene` will remain as a facade.

### 2026-02-01 12:55 (UTC+08)
- What changed:
  - Implemented `SceneControl` to own:
    - circle wipe overlay creation
    - circle wipe shader material instancing
    - transition state machine (wipe-in → unload → delay → load → set active → delay → wipe-out)
    - instant initial scene load API (`LoadSceneInstant`) for first scene.
  - Refactored `GlobalControl` to:
    - remove all transition logic
    - implement initial load selection (firstRun → Game + Mission_1-1; else Lobby) using *instant* load
    - delegate all subsequent `ChangeScene` calls to `SceneControl`.
- Files touched:
  - `Scripts/Controllers/GlobalScene/SceneControl.cs`
  - `Scripts/Controllers/GlobalScene/GlobalControl.cs`
- Notes:
  - `SceneControl` now centralizes the music stop behavior before any scene load.

### 2026-02-01 13:21 (UTC+08)
- What changed:
  - Fixed duplicate `GlobalScene` instances by removing additive `LoadScene("GlobalScene")` behavior from `Bootstrap`.
  - Fixed content scenes not unloading (e.g., Game → Lobby) by ensuring the active scene is set back to the persistent scene before unloading.
- Files touched:
  - `Scripts/Controllers/GlobalScene/Bootstrap.cs`
  - `Scripts/Controllers/GlobalScene/SceneControl.cs`
- Notes:
  - `GlobalScene` must be Build Settings index 0 and is loaded normally by Unity.

### 2026-02-01 13:24 (UTC+08)
- What changed:
  - Strengthened scene unloading to be robust even if internal scene tracking gets out of sync:
    - On instant loads, unload *all* loaded scenes except the persistent `GlobalScene` and (optionally) the target.
    - On transitions, unload all non-persistent scenes prior to loading the next scene (supports reload behavior).
- Files touched:
  - `Scripts/Controllers/GlobalScene/SceneControl.cs`

### 2026-02-01 13:35 (UTC+08)
- What changed:
  - Verified in Play Mode that:
    - duplicate `GlobalScene` no longer occurs
    - scene switching works and previous scenes unload correctly.
- Notes:
  - Related commits:
    - `fixed having two globalscenes on the game`
    - `changing of scenes is working well`

## Decisions

### 2026-02-01 12:38 (UTC+08) — Keep GlobalControl.ChangeScene as facade
- Decision:
  - Keep `GlobalControl.ChangeScene(...)` public API, but delegate to `SceneControl`.
- Alternatives considered:
  - Update all call sites to reference `SceneControl` directly.
- Rationale:
  - Minimizes refactor surface area and reduces risk of missing call sites.

## Blockers
- None

## Verification
- How to test:
  - Clear PlayerPrefs to test first-run instant load.
  - Confirm subsequent scene changes use circle wipe.
- Expected result:
  - Initial scene load is instant (no wipe) to `Game` or `Lobby` depending on save state.
  - Later scene changes transition with circle wipe.
