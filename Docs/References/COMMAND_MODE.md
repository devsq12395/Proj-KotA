# Command Mode

## Summary
Command Mode is the in-mission RTS-style control layer.

It allows you to:
- Select owned units (single-click or drag-box multi-select)
- Issue movement orders (`Move` and `AttackMove`)
- Store and recall control groups (Ctrl+number to save, number to recall)
- Double-press a control group number to focus the camera on the group centroid
- Pan the camera freely while command mode is active

Command Mode is implemented as an overlay on top of the normal Game scene systems:
- It does **not** pause the game.
- It blocks normal player input routing while active.

## Key scripts
- `Scripts/Controllers/CommandModeController.cs`
  - Owns command-mode state, selection, selection UI markers, and order issuing.
- `Scripts/Controllers/InputController.cs`
  - Routes input to `CommandModeController` when active.
- `Scripts/Data/ObjectAIs/AI_PlayerCombatSuit.cs`
  - Optional AI helper for the player combat suit, enabled in Command Mode to auto-fire at nearby enemies.
- `Scripts/Data/ObjectAIs/AIBase.cs`
  - Executes `InGameObject.currentOrder` behavior for AI units.
- `Scripts/Data/Types/Types_Character.cs`
  - Defines `OrderType` and `Order`.

Related runtime flag:
- `InGameObject.movementState.suppressFacingFromMovement`
  - Used to prevent movement from overriding facing while a unit is aiming/firing.

## Entering / Exiting Command Mode
### Input binding
`CommandModeController` reads a Unity Input Manager button name:
- `CommandModeController.inputButtonName` (default: `CommandMode`)

When pressed:
- `CommandModeController.Toggle()` flips command mode on/off.

### Input routing (blocking normal gameplay input)
In `InputController.Update()`:
- Always checks `CommandModeController.I.HandleToggleInput()`.
- If `CommandModeController.I.IsCommandMode == true`:
  - calls `CommandModeController.I.HandleCommandModeInput()`
  - returns early, so the normal player movement / aim input does not run.

This is the core contract that makes Command Mode a “modal input layer”.

## Camera controls
While in command mode:
- `CommandModeController.UpdateCameraMovement()` pans the camera using:
  - `Horizontal` axis
  - `Vertical` axis

Speed:
- `CommandModeController.cameraMoveSpeed` (world units/sec)

Note:
- This moves `InGameCamera.I.transform.position` directly.

### Mode switch camera tweening
Entering / exiting Command Mode tweens the camera to the player with a short transition.

If `CameraZoomController` is present, the transition also tweens zoom (`Camera.orthographicSize`) using:
- `CameraZoomController.commandStartOrthoSize` when entering
- `CameraZoomController.pilotStartOrthoSize` when exiting

Related:
- Minimap click/drag camera pan and zoom (mode-specific zoom settings):
  - `Assets/Docs/Features/MINIMAP_AND_CAMERA_ZOOM.md`

## Selection
Command Mode selection operates on `InGameObject` instances where:
- `obj.owner == 1`

### Single-select
- Left click
- Implementation:
  - `CommandModeController.ClickSelect(...)`
  - Uses `Physics2D.OverlapPoint(worldPos)` and resolves an `InGameObject` from the collider.

### Drag-box multi-select
- Left mouse button down starts a possible drag
- If cursor moves beyond `dragSelectThresholdPixels`, it becomes a drag-select
- On mouse up:
  - computes a world-space rect from the screen-space box
  - selects all owned characters whose bounds overlap the rect

Bounds source:
- Prefers a child `Collider2D` bounds
- Falls back to sprite renderer bounds aggregation

### Selected unit markers
Selection UI is built from `selectedUiPrefab`.

Important behavior:
- The controller attempts to strip nested canvases from the prefab and only keep the `Image` marker to prevent world-space UI becoming “stuck”.

Markers are updated every frame in command mode:
- `UpdateSelectionMarkers()`

## Cards Bar (Unit Cards)
Command Mode can show a Cards Bar UI that displays a unit card for each player-owned unit.

Behaviors:
- Single-click selects.
- Shift-click adds to selection.
- Ctrl-click toggles selection.
- Double-click focuses the camera to the clicked unit.

