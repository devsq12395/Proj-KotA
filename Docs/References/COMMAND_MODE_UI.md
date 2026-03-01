# Command Mode UI (Cards Bar + Ship Management)

## Summary
This document describes the **Command Mode UI layer**, specifically:
- **Cards Bar** (unit cards)
- **Command Buttons Bar** (contextual commands)
- **Ship Management** UI (ship/base-only panel)
- **Ship Management tabs** and the **Launch Bay** page

The goal of this UI is to provide RTS-style selection + command issuance while staying consistent with the underlying Command Mode state managed by `CommandModeController`.

## Key scripts
### Runtime controllers
- `Scripts/Controllers/CommandModeController.cs`
  - Source of truth for Command Mode on/off state.
  - Source of truth for unit selection (`SelectedUnits`).
  - Provides public selection mutation methods used by UI.

- `Scripts/UI/MainGame/CommandMode/CardsBarController.cs`
  - Creates and updates unit cards for owned units.
  - Applies selection highlight state to cards.
  - Drives selection mutations through `CommandModeController` public APIs.

- `Scripts/UI/MainGame/CommandMode/UnitCardView.cs`
  - Individual card view + click handling.
  - Implements single select / add / toggle patterns.
  - Implements double-click focus via `InGameCamera.StartTweenToTarget`.

- `Scripts/UI/MainGame/CommandMode/CommandButtons/CommandButtonsBarController.cs`
  - Shows contextual command buttons based on selection gating.
  - Owns the Ship Management open/close transition.
  - Tracks the current Ship Management target via `CurrentShipManagementUnit`.

- `Scripts/UI/MainGame/CommandMode/ShipManagementTabsController.cs`
  - Simple tab controller:
    - button -> pageRoot activation
    - optional default tab selection

- `Scripts/UI/MainGame/CommandMode/ShipManagementLaunchBayController.cs`
  - Launch Bay page controller.
  - Lists docked units from the selected ship/base’s `DockingBay`.
  - Uses `CommandButtonsBarController.CurrentShipManagementUnit` as its primary context.

- `Scripts/UI/MainGame/CommandMode/ShipManagementGunPageController.cs`
  - Gun page controller used by Ship Management pages (Main Guns / AA Guns).
  - Shows module icon + weapon stats.
  - Edits crew assignment and persists it to save data.

### Docking-related
- `Scripts/Objects/DockingBay.cs`
  - Per-ship/base docking storage (`dockedUnits`).

- `Scripts/Objects/DockedUnitState.cs`
  - Tracks docked state on a unit.
  - While docked:
    - hides renderers/colliders
    - disables AI scripts
    - blocks module update/trigger (via `Checks_MainGame`)
    - moves the unit to an off-map parking position
    - removes it from Command Mode selection

## UI visibility model
The Command Mode UI is designed around these principles:
- **Selection is owned by `CommandModeController`**; UI is a view/controller on top of it.
- **Ship Management is not “auto-shown every frame”**; it is intentionally gated by a command/hotkey.
- **UI hiding should prefer `CanvasGroup` over `SetActive(false)`** for roots that own scripts.
  - Disabling the GameObject can break update loops and state recovery when selection changes.

## Cards Bar
### Purpose
The Cards Bar provides a fast UI selection surface for owned units.

### Unit icon resolution
Cards (and other Command Mode UI surfaces) resolve the unit "machine icon" in this order:
1) `InGameObject.machineIcon` (authoritative override)
2) Sprite-part based fallback (e.g., `allSpriteParts`)
3) Renderer-based fallback (scan `SpriteRenderer` children)

To keep icons consistent across UI, prefer setting `InGameObject.machineIcon` on prefabs.

Spawn-time auto-fill:
- `CharacterController.CreateCharacter(...)` attempts to auto-fill `InGameObject.machineIcon` from `DB_MechsAndPilots.combatSuits[*].icon` when the spawned unit matches a `CombatSuit` entry and `machineIcon` is unset.

