**TASK SUMMARY:** Slightly reduce straight-line speed after the user found the 18-unit/s balance hard to control.

**FILES MODIFIED:**

| File | Changed lines |
|---|---|
| `Assets/Ghostline/Game/CarController.cs` | 23 |
| `Assets/Ghostline/Editor/GhostlineSceneBuilder.cs` | 156 |
| `Assets/Ghostline/Tests/Scene/CarDrivingTests.cs` | 95, 161, 168, 177 |
| `Assets/Ghostline/Tests/Scene/CountdownAndDeltaTests.cs` | 88 |
| `Assets/Scenes/Main.unity` | 9725 (committed scene); 8288 (working scene). Only Drag committed; existing scene edits preserved unstaged. |
| `Assets/Ghostline/Core/BestLapData.cs` | 10 |
| `Assets/Ghostline/Tests/EditMode/BestLapRepositoryTests.cs` | 102-103 |
| `Assets/Ghostline/Tests/EditMode/Storage/JsonFileBestLapStorageTests.cs` | 53-54, 127 |
| `README.md` | 45, 92, 150 |
| `SCENE_SETUP.md` | 28, 73, 91 |
| `docs/speed-trim-handoff.md` | 1-51 (new handoff) |

**IMPLEMENTATION:**

- `CarController` and the saved Inspector Drag are **0.85**, up from 0.8. The existing force/drag balance settles near **17.14 units/s**, a **4.76%** reduction. Top Speed remains the curve reference of 18; Max Acceleration remains 18. The initial acceleration at rest, steering curves, throttle ramp, brake acceleration, reverse behavior, wall-speed loss, road width, and Continuous detection retain their existing settings. Existing pure Core force and curve math is reused; no new Core algorithm or dependency.
- `BestLapData.CurrentVersion` is **6**. Version-5 records are quietly ignored; repository and JSON tests cover version 5, future version 7, current-version round trips, and hidden ghosts for legacy saves. Invalid-split tests continue to use the current schema.
- `CarDrivingTests.DefaultForcesReachHigherTerminalSpeedAndBrakeGradually` now expects terminal speed 17.14 ? 0.05 and 13.2?13.5 after braking for 0.2 seconds. The near-rest brake check expects 1.4 seconds with one fixed-step tolerance, accommodating float representation at the boundary. Wall and actual checkpoint-callback tests retain an 18-unit/s speed, above the new terminal balance.
- The working scene is preserved byte-for-byte except its Drag value. Its existing unrelated edits remain unstaged; the scene commit stages only the approved Drag change against the committed scene. Source defaults and regenerated scenes agree on Drag 0.85.

**VERIFICATION:**

- Standalone Core: **109 passed, zero failed/skipped**, exit 0.
- Unity handling suite: **7 passed, zero failed/skipped**, exit 0; covers terminal speed, braking/reverse, wall collisions, gate callbacks, throttle/mass behavior, and curve parity.
- Exact preserved saved scene: **3 passed, zero failed/skipped**, exit 0; covers generated-circuit configuration, gate/spawn alignment, car presentation, and saved Continuous mode.
- Initial full Unity run: **172/173 passed**. Its sole failure was a float boundary (`1.3999995` vs lower bound `1.4`) in the brake-duration assertion. The assertion now uses one fixed-step tolerance; all seven handling tests passed on rerun. All other Core, storage, scene, geometry, crossover, and render cases passed in that full run.
- All **52 C# / assembly files** match the verified copy. The exact scene remained unchanged by its three checks. Its only task change is Drag; all pre-existing scene edits remain unstaged. Both regression checks failed against the old values before implementation (terminal 17.9991 vs expected 17.14; version-5 compatibility case).

Commands:

```powershell
dotnet test 'X:/GhostlineVerification/CoreTests/CoreTests.csproj' --no-restore --verbosity quiet
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
$verifyRoot = 'X:/GhostlineVerification/GameplayTuning-20261003'
Start-Process -FilePath $unity -WindowStyle Hidden -Wait -PassThru -ArgumentList @('-batchmode','-runTests','-testPlatform','EditMode','-projectPath',('"'+$verifyRoot+'"'),'-testResults',('"'+$verifyRoot+'/speedtrim-confirmed.xml"'),'-logFile',('"'+$verifyRoot+'/speedtrim-confirmed.log"'))
# Handling rerun after correcting the timing tolerance:
Start-Process -FilePath $unity -WindowStyle Hidden -Wait -PassThru -ArgumentList @('-batchmode','-runTests','-testPlatform','EditMode','-testFilter','Ghostline.Tests.Scene.CarDrivingTests','-projectPath',('"'+$verifyRoot+'"'),'-testResults',('"'+$verifyRoot+'/speedtrim-handling.xml"'),'-logFile',('"'+$verifyRoot+'/speedtrim-handling.log"'))
# Restore the preserved Main.after.unity snapshot before the scene-only run:
Start-Process -FilePath $unity -WindowStyle Hidden -Wait -PassThru -ArgumentList @('-batchmode','-runTests','-testPlatform','EditMode','-testFilter','Ghostline.Tests.Scene.TrackGeneratorTests.SavedSceneUsesGeneratedCircuitInsteadOfRectangles;Ghostline.Tests.Scene.TrackGeneratorTests.GatesAndSpawnMatchSplineDirectionsAndRaceConfiguration;Ghostline.Tests.Scene.CarSpriteTests.SavedSceneCarsAlignWithMovementAndKeepTheirPhysics','-projectPath',('"'+$verifyRoot+'"'),'-testResults',('"'+$verifyRoot+'/speedtrim-scene.xml"'),'-logFile',('"'+$verifyRoot+'/speedtrim-scene.log"'))
```

Unity uses an external copy of Assets/Packages/ProjectSettings. Workspace Library/, Temp/, and UserSettings/ are untouched. Test evidence is copied to ignored workspace Logs/SpeedTrim-*.

Commits: `9cdffc9` record compatibility; `737a93b` speed trim and handling regressions. Documentation is committed separately.

**OPEN ITEMS:** Human Play-mode driving-feel review remains. Tune **Drag (0.85)** first for another small speed adjustment; Top Speed (18) is still the curve reference, not a speed clamp. Version-5 best laps/ghosts intentionally disappear until a valid new lap is saved.