Key scripts:
- `Scripts/UI/MainGame/CommandMode/CardsBarController.cs`
- `Scripts/UI/MainGame/CommandMode/UnitCardView.cs`

## Ship manage buttons
If `shipManageButtonPrefab` is assigned, command mode spawns an extra world-space button for any selected unit whose `role` indicates it is a ship:
- `role == "ship"` or `role == "base"`

Implementation:
- `UpdateShipManageButtons()`

### Ship Management panel transitions
If the Command Mode UI has both a Cards Bar and a Ship Management panel, `CommandButtonsBarController` coordinates the transition:
- Opening Ship Management:
  - Cards Bar hides first (Y-scale tween)
  - Ship Management shows after (Y-scale tween)
- Closing Ship Management:
  - Ship Management hides first (Y-scale tween)
  - Cards Bar shows after (Y-scale tween)

The tween is designed to scale from the center (pivot Y = 0.5) for a “futuristic” expand/collapse look.

Close button wiring:
- Add a UI Button on the Ship Management panel and hook it to:
  - `CommandButtonsBarController.CloseShipManagementUI()`

## Command Buttons Bar (contextual commands)
Command Mode can show a contextual Command Buttons Bar based on the current selection.

Selection gating:
- Visible when:
  - Exactly 1 ship/base is selected, or
  - Multiple selected units are all the same non-ship role
- Hidden when:
  - Mixed roles are selected
  - Multiple ships/bases are selected

Ship Management access:
- `Access Ship Commands` command toggles the Ship Management UI.
- Hotkey: `Q` (when the command is available).

Key scripts:
- `Scripts/UI/MainGame/CommandMode/CommandButtons/CommandButtonsBarController.cs`
- `Scripts/UI/MainGame/CommandMode/CommandButtons/CommandButtonDatabase.cs`

## Issuing orders
### Order types
Orders are stored per unit in:
- `InGameObject.currentOrder : Order`

Order definitions:
- `OrderType.Move`
- `OrderType.AttackMove`
- `OrderType.MovePilotLike`

Defined in:
- `Scripts/Data/Types/Types_Character.cs`

Notes:
- `MovePilotLike` is used for player-owned combat suit-like characters in Command Mode to make right-click movement reuse the same movement path as Pilot Mode (`SetWalk` + acceleration/drift via `movementState.hasMoveInput`) and to spawn the same movement trail effects.
- Ships/bases still use `Move` and keep the existing move-to-target behavior.
- While a unit has an active combat target during these orders, it will face the target while firing (movement-facing is suppressed via `movementState.suppressFacingFromMovement`).

Simple style dash behavior:
- In `CommandModeStyle.Simple`, double right-clicking on empty ground attempts to dash eligible selected units toward the target while still issuing the underlying move order.

### How orders are issued
Orders are issued by `CommandModeController.IssueOrderToSelection(type, targetPos)`.

Behavior:
- For each selected unit:
  - ensure `u.currentOrder` exists
  - set:
    - `currentOrder.type`
    - `currentOrder.targetPos`
    - `currentOrder.stopDistance`
    - `currentOrder.issuedTime`

Per-unit Move behavior:
- When `IssueOrderToSelection(OrderType.Move, ...)` is used, the controller will assign `OrderType.MovePilotLike` for player-owned non ship/base characters.
- This keeps UI and input behavior stable (right-click ground and the Move button still call `Move`) while allowing combat suits to have pilot-like feel.

### Formation behavior
When issuing orders to multiple units:
- The controller computes the centroid of the selected group.
- Each unit keeps its relative offset from that centroid.

So the issued target becomes:
- `finalTarget = targetPos + (unitPos - centroid)`

This preserves formation spacing during group moves.

## Command order visuals
Command Mode can show order visuals when issuing orders and while selected units have active orders.

Behavior:
- On order issue, a target ping effect is spawned at the clicked location.
- For each currently selected unit with an active `Move` / `AttackMove` / `MovePilotLike` order, a sprite-based moving trail is shown from the unit to its `currentOrder.targetPos`.
- Trails are selection-scoped:
  - If a unit is not selected, its trail is hidden.
  - Trails are hidden when Command Mode exits.

