# Ghostline

A small top-down Unity 2D time-trial game built as a software engineering portfolio project. Drive a rectangular loop, pass four checkpoints in order, and cross the start/finish line. A faster completed lap is saved locally and becomes a translucent replay car on later attempts and later runs.

## Open and play

The working project is **`X:\Unity Projects\Ghostline`**, not the originally proposed `C:\Dev\Ghostline`. Its recorded Editor version is **6000.6.4f1** (`ProjectSettings/ProjectVersion.txt`). Open this project with that version through Unity Hub. No package installation, external artwork, input action asset, or custom game material is required.

1. Wait for compilation and check the Console for errors.
2. Verify **Edit > Project Settings > Player > Other Settings > Configuration > Active Input Handling** is **Input System Package (New)** or **Both**. The manifest includes `com.unity.inputsystem`; the supplied project is already set to New.
3. Import **Window > TextMeshPro > Import TMP Essential Resources**, click **Import**, and wait until importing finishes. Do this before creating the HUD or running the scene builder.
4. Run **Tools > Ghostline > Build Scene**. It saves and opens `Assets/Scenes/Main.unity`. Rebuilding replaces edits to that scene after confirmation.
5. If rectangles appear black, follow the **Sprite-Unlit-Default** material instructions in [SCENE_SETUP.md](SCENE_SETUP.md). The builder preserves default sprite materials and creates no lights.
6. Press **Play**, click the **Game** view to give it keyboard focus, and drive across the white line to start.

[SCENE_SETUP.md](SCENE_SETUP.md) includes the full manual scene construction fallback, every component and Inspector reference, and a Play verification checklist.

## Controls and race rules

| Key | Action |
| --- | --- |
| W / Up | Accelerate forward |
| S / Down | Brake while moving forward, then reverse |
| A / Left | Steer left |
| D / Right | Steer right |
| R | Reset the car and current attempt; preserve the saved best |

The cyan car begins on the bottom lane facing right. Drive right, up the right lane, left along the top, down the left lane, and right along the bottom. Release acceleration or briefly brake before corners. Steering requires movement and reverses naturally while backing up.

The timer stays at zero until the first forward start crossing. Checkpoints are numbered 1 through 4 in the HUD (indices 0 through 3 in code). Duplicate, skipped, out-of-order, and backward gate entries do not advance progress. After all four checkpoints, the next forward finish crossing completes the attempt. The timer freezes and driving stops until R; this game runs one attempt at a time.

The ghost is hidden before the start and when there is no valid best lap. It replays the previous best on the current lap clock and remains at its final pose if the current attempt takes longer. A new best becomes the replay on the next reset. Lap timing and trigger detection use Unity's fixed physics steps; they do not estimate sub-step crossing times.

## Structure

```text
Assets/Ghostline/
  Core/             Ghostline.Core.asmdef; pure C# race rules and recording
  Game/             Ghostline.Game.asmdef; Unity, keyboard, presentation, file adapters
  Tests/EditMode/   Ghostline.Tests.EditMode.asmdef; NUnit tests referencing Core
  Editor/           Ghostline.Editor.asmdef; Editor-only scene builder
```

Core has `noEngineReferences: true`. Game references Core, `Unity.InputSystem`, `Unity.TextMeshPro`, and `UnityEngine.UI`. Tests reference Core and the template's Test Framework through `TestAssemblies`; they are Editor-only. Editor references Core, Game, TMP, and UI, and is excluded from players.

### Core classes

**LapTimer** owns `NotStarted`, `Running`, and `Finished`, accumulates supplied seconds only while running, and raises start, finish, and reset events. It rejects negative or nonfinite tick values.

**CheckpointTracker** accepts only the next zero-based checkpoint. A separate finish call completes an ordered lap once; reset clears progress. Checkpoint entry alone never finishes a lap.

**GhostSample** is a readonly struct containing lap-relative time, X, Y, and Z rotation in degrees. Its constructor rejects nonfinite poses and negative timestamps.

**GhostRecording** copies a nonempty, strictly time-ordered sequence into read-only storage. `Evaluate` uses binary search and linear position interpolation with shortest-path angular interpolation, clamping outside the recorded range.

**GhostRecorder** resamples supplied poses at a fixed interval, defaulting to 0.05 seconds. It interpolates across uneven updates, records time zero, includes the exact finish pose, and prevents duplicate endpoint timestamps.

**BestLapData** is a serializable mutable transport object holding duration and samples. It represents a completed lap, rather than a running recorder.

**IBestLapStorage** defines loading and saving independently of disk, Unity, or serialization. A missing or unreadable save returns null; save failures can be reported by the application adapter.

**BestLapRepository** validates ordered start/finish samples and positive duration, returns defensive copies, and saves only the first or strictly faster valid lap. Equal durations do not replace the best.

**RaceSession** coordinates the timer, checkpoints, and recorder for a single attempt. Its start/finish, checkpoint, tick, and reset methods keep race rules out of MonoBehaviours.

**NumericGuard** is an internal helper shared by Core classes to enforce finite and nonnegative numeric inputs without depending on Unity's math API.

### Game classes

