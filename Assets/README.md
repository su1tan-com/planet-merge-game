# PlanetMerge — Code Map

Quick reference for finding what to change and where. All visuals are procedurally generated at runtime — no external sprite assets needed.

---

## File Structure

```
Assets/Scripts/
├── Data/
│   └── PlanetData.cs           ScriptableObject — one per planet tier
├── Controllers/
│   ├── Planet.cs               Per-planet component (physics, launch, merge detection)
│   ├── SlingshotController.cs  Mouse input → drag → fire
│   ├── TrajectoryRenderer.cs   Dotted arc preview during drag
│   ├── BlackHoleController.cs  Black hole visuals + physics surface
│   ├── GravityManager.cs       Radial gravity + safe-zone boundary + lose detection
│   └── GravityZoneVisual.cs    Animated effects on top of GravityManager
├── Managers/
│   ├── GameManager.cs          Game loop (queue, launch, win/lose, restart)
│   ├── MergeManager.cs         Executes merges safely between physics frames
│   ├── ScoreManager.cs         Score tracking + high score (PlayerPrefs)
│   └── UIManager.cs            All on-screen UI built in code
└── Utilities/
    ├── PlanetFactory.cs        Spawns planet GameObjects + generates sprites
    └── BackgroundManager.cs    Deep-space gradient background + star field
```

---

## Class Responsibilities

### `PlanetData` — `Data/PlanetData.cs`
ScriptableObject asset. One file per tier (Mercury → Saturn) in `Assets/Data/Planets/`.

| Want to change | Field |
|---|---|
| Planet name | `planetName` |
| Tier order (1=smallest) | `tier` |
| Color in game | `primaryColor`, `glowColor` |
| Physical size | `radius` |
| How heavy it is | `mass` |
| How fast it slows down | `drag`, `angularDrag` |
| How bouncy | `bounciness` |
| Points on merge | `scoreOnMerge` |
| What it merges into | `nextTier` (drag another PlanetData asset here) |

> Saturn's `nextTier` is **null** — that's how the win condition is detected.

---

### `Planet` — `Controllers/Planet.cs`
Added automatically to every planet GameObject by `PlanetFactory`. Handles per-planet state.

| Want to change | Where |
|---|---|
| Launch physics (velocity, freeze) | `Launch()`, `Freeze()`, `Unfreeze()` |
| Merge detection logic | `OnCollisionEnter2D()` → `TryScheduleMerge()` |
| Win detection | `TryTriggerWin()` |
| Prevent double-merge bugs | `s_mergeLock` + `s_mergingIds` HashSet |

**Key properties read by other classes:** `IsLaunched`, `IsMerging`, `data`, `Rb`

---

### `GameManager` — `Managers/GameManager.cs`
Singleton. Central game loop controller.

| Want to change | Where |
|---|---|
| How many planets in the queue | `preQueueSize` (Inspector) |
| Max tier that can spawn randomly | `maxSpawnTier` (Inspector) |
| Delay before next planet loads | `nextPlanetDelay` (Inspector) |
| What planets exist in the game | `allPlanetData[]` (Inspector) |
| Restart logic | `StartGame()` |
| Win/lose triggers | `TriggerWin()`, `TriggerLose()` |

---

### `MergeManager` — `Managers/MergeManager.cs`
Singleton. Deferred merge executor — runs one frame after collision so Unity physics callbacks finish cleanly.

| Want to change | Where |
|---|---|
| Merge VFX (ring burst) | `FlashRing()` coroutine |
| Ring size / duration | `maxRadius`, `duration` constants inside `FlashRing()` |
| Score awarded on merge | Change in `PlanetData.scoreOnMerge` (not here) |

---

### `ScoreManager` — `Managers/ScoreManager.cs`
Singleton. Adds points, tracks high score in `PlayerPrefs`.

| Want to change | Where |
|---|---|
| Win bonus points | `GameManager.TriggerWin()` — calls `AddScore(1000)` |
| Points per merge | `PlanetData.scoreOnMerge` on each asset |
| High score key (save slot) | `HighScoreKey` constant |

---

### `SlingshotController` — `Controllers/SlingshotController.cs`
Singleton. Handles all mouse input for drag-and-fire.

| Want to change | Where |
|---|---|
| How hard planets launch | `forceMultiplier` (Inspector) |
| Max launch speed cap | `maxLaunchSpeed` (Inspector) |
| How far you can drag | `maxDragRadius` (Inspector) |
| Click detection radius | `dist <= _currentPlanet.data.radius * 3f` in `HandleInput()` |
| Where planets spawn | `spawnPoint` Transform (Inspector) |

---

### `TrajectoryRenderer` — `Controllers/TrajectoryRenderer.cs`
Attached alongside `SlingshotController`. Draws dotted arc preview during drag.

| Want to change | Where |
|---|---|
| Number of dots | `dotCount` in `Awake()` |
| How long the arc is | `stepsPerDot` in `Awake()` — higher = longer arc |
| Dot size | `dotRadius` in `Awake()` |
| Dot color | `dotColor` (Inspector) |
| Simulation accuracy | `timeStep` (Inspector) |

