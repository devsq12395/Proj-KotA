# Scene Control System Proposal

## Summary

Refactor scene management by consolidating all scene loading and circle wipe transition logic into a dedicated `SceneControl` class (`Scripts/Controllers/GlobalScene/SceneControl.cs`). This separates concerns, making `GlobalControl` a lighter singleton focused on global state, while `SceneControl` handles all scene transitions.

## Goals

1. **Centralize scene management** — All scene loading/unloading and transition logic in `SceneControl.cs`
2. **Centralize circle wipe transitions** — Move all circle wipe overlay, material, and animation logic to `SceneControl`
3. **Support instant initial load** — First scene load (Game or Lobby) happens instantly without transition effect
4. **Support conditional initial scene** — On first run, load `Game` scene (Mission-1-1); otherwise load `Lobby` scene
5. **Clean up GlobalControl** — Remove all scene management and transition code from `GlobalControl.cs`
6. **No `Start()` in SceneControl** — Initialization is triggered explicitly by `GlobalControl.TriggerInitialLoad()`

## Non-Goals

- Changing the circle wipe shader itself
- Modifying `SaveController` logic beyond reading `firstRun`
- Adding new scene types or transition effects (out of scope for this refactor)

## Scope

**In scope:**
- Moving scene transition state machine from `GlobalControl` to `SceneControl`
- Moving circle wipe overlay canvas/image/material management to `SceneControl`
- Implementing instant (no-transition) initial scene load
- Reading `firstRun` from `SaveController` to determine initial scene
- Providing a public API for other systems to request scene changes

**Out of scope:**
- Changing how `SoundsController` interacts with scene changes (already handled)
- Modifying mission loading logic in `MissionController`
- UI changes beyond the transition overlay

## References / Existing Files

**Docs:**
- `@/c:/Don/Projects/Unity/PROJ Mashu/Assets/AI-Analysis.md` — Project architecture overview

**Code:**
- `@/c:/Don/Projects/Unity/PROJ Mashu/Assets/Scripts/Controllers/GlobalScene/GlobalControl.cs` — Current scene management (to be refactored)
- `@/c:/Don/Projects/Unity/PROJ Mashu/Assets/Scripts/Controllers/GlobalScene/SceneControl.cs` — Target file (currently empty stub)
- `@/c:/Don/Projects/Unity/PROJ Mashu/Assets/Scripts/Data/SaveController.cs` — Contains `firstRun` value
- `@/c:/Don/Projects/Unity/PROJ Mashu/Assets/Shaders/UI_CircleWipeTransition.shader` — Circle wipe shader

## Architecture / Design

### Components

1. **SceneControl** (singleton)
   - Owns the transition overlay canvas, image, and material instance
   - Manages the transition state machine
   - Provides public API: `LoadSceneInstant()`, `ChangeScene()`, `IsTransitioning`
   - Does NOT use `void Start()` — initialized via `Init()` called by `GlobalControl`

2. **GlobalControl** (singleton, simplified)
   - Retains `DontDestroyOnLoad` and global singleton role
   - Calls `SceneControl.I.Init()` in its `Awake()`
   - Calls `SceneControl.I.TriggerInitialLoad()` when ready (after preroll ad, etc.)
   - Delegates `ChangeScene()` calls to `SceneControl`

### Persistent Scene Bootstrapping (GlobalScene)

Observed issue during implementation: **two `GlobalScene` instances appeared in the Hierarchy**.

Root cause:
- `Scripts/Controllers/GlobalScene/Bootstrap.cs` contains a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` method that calls:
  - `SceneManager.LoadScene("GlobalScene", LoadSceneMode.Additive)`
- If `GlobalScene` is already the **Build Settings index 0** startup scene, Unity will load it normally, and the bootstrap will also load it additively, resulting in **duplicate `GlobalScene`**.

Proposed fix:
- **Do not load/create `GlobalScene` from `Bootstrap` at all.**
- Make `GlobalScene` the **Build Settings index 0** startup scene.
- Use `Bootstrap` only for security checks / initialization *within* the already-loaded `GlobalScene`.

Implementation approach (preferred):
- Move `Bootstrap` to be a **scene object inside `GlobalScene`** (regular `Awake()` / `Start()`), and remove the runtime method that calls `LoadScene("GlobalScene", Additive)`.
- Ensure the objects that must persist are explicitly `DontDestroyOnLoad` (e.g., `GlobalControl`, `SaveController`, `AdController` (if used), and `SceneControl`).

Alternative approach (if `Bootstrap` must remain static):
- Keep `[RuntimeInitializeOnLoadMethod]`, but it should only:
  - perform security checks
  - ensure required singletons exist
  - and **never call `LoadScene("GlobalScene")`**.

Design invariant (after fix):
- `GlobalScene` is loaded exactly once and remains loaded for the lifetime of the session.

### Data Flow

```
[Game Boot]
    │
    ▼
