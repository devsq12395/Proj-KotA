# RTS System Proposal

## Summary

Design and integrate an RTS-style command layer into the Game scene that enables selection, control groups, and order issuance for player-owned `InGameObject` units. Orders are executed by a lightweight AI component that drives movement via `MovementController` and simple combat via `CharacterController` damage APIs.

This proposal uses:
- `InGameObject` as the unit type and `CharacterController.characters` as the unit registry.
- `CharacterController` and `MovementController` as core controllers to issue movement and combat interactions.

Scope (MVP):
- Command Mode toggle.
- Single- and drag-box multi-selection of owned units.
- Control groups (save/recall; double-press to focus camera).
- Orders: `Move`, `AttackMove`.
- Order execution: move-to-destination with simple target-acquisition and basic firing behavior.
- Minimal UI (optional markers) with clean extension points for advanced visuals later.

## Goals
- Provide reliable RTS-style selection and order issuance for units of type `InGameObject`.
- Preserve formation offsets when issuing group orders.
- Ensure order execution continues seamlessly while moving (e.g., attack while advancing, within limits of current combat system).
- Clean, minimally invasive integration that matches current controllers and scene lifecycle patterns.

## Non-Goals
- Full Command Mode UI suite (Cards Bar, Ship Management) from the reference—these are out of scope for MVP.
- Complex AI behaviors (evasion, corner-avoidance, docking, ship gun targeting, crew systems).
- Weapon module orchestration and FX-heavy order trail visuals (can be added later).

## References / Existing Files
- Reference design docs:
  - `Docs/References/RTS_COMMANDS_AND_SYSTEMS.md`
  - `Docs/References/COMMAND_MODE.md`
  - `Docs/References/COMMAND_MODE_UI.md`
- Current unit and controllers:
  - `Scripts/Types/InGameObject.cs`
  - `Scripts/Controllers/MainGame/CharacterController.cs`
  - `Scripts/Controllers/MainGame/MovementController.cs`
  - `Scripts/Types/Types_Character.cs`

## Architecture / Design

High-level components:
- `OrderType` enum and `Order` data
  - Defines supported RTS orders (`None`, `Move`, `AttackMove`).
  - Stored per-unit on `InGameObject.currentOrder`.
- `CommandModeController` (new)
  - Toggles Command Mode, manages selection (single/drag-box), control groups, and order issuance with formation preservation.
  - Computes target offsets per selected unit and writes `currentOrder`.
- `AIBase` (new)
  - Per-unit executor that reads `currentOrder` and drives movement via `MovementController` and basic combat via `CharacterController`.
  - Order dispatcher with `TickMoveOrder` and `TickAttackMoveOrder`.
- `MovementController` extension
  - Add helpers to move toward a world target (`SetMoveToTarget`) and to face an angle (`FaceAngle`).
  - Reuse existing `SetLinearMove`/`StopLinearMove` under the hood.

Key data contracts:
- `InGameObject`
  - Add: `Order currentOrder` and `float engagementRadius` (default 8f).
  - Uses existing `id`, `owner`, `hp`, `hpMax` fields.
- `CharacterController`
  - Uses `characters` list as authoritative list of spawned units for selection/targeting.
  - Provides `DealDamage(...)` used in simple melee-range attack behavior.

Target acquisition (MVP):
- Find closest enemy using `CharacterController.I.characters` where `owner != unit.owner`, within `engagementRadius`.

Movement (MVP):
- For `Move`: Travel to destination, clear order within a small `stopDistance`.
- For `AttackMove`: If enemy within `engagementRadius`:
  - face and approach until within `preferredRange`, then stop and fire; else continue moving toward destination.

Control groups:
- Store `InGameObject.id` lists per number key 0–9.
- Save: Ctrl + number; Recall: number; Double-press: focus camera to group centroid.

## Step-by-Step Integration Plan

1) Core Types (Orders)
- Add `OrderType` and `Order` to `Scripts/Types/Types_Character.cs`.
  - `enum OrderType { None, Move, AttackMove }`
  - `class Order { OrderType type; Vector2 targetPos; float stopDistance; float issuedTime; }`
- Extend `InGameObject` with:
  - `public Order currentOrder;`
  - `public float engagementRadius = 8f;`

2) MovementController Helpers
- Add the following methods in `Scripts/Controllers/MainGame/MovementController.cs`:
  - `public void FaceAngle(InGameObject obj, float angle)` → wraps `UpdateFacingByAngle` safely.
  - `public void SetMoveToTarget(InGameObject obj, Vector2 dest, float speed, float stopDistance)`
    - Computes angle to `dest`, calls `SetLinearMove(obj, speed, angle)`. Caller handles stop check.
  - `public void StopMoveToTarget(InGameObject obj)` → alias to `StopLinearMove`.
  - `public void StopVelocity(InGameObject obj)` → zeroes current speed; guard if needed.

3) AIBase (Order Execution)
- New script `Scripts/Data/ObjectAIs/AIBase.cs` (attached to AI-capable units):
  - Fields: `public InGameObject objOwner;`
  - `void Update()` → `UpdateAI()` when in Game scene.
  - `void UpdateAI()`
    - If `objOwner.currentOrder != null && objOwner.currentOrder.type != OrderType.None`, call `TickOrder(objOwner, objOwner.currentOrder)`.
  - `bool TickOrder(InGameObject obj, Order order)` dispatcher:
    - `Move` → `TickMoveOrder`
    - `AttackMove` → `TickAttackMoveOrder`
  - `TickMoveOrder`:
    - If within `stopDistance`, `StopLinearMove` and clear `currentOrder`.
    - Else compute angle to destination and `SetMoveToTarget`.
    - Acquire nearest enemy in `engagementRadius`; face target while moving (optional) and, if in `preferredRange`, opportunistically call `DealDamage` on a cooldown.
  - `TickAttackMoveOrder`:
    - If enemy within `engagementRadius`: approach until within `preferredRange`; stop and fire; else continue toward destination.
  - Utility: `FindNearestEnemy(obj, maxDist)` scanning `CharacterController.I.characters`.
  - Simple firing cadence (timer) to avoid frame-by-frame damage spam.

