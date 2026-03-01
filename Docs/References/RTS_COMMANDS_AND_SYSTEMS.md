# RTS Commands and Systems

## Summary
This document describes the RTS-style command layer used in-mission.

The RTS layer consists of:
- **Command Mode** (selection, control groups, order issuance)
- **Orders** (data stored on `InGameObject.currentOrder`)
- **Order execution** (performed by AI logic in `AIBase`)

This layer is designed so that owned units continue to:
- move toward their commanded destination
- evade missiles
- acquire targets and fire while moving (when appropriate)

## Core data types
### OrderType
Defined in `Scripts/Data/Types/Types_Character.cs`:
- `None`
- `Move`
- `AttackMove`

### Order
Defined in `Scripts/Data/Types/Types_Character.cs`:
- `OrderType type`
- `Vector2 targetPos`
- `float stopDistance`
- `float issuedTime`

Stored on units in:
- `Scripts/Objects/InGameObject.cs`
  - `public Order currentOrder;`

## Order issuing (Command Mode)
Orders are issued by:
- `Scripts/Controllers/CommandModeController.cs`
  - `IssueOrderToSelection(OrderType type, Vector2 targetPos)`

Key behaviors:
- **Per-unit formation offset** is preserved for multi-select by offsetting each unit’s target by its offset from the selection centroid.
- Orders are not queued; issuing a new order overwrites the previous order.

## Order execution (AIBase)
Orders are executed by:
- `Scripts/Data/ObjectAIs/AIBase.cs`
  - `UpdateAI()`
  - `TickOrder_Local(InGameObject obj, Order order)`

Important rules:
- The player (the main controlled combat suit) is excluded from AI execution:
  - `AIBase.UpdateAI()` returns early when `objOwner == MainGameController.I.player`
- For non-player owned units, orders are executed even if the unit is friendly.

### Move order (`OrderType.Move`)
Implementation:
- `TickMoveOrder_Local(obj, order)`

Behavior:
- Drives `MovementController.I.SetMoveToTarget(obj, dest, speed, stopDist)`.
- While moving:
  - tries to acquire a combat target within engagement range
  - faces the target and fires the selected module at a cadence
- When close enough to destination:
  - stops movement
  - clears `obj.currentOrder`

Combat target acquisition:
- Primary: `DB_AIMoveLists.I.GetCombatTarget(obj, refresh=3s, maxDist=engageRadius)`
- Fallback: `FindNearestEnemy_Local(obj, engageRadius)`

Engagement range source:
- `InGameObject.engagementRadius` (defaults to 8)

### AttackMove order (`OrderType.AttackMove`)
Implementation:
- `TickAttackMoveOrder_Local(obj, order)`

Behavior:
- If an enemy exists within engagement radius:
  - stops “move-to-target”
  - faces the enemy
  - approaches until within preferred range
  - fires the selected weapon
- If no enemy is found:
  - moves toward destination
  - may still fire (at cadence)

### Safety / evasive behavior while ordered
Before processing an order, `AIBase.TickOrder_Local(...)` attempts:
- `DB_AIMoveLists.I.TryPreventCornerEntrapment(obj)`
- `DB_AIMoveLists.I.TryAvoidBorders(obj, margin=0.5f)`
- `DB_AIMoveLists.I.TryEvade(obj)`

These are designed to keep ordered units alive and prevent mission-boundary edge cases.

## Targeting system (shared)
The canonical “closest enemy” helper is:
- `Scripts/Data/ObjectAIs/AI_Movements/DB_AIMoveLists.cs`
  - `GetCombatTarget(InGameObject obj, float refreshIntervalSeconds = 3f, float maxDist = -1f, bool ignoreEngagementRadius = false)`

Properties:
- **Closest enemy selection** (by distance)
- **Per-AI caching** using `InGameObject.id`
- **3-second retarget interval** by default
- Optional engagement radius clamp:
  - If `maxDist <= 0` and `obj.engagementRadius > 0`, it uses `engagementRadius`.
  - If `ignoreEngagementRadius == true`, it can target globally.

