# Suzuka implementation handoff

**TASK SUMMARY:** Replace the rectangular prototype with the specified spline-generated Suzuka circuit, preserving the Lambo player and ghost presentation.

**FILES MODIFIED:** Ranges refer to the final files, not the previous rectangle scene.

| File | Lines | Change |
| --- | --- | --- |
| `Assets/Ghostline/Editor/GhostlineSceneBuilder.cs` | 1-222, 247-256 | Splines imports, sole 79-knot normalized trace, scale calculation, generator placement, tangent-based spawn and race wiring |
| `Assets/Ghostline/Game/TrackGenerator.cs` | 1-491 | Game component, arc-length sampling, road/wall meshes, radius validation, split wall colliders, gates and mesh lifecycle |
| `Assets/Ghostline/Game/TrackGenerator.cs.meta` | 1-2 | Component GUID |
| `Assets/Ghostline/Core/CheckpointTracker.cs` | 21-22 | Expose configured count without Unity references |
| `Assets/Ghostline/Game/RaceManager.cs` | 18-58 | Default/configurable count, effective count getter, configured spawn pose and session construction |
| `Assets/Ghostline/Game/HudView.cs` | 34-36 | Dynamic checkpoint progress and finish instruction |
| `Assets/Ghostline/Game/CheckpointTrigger.cs` | 10 | Nonnegative index instead of four-gate range |
| `Assets/Ghostline/Game/JsonFileBestLapStorage.cs` | 13 | Version 2 rejects rectangle-track saves |
| `Assets/Ghostline/Editor/Ghostline.Editor.asmdef` | 4 | Splines and Mathematics references |
| `Assets/Ghostline/Game/Ghostline.Game.asmdef` | 4 | Splines and Mathematics references |
| `Assets/Ghostline/Tests/Scene/Ghostline.Tests.Scene.asmdef` | 4 | Splines, Mathematics, TMP and UI test references |
| `Assets/Ghostline/Tests/EditMode/CheckpointTrackerTests.cs` | 9-58 | Counts 1/4/12, ordered completion, skip/duplicate rejection and reset |
| `Assets/Ghostline/Tests/EditMode/RaceSessionTests.cs` | 8-44 | Configurable-count lap regressions |
| `Assets/Ghostline/Tests/Scene/CarSpriteTests.cs` | 190-195, 209-213 | Spline spawn heading replaces fixed rectangle heading; existing art/physics assertions retained |
| `Assets/Ghostline/Tests/Scene/TrackGeneratorTests.cs` | 1-281 | Geometry, crossover clearance, gate wiring, mismatched-count rejection, regeneration, mesh restoration and GPU render checks |
| `Assets/Ghostline/Tests/Scene/TrackGeneratorTests.cs.meta` | 1-2 | Test GUID |
| `Assets/Ghostline/Tests/Scene/RaceConfigurationTests.cs` | 1-176 | HUD, reset pose/motion/camera, count validation and save-version tests |
| `Assets/Ghostline/Tests/Scene/RaceConfigurationTests.cs.meta` | 1-2 | Test GUID |
| `Assets/Scenes/Main.unity` | 1-9746 | Rebuilt circuit, collision/gates, spawn and camera; existing car/ghost/HUD setup retained |
| `Packages/manifest.json` | 17 | Unity Splines 2.8.4 |
| `Packages/packages-lock.json` | 235-263 | Resolved Splines and settings-manager dependency entries |
| `README.md` | 3-41, 97-149 | Circuit/race description, preview age, generator and test documentation |
| `SCENE_SETUP.md` | 1-69 | Rebuild/tuning instructions, current geometry and preserved Lambo setup |
| `docs/suzuka-handoff.md` | Entire file | This review handoff |

**IMPLEMENTATION:** `GhostlineSceneBuilder.SuzukaNormalizedKnots` stores x = pixelX / 1280 and y = 1 - pixelY / 720, in driving order from the upper-right flag. `BuildScene()` restores the image aspect ratio and sets the image width to 300 units. The closed spline measures **724.02 units**. Assuming 60-75% of `CarController`'s 12-unit/s ceiling gives approximately **80-101 seconds**, or **86 seconds at 70%**. This is a sizing estimate, not a measured driven lap.

`TrackGenerator` follows existing Game-component configuration patterns, uses the project's URP Sprite-Unlit-Default material, and follows `SolidSprite`'s transient-resource cleanup pattern. Dense measured distance lookup corrects the package's coarse per-curve lookup, keeping the arc-length spacing test unchanged. Mesh vertex colors are converted in Linear color space so the rendered road matches RGB **43/51/64**.

Road width is **2.2**, wall thickness **0.2**, samples **2048**, and checkpoints **12**, plus a separate start/finish gate. Minimum local radius is **1.43**, exceeding the **1.40** requirement. Radius comparisons use consecutive samples only. The single crossover keeps both roads open; wall polylines split at foreign road corridors. Both crossover arcs contain required gates. Gate nudging avoids the intersection; generation rejects counts that do not match the effective race session before modifying geometry. Invalid widths/counts/sample spacing and failing radii stop generation with diagnostics.

Player and ghost spawn four units behind the flag, aligned with the tangent. `RaceManager.Restart()` uses the configured pose through existing `CarController.ResetPose()` and camera snapping. Sprite import, child rotation, ghost material, collider dimensions, and driving parameters retain the Lambo setup. No curbs, grass, lane markings, textures, shaders, lights, or sound were added.

**VERIFICATION:** Unity **6000.6.4f1**, actual project, final EditMode suite: **50 passed, 0 failed, 0 skipped**; Unity exit code **0**. This includes builder rebuilds, saved-scene reload, GPU road/ghost renders, Core regressions, and the new geometry/race tests. Spec and code review found no remaining material issue. Scoped `git diff --check` passed. Existing unrelated user changes were compared against the pre-implementation copy and preserved.

Exact suite invocation (PowerShell, with Unity closed):

```powershell
$verificationRoot = Join-Path $env:TEMP 'Ghostline-Suzuka-Verification'
Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' -WindowStyle Hidden -Wait -ArgumentList @(
    '-batchmode', '-runTests', '-testPlatform', 'EditMode',
    '-projectPath', '"X:\Unity Projects\Ghostline"',
    '-testResults', ('"' + $verificationRoot + '\handoff.xml"'),
    '-logFile', ('"' + $verificationRoot + '\handoff.log"')
)
```

Evidence: `Logs/SuzukaEditMode.xml` (copied final results), `Logs/SuzukaPreview.png`, and `Logs/LamboPreview.png`; Logs is ignored. The original scene backup is `%TEMP%\Ghostline-Suzuka-Verification\Main.before-suzuka.unity`. Package/API reference: [Unity Splines](https://docs.unity3d.com/Packages/com.unity.splines@2.8/api/UnityEngine.Splines.Spline.html).

**OPEN ITEMS:** Manual driving feel and actual lap time remain unverified. The automatic reset, geometry, crossover, gate ordering and render checks passed. Changes remain uncommitted for Claude review.