GlobalControl.Awake()
    │
    ├─► SceneControl.I.Init()  (creates overlay, material, etc.)
    │
    ▼
GlobalControl.TriggerInitialLoad()
    │
    ├─► SaveController.I.GetValue("firstRun")
    │       │
    │       ├─ firstRun == "1" ──► SceneControl.I.LoadSceneInstant("Game")
    │       │                        └─► Sets currentMission = "Mission_1-1"
    │       │
    │       └─ firstRun != "1" ──► SceneControl.I.LoadSceneInstant("Lobby")
    │
    ▼
[Scene Loaded Instantly — No Wipe]
    │
    ▼
[Subsequent Scene Changes]
    │
    ├─► SceneControl.I.ChangeScene("Lobby") ──► Circle Wipe In → Unload → Load → Circle Wipe Out
    │
    └─► SceneControl.I.ChangeScene("Game")  ──► Circle Wipe In → Unload → Load → Circle Wipe Out
```

### API/Interfaces

**SceneControl public API:**
```csharp
public static SceneControl I;

// Initialize overlay and material (called by GlobalControl.Awake)
public void Init();

// Load a scene instantly without transition (used for initial load)
public void LoadSceneInstant(string sceneName);

// Load a scene with circle wipe transition
public void ChangeScene(string sceneName);
public void ChangeScene(string sceneName, bool reloadIfSame);