### Expected behavior
- Clicking a card mutates Command Mode selection:
  - single click: select only that unit
  - modifier click: add / toggle
- Double-click focuses camera to the unit.

### Selection integration (important)
Cards Bar should not mutate `CommandModeController.SelectedUnits` directly.

Instead it should use the public wrappers:
- `SetSelectionSingle_Public(...)`
- `AddToSelection_Public(...)`
- `RemoveFromSelection_Public(...)`
- `ToggleSelection_Public(...)`
- `ClearSelection_Public()`

This ensures:
- selection markers are rebuilt
- ship manage buttons are rebuilt
- order trail visuals refresh correctly

### Docked units
Docked units are excluded from Cards Bar population.

## Command Buttons Bar
### Purpose
Provides contextual commands based on selection, including Ship Management access.

### Selection gating (high level)
Typical gating rules:
- show when selection is compatible (e.g., exactly one ship/base, or consistent non-ship roles)
- hide for mixed roles or invalid selection

### Ship Management access
Ship Management is opened via:
- a command button (`Access Ship Commands`)
- hotkey `Q` (when the command is available)

### Ship Management context tracking
When Ship Management is opened, `CommandButtonsBarController` sets:
- `CurrentShipManagementUnit` (the ship/base being managed)

This value is used by Ship Management pages (e.g., Launch Bay) to avoid relying on selection during transitions.

## Ship Management UI
### Purpose
Ship Management is a ship/base-only panel that replaces the Cards Bar while open.

### Open/close transitions
`CommandButtonsBarController` coordinates the transition:
- **Open**:
  - hide Cards Bar first
  - show Ship Management next
  - then select default tab (`ShipManagementTabsController.SelectDefaultTab()`)

- **Close**:
  - hide Ship Management first
  - show Cards Bar next

This sequence exists to prevent both panels being visible simultaneously and to keep the user’s attention focused.

## Ship Management tabs
`ShipManagementTabsController` is an Editor-driven tab list:
- each tab entry contains:
  - `tabId`
  - `tabButton`
  - `pageRoot`

Rules:
- exactly one `pageRoot` is active at a time
- default tab is selected on Ship Management open

## Gun pages (Main Guns / AA Guns)

Gun pages are Ship Management pages that display and edit crew assignment for a ship module slot.

Controller:

- `ShipManagementGunPageController`
  - `moduleSlot` selects which ship module slot is shown.
    - example values: `mainGun`, `aaGuns`

Context resolution order:

1) `CommandButtonsBarController.CurrentShipManagementUnit`
2) Command Mode selection fallback (only if needed)

UI binding:

- The controller can auto-resolve child objects by name (optional):
  - `I_MainGun` (Image for the module icon)
  - `T_GunName`
  - `T_Data`
  - `T_Status`
  - `Crew_Panel/T_AssignedCrews`
  - `Crew_Panel/BTN_More`
  - `Crew_Panel/BTN_Less`

Displayed data:

- Module icon comes from `ModuleBase.icon`.
- Assigned crew is displayed as:
  - `assigned` and `max` are displayed separately (typically `assigned` + `"/" + max`)
- Range is displayed in kilometers using:
  - `ModuleBase_MainGun.maxRange` (world units)
  - `MainGameController.metersPerWorldUnit` for conversion
- With `crewCount == 0` (for crewed ship weapons), the page shows:
  - `Fire Rate: 0sec.`
  - `Accuracy: 0%`

Crew assignment editing:

- `BTN_More` / `BTN_Less` adjust:
  - `ModuleBase.crewCount`
  - `InGameObject.crewCountIdle`
- Assignments are persisted to save data using:
  - `SaveController.TrySetShipCrewAssigned(shipIndex, moduleSlot, assigned, saveNow: true)`

## Launch Bay page
### Data source
Launch Bay lists docked units from:
- `DockingBay.dockedUnits` on the current ship/base.

### Docked unit icons
Launch Bay docked unit cards use the same machine icon resolution priority as the Cards Bar.

