# PlanetMerge — Complete Project Documentation

> **Engine:** Unity 6 LTS (6000.3.7f1) · **Pipeline:** URP (Universal Render Pipeline)  
> **All visuals are procedurally generated at runtime — no external art assets required.**

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Architecture & Object Hierarchy](#2-architecture--object-hierarchy)
3. [Data Layer — ScriptableObjects](#3-data-layer--scriptableobjects)
4. [Controllers](#4-controllers)
   - [Planet](#41-planet)
   - [BlackHoleController](#42-blackholecontroller)
   - [GravityManager](#43-gravitymanager)
   - [GravityZoneVisual](#44-gravityzonevisual)
   - [SlingshotController](#45-slingshotcontroller)
   - [SlingshotVisual](#46-slingshotvisual)
   - [TrajectoryRenderer](#47-trajectoryrenderer)
5. [Managers](#5-managers)
   - [GameManager](#51-gamemanager)
   - [UIManager](#52-uimanager)
   - [AudioManager](#53-audiomanager)
   - [ScoreManager](#54-scoremanager)
   - [MergeManager](#55-mergemanager)
6. [Utilities](#6-utilities)
   - [PlanetFactory](#61-planetfactory)
   - [BackgroundManager](#62-backgroundmanager)
7. [Game Rules & Win / Lose Conditions](#7-game-rules--win--lose-conditions)
8. [Planet Tier Chain](#8-planet-tier-chain)
9. [Physics System](#9-physics-system)
10. [Visual Systems](#10-visual-systems)
11. [Audio System](#11-audio-system)
12. [UI System](#12-ui-system)
13. [Inspector Tuning Reference](#13-inspector-tuning-reference)
14. [PlayerPrefs Keys](#14-playerprefs-keys)

---

## 1. Project Overview

PlanetMerge is a slingshot-merge puzzle game. The player launches planets from a fixed spawn point toward a central black hole. When two planets of the same tier collide they merge into the next tier (Mercury → Venus → Earth → Mars → Jupiter → Saturn). Two Saturns merging triggers a win. If any planet settles with part of its body outside the gravity zone boundary, the game is lost.

**Core loop:**
1. A planet appears at the spawn point.
2. The player clicks/touches and drags to aim and set power.
3. On release the planet is launched with radial gravity continuously pulling it toward the black hole.
4. Same-tier collisions trigger merges; higher-tier planets are created at the midpoint.
5. The queue feeds the next planet automatically after a short delay.

---

## 2. Architecture & Object Hierarchy

```
Scene
├── BlackHole (GameObject)
│   ├── BlackHoleController   — visual layers + win sequence
│   ├── GravityManager        — radial gravity + boundary + lose detection
│   └── GravityZoneVisual     — particles, field lines, gradient, warning pulses
│
├── Managers (GameObject)
│   ├── GameManager           — game loop, queue, win/lose
│   ├── UIManager             — all HUD and overlay UI (built in code)
│   ├── ScoreManager          — score + high score + PlayerPrefs
│   ├── MergeManager          — deferred merge execution
│   ├── AudioManager          — background music + SFX (auto-creates itself)
│   ├── PlanetFactory         — planet GameObject creation + sprite generation
│   ├── BackgroundManager     — starfield + gradient background mesh
│   ├── SlingshotController   — drag input + launch logic
│   ├── SlingshotVisual       — spawn circle + pull line visuals
│   └── TrajectoryRenderer    — dotted arc preview
│
└── Canvas (GameObject)
    └── (all UI built at runtime by UIManager — nothing in the scene)
```

Every manager uses the **singleton pattern** (`public static T Instance`). All singletons survive their own duplicates via the guard:
```csharp
if (Instance != null && Instance != this) { Destroy(gameObject); return; }
Instance = this;
```

---

## 3. Data Layer — ScriptableObjects

### `PlanetData` (ScriptableObject)

Location: `Assets/Scripts/Data/PlanetData.cs`  
Asset files: `Mercury.asset`, `Venus.asset`, `Earth.asset`, `Mars.asset`, `Jupiter.asset`, `Saturn.asset`

Each planet type is a single `.asset` file. No code changes are needed to add or tweak a planet — only the asset needs updating.

| Field | Type | Description |
|---|---|---|
| `planetName` | string | Display name (shown in Next Queue) |
| `tier` | int | Sort order 1–6. Mercury = 1, Saturn = 6 |
| `primaryColor` | Color | Main body colour of the sprite |
| `glowColor` | Color | Highlight/glow colour blended toward the centre |
| `radius` | float | Visual radius in Unity world units |
| `mass` | float | Rigidbody2D mass (affects gravity response) |
| `drag` | float | Linear damping — slows the planet over time |
| `angularDrag` | float | Rotational damping |
| `bounciness` | float | PhysicsMaterial2D bounciness (0 = no bounce, 1 = perfect elastic) |
| `scoreOnMerge` | int | Points awarded when two of this tier merge |
| `nextTier` | PlanetData | Reference to the tier produced by merging. **Null on Saturn** (triggers win instead) |

**Current planet values:**

| Planet | Tier | Radius | Mass | primaryColor | scoreOnMerge |
|---|---|---|---|---|---|
| Mercury | 1 | 0.34 | 0.6 | #9CA3AF (silver-gray) | 10 |
| Venus | 2 | 0.43 | 1.0 | #F59E0B (amber) | 30 |
| Earth | 3 | 0.52 | 1.5 | #3B82F6 (blue) | 60 |
| Mars | 4 | 0.61 | 2.0 | #EF4444 (red) | 120 |
| Jupiter | 5 | 0.73 | 3.0 | #FB923C (orange) | 250 |
| Saturn | 6 | 0.84 | 4.0 | #FBBF24 (gold) | 500 |

---

## 4. Controllers

### 4.1 Planet

**File:** `Assets/Scripts/Controllers/Planet.cs`  
**Requires:** `Rigidbody2D`, `CircleCollider2D`, `SpriteRenderer`

The core component placed on every planet GameObject. Created exclusively by `PlanetFactory`.

#### State Properties

| Property | Type | Description |
|---|---|---|
| `data` | PlanetData | The ScriptableObject that defines this planet's stats |
| `IsLaunched` | bool | True after `Launch()` is called; enables gravity and collision logic |
| `IsMerging` | bool | True while a merge is in progress; disables further collision handling |
| `Rb` | Rigidbody2D | Public accessor to the physics body |
| `Sr` | SpriteRenderer | Public accessor to the renderer |

#### Key Methods

**`Initialise(PlanetData data)`**  
Called by PlanetFactory immediately after the GameObject is created. Sets physics properties (mass, drag, bounciness), sets `gravityScale = 0` (GravityManager handles gravity manually), and applies the visual scale from `data.radius`.

**`Freeze()`**  
Switches the Rigidbody2D to Kinematic and zeroes velocity/angular velocity. Called when the planet is loaded at the spawn point waiting to be shot.

**`Unfreeze()`**  
Restores Dynamic body type. Called just before `Launch()`.

**`Launch(Vector2 velocity)`**  
Sets `IsLaunched = true`, applies the given velocity, and registers the planet with `GravityManager` so it receives radial gravity each `FixedUpdate`.

**`OnCollisionEnter2D(Collision2D col)`**  
The merge detection entry point. When two launched planets of the same tier collide:
- If `data.nextTier == null` (both are Saturn) → calls `TryTriggerWin()`
- Otherwise → calls `TryScheduleMerge()` which enqueues the merge in `MergeManager`

Also calls `CheckWarningPulse()` on every planet–planet collision regardless of tier.

**`CheckWarningPulse(Planet other)`**  
Checks whether either colliding planet's edge (`center + radius`) is at or past the warning ring radius. If so, calls `GravityZoneVisual.Instance.EmitWarningPulse()` to fire the inward red pulse wave.

**`OnDestroy()`**  
Removes the planet from GravityManager's list and clears its merge ID from the static merge guard set.

**`ClearMergeState()` (static)**  
Clears the static HashSet of currently-merging IDs. Called by GameManager on restart.

---

### 4.2 BlackHoleController

**File:** `Assets/Scripts/Controllers/BlackHoleController.cs`

Builds and animates the black hole visual layers entirely in code. Also manages the solid physics surface that planets land on.

#### Inspector Fields

| Field | Default | Description |
|---|---|---|
| `coreRadius` | 0.66 | World-space radius of the solid collider AND visual core |
| `shimmerSpeed` | 2.5 | Speed of the edge ring brightness animation |
| `winDuration` | 1.8 | Seconds the win suck-in sequence plays |
| `winMaxScale` | 10 | Maximum scale factor of the core during win animation |
| `winSuckForce` | 250 | Force applied to all planets during the win sequence |

#### Visual Layers (built procedurally, no textures needed)

| Child Name | Description |
|---|---|
| `Halo` | Large soft radial gradient disc — dark purple fading to transparent at edges. Radius = `coreRadius × 7`. Sort order 0. |
| `Core` | Solid black sphere with a subtle blue-violet centre tint and a bright rim gradient toward the edge. Radius = `coreRadius × 2`. Sort order 4. |
| `EdgeRing` | Thin bright annulus (82%–100% of core radius) with anti-aliased inner and outer edges. Pulses brightness. Sort order 5. |
| `BlackHoleBody` | Child GameObject with a solid `CircleCollider2D` (radius = `coreRadius`). This is what planets physically rest on. |

#### Key Methods

**`ResetVisuals()`**  
Destroys only `BlackHoleBody`, `Halo`, `Core`, and `EdgeRing` — leaves GravityManager's boundary visual and GravityZoneVisual's particles/field lines untouched. Rebuilds all four. Called by `GameManager.StartGame()` to clear the win animation state.

**`PlayWinSequence()` (IEnumerator)**  
Over `winDuration` seconds: scales the Core up toward `winMaxScale`, applies increasing radial force on all planets, and shrinks planets as they approach the center. After the sequence, all planet GameObjects are destroyed. Called by `GameManager.WinSequence()`.

**`AnimateEdge()` (called in Update)**  
Lerps the EdgeRing's color between `EdgeBase` (vivid purple) and `EdgeShimmer` (bright lavender) using a sine wave at `shimmerSpeed`.

---

### 4.3 GravityManager

**File:** `Assets/Scripts/Controllers/GravityManager.cs`  
**Attach to:** The BlackHole GameObject.

The central physics authority. Replaces Unity's built-in gravity (all planet `gravityScale` values are 0). Applies a radial attraction force toward the black hole on every launched planet every `FixedUpdate`. Also owns the lose-condition logic and draws the boundary visuals.

#### Inspector Fields

| Field | Default | Description |
|---|---|---|
| `gravityConstant` | 80 | Gravity force = `gravityConstant / distance`. Higher = stronger pull |
| `zoneRadius` | 3.5 | World-space radius of the safe zone boundary |
| `settleSpeedThreshold` | 0.4 | Speed (units/s) below which a planet is considered settling |
| `settleRequiredTime` | 1.8 | Seconds a planet must stay below threshold before a settle is confirmed |
| `drawBoundary` | true | Toggle the boundary ring visuals on/off |
| `boundaryColor` | cyan | Color of the outer dashed ring |
| `boundaryLineWidth` | 0.035 | Thickness of outer ring dashes |
| `boundarySegments` | 140 | Number of dash segments in the outer ring |
| `dashFraction` | 0.18 | Fraction of each segment that is drawn (rest is gap) |
| `warningRingFraction` | 0.88 | Warning ring radius = `zoneRadius × warningRingFraction` |
| `warningRingColor` | red-orange | Color of the inner warning ring |
| `warningRingWidth` | 0.022 | Thickness of warning ring dashes |
| `warningRingSegments` | 100 | Number of dashes in the warning ring |
| `warningDashFraction` | 0.12 | Fraction drawn per warning ring dash |

#### Public Properties

| Property | Description |
|---|---|
| `ZoneRadius` | The outer safe-zone radius (read-only accessor) |
| `WarningRingRadius` | `zoneRadius × warningRingFraction` — the alert threshold |

#### Key Methods

**`RegisterPlanet(Planet p)`**  
Adds a planet to the active list and sets its `gravityScale = 0`. Called automatically by `Planet.Launch()`.

**`UnregisterPlanet(Planet p)`**  
Removes a planet and clears its settle timer. Called by `Planet.OnDestroy()` and by `MergeManager` before destroying merged planets.

**`ApplyRadialGravity(Planet p)` (FixedUpdate)**  
Calculates the direction from planet to black hole center, divides `gravityConstant` by distance, and applies that force. Force formula: `F = G / d` (inverse linear, not inverse-square).

**`CheckSettle(Planet p)` (FixedUpdate)**  
If the planet's speed drops below `settleSpeedThreshold` for `settleRequiredTime` seconds AND the planet's edge (`center distance + planet.radius`) is outside `zoneRadius` → calls `GameManager.TriggerLose()`.

**`BuildBoundaryVisual()` (public)**  
Destroys any existing `BoundaryVisual` child and rebuilds two concentric dashed rings:
- **Outer ring** (cyan): at `zoneRadius` — the hard safe-zone boundary.
- **Warning ring** (red-orange): at `zoneRadius × warningRingFraction` — the early-warning indicator.

**`ResetTimers()`**  
Clears all settle timers and the active planet list. Called by `GameManager.StartGame()`.

---

### 4.4 GravityZoneVisual

**File:** `Assets/Scripts/Controllers/GravityZoneVisual.cs`  
**Attach to:** The BlackHole GameObject.

Pure visual component — no gameplay logic. Adds four atmospheric effects and the warning pulse system.

#### Effects

**1. Boundary Pulse (`PulseBoundary`)**  
Animates only the outer boundary ring (children named `"OuterD…"`) using a sine wave to vary alpha and line width. Warning ring dashes (`"WarnD…"`) are deliberately excluded and keep their static red color.

**2. Screen-Wide Gravity Gradient (`BuildGravityGradient`)**  
A large radial mesh (24 rings × 64 segments) centered on the black hole. Transparent at the center, darkening toward the screen edges with a dark-purple tint. Gives the sense of a gravity well pulling inward across the whole screen. Sort order −50 (behind everything).

**3. Gravity Particles (`BuildParticles` / `UpdateParticles`)**  
120 small dot sprites scattered across the screen that drift continuously toward the black hole. Each particle:
- Spawns at a random position on the outer boundary (`spawnRadius`)
- Accelerates as it gets closer (speed ∝ `spawnRadius / distance`)
- Twinkles via a sine wave on its alpha
- Fades to transparent near the black hole
- Respawns at a new outer boundary position on arrival

**4. Radial Field Lines (`BuildFieldLines` / `PulseFieldLines`)**  
20 LineRenderer lines pointing inward. They are narrow at the outer end and widen toward the black hole, suggesting increasing force. Their alpha pulses on a slow sine cycle.

**5. Warning Pulse (`EmitWarningPulse` / `WarningPulseCoroutine`)**  
Triggered by `Planet.CheckWarningPulse()`. A red-orange circle ring starts at the warning ring radius and shrinks inward to near-zero over `warningPulseDuration` (0.75 s) while fading to transparent. This signals that a planet near the zone edge has just collided.

#### Inspector Fields

| Group | Field | Default | Description |
|---|---|---|---|
| Boundary Pulse | `pulseSpeed` | 2.2 | Sine wave speed for outer ring animation |
| Boundary Pulse | `pulseMinAlpha` | 0.45 | Minimum alpha of outer ring |
| Boundary Pulse | `pulseMaxAlpha` | 1.00 | Maximum alpha of outer ring |
| Gravity Gradient | `showGravityGradient` | true | Enable/disable gradient mesh |
| Gravity Gradient | `gradientRadius` | 14 | Radius of gradient mesh (should cover screen diagonally) |
| Gravity Gradient | `gradientEdgeAlpha` | 0.30 | Darkness at the outer edge |
| Particles | `particleCount` | 120 | Number of drifting dust particles |
| Particles | `spawnRadius` | 10 | Distance from BH where particles spawn |
| Particles | `driftSpeed` | 1.8 | Base particle movement speed |
| Field Lines | `showFieldLines` | true | Enable/disable field lines |
| Field Lines | `fieldLineCount` | 20 | Number of radial lines |
| Field Lines | `fieldLineOuter` | 4.0 | Outer end of field lines |
| Warning Pulse | `warningPulseColor` | red-orange | Color of the inward pulse ring |
| Warning Pulse | `warningPulseDuration` | 0.75 | Seconds the pulse animation plays |

---

### 4.5 SlingshotController

**File:** `Assets/Scripts/Controllers/SlingshotController.cs`

Handles all drag-and-release input and converts it to planet launch velocity.

#### Inspector Fields

| Field | Default | Description |
|---|---|---|
| `spawnPoint` | (scene ref) | Transform where planets are placed while waiting to be shot |
| `mainCamera` | Camera.main | Camera used for screen-to-world coordinate conversion |
| `forceMultiplier` | 5 | Velocity = `dragVector × forceMultiplier` |
| `maxLaunchSpeed` | 9 | Hard cap on launch velocity (units/s) |
| `maxDragRadius` | 3 | Maximum drag distance from spawn point (world units) |

#### Public Read-Only Properties (used by SlingshotVisual)

| Property | Description |
|---|---|
| `IsDragging` | True while the player is holding the mouse button and dragging |
| `HasPlanetLoaded` | True when a planet is waiting at the spawn point |
| `CurrentPlanetPos` | Current world position of the loaded planet (follows drag) |

#### Input Flow

1. **Press** — If the cursor is within `planet.radius × 3` of the spawn point, dragging begins.
2. **Hold** — `dragVec` is clamped to `maxDragRadius`. Planet position follows the cursor. Launch velocity = `−dragVec × forceMultiplier`, clamped to `maxLaunchSpeed`. Trajectory preview is updated every frame via `TrajectoryRenderer.Show()`.
3. **Release** — If `launchVelocity.magnitude > 0.5`, `Fire()` is called. Otherwise the planet snaps back to spawn.

#### `Fire()`
- Resets planet position to spawn point
- Calls `planet.Unfreeze()` then `planet.Launch(launchVelocity)`
- Calls `GameManager.OnPlanetLaunched()` which starts the delay before the next planet loads
- Sets `_currentPlanet = null`

#### `LoadPlanet(Planet planet)`
Called by `GameManager.LoadNextPlanet()`. Places the given planet at the spawn point and freezes it.

---

### 4.6 SlingshotVisual

**File:** `Assets/Scripts/Controllers/SlingshotVisual.cs`  
**Attach to:** Same GameObject as SlingshotController.

Two purely cosmetic elements — no gameplay impact.

#### Spawn Circle

A static dotted ring around the spawn point. Built once in `Start()` using world-space `LineRenderer` segments at `spawnCircleRadius` (0.68 world units — slightly larger than Earth's 0.52). Indicates where the planet will be placed before launch.

| Field | Default | Description |
|---|---|---|
| `spawnCircleRadius` | 0.68 | World-space ring radius |
| `spawnCircleSegments` | 64 | Number of dash segments |
| `spawnCircleDash` | 0.14 | Fraction of each segment that is drawn |
| `spawnCircleColor` | white 22% | Colour of the ring |
| `spawnCircleWidth` | 0.018 | Line thickness |

#### Pull Line

A tapered `LineRenderer` from the spawn point to the planet's current drag position, visible only while dragging.

- **Near spawn:** white, thick end (`pullLineWidth = 0.035`)
- **At planet:** tapers to a thin tip (`pullLineTipWidth = 0.010`) at 15% alpha
- **Color shift:** Lerps from white → orange-red as the drag distance approaches `maxDragRadius`. Gives an intuitive "power meter" feel without any UI widget.

---

### 4.7 TrajectoryRenderer

**File:** `Assets/Scripts/Controllers/TrajectoryRenderer.cs`  
**Attach to:** Same GameObject as SlingshotController.

Shows a dotted arc preview of where the planet will travel, accounting for the black hole's radial gravity (not simple downward gravity).

#### How It Works

Uses Euler forward integration — simulates the physics manually step by step:
1. Each step: compute gravity direction and magnitude (`G / distance`)
2. Update velocity by adding gravity × `timeStep`
3. Update position by adding velocity × `timeStep`
4. Every `stepsPerDot` steps, place a dot sprite at the current position

This means the arc matches real in-game flight paths exactly, including the curve toward the black hole.

#### Inspector Fields

| Field | Default | Description |
|---|---|---|
| `dotColor` | cyan | Color of trajectory dots |
| `dotRadius` | 0.035 | World-space size of each dot |
| `dotCount` | 16 | Number of visible dots |
| `stepsPerDot` | 2 | Simulation steps between each dot |
| `timeStep` | 0.018 | Seconds per simulation step |

Dots fade from 90% to 10% alpha along the arc (near → far). The pool of 16 dot `GameObject`s is created once in `Awake()` and reused every frame via `SetActive()`.

---

## 5. Managers

### 5.1 GameManager

**File:** `Assets/Scripts/Managers/GameManager.cs`

The central game loop controller. Owns the planet queue and coordinates all other systems.

#### Inspector Fields

| Field | Default | Description |
|---|---|---|
| `allPlanetData` | (array) | All 6 PlanetData assets in order Mercury→Saturn |
| `preQueueSize` | 8 | Number of planets kept pre-generated in the queue |
| `maxSpawnTier` | 3 | Highest tier that can appear in the queue (1 = Mercury only, 3 = up to Earth) |
| `nextPlanetDelay` | 1.2 | Seconds after a launch before the next planet loads |

#### State Properties

| Property | Description |
|---|---|
| `CanShoot` | True when a planet is loaded and no game-ending condition is active |
| `IsGameOver` | True after `TriggerLose()` |
| `IsWon` | True after `TriggerWin()` |
| `CurrentLevel` | Placeholder — always 1 currently |

#### Key Methods

**`StartGame()`**  
Full reset sequence in order:
1. Reset state flags (`IsGameOver`, `IsWon`, `CanShoot`)
2. `UIManager.HideOverlays()` — hide Game Over / Win panels
3. `BlackHoleController.ResetVisuals()` — clear win animation
4. `AudioManager.ResumeMusic()` — restart background music
5. `Planet.ClearMergeState()` — clear merge ID guard
6. `GravityManager.ResetTimers()` — clear settle timers and planet list
7. Destroy all existing Planet objects in scene
8. `ScoreManager.ResetScore()`
9. Refill queue with `preQueueSize` random planets
10. `LoadNextPlanet()`

**`OnPlanetLaunched()`**  
Sets `CanShoot = false` and starts the `WaitThenLoad` coroutine (waits `nextPlanetDelay` seconds, then calls `LoadNextPlanet()`).

**`LoadNextPlanet()`**  
Tops up the queue to `preQueueSize`, dequeues the next `PlanetData`, spawns it at the spawn point via `PlanetFactory`, loads it into `SlingshotController`, and updates the UI queue display.

**`PickRandom()`**  
Returns a random `PlanetData` from tier 0 to `maxSpawnTier − 1`.

**`TriggerLose()`**  
If not already in an end state: sets `IsGameOver = true`, `CanShoot = false`, calls `AudioManager.PauseMusic()`, then `UIManager.ShowGameOver()`.

**`TriggerWin()`**  
If not already in an end state: sets `IsWon = true`, `CanShoot = false`, awards 1000 bonus score, calls `AudioManager.PauseMusic()`, then starts `WinSequence()`.

**`WinSequence()` (IEnumerator)**  
Runs `BlackHoleController.PlayWinSequence()` to completion, then calls `UIManager.ShowWin(finalScore)`.

**`RestartGame()`**  
Alias for `StartGame()`. Called by UI buttons.

---

### 5.2 UIManager

**File:** `Assets/Scripts/Managers/UIManager.cs`

Builds the entire HUD and all overlays in code at runtime — nothing needs to be set up in the scene's Canvas hierarchy.

#### Layout Overview

```
Canvas
├── LevelPill   (top-left)   — "LEVEL / 1"
├── ScorePill   (top-left)   — "SCORE / 0"
├── SettingsBtn (top-right)  — ⚙ gear icon
├── SettingsPanel (hidden by default)
│   ├── MusicRow  — "MUSIC" label + ON/OFF toggle button
│   └── SoundRow  — "SOUND" label + ON/OFF toggle button
├── Next        (bottom-left) — next 3 planet queue
│   ├── "NEXT" header
│   └── 3 × (circle dot + name label)
├── GameOver    (full-screen overlay, hidden by default)
│   └── Card → GAME OVER title, subtitle, RETRY button
└── Win         (full-screen overlay, hidden by default)
    └── Card → WIN!, score display, PLAY AGAIN button
```

#### Key Public Methods

| Method | Description |
|---|---|
| `UpdateScore(int score, int best)` | Updates the score pill text |
| `UpdateLevel(int level)` | Updates the level pill text |
| `UpdateQueue(List<PlanetData> upcoming)` | Updates the 3 queue dot colors and planet name labels |
| `HideOverlays()` | Hides Game Over and Win panels (called on restart) |
| `ShowGameOver()` | Makes the Game Over overlay visible |
| `ShowWin(int score)` | Makes the Win overlay visible and fills in the final score |

#### Settings Panel

- The ⚙ button's `Image.raycastTarget` is explicitly set to `true` so the `Button` component receives pointer events.
- The Canvas has a `GraphicRaycaster` component added programmatically if not already present.
- `ToggleMusic()` / `ToggleSound()` call the respective `AudioManager` methods and then call `RefreshSettingsLabels()` to update button colors (cyan = ON, gray = OFF).
- Settings state is persisted to `PlayerPrefs` by `AudioManager`.

#### Procedural Sprite Generation

All panels use a cached 9-sliced rounded-rectangle sprite generated by `RoundedSprite(int cornerPx)`. The sprite is a small texture where pixels inside the rounded rect are white and outside are transparent. Unity's 9-slice stretching then handles any size without distortion.

---

### 5.3 AudioManager

**File:** `Assets/Scripts/Managers/AudioManager.cs`

Auto-created at scene load via `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` — no scene setup needed.

#### Music

Loads `Resources/Audio/Midnight_Geometry` (MP3 placed at `Assets/Resources/Audio/Midnight_Geometry.mp3`). Plays looped at volume 0.45. Does not use `DontDestroyOnLoad` — the `[RuntimeInitializeOnLoadMethod]` recreates it after each scene load if needed.

#### Public Methods

| Method | Description |
|---|---|
| `SetMusic(bool on)` | Enables or disables music, saves to PlayerPrefs |
| `SetSound(bool on)` | Enables or disables SFX, saves to PlayerPrefs |
| `PauseMusic()` | Pauses background music (called on win/lose, does not change user preference) |
| `ResumeMusic()` | Resumes music only if user has it enabled (called on restart) |
| `PlaySFX(AudioClip clip, float volume)` | Plays a one-shot clip at the camera position. Respects `SoundEnabled` |

#### State

| Property | Description |
|---|---|
| `MusicEnabled` | Current music on/off state (read-only, set via `SetMusic`) |
| `SoundEnabled` | Current SFX on/off state (read-only, set via `SetSound`) |

---

### 5.4 ScoreManager

**File:** `Assets/Scripts/Managers/ScoreManager.cs`

Minimal score tracker. Updates UIManager on every change.

| Method | Description |
|---|---|
| `AddScore(int points)` | Adds to `CurrentScore`. Updates `HighScore` and saves to `PlayerPrefs` if exceeded |
| `ResetScore()` | Sets `CurrentScore = 0` and refreshes UI |

Score is awarded on every successful merge (`PlanetData.scoreOnMerge`) plus a 1000-point bonus on win.

---

### 5.5 MergeManager

**File:** `Assets/Scripts/Managers/MergeManager.cs`

Executes merges safely across physics frames.

**Why deferred?** `Planet.OnCollisionEnter2D` fires while Unity is processing physics. Calling `Destroy()` inside a collision callback causes errors. MergeManager queues the request and processes it the next `Update()` after all collision callbacks have completed.

#### Flow

1. `Planet.TryScheduleMerge()` adds both planet IDs to the merge guard set and calls `MergeManager.ScheduleMerge()`.
2. Next `Update()`: `ExecuteMerge()` runs.
3. Both planets are unregistered from GravityManager.
4. Score is added for planet A's tier.
5. Both GameObjects are destroyed.
6. A new planet of `nextTier` is spawned at the midpoint with the average velocity of both planets.
7. `FlashRing()` coroutine plays a brief expanding ring at the merge position.

#### `FlashRing(Vector2 pos, Color color)` (IEnumerator)

A `LineRenderer` ring that expands from radius 0 to 1.4 world units over 0.35 seconds while fading from opaque to transparent. The color matches the merged planet's `primaryColor`.

---

## 6. Utilities

### 6.1 PlanetFactory

**File:** `Assets/Scripts/Utilities/PlanetFactory.cs`

Creates planet GameObjects on demand. Caches one generated sprite per tier so texture generation only happens once per tier per session.

#### `SpawnPlanet(PlanetData data, Vector2 position)`

1. Creates a new `GameObject` named after `data.planetName`
2. Adds `SpriteRenderer` with the cached/generated gradient sprite
3. Adds `CircleCollider2D` (radius 0.5 — visual scale handled by transform)
4. Adds `Rigidbody2D` with `Kinematic` body type, `gravityScale = 0`, `FreezeRotation`
5. Adds `Planet` component and calls `planet.Initialise(data)`
6. Returns the configured `Planet`

#### `GeneratePlanetSprite(Color primary, Color glow, int res)` (static)

Generates a `res × res` RGBA texture for a planet:
- **Radial gradient:** centre = `glow × 1.2`, edge = `primary × 0.35` (dark)
- **Specular highlight:** a soft white circle at the top-left quadrant (mimics a light source)
- **Rim glow:** outer 15% blends in a semi-transparent glow tint
- **Anti-aliased edge:** alpha falls off smoothly across the last pixel

---

### 6.2 BackgroundManager

**File:** `Assets/Scripts/Utilities/BackgroundManager.cs`

Generates the deep-space background at runtime using a vertex-colored mesh (no textures).

#### Background Mesh

A 60×40 world-unit quad with 4 vertices. Per-vertex colors create a diagonal gradient:
- Top-left: `#0D0820` (deep indigo)
- Top-right: blended toward near-black
- Bottom-right: `#020005` (near-black)
- Bottom-left: blended indigo-purple

Sort order −100 (behind every other sprite).

#### Star Field

A secondary mesh of 180 small quads (each a "star") scattered randomly across the background. Stars vary in:
- **Size:** 0.02–0.10 world units
- **Brightness:** 40–100% white
- **Opacity:** 50–100%

Both meshes use a Sprite-Unlit shader to bypass URP's Global Light 2D tinting (which would otherwise wash out the dark space look).

---

## 7. Game Rules & Win / Lose Conditions

### Win Condition
Two **Saturn** planets (tier 6) collide → `GameManager.TriggerWin()`:
1. Music pauses
2. `BlackHoleController.PlayWinSequence()` runs — black hole grows, sucks in all planets
3. Win overlay appears with final score

### Lose Condition
A planet **settles** (slows below `settleSpeedThreshold` for `settleRequiredTime` seconds) with any part of its body **outside** the safe zone (i.e., `distance_from_center + planet.radius > zoneRadius`):
1. Music pauses
2. Game Over overlay appears

### Warning System (not a direct lose trigger)
When a planet collision happens and either planet's edge is past the warning ring (`WarningRingRadius = zoneRadius × 0.88`), a red pulse wave plays inside the zone as a visual alert.

---

## 8. Planet Tier Chain

```
Mercury (1) ──merge──► Venus (2) ──merge──► Earth (3)
                                                │
                                             merge
                                                ▼
                                          Mars (4)
                                                │
                                             merge
                                                ▼
                                         Jupiter (5)
                                                │
                                             merge
                                                ▼
                                          Saturn (6)
                                                │
                                       merge ── WIN!
```

Each merge:
- Destroys both source planets
- Spawns one planet of the next tier at their midpoint
- Awards `scoreOnMerge` points for the consumed tier
- Plays `FlashRing` burst at the merge point

---

## 9. Physics System

### Gravity
`F = gravityConstant / distance` (inverse-linear, not inverse-square).

Applied in `GravityManager.FixedUpdate()` to every registered (launched) planet.
Force increases as planets get closer to the center, but not as sharply as real gravity.

### Collision Detection
All planets use `CollisionDetectionMode2D.Continuous` to prevent fast-moving planets from tunneling through the black hole or each other.

### Planet Stacking
The black hole has a solid (non-trigger) `CircleCollider2D` on its `BlackHoleBody` child. Planets physically stack on top of it and each other.

### Merge Guard
`static HashSet<int> s_mergingIds` prevents the same planet from being merged twice. Checked and written atomically using `lock(s_mergeLock)` in `TryScheduleMerge` and `TryTriggerWin`.

### Physics Materials
Each planet gets a `PhysicsMaterial2D` with `bounciness` from its `PlanetData` and friction `0.25`. The black hole body gets `bounciness = 0.2` and `friction = 0.4`.

---

## 10. Visual Systems

### Rendering Order (sortingOrder)

| Value | Element |
|---|---|
| −100 | Background mesh |
| −99 | Stars |
| −50 | Gravity gradient mesh |
| 0 | Radial field lines, Halo |
| 1 | Gravity particles |
| 2 | Planet sprites |
| 4 | Black hole Core |
| 4 | Warning ring dashes |
| 5 | Outer boundary dashes, EdgeRing |
| 6 | Warning pulse waves |
| 10 | Trajectory dots, Spawn circle, Pull line |

### Procedural Texture Generation

Every sprite in the project is generated via `Texture2D.SetPixel()`:
- **Planet sprites** — radial gradient with specular + rim glow (128×128)
- **Black hole Core** — void sphere with blue-violet depth tint + edge gradient (256×256)
- **Black hole Halo** — dark purple radial glow (256×256)
- **Black hole EdgeRing** — thin bright annulus (128×128)
- **Gravity particle dots** — small soft circle (16×16)
- **Trajectory dots** — anti-aliased circle (32×32)
- **UI rounded rectangles** — 9-sliceable rounded rect (variable, corner×3 pixels)

All sprites are cached after first generation so repeated calls are free.

---

## 11. Audio System

| State | Music | SFX |
|---|---|---|
| Game running | Playing (loop) | Enabled |
| Win triggered | Paused | Enabled |
| Lose triggered | Paused | Enabled |
| Restart | Resumed (if user has it on) | Enabled |
| User toggles OFF | Paused | Disabled |

SFX playback uses `AudioSource.PlayClipAtPoint()` at the camera position. Currently no SFX clips are loaded by default — the infrastructure is ready for additional clips.

---

## 12. UI System

### Color Palette (UIManager Inspector fields)

| Field | Default Color | Usage |
|---|---|---|
| `bgPanel` | dark purple 90% | Large overlay and queue panel background |
| `bgPill` | medium purple 93% | Score/Level/Settings pill background |
| `borderDim` | white 10% | Subtle border on queue panel |
| `borderGlow` | purple 35% | Glowing border on score/level pills |
| `btnBg` | dark violet | Button background |
| `textBright` | white 95% | Primary text |
| `textDim` | white 40% | Secondary / label text |
| `colorCyan` | #04D2F2 | Accent — NEXT label, Win panel border, score text, ON state |
| `colorAmber` | #FAC226 | Win title text |
| `colorRed` | #F24747 | Game Over border, title, button glow |

### CanvasScaler Settings (set programmatically)

| Setting | Value |
|---|---|
| Scale Mode | ScaleWithScreenSize |
| Reference Resolution | 1080 × 1920 |
| Match Width or Height | 0.5 (balanced) |

---

## 13. Inspector Tuning Reference

### Making the game easier
- Increase `GravityManager.zoneRadius` — bigger safe zone
- Decrease `GravityManager.gravityConstant` — weaker pull, planets move more slowly
- Increase `GravityManager.settleRequiredTime` — more time before loss triggers
- Increase `GameManager.maxSpawnTier` — harder planets (higher mass) appear in queue

### Making the game harder
- Decrease `GravityManager.zoneRadius`
- Increase `GravityManager.gravityConstant`
- Decrease `GravityManager.settleRequiredTime`
- Decrease `GameManager.maxSpawnTier` (e.g. 1 = Mercury only in queue)

### Adjusting feel of shooting
- `SlingshotController.forceMultiplier` — how fast the planet moves per unit of drag (default 5)
- `SlingshotController.maxLaunchSpeed` — absolute velocity cap (default 9)
- `SlingshotController.maxDragRadius` — how far the player can pull back (default 3)

### Adding a new planet tier
1. Create a new `PlanetData` ScriptableObject (`Assets > Create > PlanetMerge > Planet Data`)
2. Fill in all fields. Set `nextTier` to the planet above it in the chain.
3. On the previous highest tier's asset, set its `nextTier` to this new asset.
4. On the new top-tier asset, leave `nextTier` as `None` (collision → win).
5. Add the new asset to `GameManager.allPlanetData` array in the Inspector.
6. Increase `GameManager.preQueueSize` / `maxSpawnTier` as desired.

---

## 14. PlayerPrefs Keys

| Key | Type | Description |
|---|---|---|
| `PlanetMerge_Music` | int (0/1) | Music enabled preference |
| `PlanetMerge_Sound` | int (0/1) | Sound enabled preference |
| `PlanetMerge_HighScore` | int | All-time high score |

All keys are namespaced with `PlanetMerge_` to avoid collision with other projects on the same device.

---

*Documentation generated from source — April 2026.*
