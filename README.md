# Ghostline

A small top-down Unity 2D time-trial game built as a software engineering portfolio project. Drive a spline-generated Suzuka figure-8 circuit with grass, asphalt edge paint, striped curbs, barriers, tire walls, grid boxes, and a checkered start/finish. Pass twelve checkpoints in order. A faster completed lap is saved locally and becomes a translucent replay car on later attempts and later runs.

![Ghostline gameplay showing the rectangular track, player car, saved ghost, and lap HUD](docs/images/ghostline-gameplay.png)

*This preview predates the Suzuka track and Lambo sprite changes. Normal play follows the car.*

## Open and play

Use **Unity 6000.6.4f1**, the version recorded in `ProjectSettings/ProjectVersion.txt`, and **Git LFS** for the template/TMP binary assets. The manifest includes Unity Splines 2.8.4, resolved automatically by Package Manager. No additional artwork, input action assets, shaders, or lights are needed.

```shell
git lfs install
git clone https://github.com/nathantran07/Ghostline.git
cd Ghostline
git lfs pull
```

1. In Unity Hub, choose **Add > Add project from disk** and select the cloned **Ghostline** folder. Open it with the recorded Editor version, wait for package resolution/compilation, and check the Console for errors.
2. Double-click **Assets/Scenes/Main.unity** in the Project window. The playable scene and TMP Essential Resources are included in the repository; rebuilding or importing them again is unnecessary for a normal first run.
3. Verify **Edit > Project Settings > Player > Other Settings > Configuration > Active Input Handling** is **Input System Package (New)** or **Both**. The manifest includes `com.unity.inputsystem`; the supplied project is already set to New.
4. Press **Play**, click the **Game** view to give it keyboard focus, and drive across the white line to start.

For rebuilding and tuning the circuit, use [SCENE_SETUP.md](SCENE_SETUP.md). That guide covers TMP resources, normalized knots, geometry settings, and material checks. Rebuilding replaces saved scene edits after confirmation.

## Controls and race rules

| Key | Action |
| --- | --- |
| W / Up | Accelerate forward |
| S / Down | Brake while moving forward, then reverse |
| A / Left | Steer left |
| D / Right | Steer right |
| R | Reset the car and current attempt, restart the countdown; preserve the saved best |

The yellow Lambo starts behind the upper-right start line, facing down-right into corner 1. Follow corners 1 through 18 and continue straight through the flat crossover on each visit. Release acceleration or briefly brake before corners. Steering requires movement and reverses naturally while backing up.

On scene start and every R reset, a centered countdown shows **3, 2, 1** for one second each, then **GO** for 0.75 seconds. The car stays still with throttle and steering disabled until GO. Countdown timing does not start the lap clock: the timer stays at zero until the first forward start crossing from the spawn position behind the line.

Checkpoints are numbered 1 through 12 by default in the HUD (indices 0 through 11 in code). Duplicate, skipped, out-of-order, and backward gate entries do not advance progress. Both arcs connecting the crossover passes contain required gates, so turning at the intersection cannot complete a lap. Each accepted gate records its lap-clock split. After all configured checkpoints, the next forward finish crossing completes the attempt. The timer freezes and driving stops until R; this game runs one attempt at a time.

With a saved best, the delta beside the lap time updates at each accepted gate and at the finish: current lap-clock time minus the corresponding best split or duration. Negative values are green (ahead), positive values red (behind), and zero white; all show a sign and three decimals, such as **-0.142** or **+0.312**. Each value remains solid for two seconds, fades over the third, and is replaced by the next crossing or cleared on reset. The finish compares against the previous best before a faster lap replaces it. No saved best means no delta, including the first finish.

Save format **6** stores gameplay version and track ID (`suzuka`) with checkpoint splits. Missing or mismatched metadata discards the saved lap and ghost quietly, including format-5 records. A new valid lap can replace an incompatible record regardless of its old time.

The ghost is hidden before the start and when there is no valid best lap. It replays the previous best on the current lap clock and remains at its final pose if the current attempt takes longer. A new best becomes the replay on the next reset. Lap timing and trigger detection use Unity's fixed physics steps; they do not estimate sub-step crossing times.