### Context resolution
The Launch Bay controller resolves its ship/base context in this order:
1) `CommandButtonsBarController.CurrentShipManagementUnit`
2) Command Mode selection fallback (only if needed)

This avoids transient selection flicker during UI transitions.

### Refresh / rebuild strategy
Launch Bay rebuilds deterministically:
- on page enable
- after a successful launch action

It does not rely on per-frame polling.

### Docked unit card
`DockedUnitCardView` binds to a docked unit and provides a Launch button.

`ShipManagementLaunchBayController.RequestLaunch(unit)` calls:
- `DockingBay.Launch(unit)`

which:
- removes it from the bay list
- clears `DockedUnitState.isDocked`
- positions it at a launch point and gives it an initial move-to-target

## Common pitfalls and debugging
### CanvasGroup alpha hiding
If a UI root or any parent retains `CanvasGroup.alpha = 0`, children can exist in hierarchy but be invisible.

### ScrollRect / Content issues
If cards exist but are not visible, check:
- `ScrollRect.viewport` masking
- `contentRoot` anchored position
- layout rebuild after list changes

### Selection flicker during UI transitions
Selection-based UI should avoid rebuilding from selection every frame while an open/close tween is running.

Using `CurrentShipManagementUnit` prevents Ship Management pages from losing their target context.

## Verification
- Enter Command Mode.
- Verify Cards Bar appears and selection via cards matches world selection.
- Select a ship/base and open Ship Management via `Q`.
- Verify Cards Bar hides and Ship Management shows.
- Verify tabs switch pages.
- Verify Launch Bay lists docked units and Launch undocks + spawns the unit correctly.
- Verify docked units are:
  - hidden
  - not on minimap
  - not selectable / not orderable
  - not shooting / AI disabled

---

# Source Code Reference

## Cards Bar Controller

### Class Structure
**File:** `Scripts/UI/MainGame/CommandMode/CardsBarController.cs`

```csharp
public class CardsBarController : MonoBehaviour
{
  [Header("UI")]
  public Transform cardsContent;
  public UnitCardView unitCardPrefab;
  public GameObject cardsBarRoot;
  public GameObject shipManagementRoot;

  [Header("Behavior")]
  public float reconcileIntervalSeconds = 0.25f;

  readonly Dictionary<int, UnitCardView> _cardsByInstanceId = new Dictionary<int, UnitCardView>();
  float _nextReconcileTime;
}
```

### Card Reconciliation
**File:** `Scripts/UI/MainGame/CommandMode/CardsBarController.cs`

```csharp
void ReconcileCards(bool force)
{
  if (!force && Time.time < _nextReconcileTime) return;
  _nextReconcileTime = Time.time + Mathf.Max(0.05f, reconcileIntervalSeconds);

  var chars = (CharacterController.I != null) ? CharacterController.I.characters : null;
  if (chars == null)
  {
    PruneMissingCards(null);
    return;
  }

  for (int i = 0; i < chars.Count; i++)
  {
    var u = chars[i];
    if (u == null || u.gameObject == null) continue;
    if (u.owner != 1) continue;

    var docked = u.GetComponent<DockedUnitState>();
    if (docked != null && docked.isDocked) continue;

    int key = u.GetInstanceID();
    if (_cardsByInstanceId.ContainsKey(key)) continue;
    CreateCardFor(u);
  }

  PruneMissingCards(chars);

  foreach (var kv in _cardsByInstanceId)
  {
    if (kv.Value == null) continue;
    kv.Value.RefreshDynamic();
  }
}

void CreateCardFor(InGameObject unit)
{
  if (unit == null || unit.gameObject == null) return;
  if (cardsContent == null || unitCardPrefab == null) return;

  var go = Instantiate(unitCardPrefab.gameObject, cardsContent);
  if (go == null) return;

  var view = go.GetComponent<UnitCardView>();
  if (view == null) return;
  view.Bind(this, unit);

  _cardsByInstanceId[unit.GetInstanceID()] = view;
}
```

