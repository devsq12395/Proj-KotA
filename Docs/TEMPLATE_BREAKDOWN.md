# SQ Unity Framework - Template Breakdown

**Version:** 1.0  
**Last Updated:** 2026-03-01

---

## Table of Contents

1. [Overview](#overview)
2. [Core Architecture](#core-architecture)
3. [Feature Breakdown](#feature-breakdown)
4. [System Controllers](#system-controllers)
5. [Data Management](#data-management)
6. [Type System](#type-system)
7. [Project Structure](#project-structure)
8. [Usage Guidelines](#usage-guidelines)

---

## Overview

The SQ Unity Framework is a modular, production-ready Unity template designed for 2D games (particularly space shooters and action games). It features a persistent scene architecture, comprehensive audio management, save system, and extensible game object controllers.

### Key Design Principles

- **Persistent Scene Architecture**: GlobalScene persists across all content scenes
- **Singleton Pattern**: Controllers use static instances for global access
- **Event-Driven**: Extensible event hooks for collision, damage, and destruction
- **Scene Safety**: Controllers validate they're in the correct scene before initialization
- **Modular Design**: Clear separation between global and game-specific systems

---

## Core Architecture

### Scene Hierarchy

The framework uses a **two-tier scene system**:

1. **GlobalScene** (Persistent - Build Index 0)
   - Always loaded
   - Contains global singletons
   - Manages scene transitions
   - Handles audio, saves, and ads

2. **Content Scenes** (Additive)
   - `Game` - Main gameplay scene
   - `Lobby` - Menu/hub scene
   - Loaded/unloaded dynamically
   - Managed by SceneControl

### Boot Flow

```
1. Project starts → GlobalScene (Build Index 0)
2. GlobalControl.Awake() → Initialize SceneControl
3. Initial load delay → TriggerInitialLoad()
4. AdController shows ad (real or fake)
5. Ad callback determines:
   - First run? → Load "Game" with Mission_1-1
   - Returning? → Load "Lobby"
6. Initial load is INSTANT (no transition)
7. Subsequent loads use circle wipe transition
```

---

## Feature Breakdown

### 1. **GlobalScene System** ⭐

**Purpose:** Persistent scene that holds global controllers and manages the game lifecycle.

**Location:** `Scenes/GlobalScene.unity`

**Key Components:**
- `GlobalControl` - Boot orchestrator and scene change delegator
- `SceneControl` - Scene transition state machine
- `SoundsController` - Audio management
- `SaveController` - Save/load system
- `Bootstrap` - Security checks and domain validation
- `DB_Sounds` - Audio clip database

**Features:**
- Persists across all scenes (DontDestroyOnLoad)
- Must be Build Settings index 0
- Automatically loads on project start
- Ensures only one instance of each controller exists

---

### 2. **Scene Control & Transitions** ⭐

**Purpose:** Centralized scene management with smooth circle wipe transitions.

**Location:** `Scripts/Controllers/GlobalScene/SceneControl.cs`

**Features:**
- **Circle Wipe Transition**: Custom shader-based circular reveal/cover effect
- **State Machine**: 9-state transition flow (Idle → WipeIn → Unload → Load → WipeOut)
- **Instant Loading**: First scene load has no transition
- **Safe Unloading**: Prevents unloading persistent scenes
- **Active Scene Management**: Properly sets Unity's active scene

**Public API:**
```csharp
SceneControl.EnsureExists().Init()
SceneControl.I.LoadSceneInstant(sceneName, reloadIfSame)
SceneControl.I.ChangeScene(sceneName, reloadIfSame)
bool SceneControl.I.IsTransitioning
string SceneControl.I.CurrentSceneName
```

**Transition States:**
1. `Idle` - No transition active
2. `SceneCoverWipeIn` - Circle wipe covers screen
3. `SceneUnload` - Unload old content scenes
4. `ScenePreLoadDelay` - Optional delay before load
5. `SceneLoad` - Load new scene additively
6. `SceneSetActive` - Set new scene as active
7. `ScenePostLoadWaitFrame` - Wait one frame
8. `ScenePostLoadDelay` - Optional delay after load
9. `SceneRevealWipeOut` - Circle wipe reveals new scene

**Circle Wipe Shader:**
- Uses `UI/CircleWipeTransition` shader
- Configurable softness and center point
- Eased animation (EaseInOutCubic)
- Automatically creates overlay canvas if needed

---

### 3. **Audio System** ⭐

**Purpose:** Comprehensive audio management for music and sound effects.

**Location:** `Scripts/Controllers/GlobalScene/SoundsController.cs`

**Features:**

#### Music System
- **Playlist Management**: Sequential music playback
- **Auto-Advance**: Automatically plays next track when current ends
- **Volume Control**: Separate music and SFX volume
- **Pause/Resume**: Handles application focus loss
- **Dynamic Playlists**: Can change playlist on scene change

**Music API:**
```csharp
SoundsController.I.SetMusicPlaylist(clipNames)
SoundsController.I.AddMusicToPlaylist(clipName)
SoundsController.I.SetMusicVolume(volume)
SoundsController.I.StopAllMusic()
```

#### SFX System
- **Spatial Audio**: Plays sounds at world positions
- **Viewport Culling**: Only plays sounds for visible objects
- **Debouncing**: Prevents sound spam with cooldown system
- **Instance Limiting**: Limits concurrent instances of specific sounds
- **Owner Tracking**: Associates sounds with game objects
- **Looping Support**: Can play looping sounds tied to objects

**SFX API:**
```csharp
SoundsController.I.PlaySound(soundId)
SoundsController.I.PlaySoundInGame(soundId, worldPos)
SoundsController.I.PlaySoundInGameForOwner(soundId, owner, loop)
SoundsController.I.PlayLoopForOwner(soundId, owner)
SoundsController.I.StopLoopForOwner(soundId, owner)
SoundsController.I.StopAllSoundsForOwner(owner)
```

#### SFX Player Component
- **Auto-Destruction**: SFX instances destroy after lifetime (default 10s)
- **Prefab-Based**: Uses `sfxPrefab` for instantiation
- **2D Audio**: Configured for 2D spatial blend
- **Volume Control**: Respects global SFX volume setting

#### Sound Database
- **Centralized Clips**: All audio clips stored in `DB_Sounds`
- **String-Based Access**: Retrieve clips by string ID
- **Categories**: Music, SFX, Voice clips
- **Extensible**: Easy to add new sounds

---

### 4. **Save System** ⭐

**Purpose:** JSON-based save system using PlayerPrefs with dot-notation access.

**Location:** `Scripts/Data/SaveController.cs`

**Features:**
- **JSON Storage**: Uses MiniJSON for serialization
- **Dot-Path Access**: `GetValue("settings.musicVolume")`
- **Schema Versioning**: Supports save schema versions
- **Default Values**: Fallback to defaults if key missing
- **Auto-Migration**: Handles legacy save keys
- **Type Conversion**: Automatic type conversion for numbers
- **Event System**: OnMoneyChanged, OnCombatSuitChanged events

**Save Schema:**
```json
{
  "schema-version": "1.0",
  "money": "0",
  "firstRun": "1",
  "settings": {
    "musicVolume": 0.2,
    "sfxVolume": 0.4
  }
}
```

**API:**
```csharp
SaveController.I.LoadData()
SaveController.I.SaveData()
object SaveController.I.GetValue(path, fallback)
SaveController.I.SetValue(key, value)
```

**Features:**
- Nested object support via dot notation
- Array access via numeric indices
- Automatic save on critical changes
- FORCE_REPLACE option for development
- Per-game save keys (supports multiple games)

---

### 5. **Bootstrap & Security** ⭐

**Purpose:** Security checks and domain validation for web builds.

**Location:** `Scripts/Controllers/GlobalScene/Bootstrap.cs`

**Features:**
- **Domain Lock**: Restricts game to allowed domains
- **Anti-Tamper**: Basic integrity verification
- **WebGL Support**: Deferred security checks for WebGL
- **Allowed Domains**: localhost, 127.0.0.1, gamepix.com
- **Graceful Degradation**: Logs errors instead of hard quit on WebGL

**Security Checks:**
1. Domain validation (checks Application.absoluteURL)
2. Integrity verification (component existence)
3. Scene name validation
4. Obfuscation helpers

---

### 6. **Ad Controller** ⭐

**Purpose:** Generic ad orchestration with provider abstraction.

**Location:** `Scripts/Controllers/GlobalScene/AdController.cs` (referenced in docs)

**Features:**
- **Provider-Agnostic**: Works with custom ad providers
- **Fake Ads**: Built-in fake ads for local testing
- **Game Pause**: Automatic pause/resume during ads
- **Audio Muting**: Automatic audio mute during ads
- **Scene Gating**: Configurable interstitial rules
- **UnityEvents**: Inspector-assignable hooks
- **C# Events**: Code-based event subscriptions

**API:**
```csharp
AdController.EnsureExists().ShowAd(onClosed)
AdController.EnsureExists().ShowRewardedAd(onFinished)
AdController.I.SetProvider(provider)
AdController.I.UseFakeAds = true/false
```

**Hooks:**
- `onPauseGameRequested` - Pause game systems
- `onResumeGameRequested` - Resume game systems
- `onMuteAudioRequested` - Mute audio
- `onUnmuteAudioRequested` - Unmute audio
- `onAdStartedUnity` - Ad started
- `onAdClosedUnity` - Ad closed
- `onRewardedAdFinishedUnity` - Rewarded ad finished

---

### 7. **InGameObject System** ⭐

**Purpose:** Base component for all game entities (characters, missiles, effects).

**Location:** `Scripts/Types/InGameObject.cs`

**Features:**

#### Core Properties
- `name` - Internal identifier
- `nameUI` - Display name
- `portrait` - UI sprite
- `type` - "Character", "Missile", "Effect", "Powerup"
- `tags` - String list for categorization
- `id` - Unique 8-digit random ID
- `owner` - Owner player/team ID
- `ownerObj` - Reference to owner InGameObject

#### Stats System
- `hp`, `hpMax` - Health points
- `attack` - Attack power
- `defense` - Defense stat
- `damage` - Damage dealt (for missiles/effects)

#### State Flags
- `isRunning` - Movement state
- `isAtk` - Attacking state
- `isInvul` - Invulnerability flag
- `isAI` - AI-controlled flag

#### Movement System
- `movement` - MovementData instance
- `angle` - Current facing angle
- `velocity`, `velocityDuration` - Legacy velocity
- `facing` - Facing direction string

#### Reference Points
- `referencePoints` - List of named attachment points
- `TryGetReferencePoint(name)` - Get point by name
- Used for muzzle flashes, attachment points, etc.

#### Trail Effects
- `trailEffects` - Configuration list
- `trailStates` - Runtime state list
- Spawns effects at intervals during movement

#### Components
- `anim` - Animator reference
- `rb` - Rigidbody2D reference
- `renderer` - Renderer reference

#### Collision
- `OnTriggerStay2D` - Delegates to OnCollisionFunctions

---

### 8. **Movement System** ⭐

**Purpose:** Centralized movement controller for linear motion.

**Location:** `Scripts/Controllers/MainGame/MovementController.cs`

**Features:**
- **Linear Movement**: Constant speed in a direction
- **Angle-Based**: Uses degrees for direction
- **Automatic Rotation**: Updates object rotation to face direction
- **Offset Support**: Rotation and angle offsets for sprite alignment

**Movement Data:**
```csharp
public class MovementData {
  public bool isLinearMoving;
  public float linearAngle;      // degrees
  public float linearSpeed;      // units/sec
  public float currentSpeed;     // runtime speed
  public float rotationOffset;   // sprite alignment
  public float angleOffset;      // extra rotation
}
```

**API:**
```csharp
MovementController.I.SetLinearMove(obj, speed, angle)
MovementController.I.StopLinearMove(obj)
MovementController.I.MoveUpdate(obj)  // Call in Update
```

---

### 9. **Character Controller** ⭐

**Purpose:** Manages character lifecycle, creation, and damage system.

**Location:** `Scripts/Controllers/MainGame/CharacterController.cs`

**Features:**
- **Character Creation**: Instantiate from database
- **Scene Safety**: Only exists in "Game" scene
- **Damage System**: Deal damage with attacker tracking
- **AOE Damage**: Area-of-effect damage
- **Destruction Queue**: Safe deferred destruction
- **Event Hooks**: OnDestroyFunctions integration

**API:**
```csharp
CharacterController.I.CreateCharacter(name, pos, owner)
CharacterController.I.DealDamage(target, attacker, damage)
CharacterController.I.DealDamage_AOE(attacker, damage, range)
CharacterController.I.DestroyCharacter(target)
CharacterController.I.GetDistance(a, b)
```

---

### 10. **Missile Controller** ⭐

**Purpose:** Manages missile/projectile lifecycle and trail effects.

**Location:** `Scripts/Controllers/MainGame/MissileController.cs`

**Features:**
- **Missile Creation**: Instantiate from database
- **Owner Tracking**: Associates missiles with shooters
- **Muzzle Flash Points**: Spawn at reference points
- **Trail Effects**: Automatic trail spawning system
- **Indestructible Tag**: Missiles tagged "indestructible" can't be destroyed
- **Movement Integration**: Uses MovementController

**Trail System:**
- Configurable effect name, spawn rate, reference point
- Angle inheritance from movement
- Extra angle offset support
- Runtime timer management

**API:**
```csharp
MissileController.I.CreateMissile(name, pos, ownerObj)
MissileController.I.CreateMissileOnMuzzleFlashPoint(name, owner)
MissileController.I.SetMissileDetails_Linear(missile, owner, speed, attack)
MissileController.I.DestroyMissile(missile)
```

---

### 11. **Effects Controller** ⭐

**Purpose:** Manages visual effects and particle systems.

**Location:** `Scripts/Controllers/MainGame/EffectsController.cs`

**Features:**
- **Effect Creation**: Instantiate from database
- **Angle Support**: Rotate effects on spawn
- **Delayed Effects**: Create effects after delay
- **Radiating Effects**: Spawn multiple effects in a circle pattern
- **Fade System**: Automatic opacity fade and destruction
- **Movement Integration**: Effects can move

**Radiating Effects:**
```csharp
EffectsController.I.CreateRadiatingEffects(
  effectName: "smoke-01",
  origin: pos,
  count: 8,
  startAngleDeg: 0f,
  angleStepDeg: 45f,
  speed: 3f,
  lifetime: 1.25f,
  startAlpha: 1f,
  endAlpha: 0f
)
```

**API:**
```csharp
EffectsController.I.CreateEffect(name, pos, angle)
EffectsController.I.CreateDelayedEffect(name, pos, angle, delay)
EffectsController.I.DestroyEffect(effect)
EffectsController.I.DestroyEffect_GameObject(gameObject)
```

---

### 12. **Event System** ⭐

**Purpose:** Extensible hooks for game events.

**Location:** `Scripts/Data/Events/`

**Components:**

#### OnCollisionFunctions
- Handles collision between InGameObjects
- Debouncing to prevent duplicate collisions
- Generic missile-enemy collision handling
- Extensible switch statements for custom behavior

#### OnDamageFunctions
- Damage calculation hooks
- Modify damage before application
- Add damage multipliers, resistances, etc.

#### OnDestroyFunctions
- Execute code when objects are destroyed
- Spawn loot, effects, etc.
- Custom per-object destruction logic

---

### 13. **Database System** ⭐

**Purpose:** Centralized prefab and data storage.

**Locations:**
- `Scripts/Data/Database_MainGame.cs` - Game prefabs
- `Scripts/Data/GlobalDatas/DB_Sounds.cs` - Audio clips

**Database_MainGame:**
- `characters` - Character prefab list
- `effects` - Effect prefab list
- `missiles` - Missile prefab list
- `emptyPrefab` - Utility empty prefab

**DB_Sounds:**
- `bgmGame` - Music clips array
- Individual SFX clip fields
- Voice clip fields
- `GetClip(soundId)` - String-based retrieval
- `FindMusicClipByName(clipName)` - Music lookup

---

### 14. **Checks System** ⭐

**Purpose:** Validation and rule checking.

**Location:** `Scripts/Data/Checks/Check_MainGame.cs`

**Features:**
- `MissileCanHitEnemy` - Friendly fire check
- `CheckDealDamage` - Invulnerability check
- Extensible for custom game rules

---

### 15. **Helper Utilities** ⭐

**Purpose:** Common utility functions.

**Location:** `Scripts/Core/Utils/Helpers.cs`

**Functions:**
- `GenerateRandomIntId(digits)` - Random integer IDs
- `GenerateRandomStringId(length)` - Random string IDs
- `StringToListString(string)` - Parse semicolon-delimited strings
- `IsMobile()` - Platform detection with editor override

---

## System Controllers

### GlobalScene Controllers (Persistent)

| Controller | Purpose | Singleton | DontDestroyOnLoad |
|------------|---------|-----------|-------------------|
| `GlobalControl` | Boot orchestrator, scene change delegator | ✅ | ✅ |
| `SceneControl` | Scene transition state machine | ✅ | ✅ |
| `SoundsController` | Audio management | ✅ | ❌ (in scene) |
| `SaveController` | Save/load system | ✅ | ✅ |
| `Bootstrap` | Security checks | ❌ | ❌ |
| `DB_Sounds` | Audio clip database | ✅ | ✅ |

### MainGame Controllers (Scene-Specific)

| Controller | Purpose | Singleton | Scene Lock |
|------------|---------|-----------|------------|
| `MainGameController` | Game loop orchestrator | ✅ | "Game" |
| `CharacterController` | Character management | ✅ | "Game" |
| `MissileController` | Missile management | ✅ | "Game" |
| `EffectsController` | Effect management | ✅ | "Game" |
| `MovementController` | Movement system | ✅ | "Game" |

### Event Controllers

| Controller | Purpose | Singleton | Scene Lock |
|------------|---------|-----------|------------|
| `OnCollisionFunctions` | Collision handling | ✅ | ❌ |
| `OnDamageFunctions` | Damage calculation | ✅ | ❌ |
| `OnDestroyFunctions` | Destruction hooks | ✅ | ❌ |
| `Checks_MainGame` | Game rule validation | ✅ | ❌ |

---

## Data Management

### Save System Architecture

**Storage:** PlayerPrefs (JSON serialized)  
**Serializer:** MiniJSON  
**Schema Version:** 1.0

**Key Features:**
- Dot-notation path access
- Nested object support
- Array index access
- Type conversion
- Default fallbacks
- Event notifications

**Example Usage:**
```csharp
// Get nested value
float musicVol = (float)SaveController.I.GetValue("settings.musicVolume", 0.5f);

// Set value
SaveController.I.SetValue("money", 1000);
SaveController.I.SaveData();

// Listen to events
SaveController.I.OnMoneyChanged += (newMoney) => {
  UpdateMoneyUI(newMoney);
};
```

---

## Type System

### Core Types

**InGameObject** - Base component for all game entities
- Stats, state, movement, collision
- Reference points for attachments
- Trail effect system

**MovementData** - Movement configuration
- Linear movement state
- Speed and angle
- Rotation offsets

**TrailEffectConfig** - Trail effect configuration
- Effect name, spawn rate
- Reference point, angle settings

**TrailEffectState** - Runtime trail state
- Config reference, timer

**ReferencePoint** - Named attachment point
- Name, local position

**CollisionHandled** - Collision debounce tracking
- ID pair, duration

**Character** - Character data (unused in current codebase)
- Name, portrait, description

---

## Project Structure

```
sq-unity-framework/
├── Docs/
│   ├── Dev/
│   │   ├── AI-Proposals/          # System proposals
│   │   │   ├── SCENE_CONTROL_SYSTEM_PROPOSAL.md
│   │   │   └── CIRCLE_WIPE_TRANSITION_PROPOSAL.md
│   │   └── System-Dev-Logs/       # Development logs
│   │       ├── SCENE_CONTROL_SYSTEM_*.md
│   │       └── CIRCLE_WIPE_TRANSITION.md
│   ├── Features/                  # Feature documentation
│   │   └── NEW_SCENE_CONTROL.md
│   └── SQ_TEMPLATE_BREAKDOWN.md   # This file
│
├── Scenes/
│   ├── GlobalScene.unity          # Persistent scene (Build Index 0)
│   └── Game.unity                 # Main game scene
│
├── Scripts/
│   ├── Controllers/
│   │   ├── GlobalScene/           # Persistent controllers
│   │   │   ├── Bootstrap.cs
│   │   │   ├── GlobalControl.cs
│   │   │   ├── SceneControl.cs
│   │   │   └── SoundsController.cs
│   │   └── MainGame/              # Game-specific controllers
│   │       ├── MainGameController.cs
│   │       ├── CharacterController.cs
│   │       ├── MissileController.cs
│   │       ├── EffectsController.cs
│   │       └── MovementController.cs
│   │
│   ├── Data/
│   │   ├── Checks/
│   │   │   └── Check_MainGame.cs
│   │   ├── Events/
│   │   │   ├── OnCollisionFunctions.cs
│   │   │   ├── OnDamageFunctions.cs
│   │   │   └── OnDestroyFunctions.cs
│   │   ├── GlobalDatas/
│   │   │   └── DB_Sounds.cs
│   │   ├── Database_MainGame.cs
│   │   └── SaveController.cs
│   │
│   ├── Types/
│   │   ├── InGameObject.cs
│   │   ├── Types.cs
│   │   ├── Types_Character.cs
│   │   └── Types_Trail.cs
│   │
│   ├── Core/
│   │   └── Utils/
│   │       └── Helpers.cs
│   │
│   ├── Audio/
│   │   └── SFXPlayer.cs
│   │
│   └── ThirdParty/
│       └── MiniJSON.cs
│
└── .gitignore
```

---

## Usage Guidelines

### Starting a New Project

1. **Set Build Settings**
   - Add `GlobalScene` as index 0
   - Add `Lobby` and `Game` scenes
   - Configure platform settings

2. **Configure GlobalScene**
   - Assign audio sources to SoundsController
   - Set up DB_Sounds with audio clips
   - Configure SaveController default JSON
   - Set Bootstrap allowed domains

3. **Create Content Scenes**
   - Create Lobby scene with UI
   - Create Game scene with gameplay
   - Add scene-specific controllers to Game scene

4. **Set Up Databases**
   - Populate Database_MainGame with prefabs
   - Assign audio clips to DB_Sounds
   - Configure character/missile/effect prefabs

5. **Implement Game Logic**
   - Extend OnCollisionFunctions for custom collisions
   - Extend OnDamageFunctions for damage modifiers
   - Extend OnDestroyFunctions for death effects

### Best Practices

#### Scene Management
- Always use `GlobalControl.I.ChangeScene()` for scene changes
- Never manually load/unload GlobalScene
- Use `LoadSceneInstant()` only for initial load
- Check `SceneControl.I.IsTransitioning` before scene changes

#### Audio
- Use `PlaySoundInGame()` for spatial audio
- Use `PlaySound()` for UI sounds
- Set playlist with `SetMusicPlaylist()` on scene change
- Always check `SoundsController.I != null` before playing

#### Save System
- Call `SaveData()` after important changes
- Use dot-notation for nested values
- Provide fallback values in `GetValue()`
- Subscribe to events for UI updates

#### Game Objects
- Always use controllers to create objects
- Set owner and ownerObj for missiles
- Use reference points for spawn positions
- Tag indestructible objects appropriately

#### Controllers
- Check singleton instance before use: `if (Controller.I != null)`
- Use `EnsureExists()` for controllers that might not exist
- Don't call Update methods directly (MainGameController handles it)
- Validate scene before controller initialization

### Common Patterns

#### Creating a Character
```csharp
InGameObject character = CharacterController.I.CreateCharacter(
  "enemy-fighter-1",
  new Vector2(0, 5),
  owner: 1  // enemy team
);
```

#### Shooting a Missile
```csharp
InGameObject missile = MissileController.I.CreateMissileOnMuzzleFlashPoint(
  "plasma-bolt",
  playerCharacter
);
MissileController.I.SetMissileDetails_Linear(
  missile,
  playerCharacter,
  speed: 10f,
  attack: 25
);
```

#### Playing Spatial Audio
```csharp
SoundsController.I.PlaySoundInGame(
  "explosion-1",
  explosionPosition
);
```

#### Changing Scenes
```csharp
GlobalControl.I.ChangeScene("Lobby");
```

#### Saving Data
```csharp
SaveController.I.SetValue("player.level", 5);
SaveController.I.SetValue("player.xp", 1250);
SaveController.I.SaveData();
```

#### Creating Effects
```csharp
// Single effect
EffectsController.I.CreateEffect("explosion-1", position, angle: 0f);

// Radiating effects
EffectsController.I.CreateRadiatingEffects(
  "smoke-particle",
  position,
  count: 12,
  startAngleDeg: 0f,
  angleStepDeg: 30f,
  speed: 2f,
  lifetime: 2f,
  startAlpha: 1f,
  endAlpha: 0f
);
```

---

## Extension Points

### Adding New Features

1. **New Character Type**
   - Create prefab with InGameObject component
   - Add to Database_MainGame.characters
   - Implement custom behavior in OnCollisionFunctions
   - Add custom damage logic in OnDamageFunctions

2. **New Weapon/Missile**
   - Create missile prefab with InGameObject
   - Add to Database_MainGame.missiles
   - Configure trail effects if needed
   - Implement collision logic

3. **New Audio**
   - Add AudioClip field to DB_Sounds
   - Add case in GetClip() switch
   - Use string ID to play sound

4. **New Scene**
   - Create scene in Unity
   - Add to Build Settings
   - Add scene-specific controllers if needed
   - Use GlobalControl.I.ChangeScene() to load

5. **Custom Save Data**
   - Extend defaultJson in SaveController
   - Use dot-notation to access nested data
   - Add events for UI updates if needed

6. **Custom Transitions**
   - Modify SceneControl transition parameters
   - Adjust delays and durations
   - Create custom shader for different effects

---

## Dependencies

### Unity Packages
- **DOTween** (referenced in CharacterController)
- **TextMeshPro** (referenced in EffectsController)

### Third-Party Scripts
- **MiniJSON** - JSON serialization (`Scripts/ThirdParty/MiniJSON.cs`)

### Custom Shaders
- **UI/CircleWipeTransition** - Circle wipe shader for scene transitions

---

## Notes

### Scene Safety
Many controllers validate they're in the correct scene:
```csharp
void Awake() {
  if (gameObject.scene.name != "Game") {
    Destroy(gameObject);
    return;
  }
  // ... rest of initialization
}
```

This prevents controllers from existing in wrong scenes and causing errors.

### Singleton Pattern
All controllers use the singleton pattern:
```csharp
public static ControllerName I;

void Awake() {
  if (I == null) { I = this; }
  else { Destroy(gameObject); }
}
```

Some also use `DontDestroyOnLoad()` for persistence.

### EnsureExists Pattern
Some controllers provide `EnsureExists()` for lazy initialization:
```csharp
public static SceneControl EnsureExists() {
  if (I != null) return I;
  var existing = FindFirstObjectByType<SceneControl>();
  if (existing != null) {
    I = existing;
    return I;
  }
  var go = new GameObject("SceneControl");
  I = go.AddComponent<SceneControl>();
  DontDestroyOnLoad(go);
  return I;
}
```

### Movement System
The framework uses a centralized movement system:
- `MovementData` stores movement state on each InGameObject
- `MovementController` updates all moving objects
- Linear movement is the primary movement type
- Future: Could add homing, arc, bezier, etc.

### Trail Effects
Missiles and effects can spawn trails:
- Configure `TrailEffectConfig` on prefab
- Runtime `TrailEffectState` tracks timers
- Automatically spawns effects at intervals
- Supports reference points and angle inheritance

---

## Version History

**v1.0** (2026-03-01)
- Initial template breakdown
- Documented all core systems
- Added usage guidelines and examples

---

## Related Documentation

- [New Scene Control System](Features/NEW_SCENE_CONTROL.md) - Detailed scene control documentation
- [Scene Control Proposal](Dev/AI-Proposals/SCENE_CONTROL_SYSTEM_PROPOSAL.md) - Original system design
- [Circle Wipe Transition](Dev/System-Dev-Logs/CIRCLE_WIPE_TRANSITION.md) - Transition implementation log

---

**End of Document**
