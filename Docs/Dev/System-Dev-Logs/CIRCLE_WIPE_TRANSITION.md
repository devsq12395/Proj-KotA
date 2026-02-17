# Circle Wipe Transition

## Status
- Current: In Progress
- Owner:
- Last Updated: 2026-01-29

## Links
- Proposal: `Docs/Dev/AI-Proposals/CIRCLE_WIPE_TRANSITION_PROPOSAL.md`
- Related files:
  - `Assets/Scripts/Controllers/GlobalScene/GlobalControl.cs`
  - `Assets/Shaders/UI_CircleWipeTransition.shader`
  - `Assets/Scripts/Controllers/GlobalScene/AdController.cs`

## Milestones
- [x] Milestone 1: Add circle wipe shader and runtime material hookup
- [x] Milestone 2: Replace slide transition with circle wipe in `GlobalControl`
- [x] Milestone 3: Verify transitions across scene changes and timeScale

## Implementation Summary
- Goal:
  - Replace the legacy slide transition with a screen-centered circle wipe.
- Approach:
  - Use the existing transition overlay `Image` and apply a custom UI shader.
  - Drive transition progress by updating a shader float property (`_Radius`) in `Update()` using unscaled time.
- Design constraints:
  - Must be robust across additive scene unload/load.
  - Must work even if `Time.timeScale` is 0.
  - Must render on top of per-scene UI.

## Runtime Objects & Lifetime
- **GlobalControl singleton**
  - `GlobalControl.Awake()` sets `GlobalControl.I` and calls `DontDestroyOnLoad(gameObject)`.
- **Overlay canvas**
  - Created (if missing) in `EnsureOverlayReady()` as a `GameObject` named `GlobalTransitionCanvas`.
  - Lifetime strategy:
    - Canvas is created once and kept in the GlobalScene alongside `GlobalControl`.
    - It is **not** moved between scenes during transitions.
  - Render mode:
    - Forced to `ScreenSpaceOverlay` for stability across additive unload/load.
  - Transition camera:
    - Optional and not required when using `ScreenSpaceOverlay`.
  - Sorting:
    - `overrideSorting = true`
    - `sortingOrder = 32767`
  - CanvasScaler:
    - `Scale With Screen Size`
    - `Reference Resolution = 1280 x 720`
    - `Screen Match Mode = Match Width Or Height`
    - `Match = 0.5`
    - `Reference Pixels Per Unit = 100`
- **Transition image**
  - Created (if missing) in `EnsureOverlayReady()` as a child object named `TransitionImage`.
  - **Important invariant:** `TransitionImage` must remain parented under `GlobalTransitionCanvas`.
    - We do **not** call `DontDestroyOnLoad()` on the child image, because Unity can unparent/move it.
    - If the image exists but is not a child of the canvas, we re-parent it.

## Shader / Material Contract
- Shader name:
  - `UI/CircleWipeTransition` (loaded via `Shader.Find`)
- Runtime material:
  - Created on demand in `EnsureCircleWipeMaterialReady()` and stored in `_circleWipeMaterialInstance`.
  - Assigned to `_transitionImage.material`.
- Shader properties:
  - `_Radius` (float)
    - Transition progress.
  - `_Softness` (float)
    - Feather amount at the edge.
  - `_Center` (vector)
    - Circle center in UV space; currently fixed to `(0.5, 0.5)`.
- Radius range:
  - `GetCircleWipeMaxRadius()` computes a max radius based on aspect ratio so the circle can cover corners.

## Logic Flows (Authoritative)

### Boot / Initial Reveal
- `GlobalControl.Awake()`
  - Sets singleton and `DontDestroyOnLoad`.
  - `_currentSceneName = SceneManager.GetActiveScene().name`.
  - `EnsureOverlayReady()`.
  - `HideTransitionCamera()`.
- `GlobalControl.Start()`
 - `GlobalControl.Start()`
  - Always calls `BeginInitialReveal()` once on startup.
  - Starts a preroll countdown timer (unscaled) in `Update()`.
    - Ads are currently temporarily disabled by `AdController.disableAdsTemporarily`.