Implementation:
- `OrderTrailSegment` is a pooled sprite segment that moves at constant speed toward the target.
- Segments spawn based on a gap distance, and are returned to the pool when they reach the target.
- On order issue, trails are *primed* so the full path shows immediately.
- For very long distances, the system increases the effective gap to cap the number of active segments per trail.

Editor wiring (in `CommandModeController`):
- `orderTargetEffectId`
- `orderTrailSegmentPrefab`
- `orderTrailSegmentSpeed`
- `orderTrailGapDistance`
- `moveTrailColor`
- `attackMoveTrailColor`
- `orderTrailZ`
- `segmentAlphaAtSpawn`
- `segmentAlphaFullAtPathPercent`

## Advanced vs Simple command mode styles
`CommandModeStyle`:
- `Simple`
  - Right-click on empty ground issues a `Move` order immediately.
- `Advanced`
  - Clicking on ground spawns a world-space command buttons prefab (`commandButtonsPrefab`) at the clicked position.
  - Buttons named:
    - `BTN_Move`
    - `BTN_AttackMove`
  - Bind to issue the order and then destroy the command buttons instance.

## Control groups
Control groups are stored as lists of stable `InGameObject.id` values.

### Save
- Ctrl + number (0-9)
- Saves the currently selected units into that group.

### Recall
- Number (0-9)
- Rebuilds selection by finding owned units whose `id` is in the saved list.

If the control group is empty/missing, recall clears selection.

### Camera focus
- Double press the group number within `controlGroupDoublePressWindowSeconds`
- Focuses `InGameCamera` to the centroid of the group.

## Dependencies / Scene wiring
The feature assumes the following exist and are configured:
- `CommandModeController` is present in the `Game` scene.
- Unity Input Manager button mapping exists for `CommandMode`.
- `selectedUiPrefab` is assigned (optional, but recommended).
- `commandButtonsPrefab` is assigned if using Advanced style.
- `InputController` is present and runs in the `Game` scene.

Optional combat-suit auto-fire:
- The player combat suit can have `AI_PlayerCombatSuit` attached on spawn and toggled on/off by `CommandModeController.Enter()`/`Exit()`.
- This means the auto-fire only runs during Command Mode.

Optional UI wiring:
- Cards Bar:
  - Add `CardsBarController` in the Command Mode UI hierarchy.
  - Assign its `cardsBarRoot`, `unitCardsContent`, and `unitCardPrefab`.
- Command Buttons Bar:
  - Add `CommandButtonsBarController` in the Command Mode UI hierarchy.
  - Assign `buttonsBarRoot`, `buttonsContent`, and `database`.
  - Assign `shipManagementRoot` and `cardsBarRoot` for visibility coordination.

## Verification checklist
- Toggle command mode on/off.
- In command mode:
  - Click-select a unit.
  - Drag-select multiple units.
  - Issue a Move order.
  - For player-owned combat suits, verify Move uses pilot-like feel (accel/drift + movement trail).
  - Issue an AttackMove order.
  - Double right click on ground: verify dash attempts for eligible selected units and respects cooldown.
  - Save and recall a control group.
  - Double press a control group to focus camera.
- Exit command mode:
  - Verify normal player input is restored.

---

# Source Code Reference

## Core Command Mode Controller

### Class Structure
**File:** `Scripts/Controllers/CommandModeController.cs`

```csharp
public class CommandModeController : MonoBehaviour
{
  public static CommandModeController I;

  public enum CommandModeStyle
  {
    Simple,
    Advanced,
  }

  [Header("Hotkey")]
  public string inputButtonName = "CommandMode";

  [Header("Camera")]
  public float cameraMoveSpeed = 12f;

  [Header("UI")]
  public GameObject pilotModeUIRoot;
  public GameObject commandModeUIRoot;
  public GameObject selectedUiPrefab;
  public Vector3 selectedUiScale = new Vector3(5f, 5f, 1f);
  public float selectedUiPaddingWorld = 0.15f;

  [Header("Command Mode Style")]
  public CommandModeStyle commandModeStyle = CommandModeStyle.Simple;

  [Header("RTS Orders UI")]
  public GameObject commandButtonsPrefab;
  public float dragSelectThresholdPixels = 12f;
  public Color dragSelectBoxColor = new Color(0f, 1f, 1f, 0.15f);

  bool isCommandMode;
  InGameObject selected;
  readonly List<InGameObject> selectedUnits = new List<InGameObject>();
  readonly Dictionary<int, SelectionMarkers> selectionMarkers = new Dictionary<int, SelectionMarkers>();
  readonly Dictionary<int, List<int>> controlGroupIds = new Dictionary<int, List<int>>();

  public bool IsCommandMode => isCommandMode;
  public InGameObject Selected => selected;
  public List<InGameObject> SelectedUnits => selectedUnits;
}
```