**SolidSprite** uses `[ExecuteAlways]` to create a rectangle from `Texture2D.whiteTexture`. It applies Inspector color, world-unit size, and sorting order, preserves the SpriteRenderer's material, recreates sprites after scene loading, and releases each generated sprite when disabled. Size controls the object's local scale; use unit-sized BoxCollider2D components.

**CarController** reads `Keyboard.current` and applies acceleration, speed limiting, speed-dependent steering, and exponential lateral grip to a Rigidbody2D. Inspector fields expose speed, acceleration, steering, grip, and damping. `ResetPose` clears motion and restores a planar spawn pose.

**CameraFollow** smoothly follows the car on the XY plane while keeping camera Z at -10. Reset can snap the camera directly to the spawn position.

**CheckpointTrigger** translates a car's trigger entry into a race command. It filters by CarController and Rigidbody2D, accepts movement only along the configured forward direction, and requires no custom tags or layers.

**RaceManager** creates and connects Core objects, supplies fixed-step ticks and car poses, responds to restart input, and coordinates saved laps with the HUD and ghost. Storage failures produce a Console warning and HUD status while leaving the completed attempt visible.

**GhostCarView** displays an interpolated saved pose on the current attempt's clock. It uses a translucent SolidSprite and has no collider or rigidbody, so playback cannot affect driving.

**HudView** renders current time, best time, and attempt/checkpoint status into three assigned TMP text components. It formats presentation but does not decide race outcomes.

**JsonFileBestLapStorage** is a plain C# Unity adapter implementing `IBestLapStorage`, rather than a component to attach to an object. It maps readonly Core samples to private mutable JSON DTOs, validates a versioned file, writes through a temporary file, and falls back to no ghost for missing, malformed, incompatible, or unreadable data.

### Editor class

**GhostlineSceneBuilder** implements **Tools > Ghostline > Build Scene**. It checks TMP resources before changing the scene, creates and connects all objects, and saves `Assets/Scenes/Main.unity`. It leaves package configuration, rendering assets, tags, layers, and build profiles under Editor control.

## Tests

Stop Play mode. Open **Window > General > Test Runner > EditMode > Run All**. The suite contains **24 cases** across `LapTimerTests`, `CheckpointTrackerTests`, `GhostRecordingTests`, `GhostRecorderTests`, `BestLapRepositoryTests`, and `RaceSessionTests`.

The tests cover timer transitions and events, ordered checkpoints, interpolated and clamped playback, angular wraparound, fixed recording intervals, complete endpoints, storage replacement and round-tripping through an in-memory fake, and an entire Core race attempt. Tests do not need a scene, sprite, camera, or physics simulation.

For batch testing on this machine, close the project's Unity Editor first. Run this in PowerShell; `Start-Process -Wait` waits for completion even though Unity is a Windows GUI executable:

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe'
$projectPath = 'X:\Unity Projects\Ghostline'
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

Expect exit code 0, `result = Passed`, `passed = 24`, and `failed = 0`. Inspect the specified log if no results file is produced. The Test Framework exits Unity when the run finishes; omit `-quit` when using `-runTests`. See Unity's [test command-line reference](https://docs.unity.com/en-us/engine/6000.7/manual/scripting/test-framework-introduction/reference-command-line).

Implementation verification used the installed Editor against an isolated copy because this project was already open: **24 EditMode cases passed**. Additional Editor checks passed JSON round-tripping/replacement, corrupt-save fallback, generated sprites with preserved default materials, and scene saving/reloading with the expected colliders, camera, HUD references, and no lights. Visual appearance and driving feel still require the Play checks in the setup guide.

## Save data

The file is `ghostline-best-lap.json` in `Application.persistentDataPath`. With this project's current Company Name and Product Name, Windows uses `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Ghostline\ghostline-best-lap.json`. This is outside the repository. Delete only that file with Play stopped to clear the best; R never deletes it. Unknown file versions and invalid samples are ignored.

The file's mutable JSON representation is intentionally separate from `GhostSample`: `JsonUtility` serializes fields, while the Core sample exposes readonly properties. A temporary-file write followed by replacement protects the existing save from incomplete writes; write failures retain the previous best and show a warning. No ghost data is uploaded.

## Design decisions

Race rules live in Core so scene wiring, keyboard input, UI changes, and file paths cannot determine lap validity. Constructor injection lets the repository use disk storage in the game and an in-memory implementation in tests. The Core assembly explicitly excludes engine references, enforcing this boundary during compilation.

Unit tests exercise ordinary C# objects with supplied times and poses, rather than waiting for frames or simulating Unity physics. Unity's EditMode runner hosts NUnit, but the tested logic itself needs no engine objects. Trigger collision behavior, visible materials, keyboard focus, and driving feel still need the Play checks in the setup guide.

Fixed-interval samples provide small, inspectable recordings and stable playback across frame rates. The game deliberately uses one track and one completed attempt at a time; there is no asset pipeline, input action asset, networking, or configurable track system.

All gameplay art consists of generated tinted rectangles. SpriteRenderer materials remain the defaults supplied by the project; a documented manual unlit fallback handles a light-dependent default without introducing shaders or lights.

The existing Unity `.gitignore` and Git LFS `.gitattributes` are preserved. Source assets and their `.meta` files stay tracked; generated project files, caches, logs, user settings, and build directories are already ignored.