- `BeginInitialReveal()`
  - Ensures overlay exists and hides transition camera.
  - Forces the overlay to start **covered**:
    - `SetOverlayActive(true)`
    - `EnsureCircleWipeMaterialReady()`
    - `SetCircleWipeRadius(GetCircleWipeMaxRadius())`
  - Optionally waits 1 frame by moving to `TransitionState.InitialRevealWaitFrame`.
  - Then runs a reveal wipe by entering `TransitionState.InitialRevealWipeOut`.

### Scene Change
- Entry point: `GlobalControl.ChangeScene(sceneName)`
  - Guards:
    - Ignores empty/null scene name.
    - Ignores if already `_isTransitioning`.
  - Calls `BeginSceneTransition(sceneName)`.
- Transition engine:
  - Runs in `GlobalControl.Update()` via `UpdateTransitionState()`.
  - Each step is a state in `TransitionState`.

 ### Transition State Machine
 - State owner:
   - `GlobalControl._transitionState`
 - State list:
   - `Idle`
   - `InitialRevealWaitFrame`
   - `InitialRevealWipeOut`
   - `SceneCoverWipeIn`
   - `SceneUnload`
   - `ScenePreLoadDelay`
   - `SceneLoad`
   - `SceneSetActive`
   - `ScenePostLoadWaitFrame`
   - `ScenePostLoadDelay`
   - `SceneRevealWipeOut`

 ### Scene Transition Sequence (State-by-State)
 - `BeginSceneTransition(sceneName)`
   - Sets `_isTransitioning = true`.
   - Ensures overlay and shows transition camera (if enabled).
   - Stores `_previousSceneName` and `_pendingSceneName`.
   - Enters `SceneCoverWipeIn`.
 - `SceneCoverWipeIn`
   - Covers screen by running a wipe from `_Radius = 0 -> max` (unscaled time).
   - When wipe completes, enters `SceneUnload`.
 - `SceneUnload`
   - If `_previousSceneName` is valid and loaded, starts `UnloadSceneAsync` and waits until done.
   - Then enters `ScenePreLoadDelay`.
 - `ScenePreLoadDelay`
   - Waits `_delayBeforeLoadNew` using `_stateTimerRemaining` and `Time.unscaledDeltaTime`.
   - Important:
     - Timer is initialized once on state entry via `EnterScenePreLoadDelay()`.
   - When the timer reaches 0, enters `SceneLoad`.
 - `SceneLoad`
   - Starts `LoadSceneAsync(_pendingSceneName, Additive)` and waits until done.
   - Then enters `SceneSetActive`.
 - `SceneSetActive`
   - Calls `SceneManager.SetActiveScene(newScene)`.
   - Updates `_currentSceneName`.
   - Re-validates overlay (`EnsureOverlayReady()`).
   - Enters `ScenePostLoadWaitFrame`.
 - `ScenePostLoadWaitFrame`
   - One-frame barrier to allow scene initialization to run.
   - Enters `ScenePostLoadDelay`.
 - `ScenePostLoadDelay`
   - Waits `_delayAfterLoadBeforeReveal` using unscaled time.
   - Then enters `SceneRevealWipeOut`.
 - `SceneRevealWipeOut`
   - Reveals screen by running a wipe from `_Radius = max -> 0` (unscaled time).
   - On completion:
     - `SetOverlayActive(false)`
     - `HideTransitionCamera()`
     - `_isTransitioning = false`
     - returns to `Idle`.

### Wipe Animation (Cover / Reveal)
- Entry:
  - `BeginWipe(isCover: bool)` initializes wipe parameters and ensures overlay/material.
  - Wipe duration is clamped to at least `0.2` seconds.
- Per-frame update:
  - `UpdateWipe()` advances using `Time.unscaledDeltaTime`.
  - Applies a cubic ease (`EaseInOutCubic`) and writes `_Radius` each frame.
- State revalidation safety:
  - `EnsureOverlayReady()` does not force-disable the overlay while `_isTransitioning` is true.
  - `EnsureCircleWipeMaterialReady()` does not reset `_Radius` while `_isTransitioning` is true.
- Shader missing fallback:
  - If the shader/material is missing, the overlay remains solid while the wipe timer runs.