---

## Enter/Exit Command Mode

### Toggle Method
**File:** `Scripts/Controllers/CommandModeController.cs`

```csharp
public void Toggle()
{
  if (IsPlayerDocked())
  {
    if (!isCommandMode) Enter();
    return;
  }

  if (!isCommandMode) Enter();
  else Exit();
}

bool IsPlayerDocked()
{
  if (MainGameController.I == null || MainGameController.I.player == null) return false;
  var p = MainGameController.I.player;
  var docked = p.GetComponent<DockedUnitState>();
  return (docked != null && docked.isDocked);
}
```

### Enter Command Mode
**File:** `Scripts/Controllers/CommandModeController.cs`

```csharp
public void Enter()
{
  isCommandMode = true;

  EnsureOrderTrailVisuals();
  if (orderTrailVisuals != null) orderTrailVisuals.HideAll();

  if (MainGameController.I != null && MainGameController.I.player != null)
  {
    InGameObject p = MainGameController.I.player;
    var suitAI = p.gameObject != null ? p.gameObject.GetComponent<AI_PlayerCombatSuit>() : null;
    if (suitAI != null) suitAI.enabled = true;
    if (p.movementState == null) p.movementState = new InGameObject.MovementRuntimeState();
    p.movementState.hasMoveInput = false;
    if (CharacterController.I != null)
    {
      CharacterController.I.StopAllMotion(p);
    }
    else if (MovementController.I != null)
    {
      MovementController.I.StopVelocity(p);
      if (p.moveToTarget != null) p.moveToTarget.isActive = false;
    }

    prevPlayerIsAI = p.isAI;
    p.isAI = true;
    playerOrderAI = p.GetComponent<AIBase>();
    if (playerOrderAI == null)
    {
      playerOrderAI = p.gameObject.AddComponent<AIBase>();
      createdPlayerOrderAI = true;
      prevPlayerOrderAIEnabled = false;
    }
    else
    {
      createdPlayerOrderAI = false;
      prevPlayerOrderAIEnabled = playerOrderAI.enabled;
    }
    playerOrderAI.enabled = true;
  }

  selected = null;
  lastSelectedId = 0;
  selectedUnits.Clear();
  ClearSelectionMarkers();
  ClearShipManageButtons();
  DestroyCommandButtons();
  if (InGameCamera.I != null)
  {
    var target = ResolveCommandModeCameraTarget();
    if (target != null)
    {
      InGameCamera.I.SetTarget(target);
      float targetZoom = 0f;
      Camera cam = InGameCamera.I.cam;
      if (cam == null) cam = InGameCamera.I.GetComponent<Camera>();
      var zoomCtrl = Object.FindObjectOfType<CameraZoomController>();
      if (zoomCtrl != null) targetZoom = zoomCtrl.commandStartOrthoSize;
      if (cam != null && targetZoom > 0f)
      {
        InGameCamera.I.StartTweenToTarget(target, 0.12f, targetZoom, false);
      }
      else
      {
        InGameCamera.I.StartTweenToTarget(target, 0.12f, false);
      }
    }
    InGameCamera.I.DisablePointToTarget();
  }
  ResolveUIRoots();
  ApplyUIMode();
  EnsureDragBoxUI();
  ShowDragBox(false);
}
```

**Key behaviors on enter:**
- Stops player movement
- Enables `AI_PlayerCombatSuit` for auto-fire
- Adds/enables `AIBase` component to player for order execution
- Clears selection and UI
- Tweens camera to player/base with command mode zoom
- Swaps UI roots (hides Pilot Mode UI, shows Command Mode UI)