## Control groups
Control groups are implemented entirely in `CommandModeController`.

Storage:
- `Dictionary<int, List<int>> controlGroupIds`
  - key is group number 0-9
  - list is `InGameObject.id` values

Input:
- Ctrl + number: save
- number: recall
- double press number: camera focus

## Scene dependencies
This system assumes:
- `CommandModeController` exists in the Game scene
- `InputController` routes input to Command Mode when active
- Owned units to be commanded have an AI executor:
  - either `AIBase` attached
  - or a derived AI that still calls into `AIBase.UpdateAI()` and/or respects `currentOrder`

## Verification checklist
- Select a friendly unit, issue `Move`.
  - Unit should travel to destination.
  - If enemies are nearby, unit should face/fire while moving.
  - Unit should evade missiles.
- Select multiple units, issue `Move`.
  - Formation offsets should be preserved.
- Issue `AttackMove`.
  - Units should prioritize fighting when enemies are within engagement range.
  - Otherwise continue toward destination.

---

# Source Code Reference

## Order Data Types

### OrderType Enum
**File:** `Scripts/Data/Types/Types_Character.cs`

```csharp
[Serializable]
public enum OrderType {
  None,
  Move,
  AttackMove,
  MovePilotLike
}
```

### Order Class
**File:** `Scripts/Data/Types/Types_Character.cs`

```csharp
[Serializable]
public class Order {
  public OrderType type = OrderType.None;
  public Vector2 targetPos;
  public float stopDistance = 0.25f;
  public float issuedTime;
}
```

**Storage on units:**
- `InGameObject.currentOrder : Order`

---

## Order Issuing (CommandModeController)

### IssueOrderToSelection Method
**File:** `Scripts/Controllers/CommandModeController.cs`

```csharp
void IssueOrderToSelection(OrderType type, Vector2 targetPos)
{
  if (selectedUnits == null || selectedUnits.Count == 0) return;

  Vector2 centroid = Vector2.zero;
  int count = 0;
  if (selectedUnits.Count > 1)
  {
    for (int i = 0; i < selectedUnits.Count; i++)
    {
      InGameObject u = selectedUnits[i];
      if (u == null || u.gameObject == null) continue;
      Vector3 p = u.transform.position;
      centroid += new Vector2(p.x, p.y);
      count++;
    }
    if (count > 0) centroid /= count;
  }

  for (int i = 0; i < selectedUnits.Count; i++)
  {
    InGameObject u = selectedUnits[i];
    if (u == null || u.gameObject == null) continue;

    Vector2 unitPos = new Vector2(u.transform.position.x, u.transform.position.y);
    Vector2 offset = (selectedUnits.Count > 1 && count > 0) ? (unitPos - centroid) : Vector2.zero;
    Vector2 finalTarget = targetPos + offset;

    // Remove old arrow direction effect for this unit
    if (u.orderTargetEffect != null)
    {
      if (u.orderTargetEffect.gameObject != null)
      {
        Destroy(u.orderTargetEffect.gameObject);
      }
      u.orderTargetEffect = null;
    }

    // Create new arrow direction effect at target position
    if (EffectsController.I != null && !string.IsNullOrEmpty(orderTargetEffectId))
    {
      u.orderTargetEffect = EffectsController.I.CreateEffect(orderTargetEffectId, finalTarget);
    }

    OrderType perUnitType = type;
    if (type == OrderType.Move)
    {
      if (ShouldUsePilotLikeMoveOrder(u)) perUnitType = OrderType.MovePilotLike;
    }

    if (u.currentOrder == null) u.currentOrder = new Order();
    u.currentOrder.type = perUnitType;
    u.currentOrder.targetPos = finalTarget;
    u.currentOrder.stopDistance = 0.25f;
    u.currentOrder.issuedTime = Time.time;
  }

  if (orderTrailVisuals != null)
  {
    orderTrailVisuals.RefreshForSelection(selectedUnits);
    orderTrailVisuals.PrimeForSelection(selectedUnits);
  }
}

bool ShouldUsePilotLikeMoveOrder(InGameObject u)
{
  if (u == null || u.gameObject == null) return false;
  if (u.owner != 1) return false;
  if (!string.Equals(u.type, "Character")) return false;
  if (string.Equals(u.role, "ship") || string.Equals(u.role, "base")) return false;
  return true;
}
```

