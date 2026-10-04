**TASK SUMMARY:** Widen the Suzuka circuit, add Inspector-tunable force-based driving, and invalidate incompatible lap/ghost saves, with new pure logic and EditMode tests in Ghostline.Core.

**FILES MODIFIED:** Ranges cover the changed sections, relative to the verified pre-tuning baseline `caae741`. New `.meta` files contain only Unity GUIDs.

| Path | Lines |
|---|---|
| `Assets/Ghostline/Core/BestLapData.cs` | 10-15 |
| `Assets/Ghostline/Core/BestLapRepository.cs` | 11-80 |
| `Assets/Ghostline/Core/DrivingCurve.cs` | 1-138 |
| `Assets/Ghostline/Core/DrivingCurve.cs.meta` | 1-2 |
| `Assets/Ghostline/Core/DrivingMath.cs` | 1-99 |
| `Assets/Ghostline/Core/DrivingMath.cs.meta` | 1-2 |
| `Assets/Ghostline/Core/RaceSession.cs` | 1-48 |
| `Assets/Ghostline/Core/TrackOffsetGeometry.cs` | 1-218 |
| `Assets/Ghostline/Core/TrackOffsetGeometry.cs.meta` | 1-2 |
| `Assets/Ghostline/Core/TrackWidthLimit.cs` | 1-17 |
| `Assets/Ghostline/Core/TrackWidthLimit.cs.meta` | 1-2 |
| `Assets/Ghostline/Editor/GhostlineSceneBuilder.cs` | 156-182 |
| `Assets/Ghostline/Game/CarController.cs` | 1-160 |
| `Assets/Ghostline/Game/DrivingCurveAdapter.cs` | 1-31 |
| `Assets/Ghostline/Game/DrivingCurveAdapter.cs.meta` | 1-2 |
| `Assets/Ghostline/Game/JsonFileBestLapStorage.cs` | 12-99 |
| `Assets/Ghostline/Game/RaceManager.cs` | 19-72 |
| `Assets/Ghostline/Game/TrackGenerator.cs` | 3-600 |
| `Assets/Ghostline/Game/TrackVisuals.cs` | 273-444 |
| `Assets/Ghostline/Tests/EditMode/BestLapRepositoryTests.cs` | 98-182 |
| `Assets/Ghostline/Tests/EditMode/DrivingCurveTests.cs` | 1-55 |
| `Assets/Ghostline/Tests/EditMode/DrivingCurveTests.cs.meta` | 1-2 |
| `Assets/Ghostline/Tests/EditMode/DrivingMathTests.cs` | 1-99 |
| `Assets/Ghostline/Tests/EditMode/DrivingMathTests.cs.meta` | 1-2 |
| `Assets/Ghostline/Tests/EditMode/RaceSessionTests.cs` | 8-49 |
| `Assets/Ghostline/Tests/EditMode/Storage/JsonFileBestLapStorageTests.cs` | 30-146 |
| `Assets/Ghostline/Tests/EditMode/TrackOffsetGeometryTests.cs` | 1-87 |
| `Assets/Ghostline/Tests/EditMode/TrackOffsetGeometryTests.cs.meta` | 1-2 |
| `Assets/Ghostline/Tests/Scene/CarDrivingTests.cs` | 1-175 |
| `Assets/Ghostline/Tests/Scene/CarDrivingTests.cs.meta` | 1-2 |
| `Assets/Ghostline/Tests/Scene/CountdownAndDeltaTests.cs` | 87-178 |
| `Assets/Ghostline/Tests/Scene/RaceConfigurationTests.cs` | 137-161 |
| `Assets/Ghostline/Tests/Scene/TrackGeneratorTests.cs` | 6-363 |
| `Assets/Ghostline/Tests/Scene/TrackVisualGeometryTests.cs` | 109-111 |
| `Assets/Scenes/Main.unity` | 122-10874 |
| `README.md` | 45-150 |
| `SCENE_SETUP.md` | 20-91 |
| `docs/gameplay-tuning-handoff.md` | 1-111 |