**Card creation rules:**
- Creates cards for all player-owned units (`owner == 1`)
- Excludes docked units (checked via `DockedUnitState.isDocked`)
- Reconciles every `reconcileIntervalSeconds` (default 0.25s)
- Prunes cards for destroyed/non-owned/docked units

### Card Click Handling
**File:** `Scripts/UI/MainGame/CommandMode/CardsBarController.cs`

```csharp
public void OnCardClicked(UnitCardView view)
{
  if (view == null) return;
  var unit = view.Unit;
  if (unit == null || unit.gameObject == null) return;

  bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
  bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

  if (CommandModeController.I == null) return;

  if (ctrl)
  {
    CommandModeController.I.ToggleSelection_Public(unit);
  }
  else if (shift)
  {
    CommandModeController.I.AddToSelection_Public(unit);
  }
  else
  {
    CommandModeController.I.SetSelectionSingle_Public(unit);
  }
}

public void OnCardDoubleClicked(UnitCardView view)
{
  if (view == null) return;
  OnCardClicked(view);

  var unit = view.Unit;
  if (unit == null || unit.gameObject == null) return;
  if (InGameCamera.I == null) return;
  InGameCamera.I.StartTweenToTarget(unit.transform, 0.12f, false);
}
```

**Click behavior:**
- **Single click:** Select only that unit
- **Ctrl + click:** Toggle unit in/out of selection
- **Shift + click:** Add unit to selection
- **Double click:** Select unit and focus camera

---

## Unit Card View

### Class Structure
**File:** `Scripts/UI/MainGame/CommandMode/UnitCardView.cs`

```csharp
public class UnitCardView : MonoBehaviour, IPointerClickHandler
{
  [Header("UI")]
  public Image machineIcon;
  public Image pilotIcon;
  public Image selectedHighlight;
  public TMP_Text ENText;

  [Header("Bars")]
  public GameObject hpBarRoot;
  public Image hpFillMainImage;
  public Image hpFillWhiteImage;

  public GameObject enBarRoot;
  public Image enFillMainImage;
  public Image enFillWhiteImage;

  InGameObject _unit;
  CardsBarController _owner;

  public InGameObject Unit => _unit;
}
```

### Machine Icon Resolution
**File:** `Scripts/UI/MainGame/CommandMode/UnitCardView.cs`

```csharp
Sprite ResolveMachineIcon(InGameObject u)
{
  if (u == null || u.gameObject == null) return null;

  if (u.machineIcon != null) return u.machineIcon;

  if (u.allSpriteParts != null && u.allSpriteParts.Count > 0)
  {
    Sprite best = null;
    for (int i = 0; i < u.allSpriteParts.Count; i++)
    {
      var sp = u.allSpriteParts[i];
      if (sp == null) continue;

      Sprite candidate = sp.icon;
      if (candidate == null && sp.gameObject != null)
      {
        var spSr = sp.gameObject.GetComponent<SpriteRenderer>();
        if (spSr != null && spSr.sprite != null) candidate = spSr.sprite;
        else
        {
          var spChildSr = sp.gameObject.GetComponentInChildren<SpriteRenderer>(true);
          if (spChildSr != null && spChildSr.sprite != null) candidate = spChildSr.sprite;
        }
      }

      if (candidate == null) continue;
      if (!string.IsNullOrEmpty(sp.nameSlot))
      {
        var slot = sp.nameSlot.ToLowerInvariant();
        if (slot.Contains("core") || slot.Contains("body") || slot.Contains("hull")) return candidate;
      }
      if (best == null) best = candidate;
    }
    if (best != null) return best;
  }

  var srs = u.GetComponentsInChildren<SpriteRenderer>(true);
  if (srs != null && srs.Length > 0)
  {
    Sprite bestSprite = null;
    float bestArea = -1f;

    for (int i = 0; i < srs.Length; i++)
    {
      var sr = srs[i];
      if (sr == null || sr.sprite == null) continue;

      string n = sr.gameObject != null ? sr.gameObject.name.ToLowerInvariant() : string.Empty;
      if (n.Contains("core") || n.Contains("body") || n.Contains("hull")) return sr.sprite;

      var size = sr.bounds.size;
      float area = size.x * size.y;
      if (area > bestArea)
      {
        bestArea = area;
        bestSprite = sr.sprite;
      }
      if (bestSprite == null) bestSprite = sr.sprite;
    }

    if (bestSprite != null) return bestSprite;
  }

  return null;
}
```