**Key behaviors:**
- **Formation preservation:** Computes centroid of selected units, then applies each unit's offset from centroid to the target position
- **Per-unit order type:** Automatically converts `Move` to `MovePilotLike` for player-owned combat suits (non-ship/base characters)
- **Visual feedback:** Spawns order target effects and refreshes order trail visuals
- **Order overwrite:** New orders replace previous orders (no queueing)

---

## Order Execution (AIBase)

### Main Order Tick Entry Point
**File:** `Scripts/Data/ObjectAIs/AIBase.cs`

```csharp
public void UpdateAI(){
  if (objOwner == null) return;

  bool isPlayer = (MainGameController.I != null && MainGameController.I.player == objOwner);

  // Refresh combat target cache
  if (!isPlayer && DB_AIMoveLists.I != null){
    DB_AIMoveLists.I.GetCombatTarget(objOwner);
  }

  // Update classification objective (sets objectiveTarget, does NOT issue orders)
  if (!isPlayer && HasClassification()){
    UpdateClassificationObjective(Time.deltaTime);
  }

  // Command Mode orders take highest priority (player control)
  if (objOwner.currentOrder != null && objOwner.currentOrder.type != OrderType.None){
    if (TickOrder_Local(objOwner, objOwner.currentOrder)){
      return;
    }
  }

  // Player-owned ships/bases use OnUpdateAI
  if (isPlayer){
    if (objOwner != null && objOwner.isAI && (objOwner.role == "ship" || objOwner.role == "base"))
    {
      OnUpdateAI();
    }
    return;
  }

  // ... rest of AI sequence logic
}
```

### Order Dispatcher
**File:** `Scripts/Data/ObjectAIs/AIBase.cs`

```csharp
bool TickOrder_Local(InGameObject obj, Order order){
  if (obj == null || obj.gameObject == null) return false;
  if (order == null){ obj.currentOrder = null; return true; }
  if (MovementController.I == null) return true;

  // Safety helpers from DB_AIMoveLists if available
  if (DB_AIMoveLists.I != null){
    if (DB_AIMoveLists.I.TryPreventCornerEntrapment(obj)) return true;
    if (DB_AIMoveLists.I.TryAvoidBorders(obj, 0.5f)) return true;
    DB_AIMoveLists.I.TryEvade(obj);
  }

  // Handle dock-to-ship order state
  var dockState = obj.GetComponent<DockToShipOrderState>();
  if (dockState != null && dockState.isActive)
  {
    // ... docking logic (omitted for brevity)
  }

  switch (order.type){
    case OrderType.Move:
      return TickMoveOrder_Local(obj, order);
    case OrderType.MovePilotLike:
      return TickMovePilotLikeOrder_Local(obj, order);
    case OrderType.AttackMove:
      return TickAttackMoveOrder_Local(obj, order);
    default:
      return false;
  }
}
```

**Safety behaviors executed before order processing:**
1. `TryPreventCornerEntrapment` - Prevents units from getting stuck in mission corners
2. `TryAvoidBorders` - Keeps units away from mission boundaries (0.5f margin)
3. `TryEvade` - Attempts to evade incoming missiles

---

## Move Order Implementation

**File:** `Scripts/Data/ObjectAIs/AIBase.cs`