## Structure

```text
Assets/Ghostline/
  Core/             Ghostline.Core.asmdef; pure C# race rules and recording
  Game/             Ghostline.Game.asmdef; Unity, keyboard, presentation, file adapters
  Tests/EditMode/   Ghostline.Tests.EditMode.asmdef; NUnit tests referencing Core
    Storage/        Ghostline.Tests.Storage.asmdef; Unity JSON adapter tests
  Tests/Scene/      Ghostline.Tests.Scene.asmdef; Editor-only Game/tooling tests
  Editor/           Ghostline.Editor.asmdef; scene builder and additive minimap installer
```

Core and its EditMode test assembly have `noEngineReferences: true`. Game references Core, `Unity.InputSystem`, `Unity.TextMeshPro`, and `UnityEngine.UI`. The nested Storage test assembly references Core and Game so JSON adapter tests can use Unity without adding engine references to Core tests. Tests use the template's Test Framework through `TestAssemblies`; they are Editor-only. Editor references Core, Game, TMP, and UI, and is excluded from players.

### Core classes

**LapTimer** owns `NotStarted`, `Running`, and `Finished`, accumulates supplied seconds only while running, and raises start, finish, and reset events. It rejects negative or nonfinite tick values.

**CheckpointTracker** accepts only the next zero-based checkpoint. A separate finish call completes an ordered lap once; reset clears progress. Checkpoint entry alone never finishes a lap.

**GhostSample** is a readonly struct containing lap-relative time, X, Y, and Z rotation in degrees. Its constructor rejects nonfinite poses and negative timestamps.

**GhostRecording** copies a nonempty, strictly time-ordered sequence into read-only storage. `Evaluate` uses binary search and linear position interpolation with shortest-path angular interpolation, clamping outside the recorded range.

**GhostRecorder** resamples supplied poses at a fixed interval, defaulting to 0.05 seconds. It interpolates across uneven updates, records time zero, includes the exact finish pose, and prevents duplicate endpoint timestamps.

**BestLapData** is a serializable mutable transport object holding duration, samples, and a `float[] Splits` array in checkpoint order. Splits exclude the finish, which is stored in `LapTime`.

**IBestLapStorage** defines loading and saving independently of disk, Unity, or serialization. A missing or unreadable save returns null; save failures can be reported by the application adapter.

**BestLapRepository** validates ordered start/finish samples and positive duration, returns defensive copies of samples and splits, and saves only the first or strictly faster valid lap. Splits must match the configured checkpoint count, be finite, nonnegative, non-decreasing, and no greater than the duration. Equal durations do not replace the best.

**RaceSession** coordinates the timer, checkpoints, recorder, and read-only accepted split times for a single attempt. Its start/finish, checkpoint, tick, and reset methods keep race rules out of MonoBehaviours. Reset clears splits; a valid finish copies them into completed lap data.

**DeltaCalculator** exposes `AtCheckpoint` and `AtFinish`, returning current minus best seconds, or null without a saved best. It does not change the best lap.

**StartSequence** advances `Counting`, `Go`, and `Done` using supplied ticks, exposes the current label and `DrivingAllowed`, and handles ticks spanning multiple states. Number and GO durations are constructor parameters; reset restarts at 3. It is independent of the lap timer.

**MinimapProjection**, in `Assets/Ghostline/Core/MinimapProjection.cs`, maps `TrackPoint` world bounds into a padded rectangle. `Project` preserves aspect ratio, centers unused space, and returns top-left coordinates with Y flipped; `ProjectClamped` limits positions to the padded rectangle. Point bounds map to the center; a single nonzero axis still scales normally. Invalid dimensions, inverted bounds, nonfinite values, and padding that consumes the rectangle are rejected. Core retains `noEngineReferences: true`.

**NumericGuard** is an internal helper shared by Core classes to enforce finite and nonnegative numeric inputs without depending on Unity's math API.

### Game classes

**SolidSprite** uses `[ExecuteAlways]` to create a rectangle from `Texture2D.whiteTexture`. It applies Inspector color, world-unit size, and sorting order, preserves the SpriteRenderer's material, recreates sprites after scene loading, and releases each generated sprite when disabled. Size controls the object's local scale; use unit-sized BoxCollider2D components.