**IMPLEMENTATION:**

- `TrackOffsetGeometry` owns radius limits, closed-loop width smoothing/interpolation, local intersection reductions, spatially indexed segment tests, and measured width-limit intervals. `Ghostline.Core` and its test assembly retain `noEngineReferences: true`.
- `TrackGenerator.SampleSpline/PrepareWallPaths/Generate` uses Road Width 2.93 (was 2.2; +33.2%). Road meshes, collider walls, gates/triggers, decorative offsets, and grid columns share effective local width. Whole-wall clearance includes thickness and radius margin. Retained wall segments are checked for self-intersection; crossover gaps exclude walls from both road corridors. Hidden generation settings prevent a width change from leaving serialized colliders/gates behind on reload. The component context menu regenerates immediately.
- `TrackVisuals` uses local width for ribbons, sectors, checker, grid footprint checks, and spawn placement. `RaceManager.SetSpawn` updates the reset pose and editor car/ghost/camera transforms. The saved scene preserves every unrelated baseline scene block; nine approved component/pose blocks changed outside generated geometry. Camera size remains 9; start/corner previews were inspected.
- `DrivingCurve` evaluates copied Inspector keys in Core, including Hermite tangents, weighted Bezier handles, steps, clamp, loop, and ping-pong. `DrivingCurveAdapter.ToCore` only copies Unity data. Unity parity tests cover weighted curves and the exact loop-end boundary.
- `DrivingMath` owns throttle smoothing, acceleration, braking/reverse, engine braking, steering, grip/drag stability, and retained impact speed. `CarController.FixedUpdate` applies mass-scaled forces: `throttle * accelCurve.Evaluate(speed / topSpeed) * maxAcceleration`, then proportional drag. Top Speed is a curve reference; there is no velocity clamp. Negative acceleration-curve values retain their signed meaning. Rigidbody linear damping is zero to avoid double drag.
- W/Up ramps to 1 over 0.4 seconds; release ramps to zero. Coasting applies engine braking without reversing. S/Down overrides W, brakes strongly above the reverse threshold, and powers reverse near rest. Steering falls with speed and reverses with travel direction. Reset/countdown locking clears smoothed input. Wall entry retains `1 - Wall Speed Loss` of incoming speed; head-on impacts bounce instead of freezing, and multiple contacts in one physics step do not stack the loss.
- `BestLapData` carries explicit `Version`/`TrackId`. `RaceSession` stamps version 4 and configured identity; repository copies preserve both. `BestLapRepository.Load/IsValid` and `JsonFileBestLapStorage.Load` quietly reject missing, legacy/future, or wrong-track metadata before creating a ghost. Default identity is `suzuka`. A new valid lap can replace an incompatible faster record. No saved record is deleted by this pass.
- Existing underscore fields, serialized configuration, Core numeric guards, NUnit fixtures, and generated-mesh cleanup patterns are retained. FormerlySerializedAs preserves Speed/Acceleration/Linear Damping tuning. No project dependencies were added. The Core spatial index uses System.ValueTuple cell keys because Unity Vector2Int cannot be used in Core.

Default width restrictions, measured in arc-length units from start/finish:

| Interval | Minimum width |
|---|---|
| 307.92-308.27 | 2.81 |
| 363.42-366.60 | 2.08 |
| 673.46-673.82 | 2.82 |
| 714.47-719.07 | 1.87 |

The crossover passes are at 317.25 and 615.13. Their wall gaps expand for the wider corridors; the shared road remains open rather than pinched. Console generation messages and `TrackGenerator.WidthLimits` report restrictions.

Commits:

- `caae741` preserves the existing uncommitted gameplay baseline with user authorization.
- `72ee447` widens Suzuka and adds safe offsets.
- `1991188` adds force-based handling and tests.
- `95f280f` adds version/track compatibility.
- `d6c9509` aligns radius diagnostics with full-wall clearance.
- The final documentation commit updates README, setup, and this handoff.

**VERIFICATION:** Unity 6000.6.4f1, final full EditMode suite: **166 passed, 0 failed, 0 skipped; process exit code 0**. Standalone Core NUnit run: **107 passed, 0 failed**. The exact delivered serialized scene additionally passed **9 focused geometry tests**, exit 0, without changing the scene file. All C# and asmdef sources were hash-matched against the tested external copy. A scene-block comparison against `caae741` confirmed unrelated blocks were unchanged. Source/document whitespace checks passed. Unity's generated YAML retains its normal blank-field spacing.

```powershell
$verificationRoot = 'X:\GhostlineVerification\GameplayTuning-20261003'
$testProcess = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' `
    -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
        '-batchmode', '-runTests', '-testPlatform', 'EditMode',
        '-projectPath', ('"' + $verificationRoot + '"'),
        '-testResults', ('"' + $verificationRoot + '/final-confirmed.xml"'),
        '-logFile', ('"' + $verificationRoot + '/final-confirmed.log"')
    )
$testProcess.ExitCode # 0
[xml]$results = Get-Content -LiteralPath ($verificationRoot + '/final-confirmed.xml')
$results.'test-run' | Select-Object result,total,passed,failed,skipped # Passed, 166, 166, 0, 0
git diff --check caae741 HEAD -- Assets/Ghostline README.md SCENE_SETUP.md docs/gameplay-tuning-handoff.md
```

The external verification copy includes Assets, Packages, and ProjectSettings; Unity's generated working folders exist there only. This workspace's Library/, Temp/, and UserSettings/ were not read or modified. Evidence and inspected previews are copied to ignored `Logs/GameplayTuning-*`. Tests cover normal/reset/released/simultaneous input, timestep variation, curve boundaries/weights/wrapping, drag equilibrium and above-reference speed, coasting stop, brake/reverse switching, mass-independent forces, a real head-on wall collision, closed-track seam smoothing, retained wall intersections, wide-corner generation, width reloads, gate/grid/crossover clearance, silent incompatible loads, no ghost, and current-format round trips. New geometry, driving, and compatibility tests were observed failing before implementation/fixes.

**OPEN ITEMS:** Claude should drive the default scene to judge corner pacing, steering feel, and whether 0.35 wall loss is enjoyable. Automated physics and rendering checks pass; a full human-driven lap has not been measured. Existing unrelated changes to Assets/Welcome/2d-template.png and three ProjectSettings files remain outside these commits. New tuple cell keys are an implementation pattern only; no new package or architectural dependency requires installation.

Inspector fields to tune first:

| Priority | Component | Fields/defaults | Purpose |
|---|---|---|---|
| 1 | TrackGenerator | Road Width 2.93; Offset Smoothing Length 6 | Driving space and corner narrowing; regenerate after edits |
| 2 | CarController | Throttle Ramp Time 0.4 | Press/release response |
| 3 | CarController | Max Acceleration 18; Accel Curve | Launch strength and power through the speed range |
| 4 | CarController | Drag 1.2; Top Speed reference 12 | Terminal-speed balance and curve normalization |
| 5 | CarController | Steering 150; Steering Curve; Minimum Steering Speed 2 | Corner control at low/high speed |
| 6 | CarController | Engine Braking 3; Brake Acceleration 30 | Coasting and stopping distance |
| 7 | CarController | Reverse Threshold 0.3; Reverse Acceleration 8 | Brake-to-reverse transition |
| 8 | CarController | Wall Speed Loss 0.35 | Collision forgiveness; 0.35 removes 35% |

Secondary fields: Grip 12 and Angular Damping 8; Track Wall Thickness 0.2 and Radius Margin 0.3. Camera orthographic size remains 9.