```csharp
bool TickMoveOrder_Local(InGameObject obj, Order order){
  Vector2 dest = ClampInsideMission(order.targetPos, 0.5f);
  Vector2 pos = obj.transform.position;
  float stopDist = Mathf.Max(0.05f, order.stopDistance);

  if ((dest - pos).sqrMagnitude <= stopDist * stopDist){
    if (obj.moveToTarget != null) obj.moveToTarget.isActive = false;
    if (obj.movementState == null) obj.movementState = new InGameObject.MovementRuntimeState();
    obj.movementState.suppressFacingFromMovement = false;
    MovementController.I.StopLinearMove(obj);
    obj.currentOrder = null;
    return true;
  }

  if (obj.isDash) return true;

  if (MovementController.I != null){
    MovementController.I.StopLinearMove(obj);
  }

  float speed = obj.speed > 0f ? obj.speed : 3f;
  MovementController.I.SetMoveToTarget(obj, dest, speed, stopDist);

  float engageRadius = (obj != null && obj.engagementRadius > 0f) ? obj.engagementRadius : 8f;
  InGameObject target = null;

  if (DB_AIMoveLists.I != null){
    target = DB_AIMoveLists.I.GetCombatTarget(obj, 3f, engageRadius);
  }
  if (IsShipRole_Local(obj) && !IsValidShipCombatTarget_Local(obj, target)){
    target = null;
  }
  if (target == null || target.gameObject == null){
    target = IsShipRole_Local(obj)
      ? FindNearestValidShipCombatTarget_Local(obj, engageRadius)
      : FindNearestEnemy_Local(obj, engageRadius);
  }

  if (IsShipRole_Local(obj)){
    SetShipMainGunTarget_Local(obj, target);
  }

  if (target != null && target.gameObject != null){
    if (obj.movementState == null) obj.movementState = new InGameObject.MovementRuntimeState();
    bool isShip = IsShipRole_Local(obj);
    obj.movementState.suppressFacingFromMovement = !isShip;
    if (!isShip){
      Vector2 toTarget = (Vector2)target.transform.position - pos;
      if (toTarget.sqrMagnitude > 0.0001f){
        float aimAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        MovementController.I.FaceAngle(obj, aimAngle);
      }
    }
    TryFireSelectedWeapon_Local(obj);
  } else {
    if (obj.movementState == null) obj.movementState = new InGameObject.MovementRuntimeState();
    obj.movementState.suppressFacingFromMovement = false;
    if (IsShipRole_Local(obj)){
      SetShipMainGunTarget_Local(obj, null);
    }
  }
  return true;
}
```

**Behavior breakdown:**
1. **Destination reached check:** Clears order when within `stopDistance`
2. **Movement:** Uses `SetMoveToTarget` to drive unit toward destination
3. **Combat target acquisition:**
   - Primary: `DB_AIMoveLists.GetCombatTarget` (cached, 3s refresh)
   - Fallback: `FindNearestEnemy_Local` or `FindNearestValidShipCombatTarget_Local`
4. **Engagement while moving:**
   - Ships: Set main gun target
   - Non-ships: Face target and fire selected weapon
   - Uses `suppressFacingFromMovement` to prevent movement from overriding combat facing

---

## AttackMove Order Implementation

**File:** `Scripts/Data/ObjectAIs/AIBase.cs`