// State
public bool IsTransitioning { get; }
public string CurrentSceneName { get; }
```

**GlobalControl simplified API:**
```csharp
// Delegates to SceneControl
public void ChangeScene(string sceneName);
public void ChangeScene(string sceneName, bool reloadIfSame);
```

## Data / Settings / Configuration

**New settings:** None

**Existing settings used:**
- `SaveController.firstRun` — `"1"` = first run, `"0"` or missing = not first run
- `SaveController.currentMission` — Set to `"Mission_1-1"` on first run before loading Game scene

**Where stored:** PlayerPrefs via `SaveController`

**Defaults:**
- `firstRun` defaults to `"1"` in `SaveController.defaultJson`
- After first successful load, `firstRun` should be set to `"0"`

## Step-by-Step Integration Plan

### Phase 1: Implement SceneControl

1. **Add singleton and state fields to SceneControl**
   - Add `Awake()` for singleton setup (no `DontDestroyOnLoad` needed since it's in GlobalScene)
   - Add transition state enum and fields (moved from GlobalControl)
   - Add overlay canvas, image, and material fields

2. **Implement `Init()` method**
   - Create overlay canvas and transition image if not assigned
   - Initialize circle wipe material
   - Hide overlay by default

3. **Implement `LoadSceneInstant(string sceneName)`**
   - Load scene additively without any transition effect
   - Set as active scene
   - Update `_currentSceneName`

4. **Implement `ChangeScene()` with circle wipe**
   - Move the full state machine from GlobalControl
   - Handle wipe-in → unload → delay → load → set active → delay → wipe-out
   - **Important unload rule:** before unloading the previous content scene, set the active scene back to the persistent `GlobalScene`.
     - This prevents cases where Unity refuses to unload the currently-active scene or leaves objects alive.

5. **Implement `Update()` for transition state machine**
   - Move `UpdateTransitionState()` and `UpdateWipe()` logic

6. **Implement helper methods**
   - `EnsureOverlayReady()`, `EnsureCircleWipeMaterialReady()`
   - `BeginWipe()`, `SetCircleWipeRadius()`, `GetCircleWipeMaxRadius()`
   - `SetOverlayActive()`

### Phase 2: Update GlobalControl

7. **Remove transition fields and enums from GlobalControl**
   - Remove `_overlayCanvas`, `_transitionImage`, `_circleWipeMaterialInstance`, etc.
   - Remove `TransitionState` enum and state machine fields
   - Remove wipe-related fields

8. **Update GlobalControl.Awake()**
   - Keep singleton and `DontDestroyOnLoad`
   - Call `SceneControl.I.Init()` after own initialization

9. **Update GlobalControl.TriggerInitialLoad()**
   - Check `SaveController.I.GetValue("firstRun")`
   - If `"1"`: set `currentMission` to `"Mission_1-1"`, call `SceneControl.I.LoadSceneInstant("Game")`
   - Else: call `SceneControl.I.LoadSceneInstant("Lobby")`
   - Set `firstRun` to `"0"` and save

10. **Simplify GlobalControl.ChangeScene()**
    - Delegate directly to `SceneControl.I.ChangeScene()`
    - Keep `SoundsController.StopAllMusic()` call before delegating

11. **Remove all transition-related methods from GlobalControl**
    - Remove `BeginSceneTransition()`, `BeginWipe()`, `UpdateWipe()`
    - Remove `UpdateTransitionState()`, `EnsureOverlayReady()`, etc.
    - Remove camera-related transition methods if not needed elsewhere

### Phase 3: Cleanup and Verification

12. **Update GlobalControl.Update()**
    - Remove `UpdateTransitionState()` call
    - Keep only necessary updates (preroll timer, etc.)

13. **Verify scene references**
    - Ensure "Game" and "Lobby" are in Build Settings
    - Ensure "GlobalScene" is index 0 in Build Settings

13b. **Verify Bootstrap does not double-load GlobalScene**
    - Update `Scripts/Controllers/GlobalScene/Bootstrap.cs` so it does not load `GlobalScene` at all.
    - Ensure `Bootstrap` runs *from within* the already-loaded startup `GlobalScene` (preferred), or only performs security checks + singleton setup.
    - Verify in Play Mode Hierarchy: only one `GlobalScene` exists.

14. **Test first-run flow**
    - New save → loads Game scene instantly → Mission_1-1
    - Existing save → loads Lobby scene instantly

15. **Test subsequent transitions**
    - Lobby → Game: circle wipe works
    - Game → Lobby: circle wipe works

## Milestones

- **Milestone 1:** SceneControl fully implemented with Init, LoadSceneInstant, and ChangeScene
- **Milestone 2:** GlobalControl refactored to delegate to SceneControl
- **Milestone 3:** All transition code removed from GlobalControl, tests pass

## Security Considerations

- No network calls or sensitive data involved
- `firstRun` flag manipulation is safe (worst case: player replays tutorial)

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Scene load fails silently | Add null checks and fallback to synchronous load |
| Transition overlay not visible | Ensure canvas sorting order is highest (32767) |
| Race condition on first load | Use explicit state tracking, no async timing issues |
| Breaking existing ChangeScene callers | Keep GlobalControl.ChangeScene as a facade |
| Duplicate GlobalScene loaded | Fix `Bootstrap.cs` so it does not load `GlobalScene` additively when it is already the startup scene |
| Previous content scene not unloaded (e.g., Game remains loaded when switching to Lobby) | Ensure active scene is set to `GlobalScene` before unloading; track current content scene explicitly; validate unload `AsyncOperation` completion |

## Testing Plan

1. **First Run Test**
   - Clear PlayerPrefs
   - Launch game
   - Verify: loads directly to Game scene with Mission_1-1, no circle wipe

2. **Returning Player Test**
   - Launch with existing save (firstRun = "0")
   - Verify: loads directly to Lobby scene, no circle wipe

3. **Scene Transition Test**
   - From Lobby, trigger mission start
   - Verify: circle wipe in → scene unloads → Game loads → circle wipe out

4. **Return to Lobby Test**
   - From Game, trigger return to Lobby
   - Verify: circle wipe transition works correctly

5. **Reload Same Scene Test**
   - Call ChangeScene with same scene name
   - Verify: no transition occurs (unless reloadIfSame = true)

## Rollout Plan

1. Implement SceneControl in a single commit
2. Refactor GlobalControl in a second commit
3. Test locally
4. Merge to development branch
5. QA verification

## Deliverables

**Files to be added:**
- (None — SceneControl.cs already exists as stub)

**Files to be modified:**
- `Scripts/Controllers/GlobalScene/SceneControl.cs` — Full implementation
- `Scripts/Controllers/GlobalScene/GlobalControl.cs` — Remove transition code, delegate to SceneControl

**Files unchanged:**
- `Shaders/UI_CircleWipeTransition.shader`
- `Scripts/Data/SaveController.cs` (only read operations)