**CarController** ramps throttle over 0.4 seconds and applies acceleration and proportional drag as Rigidbody2D forces. Terminal speed comes from force/drag balance; Top Speed is the curve reference. Defaults balance near 17.1 units/s with Drag 0.85, Brake Acceleration 6, and Engine Braking 1.5. The car uses Continuous collision detection. Inspector curves control acceleration and steering with speed. Coasting adds engine braking; S overrides W to brake and reverses near rest. Wall contacts remove the configured incoming-speed fraction. Reset clears input and motion; countdown and finish locking remain intact. Curve evaluation and handling math live in engine-free Core.

**CameraFollow** smoothly follows the car on the XY plane while keeping camera Z at -10. Reset can snap the camera directly to the spawn position.

**CheckpointTrigger** translates a car's trigger entry into a race command. It filters by CarController and Rigidbody2D, accepts movement only along the configured forward direction, and requires no custom tags or layers.

**RaceManager** creates and connects Core objects, supplies the same fixed-step seconds to the countdown and race, responds to restart input, and coordinates saved laps with the HUD and ghost. It locks input until GO, forwards accepted gate deltas, and computes the final delta before saving a new best. Storage failures produce a Console warning and HUD status while leaving the completed attempt visible.

**GhostCarView** displays an interpolated saved pose on the current attempt's clock. It uses a translucent SolidSprite and has no collider or rigidbody, so playback cannot affect driving.

**HudView** renders current time, best time, attempt/checkpoint status, countdown, and delta into five assigned TMP text components. `RenderCountdown`, `ShowDelta`, and `Tick` handle label visibility, invariant signed decimal formatting, colors, and fade timing. It formats presentation but does not decide race outcomes.

**MinimapView**, in `Assets/Ghostline/Game/MinimapView.cs`, reads `TrackGenerator.Samples` at startup, transforms the centerline into world space, and draws a closed line and start/finish tick once into a runtime Texture2D. A top-right panel uses 30% of the scaled canvas height (1.5 times the original side length). Player and ghost dots update in `LateUpdate` using the same Core projection; resizing scales their coordinates with the retained texture. The yellow player has a dark outline; the blue ghost uses 50% alpha. With a saved recording, the ghost dot stays at the configured spawn through countdown and the approach to the start, follows playback once the lap starts, and hides when the recording or attempt ends. Without a recording it stays hidden. The world ghost's existing visibility and movement are unchanged. All dimensions and colors are serialized; the minimap uses default UI sprites/materials and releases its generated texture on destruction.

**JsonFileBestLapStorage** is a plain C# Unity adapter implementing `IBestLapStorage`, rather than a component to attach to an object. It maps readonly Core samples to private mutable JSON DTOs, validates a versioned file, writes through a temporary file, and falls back to no ghost for missing, malformed, incompatible, or unreadable data.

### Editor class

**TrackGenerator** builds the road, walls, and gates from a shared width profile. Road Width defaults to 3.4 units. Core clamps and smooths corner offsets and detects non-adjacent wall intersections; the Console reports limited intervals and crossover clearance. Crossover walls stay open. Gates, grid positions, and road decorations follow effective local width. Use the component's **Regenerate Track** context menu after Inspector changes; changed width settings also regenerate physics and gates on reload.

**TrackVisuals** builds static, combined unlit meshes from the same samples and crossover rule. Mowed grass alternates two serialized greens in wide horizontal bands anchored to the track's local origin, in one mesh with the existing ground bounds. White edge lines stay on the asphalt; red/white curbs follow the inside of qualifying corners, while metal barriers and tighter-corner white/red tire walls sit outside the existing wall colliders. Runs split at crossover gaps and drop below minimum length. The grid uses two staggered columns, checks complete footprints against the road and crossover, and puts the player in the front slot. The current short approach fits one slot and warns; up to ten are painted on a long enough straight. Pink, yellow, and blue sector lines use the start and existing gates nearest corner 8 (knot 30) and corner 15 (knot 65), with no new race logic. The checker replaces the start sprite while preserving its trigger.