## Temporary Mitigations
- Ads:
  - `AdController` currently has `disableAdsTemporarily` enabled.
  - When enabled, both `ShowAd(...)` and `ShowRewardedAd(...)` skip and immediately invoke callbacks.
  - Rationale: eliminate the fake ad overlay canvas and any timeScale side effects while diagnosing transition issues.

## Known Failure Modes / Debugging Checklist
- **Overlay exists but does not render**
  - Confirm `GlobalTransitionCanvas` exists at runtime.
  - Confirm `TransitionImage` is a child of `GlobalTransitionCanvas`.
  - Confirm `_transitionImage.gameObject.activeSelf` is true during `CircleWipeIn/Out`.
- **Wipe appears to “instantly disappear”**
  - Confirm `_delayAfterLoadBeforeReveal` is not set to 0 while expecting a buffer.
  - Confirm no other system disables the overlay image or canvas.
  - Confirm `HideTransitionCamera()` is not being called from elsewhere.
- **Shader not found / material missing**
  - Confirm shader name is exactly `UI/CircleWipeTransition`.
  - Confirm `EnsureCircleWipeMaterialReady()` is being reached.
- **Ordering issues (something draws above the transition)**
  - `GlobalTransitionCanvas` forces `overrideSorting = true` and `sortingOrder = 32767`.
  - If something still draws above it, search for other canvases also using extremely high sorting orders.
- **TimeScale issues**
  - State machine delays and wipes use `Time.unscaledDeltaTime`.

## Progress Log

### 2026-01-29
- What changed:
  - Started implementation.
  - Created this dev log.
- Files touched:
  - `Docs/Dev/System-Dev-Logs/CIRCLE_WIPE_TRANSITION.md`
- Notes:

### 2026-01-29
- What changed:
  - Added a new UI shader (`UI/CircleWipeTransition`) for the transition overlay.
  - Updated `GlobalControl` to use `CircleWipeIn()` / `CircleWipeOut()` and removed slide transition usage.
  - Added safe fallback behavior if the shader is missing (solid overlay wait).
- Files touched:
  - `Assets/Shaders/UI_CircleWipeTransition.shader`
  - `Assets/Scripts/Controllers/GlobalScene/GlobalControl.cs`
- Notes:
  - Circle wipe is screen-centered.

### 2026-01-29
- What changed:
  - Adjusted overlay lifetime to reduce risk of the transition image being detached from the overlay canvas.
  - Added unscaled-time delay after load before reveal (`_delayAfterLoadBeforeReveal`, default 3s).
  - Switched pre-load delay to `WaitForSecondsRealtime` to avoid `Time.timeScale` issues.
  - Temporarily disabled ads globally via `AdController.disableAdsTemporarily` to eliminate interference while diagnosing.
- Files touched:
  - `Assets/Scripts/Controllers/GlobalScene/GlobalControl.cs`
  - `Assets/Scripts/Controllers/GlobalScene/AdController.cs`
- Notes:
  - Transition robustness across repeated scene swaps is still under investigation.

### 2026-01-29
- What changed:
  - Refactored transition execution to an `Update()`-driven state machine (no coroutines).
  - Removed DOTween dependency from wipe animation; wipe now runs via manual unscaled-time updates.
  - Fixed a state-machine stall on repeated transitions by initializing `_delayBeforeLoadNew` timer only once on `ScenePreLoadDelay` entry.
- Files touched:
  - `Assets/Scripts/Controllers/GlobalScene/GlobalControl.cs`
- Notes:
  - This workflow is intended to make transitions more deterministic and easier to debug.

## Decisions

### 2026-01-29 — Screen-centered wipe
- Decision:
  - Implement circle wipe centered on screen (UV center).
- Alternatives considered:
  - Centered on player position.
  - RenderTexture-based wipe.
- Rationale:
  - Lowest integration complexity and least dependency on scene-specific cameras.

## Blockers
- None.

## Verification
- How to test:
  - Trigger `GlobalControl.ChangeScene(...)` transitions between Lobby and Game.
  - Confirm wipe covers before unload and reveals after load.
  - Verify with timeScale = 0 (should still animate).
  - With current settings, confirm reveal waits `(_delayAfterLoadBeforeReveal)` seconds after scene activation.
  - Adjust transition speed via `_tweenDuration` in the `GlobalControl` inspector.
- Expected result:
  - Smooth circle wipe in/out with no flashes.