### Exit Command Mode
**File:** `Scripts/Controllers/CommandModeController.cs`

```csharp
public void Exit()
{
  if (IsPlayerDocked()) return;

  isCommandMode = false;

  if (orderTrailVisuals != null) orderTrailVisuals.HideAll();

  if (MainGameController.I != null && MainGameController.I.player != null)
  {
    InGameObject p = MainGameController.I.player;
    var suitAI = p.gameObject != null ? p.gameObject.GetComponent<AI_PlayerCombatSuit>() : null;
    if (suitAI != null) suitAI.enabled = false;
    if (p.currentOrder != null) p.currentOrder = null;
    if (p.movementState == null) p.movementState = new InGameObject.MovementRuntimeState();
    p.movementState.hasMoveInput = false;

    if (CharacterController.I != null)
    {
      CharacterController.I.StopAllMotion(p);
    }
    else if (MovementController.I != null)
    {
      MovementController.I.StopVelocity(p);
      if (p.moveToTarget != null) p.moveToTarget.isActive = false;
    }

    if (createdPlayerOrderAI && playerOrderAI != null)
    {
      Destroy(playerOrderAI);
    }
    else if (playerOrderAI != null)
    {
      playerOrderAI.enabled = prevPlayerOrderAIEnabled;
    }

    p.isAI = prevPlayerIsAI;
    playerOrderAI = null;
    createdPlayerOrderAI = false;
    prevPlayerOrderAIEnabled = false;
  }

  selected = null;
  lastSelectedId = 0;
  selectedUnits.Clear();
  ClearSelectionMarkers();
  ClearShipManageButtons();
  DestroyCommandButtons();
  isPointerDown = false;
  isDraggingSelect = false;
  ShowDragBox(false);

  if (InGameCamera.I != null && MainGameController.I != null && MainGameController.I.player != null)
  {
    InGameCamera.I.SetTarget(MainGameController.I.player.transform);
    float targetZoom = 0f;
    Camera cam = InGameCamera.I.cam;
    if (cam == null) cam = InGameCamera.I.GetComponent<Camera>();
    var zoomCtrl = Object.FindObjectOfType<CameraZoomController>();
    if (zoomCtrl != null) targetZoom = zoomCtrl.pilotStartOrthoSize;
    if (cam != null && targetZoom > 0f)
    {
      InGameCamera.I.StartTweenToTarget(MainGameController.I.player.transform, 0.12f, targetZoom, true);
    }
    else
    {
      InGameCamera.I.StartTweenToTarget(MainGameController.I.player.transform, 0.12f, true);
    }
  }

  ResolveUIRoots();
  ApplyUIMode();
}
```

**Key behaviors on exit:**
- Prevents exit if player is docked
- Disables `AI_PlayerCombatSuit`
- Clears player's current order
- Removes/disables `AIBase` component from player
- Restores player's original AI state
- Tweens camera back to player with pilot mode zoom
- Swaps UI roots back to Pilot Mode

---

## Selection System

### Single-Click Selection
**File:** `Scripts/Controllers/CommandModeController.cs`

```csharp
void ClickSelect(Vector2 screenPos)
{
  InGameObject obj = ResolveOwnedObjectAtScreen(screenPos);
  if (obj == null)
  {
    if (commandModeStyle == CommandModeStyle.Advanced && selectedUnits.Count > 0)
    {
      HandleGroundClick(screenPos);
    }
    else
    {
      ClearSelection();
    }
    return;
  }
  SetSelectionSingle(obj);
}

InGameObject ResolveOwnedObjectAtScreen(Vector2 screenPos)
{
  if (InputController.I != null)
  {
    if (InputController.I.IsPointerOverUIForCommandMode(screenPos)) return null;
  }

  Vector2 wp;
  if (!TryScreenToWorld2D(screenPos, out wp)) return null;
  Collider2D c = Physics2D.OverlapPoint(wp);
  if (c == null) return null;

  InGameObject obj = c.GetComponentInParent<InGameObject>();
  if (obj == null) return null;
  if (obj.owner != 1) return null;
  return obj;
}

void SetSelectionSingle(InGameObject obj)
{
  selectedUnits.Clear();
  if (obj != null) selectedUnits.Add(obj);
  selected = obj;
  if (selected != null && selected.id != 0 && selected.id != lastSelectedId)
  {
    lastSelectedId = selected.id;
  }
  else if (selected == null)
  {
    lastSelectedId = 0;
  }
  RebuildSelectionMarkers();
  if (orderTrailVisuals != null) orderTrailVisuals.RefreshForSelection(selectedUnits);
}
```