The Inspector exposes Road Color on TrackGenerator and colors, grass margin, edge/curb/barrier/tire widths, curvature thresholds, minimum run lengths, stripe/block lengths, checker depth, grid dimensions/spacing/straightness, and sector colors/anchors on TrackVisuals. Show Grass, Show Edge Lines, and Show Curbs independently toggle these categories. Defaults include 8-unit grass bands, 0.15-unit edge lines, 0.5-unit curbs, 0.4-unit barriers, 0.8-unit tire walls, and a 1-unit-deep checker. Grass bands add no renderers, material assets, or colliders. See [SCENE_SETUP.md](SCENE_SETUP.md) for every default and the sorting order. Restart Play to review visual setting changes in an existing scene; **Tools > Ghostline > Build Scene** replaces the scene and regenerates all geometry and front-slot spawn wiring.

**DecorGenerator**, in `Assets/Ghostline/Game/DecorGenerator.cs`, adds four static combined meshes: green/blossom canopy groves, gray finish-area grandstands with crowd dots facing the track, dark circular tire stacks with lighter inner rings on red/white pair pads at selected corner exits, and evenly spaced striped color blocks along qualifying straights. It reuses the road's unlit material, uses opaque vertex colors and 32-bit indices, and sorts at **1**, below the ghost/car. `DecorPlacementGeometry` checks conservative whole footprints against every actual road/wall triangle, wall-collider segments and rounded ends, and other decor. It reads the generator's original surface meshes so static batching cannot turn grass or other decor into obstacles. This includes both crossover passes and locally narrowed corners. `DecorMeshBuilder` keeps all fills, shadows, dots and stripes inside those footprints.

Placement uses seed **271828**, margin **0.8**, bounded attempts, and separate random streams per category. Hidden categories retain their reservations, so toggling one preserves the others' footprints and mesh data. Density and placement settings rebuild the reservations. Trees use a truncated exponential distance distribution around spaced groves. `TrackGenerator.RebuildMeshes` rebuilds enabled decor after road/wall geometry, and disabling the track releases it. `DecorGenerator.OnEnable` covers attachment and reload without duplicating a track-triggered build. There are no frame callbacks or colliders. Use **Rebuild Decor** after changing decor Inspector settings; **Regenerate Track** now refreshes decor automatically. Mesh objects appear under **Track > Generated**. Inspector defaults and palette order are in [SCENE_SETUP.md](SCENE_SETUP.md).

`DecorGeneratorTests` covers nonempty categories, exhaustive road/wall clearance, decor separation, mesh containment, deterministic regeneration, toggle stability, invalid settings, empty densities, resource cleanup, unchanged colliders/gates/spawn, and GPU-rendered previews through the theme installer. `ThemeInstallerTests` covers repeated installation, Undo/Redo, settings and physics preservation, invalid targets, inactive/unbuilt tracks, automatic rebuild/cleanup, and fresh-scene reload. It also verifies invalid decor settings reject regeneration before circuit replacement, and same-frame Play rebuilds/toggles/regeneration use current geometry. Batch runs save `Logs/DecorStartPreview.png` and `Logs/DecorCornerPreview.png`; the wiring-checkpoint review copies and test evidence are retained under ignored `Logs/ThemeWiring/`.

**GhostlineSceneBuilder** implements **Tools > Ghostline > Build Scene**. It defines the normalized Suzuka knots, checks TMP resources before changing the scene, connects the generator and race objects, and saves `Assets/Scenes/Main.unity`. It leaves rendering assets, tags, layers, and build profiles under Editor control.

**GhostlineThemeInstaller**, in `Assets/Ghostline/Editor/GhostlineThemeInstaller.cs`, implements **Tools > Ghostline > Add Theme To Scene**. Stop Play, open your existing Main, and run this menu once to attach decor to Track and build its transient meshes from the available road/wall geometry. It uses one Undo group, preserves existing seed/toggles/density/palettes and enabled states, marks the scene dirty, and never saves or regenerates the track. Save Main yourself after visual review to retain the component and its settings; meshes regenerate on reload. Repeating the menu reuses the component without duplicate generated roots. Missing/ambiguous tracks or an unrelated authored **Track/Generated** object fail before changes. An inactive/unbuilt track is wired without enabling it or creating physics. Fresh **Build Scene** includes decor automatically. The Ferris wheel remains deferred.