**Icon resolution priority:**
1. `InGameObject.machineIcon` (authoritative override)
2. Sprite parts with "core", "body", or "hull" in slot name
3. Best sprite part icon (first non-null)
4. Child SpriteRenderer with "core", "body", or "hull" in name
5. Largest child SpriteRenderer by bounds area

### Dynamic Refresh
**File:** `Scripts/UI/MainGame/CommandMode/UnitCardView.cs`

```csharp
public void RefreshDynamic()
{
  if (_unit == null || _unit.gameObject == null)
  {
    SetHPBar(0f);
    SetENBar(null);
    SetENText(string.Empty, show: false);
    return;
  }

  int max = Mathf.Max(0, _unit.hpMax);
  int cur = Mathf.Clamp(_unit.hp, 0, max);
  SetHPBar(max > 0 ? (float)cur / (float)max : 0f);

  bool isShip = string.Equals(_unit.role, "ship") || string.Equals(_unit.role, "base");
  bool showEn = !isShip && _unit.energyData != null && _unit.energyData.energyMax > 0f;
  if (showEn)
  {
    float eMax = Mathf.Max(1f, _unit.energyData.energyMax);
    float e = Mathf.Clamp(_unit.energyData.energy, 0f, eMax);
    float n = Mathf.Clamp01(e / eMax);
    SetENBar(n);
    SetENText($"{Mathf.RoundToInt(e)}/{Mathf.RoundToInt(eMax)}", show: true);
  }
  else
  {
    SetENBar(null);
    SetENText(string.Empty, show: false);
  }
}
```

**Refresh behavior:**
- Updates HP bar every frame
- Shows energy bar for non-ship units with energy system
- Hides energy bar for ships/bases

---

## Command Buttons Bar Controller

### Class Structure
**File:** `Scripts/UI/MainGame/CommandMode/CommandButtons/CommandButtonsBarController.cs`

```csharp
public class CommandButtonsBarController : MonoBehaviour
{
  [Header("UI")]
  public GameObject buttonsBarRoot;
  public Transform buttonsContent;

  [Header("Visibility Targets")]
  public GameObject shipManagementRoot;
  public GameObject cardsBarRoot;

  [Header("Data")]
  public CommandButtonDatabase database;

  [Header("Ship Management Tween")]
  public bool tweenShipManagementPosition = true;
  public float shipManagementTweenDuration = 0.15f;
  public bool tweenCardsBarPosition = true;

  readonly List<GameObject> _spawnedButtons = new List<GameObject>();
  readonly HashSet<string> _visibleCommandIds = new HashSet<string>();

  public InGameObject CurrentShipManagementUnit { get; private set; }
}
```

### Ship Management Open/Close
**File:** `Scripts/UI/MainGame/CommandMode/CommandButtons/CommandButtonsBarController.cs`