### Drag-Box Multi-Select
**File:** `Scripts/Controllers/CommandModeController.cs`

```csharp
void UpdateSelection()
{
  // ... (mouse down handling)

  if (Input.GetMouseButtonDown(0))
  {
    if (InputController.I != null)
    {
      if (InputController.I.IsPointerOverUIForCommandMode(Input.mousePosition)) return;
    }

    isPointerDown = true;
    isDraggingSelect = false;
    dragStartScreen = Input.mousePosition;
    dragCurrentScreen = dragStartScreen;
  }

  if (isPointerDown && Input.GetMouseButton(0))
  {
    dragCurrentScreen = Input.mousePosition;

    if (!isDraggingSelect)
    {
      float threshold = Mathf.Max(0f, dragSelectThresholdPixels);
      if ((dragCurrentScreen - dragStartScreen).sqrMagnitude >= threshold * threshold)
      {
        isDraggingSelect = true;
        ShowDragBox(true);
      }
    }

    if (isDraggingSelect)
    {
      UpdateDragBox(dragStartScreen, dragCurrentScreen);
    }
  }

  if (isPointerDown && Input.GetMouseButtonUp(0))
  {
    isPointerDown = false;

    if (isDraggingSelect)
    {
      isDraggingSelect = false;
      ShowDragBox(false);
      DragSelect(dragStartScreen, dragCurrentScreen);
    }
    else
    {
      ClickSelect(Input.mousePosition);
    }
  }
}

void DragSelect(Vector2 startScreen, Vector2 endScreen)
{
  Rect worldRect;
  if (!TryGetWorldRectFromScreenRect(startScreen, endScreen, out worldRect))
  {
    ClearSelection();
    return;
  }

  selectedUnits.Clear();
  if (CharacterController.I != null && CharacterController.I.characters != null)
  {
    for (int i = 0; i < CharacterController.I.characters.Count; i++)
    {
      InGameObject c = CharacterController.I.characters[i];
      if (c == null) continue;
      if (c.owner != 1) continue;
      Bounds b;
      if (!TryGetSelectionBounds(c, out b)) continue;
      Rect br = BoundsToRect2D(b);
      if (!worldRect.Overlaps(br, true)) continue;
      selectedUnits.Add(c);
    }
  }

  selected = selectedUnits.Count > 0 ? selectedUnits[0] : null;
  if (selected != null && selected.id != 0)
  {
    lastSelectedId = selected.id;
  }
  else
  {
    lastSelectedId = 0;
  }

  RebuildSelectionMarkers();
  if (orderTrailVisuals != null) orderTrailVisuals.RefreshForSelection(selectedUnits);
}
```

**Drag-select behavior:**
1. Mouse down starts potential drag
2. If cursor moves beyond `dragSelectThresholdPixels` (12px), becomes drag-select
3. On mouse up, converts screen-space box to world-space rect
4. Selects all owned units whose bounds overlap the rect
5. Uses `Collider2D` bounds if available, falls back to sprite renderer bounds

---

## Camera Movement

**File:** `Scripts/Controllers/CommandModeController.cs`