**GhostlineMinimapInstaller**, in `Assets/Ghostline/Editor/GhostlineMinimapInstaller.cs`, implements **Tools > Ghostline > Add Minimap To Scene**. Open Main, stop Play mode, run the command, and save the scene yourself. It adds or repairs the minimap under the existing HUD Canvas, wires references, preserves serialized tuning, and groups changes for Undo. Repeating it creates no duplicate panel or dots. Missing required objects or ambiguous track/race/HUD objects produce a specific error. It never regenerates the track or saves Main. Fresh **Build Scene** runs include the same minimap automatically; use the additive command to preserve your existing scene edits.

## Tests

Stop Play mode. Open **Window > General > Test Runner > EditMode > Run All**. Core tests cover `LapTimerTests`, `CheckpointTrackerTests`, `GhostRecordingTests`, `GhostRecorderTests`, `BestLapRepositoryTests`, `RaceSessionTests`, `DeltaCalculatorTests`, and `StartSequenceTests`. Nested EditMode storage tests cover JSON split round trips, replacement, invalid splits, and older versions. Scene tests additionally cover generated geometry, crossover clearance, race wiring, car presentation, countdown input locking, HUD formatting/fading, and final deltas against the previous best.

The minimap adds **23 Core cases** in `MinimapProjectionTests` and **14 scene cases** in `MinimapViewTests` / `MinimapInstallerTests`. Both scene fixtures run directly from **Window > General > Test Runner > EditMode** with no extra setup or scene installation. They create their own objects and disposable preview scene, require no saved ghost or TMP-resource import, and never rebuild or save Main. Coverage includes closed-loop/tick drawing, smoothed edges, world transforms, resize alignment, countdown spawn, missing/end-of-playback visibility, texture cleanup, automatic wiring, repeat installation, repair, preserved tuning, and Undo. The existing scene-test assembly already references Game, Editor, UI, and the Test Framework; no assembly or package changes are needed.

Full minimap verification with Unity **6000.6.4f1**: **210 EditMode cases passed, 0 failed, 0 skipped** in an isolated project copy: **132 Core**, **19 Storage**, and **59 Scene**. This includes all 37 new minimap cases. The standalone engine-free Core run also passed **132/132**. The new minimap cases run in the Editor without batch mode; the existing `CarSpriteTests.BuilderProducesTheSameCarPresentation` test skips in an interactive run because rebuilding Main prompts for confirmation. Manual minimap placement/readability and driving review remain separate Play checks.

An isolated Editor smoke check also passed fresh Build Scene installation, repeat-install idempotency without disk writes, saving/reloading every minimap reference, real-track raster generation, and countdown ghost visibility. Its rendered 1600 x 900 preview was inspected for placement, line/tick visibility, and default UI rendering. Interactive Play-mode review remains open.

The top-right sizing follow-up reran all **14 minimap scene cases**, passing with `-testFilter Ghostline.Tests.Scene.Minimap`, and repeated the isolated fresh-build/reload/render check. The inspected preview shows a **270 x 270** panel with a **24-unit top/right inset** at 1600 x 900. Test counts are unchanged. Existing installed panels retain their serialized height: set **Height Fraction = 0.30** on MinimapView, then rerun **Add Minimap To Scene** and save to refresh the Editor layout.

The grass/road-paint theme checkpoint adds **9 scene cases** for alternating serialized grass colors, continuous clipped bands, independent toggles, and invalid/excessive stripe widths. Full Unity **6000.6.4f1** verification passed **219/219 EditMode cases**, with **0 failed, 0 skipped**, and Editor exit code **0**, in an isolated copy of the edited scene. The start/finish and corner renders were inspected; interactive visual approval remains pending. Physics, tuning, persistence source, and the workspace scene/settings files match their pre-change hashes. No later decor or installer work is included in this checkpoint.