```csharp
public void OpenShipManagementUI()
{
  if (shipManagementRoot == null) return;

  if (_shipUiOpening) return;

  var u = TryGetSingleSelectedShipLikeUnit();
  if (u == null)
  {
    return;
  }

  KillSequenceIfActive();

  _shipUiUnitInstanceId = u.GetInstanceID();
  CurrentShipManagementUnit = u;
  _shipUiOpening = true;
  _shipUiClosing = false;

  _shipUiSequence = DOTween.Sequence();
  if (cardsBarRoot != null && IsRootVisible(cardsBarRoot))
  {
    var tHideCards = TweenCardsBarPositionY(false, ref _cardsBarTween);
    if (tHideCards != null) _shipUiSequence.Append(tHideCards);
    else _shipUiSequence.AppendCallback(() => SetRootVisible(cardsBarRoot, false));
  }

  var tShowShip = TweenRootPositionY(shipManagementRoot, true, tweenShipManagementPosition, ref _shipManagementTween);
  if (tShowShip != null) _shipUiSequence.Append(tShowShip);
  else _shipUiSequence.AppendCallback(() => SetRootVisible(shipManagementRoot, true));

  _shipUiSequence.OnComplete(() =>
  {
    var tabs = shipManagementRoot.GetComponentInChildren<ShipManagementTabsController>(includeInactive: true);
    if (tabs != null) tabs.SelectDefaultTab();

    var launchBay = shipManagementRoot.GetComponentInChildren<ShipManagementLaunchBayController>(includeInactive: true);
    if (launchBay != null) launchBay.RefreshFromUI(force: true);
    _shipUiOpening = false;
  });
}

public void CloseShipManagementUI()
{
  if (shipManagementRoot == null) return;

  if (_shipUiClosing) return;

  KillSequenceIfActive();

  _shipUiClosing = true;
  _shipUiOpening = false;
  _shipUiUnitInstanceId = 0;
  CurrentShipManagementUnit = null;

  _shipUiSequence = DOTween.Sequence();
  if (IsRootVisible(shipManagementRoot))
  {
    var tHideShip = TweenRootPositionY(shipManagementRoot, false, tweenShipManagementPosition, ref _shipManagementTween);
    if (tHideShip != null) _shipUiSequence.Append(tHideShip);
    else _shipUiSequence.AppendCallback(() => SetRootVisible(shipManagementRoot, false));
  }

  if (cardsBarRoot != null)
  {
    var tShowCards = TweenCardsBarPositionY(true, ref _cardsBarTween);
    if (tShowCards != null) _shipUiSequence.Append(tShowCards);
    else _shipUiSequence.AppendCallback(() => SetRootVisible(cardsBarRoot, true));

    _shipUiSequence.AppendCallback(() =>
    {
      var cardsCtrl = cardsBarRoot.GetComponentInParent<CardsBarController>();
      if (cardsCtrl != null) cardsCtrl.ForceReconcileNow();
    });
  }

  _shipUiSequence.OnComplete(() =>
  {
    _shipUiClosing = false;
  });
}
```

**Transition sequence:**
1. **Open:** Hide Cards Bar → Show Ship Management → Select default tab → Refresh Launch Bay
2. **Close:** Hide Ship Management → Show Cards Bar → Force reconcile cards
3. Uses DOTween for smooth Y-position tweens (slide up/down)
4. Prevents simultaneous open/close via `_shipUiOpening` and `_shipUiClosing` flags

---

## Ship Management Launch Bay

### Class Structure
**File:** `Scripts/UI/MainGame/CommandMode/ShipManagementLaunchBayController.cs`

```csharp
public class ShipManagementLaunchBayController : MonoBehaviour
{
  [Header("UI")]
  public Transform contentRoot;
  public DockedUnitCardView dockedUnitCardPrefab;
  public ScrollRect scrollRect;

  [Header("Behavior")]
  public float refreshIntervalSeconds = 0.15f;

  float _nextRefreshTime;
  int _lastShipInstanceId;
  int _lastDockedSignature;

  CommandButtonsBarController _buttonsBar;

  readonly Dictionary<int, DockedUnitCardView> _cardsByInstanceId = new Dictionary<int, DockedUnitCardView>();
}
```

### Context Resolution
**File:** `Scripts/UI/MainGame/CommandMode/ShipManagementLaunchBayController.cs`