> **Note:** `dotCount`, `stepsPerDot`, `dotRadius` are forced in `Awake()` to prevent Unity's Inspector cache from overriding them. Change them in code, not the Inspector.

---

### `BlackHoleController` — `Controllers/BlackHoleController.cs`
Singleton. Builds the black hole visuals and the solid collision surface.

| Want to change | Where |
|---|---|
| Black hole size | `coreRadius` (Inspector) |
| Edge ring color | `EdgeBase`, `EdgeShimmer` static fields |
| Edge shimmer speed | `shimmerSpeed` (Inspector) |
| Halo appearance | `BuildHalo()` |
| Core sphere look | `BuildCore()` |
| Win suck-in animation | `PlayWinSequence()` — `winDuration`, `winMaxScale`, `winSuckForce` |
| Collision surface size | `coreRadius` (same field — collision = visual) |

---

### `GravityManager` — `Controllers/GravityManager.cs`
Singleton. Applies radial gravity every `FixedUpdate` to all launched planets. Also detects the lose condition.

| Want to change | Where |
|---|---|
| Gravity strength | `gravityConstant` (Inspector) |
| Safe zone radius (dotted circle) | `zoneRadius` (Inspector) |
| How long before "settled" = lose | `settleRequiredTime` (Inspector) |
| Speed threshold for "settling" | `settleSpeedThreshold` (Inspector) |
| Dotted boundary color | `boundaryColor` (Inspector) |
| Dot density on boundary | `boundarySegments` (Inspector) — higher = more dots |
| Dot size | `boundaryLineWidth` (Inspector) |
| Dot vs gap ratio | `dashFraction` (Inspector) — lower = smaller dots |

---

### `GravityZoneVisual` — `Controllers/GravityZoneVisual.cs`
Attached to the BlackHole GameObject. Purely cosmetic — animates the boundary and adds ambient effects.

| Want to change | Where |
|---|---|
| Boundary pulse speed/brightness | `pulseSpeed`, `pulseMinAlpha`, `pulseMaxAlpha` (Inspector) |
| Screen-wide dark vignette | `showGravityGradient`, `gradientRadius`, `gradientEdgeAlpha` (Inspector) |
| Space dust particles | `particleCount`, `spawnRadius`, `driftSpeed`, `particleColor` (Inspector) |
| Particle size | `particleSize` (Inspector) |
| Radial field lines | `showFieldLines`, `fieldLineCount`, `fieldLineColor` (Inspector) |

---

### `UIManager` — `Managers/UIManager.cs`
Singleton. Builds all UI in code — no prefabs or scene objects needed.

| Want to change | Where |
|---|---|
| Panel/pill background colors | `bgPanel`, `bgPill` public fields (Inspector) |
| Border colors | `borderDim`, `borderGlow` (Inspector) |
| Button color | `btnBg` (Inspector) |
| Text colors | `textBright`, `colorCyan`, `colorAmber`, `colorRed` (Inspector) |
| Top pill height / gap | `pillHeight`, `pillGapTop` (Inspector) |
| Next queue pill position | `nextQueueBottom` (Inspector) |
| Game Over card layout | `GameOverPanel()` method |
| Win card layout | `WinPanel()` method |
| Score display | `UpdateScore()` method |
| Queue display | `UpdateQueue()` method |

---

### `PlanetFactory` — `Utilities/PlanetFactory.cs`
Singleton. Creates planet GameObjects at runtime and generates their sprites procedurally.

| Want to change | Where |
|---|---|
| Sprite texture resolution | `spriteResolution` (Inspector) |
| Planet gradient look | `GeneratePlanetSprite()` — radial gradient algorithm |
| Specular highlight shape | `specPos`, `specRad` inside `GeneratePlanetSprite()` |
| Collider size | `col.radius = 0.5f` in `SpawnPlanet()` |
| Physics defaults | `rb.*` settings in `SpawnPlanet()` |

---

### `BackgroundManager` — `Utilities/BackgroundManager.cs`
Builds the background gradient mesh and star field at startup.

| Want to change | Where |
|---|---|
| Background colors | `ColorTopLeft`, `ColorCentre`, `ColorBottomRight` static fields |
| Star count | `starCount` in `BuildStars()` |
| Star size range | `Random.Range(0.02f, 0.10f)` in `BuildStars()` |
| Background depth (sorting) | `sortingOrder` (Inspector) |

---

## Common Tasks

**Change how a planet looks** → open its `.asset` file in `Assets/Data/Planets/`, edit `primaryColor` / `glowColor` / `radius`

**Change gravity feel** → `GravityManager` Inspector: `gravityConstant`, `zoneRadius`

**Change launch feel** → `SlingshotController` Inspector: `forceMultiplier`, `maxLaunchSpeed`, `maxDragRadius`

**Change trajectory arc** → Edit `dotCount` / `stepsPerDot` in `TrajectoryRenderer.Awake()`

**Add a new planet tier** → Create a new `PlanetData` asset, set `tier`, colors, `nextTier`, then add it to `GameManager.allPlanetData[]`

**Change UI colors live** → Select the Manager GameObject in the Hierarchy → find `UIManager` component → tweak color fields in Inspector
