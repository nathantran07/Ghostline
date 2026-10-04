**TASK SUMMARY:** Raise straight-line speed, soften braking, widen Suzuka to 3.4, verify Continuous collisions, and invalidate prior records.

**FILES MODIFIED:**

| File | Changed lines / region |
|---|---|
| `Assets/Ghostline/Game/CarController.cs` | 14, 23-25 |
| `Assets/Ghostline/Tests/Scene/CarDrivingTests.cs` | 4, 66, 69, 95-96, 99-104, 127-130, 139, 149-229 |
| `Assets/Ghostline/Tests/Scene/CarSpriteTests.cs` | 197 |
| `Assets/Ghostline/Tests/Scene/CountdownAndDeltaTests.cs` | 88 |
| `Assets/Ghostline/Game/TrackGenerator.cs` | 14, 82 |
| `Assets/Ghostline/Tests/Scene/TrackGeneratorTests.cs` | 28, 312-314, 323 |
| `Assets/Ghostline/Editor/GhostlineSceneBuilder.cs` | 156-157 |
| `Assets/Scenes/Main.unity` | 1-10873 (regenerated scene); width 5439-5454, car fields 9689-9756, Continuous 9793, spawn 10169-10170 |
| `Assets/Ghostline/Core/BestLapData.cs` | 10 |
| `Assets/Ghostline/Tests/EditMode/BestLapRepositoryTests.cs` | 101-102 |
| `Assets/Ghostline/Tests/EditMode/Storage/JsonFileBestLapStorageTests.cs` | 30, 37, 52-53, 58, 70, 107, 113, 126 |
| `Assets/Ghostline/Tests/Scene/RaceConfigurationTests.cs` | 161 |
| `README.md` | 45, 92, 108, 150 |
| `SCENE_SETUP.md` | 20, 28, 71, 73, 91 |
| `docs/straight-speed-tuning-handoff.md` | 1-65 (new handoff) |

**IMPLEMENTATION:**

- `CarController`: Top Speed reference 18, Drag 0.8, Brake Acceleration 6, Engine Braking 1.5. Acceleration 18, throttle ramp 0.4, steering curves, reverse threshold, and wall-loss fraction are unchanged. Existing Core curve/smoothing/force math is reused; no new Core algorithm or dependency. Runtime `Awake`, the scene builder, and the saved Rigidbody use Continuous detection, zero Rigidbody linear damping, and Interpolate.
- `TrackGenerator.Configure` and serialized road width default to 3.4. `Regenerate` rebuilds road/walls, gates, decorations, and spawn from the same safe local width profile. Radius clamps, smoothing, non-adjacent intersection handling, and crossover corridor gaps remain active. Camera orthographic size remains 9.
- `BestLapData.CurrentVersion` is 5; track ID remains `suzuka`. Version-4 saves load as no best and no ghost, without errors. No save file deletion or migration is needed. Storage tests use the current schema when testing invalid splits, avoiding false passes from stale-version rejection.
- `CarDrivingTests`: terminal force balance, gradual braking, near-rest reverse, acceleration above the curve reference, actual thin-wall collision at 18 units/s, and real `CheckpointTrigger` callbacks at 18 units/s across four fixed-step phases. `CarSpriteTests.AssertCars` checks saved Continuous mode. `TrackGeneratorTests` checks 3.4 and 8.0 wall intersections and retains crossover-clearance and gate-alignment coverage.

Width reductions at requested 3.4 (arc length measured from spline start, in world units):

| Authored section | Arc-length interval | Minimum width | Nearest knot (zero-based) |
|---|---:|---:|---:|
| Corners 8?9 | 307.21?308.98 | 2.81 | 34 |
| Corners 10?11, hairpin | 362.71?367.31 | 2.08 | 41 |
| Corners 16?18, chicane | 672.75?674.52 | 2.82 | 70 |
| Corners 16?18, chicane | 679.83?680.89 | 3.19 | 72 |
| Return to start | 714.12?719.42 | 1.87 | 78 |

These labels follow `SuzukaNormalizedKnots` authoring comments; exact intervals locate each reduction. Crossover passes occur at 317.25 / 615.13 and retain open wall gaps. The narrowest turns cannot accept the full requested width without changing the centerline. Console output reports all intervals after regeneration.

**VERIFICATION:**

- Full Unity EditMode suite: **171 passed, 0 failed/skipped; exit 0** (`followup-confirmed.xml`).
- Exact saved scene: **11 passed, 0 failed/skipped; exit 0** (`followup-exact-scene.xml`), including 3.4/8.0 wall intersections, crossover clearance, gate alignment, and saved Continuous mode. Scene SHA256 stayed `3d845511b4734af158c76615d56e96bb71d5d16d7de31f92c3baf90d8875d46d`.
- Standalone Core: **108 passed, 0 failed/skipped; exit 0**. Command: `dotnet test 'X:/GhostlineVerification/CoreTests/CoreTests.csproj' --no-restore --verbosity quiet`.
- All 52 C# / assembly files match the verified source copy. `verify_preservation.py` confirms all unrelated saved-scene blocks remain unchanged; eight approved component/pose blocks changed outside regenerated geometry. Start and corner render previews were inspected.

Exact Unity commands (PowerShell; use the external verification copy):

```powershell
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
$verifyRoot = 'X:/GhostlineVerification/GameplayTuning-20261003'
Start-Process -FilePath $unity -WindowStyle Hidden -Wait -PassThru -ArgumentList @('-batchmode','-runTests','-testPlatform','EditMode','-projectPath',('"'+$verifyRoot+'"'),'-testResults',('"'+$verifyRoot+'/followup-confirmed.xml"'),'-logFile',('"'+$verifyRoot+'/followup-confirmed.log"'))
# Restore the preserved Main.final.unity snapshot before this scene-only run:
Start-Process -FilePath $unity -WindowStyle Hidden -Wait -PassThru -ArgumentList @('-batchmode','-runTests','-testPlatform','EditMode','-testFilter','Ghostline.Tests.Scene.TrackGeneratorTests;Ghostline.Tests.Scene.CarSpriteTests.SavedSceneCarsAlignWithMovementAndKeepTheirPhysics','-projectPath',('"'+$verifyRoot+'"'),'-testResults',('"'+$verifyRoot+'/followup-exact-scene.xml"'),'-logFile',('"'+$verifyRoot+'/followup-exact-scene.log"'))
```

All Unity verification uses `X:\GhostlineVerification\GameplayTuning-20261003`, copied from Assets/Packages/ProjectSettings only. Workspace Library/, Temp/, and UserSettings/ are untouched. The initial regression failed at the old terminal speed (11.99998 versus expected 18), and the version-4 rejection case fails against schema 4. Evidence copies are under workspace `Logs/StraightSpeed-*` (ignored). `git diff --cached --check` passed before each commit.

Commits: `8790dd4` schema compatibility; `88b4525` speed/braking and collision regression tests; `439ca6d` track width and regenerated scene. This handoff and setup documentation are committed separately.

**OPEN ITEMS:** Human Play-mode handling review remains. Continuous collision detection does not by itself establish trigger reliability; tests exercise the actual gate callback at the approved speed, existing 0.02-second physics step, saved car dimensions, and 0.25-thick trigger. More extreme Inspector speeds/physics timesteps are outside the verified range. The local road reductions listed above are intentional.

Tune first: **Top Speed reference / Drag** together (18 / 0.8), **Brake Acceleration** (6), **Engine Braking** (1.5), **Road Width** (3.4), then acceleration and steering curves if needed. Runtime `Awake` enforces Continuous detection.