```csharp
InGameObject ResolveSelectedShipLikeUnit()
{
  if (_buttonsBar != null && _buttonsBar.CurrentShipManagementUnit != null)
  {
    var u = _buttonsBar.CurrentShipManagementUnit;
    if (u != null && u.gameObject != null && (u.role == "ship" || u.role == "base")) return u;
  }

  if (CommandModeController.I == null) return null;
  var unique = GetUniqueSelection(CommandModeController.I.SelectedUnits);
  if (unique == null || unique.Count == 0) return null;

  if (unique.Count == 1)
  {
    var u = unique[0];
    if (u == null || u.gameObject == null) return null;
    if (!(u.role == "ship" || u.role == "base")) return null;
    return u;
  }

  InGameObject shipLike = null;
  for (int i = 0; i < unique.Count; i++)
  {
    var u = unique[i];
    if (u == null || u.gameObject == null) continue;
    if (!(u.role == "ship" || u.role == "base")) continue;

    if (shipLike != null) return null;
    shipLike = u;
  }

  return shipLike;
}
```

**Context resolution priority:**
1. `CommandButtonsBarController.CurrentShipManagementUnit` (set when Ship Management opens)
2. Command Mode selection fallback (only if exactly one ship/base selected)

This prevents transient selection flicker during UI transitions.

### Launch Request
**File:** `Scripts/UI/MainGame/CommandMode/ShipManagementLaunchBayController.cs`

```csharp
public void RequestLaunch(InGameObject unit)
{
  if (unit == null || unit.gameObject == null) return;

  float deployHpFractionMin = 0.25f;
  if (unit.hpMax > 0)
  {
    float hpFraction = (float)unit.hp / (float)unit.hpMax;
    if (hpFraction < deployHpFractionMin) return;
  }

  var ship = ResolveSelectedShipLikeUnit();
  if (ship == null || ship.gameObject == null) return;

  var bay = ship.GetComponent<DockingBay>();
  if (bay == null) return;

  if (bay.Launch(unit))
  {
    _nextRefreshTime = 0f;
    Refresh(force: true);
  }
}
```

**Launch requirements:**
- Unit HP must be ≥ 25% of max HP
- Ship must have `DockingBay` component
- Calls `DockingBay.Launch(unit)` which handles undocking and positioning

---

## Docking Bay

### Class Structure
**File:** `Scripts/Objects/DockingBay.cs`

```csharp
public class DockingBay : MonoBehaviour
{
  [Header("Docking")]
  public List<InGameObject> dockedUnits = new List<InGameObject>();

  [Header("Launch")]
  public Transform launchPoint;
  public float launchOffsetY = 0.25f;
  public float launchFlyUpDistance = 1.25f;
  public float launchFlySpeed = 8f;
  public float launchStopDistance = 0.05f;
  public float missionClampMargin = 0.5f;
}
```

### Dock Method
**File:** `Scripts/Objects/DockingBay.cs`

```csharp
public bool Dock(InGameObject unit)
{
  if (unit == null || unit.gameObject == null) return false;

  if (dockedUnits == null) dockedUnits = new List<InGameObject>();
  if (!dockedUnits.Contains(unit)) dockedUnits.Add(unit);

  var st = unit.GetComponent<DockedUnitState>();
  if (st == null) st = unit.gameObject.AddComponent<DockedUnitState>();
  st.SetDocked(this, true);

  return true;
}
```

### Launch Method
**File:** `Scripts/Objects/DockingBay.cs`

```csharp
public bool Launch(InGameObject unit)
{
  if (unit == null || unit.gameObject == null) return false;
  if (!IsDocked(unit)) return false;

  Undock(unit);

  Vector2 startPos = ResolveLaunchStartPos();
  Vector2 endPos = startPos + Vector2.up * Mathf.Max(0f, launchFlyUpDistance);

  startPos = ClampToMission(startPos, missionClampMargin);
  endPos = ClampToMission(endPos, missionClampMargin);

  unit.transform.position = new Vector3(startPos.x, startPos.y, unit.transform.position.z);

  if (MovementController.I != null)
  {
    MovementController.I.StopVelocity(unit);
    MovementController.I.StopMoveToTarget(unit);
    if (launchFlySpeed > 0f)
    {
      MovementController.I.SetMoveToTarget(unit, endPos, launchFlySpeed, launchStopDistance);
    }
  }

  return true;
}
```

