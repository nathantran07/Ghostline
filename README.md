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

Save format **3** adds checkpoint splits and ignores older formats **1 and 2**. The old saved best is unavailable until a new valid lap is saved.

The ghost is hidden before the start and when there is no valid best lap. It replays the previous best on the current lap clock and remains at its final pose if the current attempt takes longer. A new best becomes the replay on the next reset. Lap timing and trigger detection use Unity's fixed physics steps; they do not estimate sub-step crossing times.

## Structure

```text
Assets/Ghostline/
  Core/             Ghostline.Core.asmdef; pure C# race rules and recording
  Game/             Ghostline.Game.asmdef; Unity, keyboard, presentation, file adapters
  Tests/EditMode/   Ghostline.Tests.EditMode.asmdef; NUnit tests referencing Core
    Storage/        Ghostline.Tests.Storage.asmdef; Unity JSON adapter tests
  Editor/           Ghostline.Editor.asmdef; Editor-only scene builder
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

**NumericGuard** is an internal helper shared by Core classes to enforce finite and nonnegative numeric inputs without depending on Unity's math API.

### Game classes

**SolidSprite** uses `[ExecuteAlways]` to create a rectangle from `Texture2D.whiteTexture`. It applies Inspector color, world-unit size, and sorting order, preserves the SpriteRenderer's material, recreates sprites after scene loading, and releases each generated sprite when disabled. Size controls the object's local scale; use unit-sized BoxCollider2D components.

**CarController** reads `Keyboard.current` and applies acceleration, speed limiting, speed-dependent steering, and exponential lateral grip to a Rigidbody2D. Inspector fields expose speed, acceleration, steering, grip, and damping. `ResetPose` clears motion and restores a planar spawn pose. `InputEnabled = false` ignores keyboard input and stops motion during the countdown; the existing `CanDrive` flag still stops driving after finish. Handling values are unchanged.

**CameraFollow** smoothly follows the car on the XY plane while keeping camera Z at -10. Reset can snap the camera directly to the spawn position.

**CheckpointTrigger** translates a car's trigger entry into a race command. It filters by CarController and Rigidbody2D, accepts movement only along the configured forward direction, and requires no custom tags or layers.

**RaceManager** creates and connects Core objects, supplies the same fixed-step seconds to the countdown and race, responds to restart input, and coordinates saved laps with the HUD and ghost. It locks input until GO, forwards accepted gate deltas, and computes the final delta before saving a new best. Storage failures produce a Console warning and HUD status while leaving the completed attempt visible.

**GhostCarView** displays an interpolated saved pose on the current attempt's clock. It uses a translucent SolidSprite and has no collider or rigidbody, so playback cannot affect driving.

**HudView** renders current time, best time, attempt/checkpoint status, countdown, and delta into five assigned TMP text components. `RenderCountdown`, `ShowDelta`, and `Tick` handle label visibility, invariant signed decimal formatting, colors, and fade timing. It formats presentation but does not decide race outcomes.

**JsonFileBestLapStorage** is a plain C# Unity adapter implementing `IBestLapStorage`, rather than a component to attach to an object. It maps readonly Core samples to private mutable JSON DTOs, validates a versioned file, writes through a temporary file, and falls back to no ghost for missing, malformed, incompatible, or unreadable data.

### Editor class

**TrackGenerator** samples the closed spline by arc length, validates local bend radii, builds the road mesh and split EdgeCollider2D walls, and places configurable ordered gates clear of the crossover. Generated meshes are reconstructed when the saved scene loads. Road width defaults to 2.2 units, four times the car collider width.

**TrackVisuals** builds static, combined unlit meshes from the same samples and crossover rule. White edge lines stay on the asphalt; red/white curbs follow the inside of qualifying corners, while metal barriers and tighter-corner white/red tire walls sit outside the existing wall colliders. Runs split at crossover gaps and drop below minimum length. The grid uses two staggered columns, checks complete footprints against the road and crossover, and puts the player in the front slot. The current short approach fits one slot and warns; up to ten are painted on a long enough straight. Pink, yellow, and blue sector lines use the start and existing gates nearest corner 8 (knot 30) and corner 15 (knot 65), with no new race logic. The checker replaces the start sprite while preserving its trigger.

The Inspector exposes Road Color on TrackGenerator and colors, grass margin, edge/curb/barrier/tire widths, curvature thresholds, minimum run lengths, stripe/block lengths, checker depth, grid dimensions/spacing/straightness, and sector colors/anchors on TrackVisuals. Defaults include 0.15-unit edge lines, 0.5-unit curbs, 0.4-unit barriers, 0.8-unit tire walls, and a 1-unit-deep checker. See [SCENE_SETUP.md](SCENE_SETUP.md) for every default and the sorting order. **Tools > Ghostline > Build Scene** regenerates all geometry and front-slot spawn wiring.

**GhostlineSceneBuilder** implements **Tools > Ghostline > Build Scene**. It defines the normalized Suzuka knots, checks TMP resources before changing the scene, connects the generator and race objects, and saves `Assets/Scenes/Main.unity`. It leaves rendering assets, tags, layers, and build profiles under Editor control.

## Tests

Stop Play mode. Open **Window > General > Test Runner > EditMode > Run All**. Core tests cover `LapTimerTests`, `CheckpointTrackerTests`, `GhostRecordingTests`, `GhostRecorderTests`, `BestLapRepositoryTests`, `RaceSessionTests`, `DeltaCalculatorTests`, and `StartSequenceTests`. Nested EditMode storage tests cover JSON split round trips, replacement, invalid splits, and older versions. Scene tests additionally cover generated geometry, crossover clearance, race wiring, car presentation, countdown input locking, HUD formatting/fading, and final deltas against the previous best.

The tests cover timer transitions and events, ordered checkpoints, interpolated and clamped playback, angular wraparound, fixed recording intervals, complete endpoints, storage replacement and round-tripping through an in-memory fake, and an entire Core race attempt. Tests do not need a scene, sprite, camera, or physics simulation.

For batch testing, close this project's Unity Editor first and open PowerShell at the repository root. Adjust the Editor executable path if Unity Hub is installed elsewhere. `Start-Process -Wait` waits for completion even though Unity is a Windows GUI executable:

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe'
$projectPath = (Get-Location).Path
$testResultsPath = Join-Path $env:TEMP 'Ghostline-EditMode.xml'
$testLogPath = Join-Path $env:TEMP 'Ghostline-EditMode.log'
$arguments = @(
    '-batchmode', '-nographics', '-runTests', '-testPlatform', 'EditMode',
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

Expect exit code 0, `result = Passed`, and `failed = 0`. Omit `-nographics` to include GPU render checks. Inspect the specified log if no results file is produced. The Test Framework exits Unity when the run finishes; omit `-quit` when using `-runTests`. See Unity's [test command-line reference](https://docs.unity.com/en-us/engine/6000.7/manual/scripting/test-framework-introduction/reference-command-line).

Historical rectangle-track verification used the installed Editor against an isolated project copy: **24 EditMode cases passed**. Additional Editor checks passed JSON round-tripping/replacement, corrupt-save fallback, generated sprites with preserved default materials, and scene saving/reloading with the expected colliders, camera, HUD references, and no lights.

Before the Suzuka replacement, a temporary PlayMode smoke test also passed two driven laps using keyboard input, ordered physical checkpoint crossings, R restart, preservation of the faster lap, ghost playback, wall collisions, and loading the saved best into a fresh scene. Its isolated save folder kept personal records untouched; the harness is outside this repository. Use the setup guide's Play checks to assess the new circuit's driving feel.

## Save data

The file is `ghostline-best-lap.json` in `Application.persistentDataPath`. With this project's current Company Name and Product Name, Windows uses `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Ghostline\ghostline-best-lap.json`. This is outside the repository. Delete only that file with Play stopped to clear the best; R never deletes it. Format **3** stores checkpoint splits alongside duration and poses. Older/unknown versions, invalid samples, and invalid or missing splits are ignored. Storage and repository validation use the race's configured checkpoint count.

The file's mutable JSON representation is intentionally separate from `GhostSample`: `JsonUtility` serializes fields, while the Core sample exposes readonly properties. A temporary-file write followed by replacement protects the existing save from incomplete writes; write failures retain the previous best and show a warning. No ghost data is uploaded.

## Design decisions

Race rules live in Core so scene wiring, keyboard input, UI changes, and file paths cannot determine lap validity. Constructor injection lets the repository use disk storage in the game and an in-memory implementation in tests. The Core assembly explicitly excludes engine references, enforcing this boundary during compilation.

Unit tests exercise ordinary C# objects with supplied times and poses, rather than waiting for frames or simulating Unity physics. Unity's EditMode runner hosts NUnit, but the tested logic itself needs no engine objects. Trigger collision behavior, visible materials, keyboard focus, and driving feel still need the Play checks in the setup guide.

Fixed-interval samples provide small, inspectable recordings and stable playback across frame rates. The game deliberately uses one track and one completed attempt at a time; there is no asset pipeline, input action asset, networking, or configurable track system.

The track uses the project's shipped unlit material and vertex colors for its road, walls, and racing visual layer. All visual additions are collider-free; Core, driving physics, gate logic, and the Lambo player/ghost presentation are preserved. No textures, shaders, packages, lights, shadows, tire marks, slowdown surfaces, gravel, pit lane, branding, or sound are added.

The existing Unity `.gitignore` and Git LFS `.gitattributes` are preserved. Source assets and their `.meta` files stay tracked; generated project files, caches, logs, user settings, and build directories are already ignored.