4) CommandModeController (Selection + Orders)
- New script `Scripts/Controllers/CommandModeController.cs`:
  - Singleton `I`, `bool IsCommandMode`.
  - Selection state: `List<InGameObject> selectedUnits`, `InGameObject selected`, and markers root (optional).
  - Input handling:
    - Toggle Command Mode (e.g., `KeyCode.Tab` or configurable input).
    - In command mode:
      - Left click → single-select (`Physics2D.OverlapPoint` → `InGameObject` where `owner == 1`).
      - Drag-left → drag-box select by rect overlap against unit bounds.
      - Right click on ground → issue `OrderType.Move`.
      - Shift + right click (or hotkey A then right click) → issue `OrderType.AttackMove`.
    - Control groups:
      - Ctrl + number → save group
      - number → recall group
      - double-press number → focus camera on group centroid
  - Order issuing API:
    - `void IssueOrderToSelection(OrderType type, Vector2 targetPos)`
      - Computes group centroid if multi-select.
      - For each unit: `finalTarget = targetPos + (unitPos - centroid)`.
      - Ensures `currentOrder` exists, sets fields, sets `issuedTime`.

5) Scene Wiring
- Ensure these persist or are loaded in the `Game` scene:
  - `CharacterController` (already present).
  - `MovementController` (already present).
  - `CommandModeController` (new MonoBehaviour in scene prefab/root).
- For AI execution, attach `AIBase` to AI-capable allied units on spawn (or add at runtime when entering Command Mode, similar to reference approach).

6) Optional Visuals (post-MVP)
- Selection corner markers per selected unit.
- Order target ping effect and path trail segments for selected units.
- Cards Bar (unit cards) and Command Buttons Bar, gated behind Command Mode.

## Data / Settings / Configuration
- `Order.stopDistance` default: 0.25f (configurable per-issue or per-unit).
- `InGameObject.engagementRadius` default: 8f (authoring-time override per prefab).
- `AIBase` preferred combat range (for `AttackMove`): 3–5f.
- Input bindings (project setting or in-code):
  - Toggle Command Mode: e.g., `Tab` (or map to `CommandMode` axis if using Input Manager).
  - Right click: issue Move; `A`+Right click: AttackMove.
  - Ctrl + number: save; number: recall; double-press number: focus.

## Security Considerations
- Input routing: Command Mode should consume inputs while active to avoid conflicting with pilot controls.
- Scene gating: All controllers self-disable if not in `Game` scene (match current pattern in `CharacterController` / `MovementController`).
- Null safety: Guard all references (`CharacterController.I`, `MovementController.I`, unit `gameObject`).

## Risks and Mitigations
- Risk: MovementController lacks target-based helpers.
  - Mitigation: Implement thin wrappers (`SetMoveToTarget`, `FaceAngle`) that reuse `SetLinearMove`.
- Risk: Over-aggressive damage calls.
  - Mitigation: Add simple timers in `AIBase` to throttle `DealDamage` calls.
- Risk: Selection bounds mismatch across sprites/colliders.
  - Mitigation: Prefer `Collider2D` bounds; fall back to aggregated `SpriteRenderer` bounds.
- Risk: Control group IDs invalidated on respawn.
  - Mitigation: Use `InGameObject.SetID()` once per instance; persist within session.

## Testing Plan
- Unit selection
  - Single-select via click on owned unit.
  - Drag-box select multiple owned units; verify selection list contents and markers (if enabled).
- Orders (Move/AttackMove)
  - Issue Move to a single unit and a group; verify formation offset and arrival/clear order.
  - Place enemies; issue Move and confirm opportunistic combat while advancing.
  - Issue AttackMove toward enemies; verify approach, stop within preferred range, and periodic damage application.
- Control groups
  - Save group with Ctrl + number; recall with number; double-press focuses camera to centroid.

## Rollout Plan
- Implement behind Command Mode toggle; default to off.
- Land core types and controllers first, then AI execution, then selection and input routing.
- Add optional visuals after functional verification.

## Deliverables
- New/updated code:
  - `Scripts/Types/Types_Character.cs`
    - Add `OrderType`, `Order`.
  - `Scripts/Types/InGameObject.cs`
    - Add `public Order currentOrder;`
    - Add `public float engagementRadius = 8f;`
  - `Scripts/Controllers/MainGame/MovementController.cs`
    - Add `FaceAngle`, `SetMoveToTarget`, `StopMoveToTarget`, `StopVelocity`.
  - `Scripts/Data/ObjectAIs/AIBase.cs` (new)
    - Implements `UpdateAI`, `TickMoveOrder`, `TickAttackMoveOrder`, nearest enemy acquisition, simple fire cadence.
  - `Scripts/Controllers/CommandModeController.cs` (new)
    - Implements Command Mode toggle, selection, control groups, and order issuance.

- Documentation updates (after implementation):
  - `Docs/Dev/Features/RTS_COMMAND_MODE.md` (new) summarizing usage, inputs, and extension points.