```csharp
bool TickAttackMoveOrder_Local(InGameObject obj, Order order){
  Vector2 dest = ClampInsideMission(order.targetPos, 0.5f);
  Vector2 pos = obj.transform.position;
  float stopDist = Mathf.Max(0.05f, order.stopDistance);

  if ((dest - pos).sqrMagnitude <= stopDist * stopDist){
    if (obj.moveToTarget != null) obj.moveToTarget.isActive = false;
    if (obj.movementState == null) obj.movementState = new InGameObject.MovementRuntimeState();
    obj.movementState.suppressFacingFromMovement = false;
    MovementController.I.StopLinearMove(obj);
    obj.currentOrder = null;
    return true;
  }

  if (obj.isDash) return true;

  float engageRadius = (obj != null && obj.engagementRadius > 0f) ? obj.engagementRadius : 8f;
  const float preferredRange = 5f;

  InGameObject enemy = IsShipRole_Local(obj)
    ? FindNearestValidShipCombatTarget_Local(obj, engageRadius)
    : FindNearestEnemy_Local(obj, engageRadius);
  if (enemy != null && enemy.gameObject != null){
    if (obj.moveToTarget != null) obj.moveToTarget.isActive = false;

    if (obj.movementState == null) obj.movementState = new InGameObject.MovementRuntimeState();
    bool isShip = IsShipRole_Local(obj);
    obj.movementState.suppressFacingFromMovement = !isShip;

    if (IsShipRole_Local(obj)){
      SetShipMainGunTarget_Local(obj, enemy);
    }

    Vector2 toEnemy = (Vector2)enemy.transform.position - pos;
    float dist = toEnemy.magnitude;
    float aimAngle = 0f;
    if (toEnemy.sqrMagnitude > 0.0001f){
      aimAngle = Mathf.Atan2(toEnemy.y, toEnemy.x) * Mathf.Rad2Deg;
    }

    float speed = obj.speed > 0f ? obj.speed : 3f;
    if (dist > preferredRange){
      float ang = Mathf.Atan2(toEnemy.y, toEnemy.x) * Mathf.Rad2Deg;
      MovementController.I.SetWalk(obj, speed, ang, 0f);
    } else {
      MovementController.I.StopLinearMove(obj);
    }

    if (!isShip && toEnemy.sqrMagnitude > 0.0001f){
      MovementController.I.FaceAngle(obj, aimAngle);
    }

    TryFireSelectedWeapon_Local(obj);
    return true;
  }

  if (obj.movementState == null) obj.movementState = new InGameObject.MovementRuntimeState();
  obj.movementState.suppressFacingFromMovement = false;

  if (IsShipRole_Local(obj)){
    SetShipMainGunTarget_Local(obj, FindNearestValidShipCombatTarget_Local(obj, engageRadius));
  }

  if (MovementController.I != null){
    MovementController.I.StopLinearMove(obj);
  }

  float moveSpeed = obj.speed > 0f ? obj.speed : 3f;
  MovementController.I.SetMoveToTarget(obj, dest, moveSpeed, stopDist);
  TryFireSelectedWeapon_Local(obj);
  return true;
}
```

**Behavior breakdown:**
1. **Enemy within engagement radius:**
   - Stops move-to-target
   - Faces enemy
   - Approaches if distance > `preferredRange` (5f)
   - Stops if within preferred range
   - Fires selected weapon
2. **No enemy found:**
   - Continues moving toward destination
   - Still attempts to fire (at cadence)

---

## Combat Target Acquisition

**File:** `Scripts/Data/ObjectAIs/AIBase.cs`

```csharp
InGameObject FindNearestEnemy_Local(InGameObject obj, float maxDist){
  if (obj == null || obj.gameObject == null) return null;
  if (CharacterController.I == null || CharacterController.I.characters == null) return null;

  float maxDistClamped = maxDist <= 0f ? float.PositiveInfinity : maxDist;
  float bestSqr = maxDistClamped * maxDistClamped;
  InGameObject best = null;
  Vector2 p = obj.transform.position;

  var chars = CharacterController.I.characters;
  for (int i = 0; i < chars.Count; i++){
    var ch = chars[i];
    if (ch == null || ch.gameObject == null) continue;
    if (ch.owner == obj.owner) continue;
    Vector2 d = (Vector2)ch.transform.position - p;
    float sqr = d.sqrMagnitude;
    if (sqr > bestSqr) continue;
    bestSqr = sqr;
    best = ch;
  }
  return best;
}
```

**Primary targeting system (cached):**
- `DB_AIMoveLists.GetCombatTarget(obj, refreshIntervalSeconds=3f, maxDist=engageRadius)`
- Returns closest enemy within range
- Caches result per-AI for 3 seconds
- Falls back to `FindNearestEnemy_Local` if cache miss

---

## Weapon Firing During Orders

**File:** `Scripts/Data/ObjectAIs/AIBase.cs`