Core tests need no engine objects, including the new curve, throttle, driving, offset/intersection, and save-identity cases. Scene tests cover geometry, rendering, and Inspector-curve parity. Physics tests automatically enter and exit Play mode to verify force/mass behavior, coasting, brake/reverse switching, and fractional wall impacts.

For batch testing, close this project's Unity Editor first and open PowerShell at the repository root. Adjust the Editor executable path if Unity Hub is installed elsewhere. `Start-Process -Wait` waits for completion even though Unity is a Windows GUI executable:

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe'
$projectPath = (Get-Location).Path
$testResultsPath = Join-Path $env:TEMP 'Ghostline-EditMode.xml'
$testLogPath = Join-Path $env:TEMP 'Ghostline-EditMode.log'
$arguments = @(
    '-batchmode', '-runTests', '-testPlatform', 'EditMode',
    '-projectPath', ('"' + $projectPath + '"'),
    '-testResults', ('"' + $testResultsPath + '"'),
    '-logFile', ('"' + $testLogPath + '"')
)
$testProcess = Start-Process -FilePath $unityEditor -ArgumentList $arguments `
    -WindowStyle Hidden -Wait -PassThru
$testProcess.ExitCode
[xml]$results = Get-Content -LiteralPath $testResultsPath
$results.'test-run' | Select-Object result, total, passed, failed
```

Expect exit code 0, `result = Passed`, and `failed = 0`. GPU checks require graphics. Inspect the log if no results file is produced. The Test Framework exits Unity when the run finishes; omit `-quit` with `-runTests`. See Unity's [test command-line reference](https://docs.unity.com/en-us/engine/6000.7/manual/scripting/test-framework-introduction/reference-command-line).

Historical rectangle-track verification used the installed Editor against an isolated project copy: **24 EditMode cases passed**. Additional Editor checks passed JSON round-tripping/replacement, corrupt-save fallback, generated sprites with preserved default materials, and scene saving/reloading with the expected colliders, camera, HUD references, and no lights.

Before the Suzuka replacement, a temporary PlayMode smoke test also passed two driven laps using keyboard input, ordered physical checkpoint crossings, R restart, preservation of the faster lap, ghost playback, wall collisions, and loading the saved best into a fresh scene. Its isolated save folder kept personal records untouched; the harness is outside this repository. Use the setup guide's Play checks to assess the new circuit's driving feel.

## Save data

The file is `ghostline-best-lap.json` in `Application.persistentDataPath`, normally `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Ghostline\ghostline-best-lap.json` on Windows. R never deletes it. Format **6** stores version, track ID, splits, duration, and poses. Incompatible versions/tracks and invalid data are ignored. Validation uses the race's configured track identity and checkpoint count.

The file's mutable JSON representation is intentionally separate from `GhostSample`: `JsonUtility` serializes fields, while the Core sample exposes readonly properties. A temporary-file write followed by replacement protects the existing save from incomplete writes; write failures retain the previous best and show a warning. No ghost data is uploaded.

## Design decisions

Race rules live in Core so scene wiring, keyboard input, UI changes, and file paths cannot determine lap validity. Constructor injection lets the repository use disk storage in the game and an in-memory implementation in tests. The Core assembly explicitly excludes engine references, enforcing this boundary during compilation.

Unit tests exercise ordinary C# objects with supplied times and poses, rather than waiting for frames or simulating Unity physics. Unity's EditMode runner hosts NUnit, but the tested logic itself needs no engine objects. Trigger collision behavior, visible materials, keyboard focus, and driving feel still need the Play checks in the setup guide.

Fixed-interval samples provide small, inspectable recordings and stable playback across frame rates. The game deliberately uses one track and one completed attempt at a time; there is no asset pipeline, input action asset, networking, or configurable track system.

The track uses the project's shipped unlit material and vertex colors for its road, walls, and racing visual layer. All visual additions are collider-free; Core, driving physics, gate logic, and the Lambo player/ghost presentation are preserved. No textures, shaders, packages, lights, shadows, tire marks, slowdown surfaces, gravel, pit lane, branding, or sound are added.

The existing Unity `.gitignore` and Git LFS `.gitattributes` are preserved. Source assets and their `.meta` files stay tracked; generated project files, caches, logs, user settings, and build directories are already ignored.
