# Circle Wipe Scene Transition Proposal

## Summary
Replace the existing slide-based scene transition in `GlobalControl` with a screen-centered **circle wipe** transition implemented via a UI `Image` + custom shader/material. This keeps the current additive load/unload flow, but changes the visual from a horizontal slide to a radial wipe.

## Goals
- Provide a clean, modern **circle wipe** transition for all scene changes driven by `GlobalControl.ChangeScene(...)`.
- Keep transition timing behavior compatible with the current flow:
  - cover screen
  - unload previous scene
  - optional delay
  - load new scene additively
  - reveal new scene
- Keep the transition using **unscaled time**.
- Make setup simple and robust across resolutions/aspect ratios.

## Non-Goals
- World-position-centered wipe (player-position wipe).
- Two-scene compositing wipe (RenderTexture snapshot of old scene).
- Creating new audio behavior (the transition continues to stop music as it does today).
- Adding additional transition styles beyond the circle wipe.

## References / Existing Files
- `Assets/Scripts/Controllers/GlobalScene/GlobalControl.cs`
  - Current slide transition lives in:
    - `EnsureOverlayReady()` (creates overlay canvas + image)
    - `SlideOverlayIn()` / `SlideOverlayOut()` (DOTween on `_transitionRect.anchoredPosition`)
    - `ChangeSceneRoutine(...)` uses slide-in to cover and slide-out to reveal
  - Uses `_transitionCamera` (optional). Current implementation does not require it when using Screen Space Overlay.

## Architecture / Design
### Core idea
Continue using the existing fullscreen UI overlay `Image` (`_transitionImage`) but swap its visual behavior from positional movement to a material-driven radial mask.

### Screen-centered wipe
- Circle center fixed at the screen center (UV `0.5, 0.5`).
- The wipe is controlled by a `float` shader property (example name: `_Radius`).

### Visual states
- **Covered**: `_Radius` such that the entire screen is filled/occluded.
- **Revealed**: `_Radius` such that the screen is fully transparent (or fully clipped away).

Implementation can choose either convention:
- radius grows to reveal OR
- radius grows to cover

The implementation must be consistent across `CircleWipeIn()` and `CircleWipeOut()`.

### Material
- Add a new `Material` field in `GlobalControl` for the transition (example: `_circleWipeMaterial`).
- The shader should support:
  - `_Radius` (required)
  - `_Softness` (optional, for feathered edge)
  - `_Color` (optional if using a pure-color wipe)

### Animation driver
Animate the shader value via an `Update()`-driven state machine using `Time.unscaledDeltaTime`.

## Step-by-Step Integration Plan
1) Add assets
- Create a new UI shader for radial wipe (e.g. `UI/CircleWipe`), and a material that uses it.
- Decide whether the wipe color is taken from:
  - the shader/material `_Color`, or
  - `GlobalControl._transitionColor`.

2) Update `GlobalControl` overlay setup
- Keep `EnsureOverlayReady()` responsible for creating:
  - `_overlayCanvas`
  - `_transitionImage`
- Update it so `_transitionImage` uses the circle wipe material when available.
 - Keep the overlay canvas in the GlobalScene and force it to `ScreenSpaceOverlay` for stability.

3) Replace slide coroutines
- Remove `SlideOverlayIn()` and `SlideOverlayOut()`.
- Add:
  - `CircleWipeIn()` (cover)
  - `CircleWipeOut()` (reveal)

4) Update scene change routine
- Replace the coroutine-based scene change routine with an `Update()`-driven state machine.

5) Startup reveal
 - Always run an initial reveal wipe once on startup.
 - Ensure initial state starts as **covered** before revealing.

6) Remove obsolete fields
- Remove fields that only exist for the slide transition (e.g. `_transitionRect` position logic and any slide-only config).

7) Editor wiring
- Assign the circle wipe material (and optional sprite if still desired).
- `_useTransitionCamera` should generally be disabled when using Screen Space Overlay.

## Data / Settings / Configuration
Suggested serialized settings on `GlobalControl`:
- `_tweenDuration` (already exists)
- `_delayBeforeLoadNew` (already exists)
- `_transitionColor` (already exists)
- `_circleWipeMaterial` (new)
- `_circleWipeSoftness` (new, optional)

## Security Considerations
- None.

## Risks and Mitigations
- **Risk: shader/material not assigned**
  - Mitigation: fall back to a solid-color fullscreen image (current behavior) and log a warning.

- **Risk: incorrect radius mapping on ultra-wide / tall screens**
  - Mitigation: in shader, compute radial distance with aspect-correct scaling (use `_ScreenParams` or equivalent).

- **Risk: transition overlay flashes on resolution changes**
  - Mitigation: preserve current behavior of disabling overlay when idle and enabling only during wipe coroutines.

## Testing Plan
- From Lobby → Game and Game → Lobby:
  - Transition covers fully before unload
  - No visible intermediate frame where scenes are missing
  - Transition reveals correctly after the new scene becomes active
- Verify with `_useTransitionCamera = false`.
- Verify time scale changes do not affect transition (should use unscaled time).

## Rollout Plan
- Implement behind a short-lived feature branch.
- Once confirmed, remove the slide transition code entirely.

## Deliverables
- New shader + material for circle wipe.
- Updated `GlobalControl` using circle wipe (slide transition removed).
- Updated documentation if needed (if there is a transition section in feature docs).