```csharp
void UpdateCameraMovement()
{
  if (InGameCamera.I == null) return;

  float h = Input.GetAxisRaw("Horizontal");
  float v = Input.GetAxisRaw("Vertical");

  Vector3 delta = new Vector3(h, v, 0f);
  if (delta.sqrMagnitude <= 0.0001f) return;

  float speed = Mathf.Max(0f, cameraMoveSpeed);

  Vector3 cur = InGameCamera.I.transform.position;
  Vector2 desired = new Vector2(cur.x, cur.y) + ((Vector2)delta.normalized * speed * Time.deltaTime);
  desired = ClampCameraCenterInsideMission(desired);
  InGameCamera.I.transform.position = new Vector3(desired.x, desired.y, cur.z);
}

Vector2 ClampCameraCenterInsideMission(Vector2 desired)
{
  Camera cam = null;
  if (InGameCamera.I != null)
  {
    cam = InGameCamera.I.cam;
    if (cam == null) cam = InGameCamera.I.GetComponent<Camera>();
  }
  if (cam == null) return desired;

  if (!TryGetMissionBounds(out var center, out float halfX, out float halfY)) return desired;

  float halfViewH = cam.orthographicSize;
  float halfViewW = cam.orthographicSize * cam.aspect;

  float minX = center.x - halfX + halfViewW;
  float maxX = center.x + halfX - halfViewW;
  float minY = center.y - halfY + halfViewH;
  float maxY = center.y + halfY - halfViewH;

  float x;
  if (minX > maxX) x = center.x;
  else x = Mathf.Clamp(desired.x, minX, maxX);

  float y;
  if (minY > maxY) y = center.y;
  else y = Mathf.Clamp(desired.y, minY, maxY);

  return new Vector2(x, y);
}
```

**Camera controls:**
- Uses `Horizontal` and `Vertical` input axes (WASD/Arrow keys)
- Speed: `cameraMoveSpeed` (default 12 world units/sec)
- Clamped to mission boundaries accounting for camera viewport size

---

## Selection Markers

**File:** `Scripts/Controllers/CommandModeController.cs`

```csharp
class SelectionMarkers
{
  public InGameObject unit;
  public GameObject tl;
  public GameObject tr;
  public GameObject bl;
  public GameObject br;
}

void CreateSelectionMarkersFor(InGameObject unit)
{
  if (selectedUiPrefab == null) return;
  if (unit == null) return;

  EnsureSelectionMarkersRoot();
  Transform parent = selectionMarkersRoot != null ? selectionMarkersRoot : ResolveWorldSpaceUIParent();
  SelectionMarkers m = new SelectionMarkers();
  m.unit = unit;
  int id = unit.GetInstanceID();
  m.tl = InstantiateSelectedMarker(parent, $"sel-ui-tl-{id}");
  m.tr = InstantiateSelectedMarker(parent, $"sel-ui-tr-{id}");
  m.bl = InstantiateSelectedMarker(parent, $"sel-ui-bl-{id}");
  m.br = InstantiateSelectedMarker(parent, $"sel-ui-br-{id}");
  selectionMarkers[unit.GetInstanceID()] = m;
}

void UpdateSelectionMarkersFor(InGameObject unit)
{
  if (unit == null) return;
  SelectionMarkers m;
  if (!selectionMarkers.TryGetValue(unit.GetInstanceID(), out m)) return;

  float pad = Mathf.Max(0f, selectedUiPaddingWorld);
  Vector3 min;
  Vector3 max;

  Bounds b;
  if (TryGetSelectionBounds(unit, out b)){
    min = b.min;
    max = b.max;
  } else {
    Vector3 p = unit.transform.position;
    float half = 0.35f;
    min = new Vector3(p.x - half, p.y - half, p.z);
    max = new Vector3(p.x + half, p.y + half, p.z);
  }
  min.x -= pad;
  min.y -= pad;
  max.x += pad;
  max.y += pad;

  float z = unit.transform.position.z;
  Vector3 tl = new Vector3(min.x, max.y, z);
  Vector3 tr = new Vector3(max.x, max.y, z);
  Vector3 bl = new Vector3(min.x, min.y, z);
  Vector3 br = new Vector3(max.x, min.y, z);

  ApplyCornerTransform(m.tl, tl, new Vector3(1f, 1f, 1f));
  ApplyCornerTransform(m.tr, tr, new Vector3(-1f, 1f, 1f));
  ApplyCornerTransform(m.bl, bl, new Vector3(1f, -1f, 1f));
  ApplyCornerTransform(m.br, br, new Vector3(-1f, -1f, 1f));
}
```

**Selection marker system:**
- Creates 4 corner markers (top-left, top-right, bottom-left, bottom-right) per selected unit
- Positions markers at unit's bounds corners with padding
- Updates every frame to follow moving units
- Strips nested Canvas components from prefab to prevent UI positioning issues