```csharp
void TryFireSelectedWeapon_Local(InGameObject obj){
  if (obj == null || obj.gameObject == null) return;
  if (CharacterController.I == null) return;
  if (string.IsNullOrEmpty(obj.selectedActiveModule)) return;

  if (_orderFireInterval <= 0f){
    float baseInterval = GetBaseFireInterval(obj);
    _orderFireInterval = baseInterval * UnityEngine.Random.Range(0.8f, 1.2f);
    _orderFireTimer = 0f;
  }

  _orderFireTimer += Time.deltaTime;
  if (_orderFireTimer >= _orderFireInterval){
    CharacterController.I.TriggerModuleInSlot(obj, obj.selectedActiveModule);
    _orderFireTimer = 0f;
    float baseInterval = GetBaseFireInterval(obj);
    _orderFireInterval = baseInterval * UnityEngine.Random.Range(0.8f, 1.2f);
  }
}

float GetBaseFireInterval(InGameObject obj){
  if (obj == null) return 3f;

  if (obj.aiFireRates != null && obj.aiFireRates.Count > 0 && !string.IsNullOrEmpty(obj.selectedActiveModule)){
    for (int i = 0; i < obj.aiFireRates.Count; i++){
      var fr = obj.aiFireRates[i];
      if (fr == null) continue;
      if (string.Equals(fr.slot, obj.selectedActiveModule)){
        if (fr.fireRate > 0f) return fr.fireRate;
      }
    }
  }

  if (obj.fireRate > 0f) return obj.fireRate;
  return 3f;
}
```

**Fire rate system:**
- Uses per-module fire rates from `obj.aiFireRates` if available
- Falls back to `obj.fireRate`
- Adds ±20% randomization to fire intervals
- Triggers weapon via `CharacterController.TriggerModuleInSlot`

---

## Control Groups

**File:** `Scripts/Controllers/CommandModeController.cs`

```csharp
readonly Dictionary<int, List<int>> controlGroupIds = new Dictionary<int, List<int>>();

void SaveControlGroup(int group)
{
  if (selectedUnits == null || selectedUnits.Count == 0)
  {
    if (controlGroupIds.ContainsKey(group)) controlGroupIds.Remove(group);
    return;
  }

  List<int> ids = new List<int>(selectedUnits.Count);
  for (int i = 0; i < selectedUnits.Count; i++)
  {
    InGameObject u = selectedUnits[i];
    if (u == null || u.gameObject == null) continue;
    if (u.id == 0) u.SetID();
    if (u.id == 0) continue;
    if (!ids.Contains(u.id)) ids.Add(u.id);
  }

  if (ids.Count == 0)
  {
    if (controlGroupIds.ContainsKey(group)) controlGroupIds.Remove(group);
    return;
  }

  controlGroupIds[group] = ids;
}

void RecallControlGroup(int group)
{
  if (!controlGroupIds.TryGetValue(group, out var ids) || ids == null || ids.Count == 0)
  {
    ClearSelection();
    return;
  }

  selectedUnits.Clear();
  if (CharacterController.I != null && CharacterController.I.characters != null)
  {
    var chars = CharacterController.I.characters;
    for (int i = 0; i < chars.Count; i++)
    {
      InGameObject c = chars[i];
      if (c == null || c.gameObject == null) continue;
      if (c.owner != 1) continue;
      if (c.id == 0) c.SetID();
      if (c.id == 0) continue;
      if (!ids.Contains(c.id)) continue;
      selectedUnits.Add(c);
    }
  }

  selected = selectedUnits.Count > 0 ? selectedUnits[0] : null;

  if (selected == null)
  {
    ClearSelection();
    return;
  }

  if (selected.id != 0) lastSelectedId = selected.id;
  else lastSelectedId = 0;

  RebuildSelectionMarkers();
  if (orderTrailVisuals != null) orderTrailVisuals.RefreshForSelection(selectedUnits);
}
```

**Input handling:**
- Ctrl + number (0-9): Save current selection to group
- Number (0-9): Recall group
- Double-press number: Focus camera on group centroid

**Storage:**
- Uses stable `InGameObject.id` values (not instance IDs)
- Survives unit destruction/recreation as long as ID is preserved