**Launch behavior:**
1. Undocks unit (removes from bay, clears `DockedUnitState`)
2. Positions unit at launch point (above ship)
3. Issues move order to fly upward by `launchFlyUpDistance` (1.25f)
4. Clamps positions to mission boundaries

---

## Docked Unit State

### Class Structure
**File:** `Scripts/Objects/DockedUnitState.cs`

```csharp
public class DockedUnitState : MonoBehaviour
{
  public bool isDocked;
  public DockingBay dockedIn;

  bool _cached;
  Renderer[] _renderers;
  bool[] _renderersEnabled;

  Collider2D[] _colliders;
  bool[] _collidersEnabled;

  MonoBehaviour[] _aiBehaviours;
  bool[] _aiBehavioursEnabled;
}
```

### SetDocked Method
**File:** `Scripts/Objects/DockedUnitState.cs`

```csharp
public void SetDocked(DockingBay bay, bool docked)
{
  CacheIfNeeded();

  isDocked = docked;
  dockedIn = docked ? bay : null;

  if (_unit == null) _unit = GetComponent<InGameObject>();

  if (docked)
  {
    ResetAttachmentCaches();

    DisableAIWhileDocked();

    if (_unit != null)
    {
      if (CommandModeController.I != null)
      {
        CommandModeController.I.RemoveFromSelection_Public(_unit);
      }
      if (_unit.currentOrder != null) _unit.currentOrder = null;
      if (_unit.moveToTarget != null) _unit.moveToTarget.isActive = false;
    }

    if (_unit != null && MovementController.I != null)
    {
      MovementController.I.StopVelocity(_unit);
      MovementController.I.StopMoveToTarget(_unit);
    }

    if (_renderers != null)
    {
      for (int i = 0; i < _renderers.Length; i++)
      {
        if (_renderers[i] == null) continue;
        _renderers[i].enabled = false;
      }
    }

    if (_colliders != null)
    {
      for (int i = 0; i < _colliders.Length; i++)
      {
        if (_colliders[i] == null) continue;
        _colliders[i].enabled = false;
      }
    }

    EnsureHiddenStateWhileDocked();

    if (!_undockReturnPosCached)
    {
      _undockReturnPos = bay != null ? bay.transform.position : transform.position;
      _undockReturnPosCached = true;
    }

    var offMap = ResolveOffMapParkingPos(transform.position);
    transform.position = offMap;

    return;
  }

  RestoreAI();

  if (_undockReturnPosCached)
  {
    transform.position = new Vector3(_undockReturnPos.x, _undockReturnPos.y, transform.position.z);
    _undockReturnPosCached = false;
  }

  RestoreAttachments();

  if (_renderers != null && _renderersEnabled != null)
  {
    for (int i = 0; i < _renderers.Length && i < _renderersEnabled.Length; i++)
    {
      if (_renderers[i] == null) continue;
      _renderers[i].enabled = _renderersEnabled[i];
    }
  }

  if (_colliders != null && _collidersEnabled != null)
  {
    for (int i = 0; i < _colliders.Length && i < _collidersEnabled.Length; i++)
    {
      if (_colliders[i] == null) continue;
      _colliders[i].enabled = _collidersEnabled[i];
    }
  }
}
```

**Docking behavior:**
1. Caches renderer/collider/AI states
2. Disables all AI scripts (AIBase and AI_* scripts)
3. Removes unit from Command Mode selection
4. Clears current order and movement
5. Disables all renderers and colliders
6. Hides status UI, ammo UI, reloading UI
7. Moves unit to off-map parking position

**Undocking behavior:**
1. Restores AI scripts to cached enabled state
2. Returns unit to cached position (near bay)
3. Restores status UI attachments
4. Re-enables renderers and colliders
