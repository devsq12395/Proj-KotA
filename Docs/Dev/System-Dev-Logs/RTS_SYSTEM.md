# RTS SYSTEM

## Status
- Current: In Progress
- Owner: Cascade
- Last Updated: 2026-03-01

## Links
- Proposal: `Docs/Dev/AI-Proposals/RTS_SYSTEM_PROPOSAL.md`
- Related files:
  - `Scripts/Types/Types_Character.cs`
  - `Scripts/Types/InGameObject.cs`
  - `Scripts/Controllers/MainGame/MovementController.cs`
  - `Scripts/Controllers/MainGame/CharacterController.cs`
  - `Scripts/Data/ObjectAIs/AIBase.cs`
  - `Scripts/Controllers/MainGame/CommandModeController.cs`

## Milestones
- [x] Add core order types and per-unit storage
- [x] Add movement helpers for target-driven movement
- [x] Implement AIBase order executor (Move/AttackMove)
- [x] Implement CommandModeController (toggle, selection, control groups, order issuance)
- [ ] Add optional selection markers and order trail visuals
- [ ] Add advanced UI (Cards Bar, Command Buttons Bar)

## Progress Log

### 2026-03-01
- What changed:
  - Added `OrderType` and `Order` to `Types_Character.cs`.
  - Extended `InGameObject` with `currentOrder` and `engagementRadius`.
  - Extended `MovementController` with `FaceAngle`, `SetMoveToTarget`, `StopMoveToTarget`, `StopVelocity`.
  - Updated `CharacterController` to initialize list, attach `AIBase` on spawn, process destroys once, and drive `MoveUpdate` per frame.
  - Created `AIBase` for order execution (Move/AttackMove) with simple targeting and damage cadence.
  - Created `CommandModeController` (toggle with Tab, click/drag selection, control groups 0-9, right-click Move, A+right-click AttackMove).
- Files touched:
  - `Scripts/Types/Types_Character.cs`
  - `Scripts/Types/InGameObject.cs`
  - `Scripts/Controllers/MainGame/MovementController.cs`
  - `Scripts/Controllers/MainGame/CharacterController.cs`
  - `Scripts/Data/ObjectAIs/AIBase.cs`
  - `Scripts/Controllers/MainGame/CommandModeController.cs`
- Notes:
  - MVP does not include UI markers or trail visuals; selection relies on colliders/sprite bounds.

## Decisions

### 2026-03-01 — MVP Scope Without Advanced UI
- Decision:
  - Land functional RTS command/AI first, defer UI polish to follow-up tasks.
- Alternatives considered:
  - Implement Cards Bar and Command Buttons Bar now.
- Rationale:
  - Reduce risk and integrate with existing controllers quickly; UI can iterate later.

## Blockers
- None identified.

## Verification
- How to test:
  - In Game scene, add `CommandModeController` to any always-on GameObject.
  - Press `Tab` to enter command mode.
  - Left-click to select units with `owner == 1`; drag to multi-select.
  - Right-click ground to issue `Move`; hold `A` and right-click for `AttackMove`.
  - Ctrl+number to save group; number to recall; double-press to focus camera.
- Expected result:
  - Units move toward target, clear order near destination, and opportunistically engage nearby enemies.
