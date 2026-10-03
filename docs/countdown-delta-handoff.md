**TASK SUMMARY:** Add a standing-start countdown and checkpoint/finish deltas against the saved ghost without changing lap rules or car handling.

**FILES MODIFIED:** Ranges refer to feature changes against the pre-task working-tree snapshot; prior Suzuka changes are excluded.

| File | Lines |
| --- | --- |
| `Assets/Ghostline/Core/BestLapData.cs` | 12-13 |
| `Assets/Ghostline/Core/BestLapRepository.cs` | 10, 12-13, 16-18, 25, 31-32, 40-41, 43, 46-54, 74 |
| `Assets/Ghostline/Core/DeltaCalculator.cs` | 1-32 |
| `Assets/Ghostline/Core/DeltaCalculator.cs.meta` | 1-2 |
| `Assets/Ghostline/Core/RaceSession.cs` | 9, 17, 24-25, 43, 53-56, 72 |
| `Assets/Ghostline/Core/StartSequence.cs` | 1-69 |
| `Assets/Ghostline/Core/StartSequence.cs.meta` | 1-2 |
| `Assets/Ghostline/Editor/GhostlineSceneBuilder.cs` | 283-293, 295 |
| `Assets/Ghostline/Game/CarController.cs` | 21, 38, 52 |
| `Assets/Ghostline/Game/HudView.cs` | 1, 13-17, 19-20, 25-60 |
| `Assets/Ghostline/Game/JsonFileBestLapStorage.cs` | 13, 15, 17-18, 22, 27-29, 39, 41, 48, 61, 63, 95 |
| `Assets/Ghostline/Game/RaceManager.cs` | 25, 59, 73, 80-83, 89, 93-95, 106, 125, 128, 133-134 |
| `Assets/Ghostline/Tests/EditMode/BestLapRepositoryTests.cs` | 14, 28-31, 35-36, 38-40, 47, 55-97, 103, 132 |
| `Assets/Ghostline/Tests/EditMode/DeltaCalculatorTests.cs` | 1-53 |
| `Assets/Ghostline/Tests/EditMode/DeltaCalculatorTests.cs.meta` | 1-2 |
| `Assets/Ghostline/Tests/EditMode/RaceSessionTests.cs` | 45-65 |
| `Assets/Ghostline/Tests/EditMode/StartSequenceTests.cs` | 1-98 |
| `Assets/Ghostline/Tests/EditMode/StartSequenceTests.cs.meta` | 1-2 |
| `Assets/Ghostline/Tests/EditMode/Storage/Ghostline.Tests.Storage.asmdef` | 1-8 |
| `Assets/Ghostline/Tests/EditMode/Storage/Ghostline.Tests.Storage.asmdef.meta` | 1-7 |
| `Assets/Ghostline/Tests/EditMode/Storage/JsonFileBestLapStorageTests.cs` | 1-94 |
| `Assets/Ghostline/Tests/EditMode/Storage/JsonFileBestLapStorageTests.cs.meta` | 1-2 |
| `Assets/Ghostline/Tests/EditMode/Storage.meta` | 1-8 |
| `Assets/Ghostline/Tests/Scene/CountdownAndDeltaTests.cs` | 1-215 |
| `Assets/Ghostline/Tests/Scene/CountdownAndDeltaTests.cs.meta` | 1-2 |
| `Assets/Ghostline/Tests/Scene/RaceConfigurationTests.cs` | 80, 137, 147, 152, 159-160, 163 |
| `Assets/Ghostline/Tests/Scene/TrackGeneratorTests.cs` | 116-126 |
| `README.md` | 35, 39-45, 56, 60, 74, 78, 80-84, 92, 98, 102, 114, 146 |
| `SCENE_SETUP.md` | 57-58, 63, 65-67 |
| `Assets/Scenes/Main.unity` | 498-499, 562-563, 9746-10019 |
| `docs/countdown-delta-handoff.md` | 1-66 |

**IMPLEMENTATION:** `RaceSession.PassCheckpoint` records only accepted ordered entries; finish copies splits into `CompletedLap` alongside `LapTime`, and reset clears attempt splits. `BestLapRepository` validates and defensively copies the checkpoint-count-sized split array. Repository and JSON storage constructors now require the configured checkpoint count. JSON format 3 adds splits and ignores versions 1/2.

`DeltaCalculator.AtCheckpoint` and `AtFinish` return nullable current-minus-best seconds. `RaceManager.CrossTrigger` computes the final delta before `TrySave`; a first finish stays blank. `StartSequence` handles configurable number/GO durations, reset, nonfinite/negative inputs, and ticks spanning phases. Its boundary tolerance accounts for Unity reporting the 0.02 fixed step as 0.0199999921. Core and its test assembly retain `noEngineReferences: true`; the nested Storage test assembly handles the Unity JSON adapter.

`CarController.InputEnabled` uses the existing stop path until GO; physics initialization, serialized handling defaults, and driving equations match the baseline exactly. HUD formats signed invariant three-decimal deltas (including the negative sign when rounding to zero), colors them, holds for two seconds, and fades over the third. Countdown uses the sequence label. The builder wires both new TMP objects. `Main.unity` receives only those builder-generated objects, canvas children, and HudView references; every other serialized scene document is unchanged.

**VERIFICATION:** Unity 6000.6.4f1, final exact-source EditMode suite: **107 passed, 0 failed, 0 skipped; exit code 0**. Independent read-only code review found no material issue. Core also passed 72 cases in a standalone NUnit run. The initial split regression and the native fixed-step boundary regression were observed failing before their fixes. Final tests include existing geometry/GPU render checks, saved-scene HUD wiring, input locking, reset, culture-independent formatting/fading, previous-best/first-finish behavior, split validation/copying, and JSON version compatibility.

Exact final suite command (full Windows path avoids the temporary-project short-path script-import issue):

```powershell
$verificationRoot = 'C:\Users\Nate Tran\AppData\Local\Temp\Ghostline-Countdown-Verification-5676666e9ac2479c8da6f8e7262b563a'
$testProcess = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' `
    -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
        '-batchmode', '-runTests', '-testPlatform', 'EditMode',
        '-projectPath', ('"' + $verificationRoot + '"'),
        '-testResults', ('"' + $verificationRoot + '/final.xml"'),
        '-logFile', ('"' + $verificationRoot + '/final.log"')
    )
$testProcess.ExitCode # 0
[xml]$results = Get-Content -LiteralPath (Join-Path $verificationRoot 'final.xml')
$results.'test-run' | Select-Object result,total,passed,failed,skipped
git diff --check -- Assets/Ghostline README.md SCENE_SETUP.md docs/countdown-delta-handoff.md
```

Scoped source/document whitespace check passed. Scene additions passed a whitespace check against the pre-task scene; a full workspace diff check still reports pre-existing Unity blank-field spaces from the earlier scene changes. Source/verification asset contents were compared before handoff. Evidence is copied to ignored `Logs/CountdownDelta-EditMode.xml` and `Logs/CountdownDelta-EditMode.log`; the original scene snapshot remains at the verification root as `Main.before-countdown.unity`.

**OPEN ITEMS:** Interactive Play-mode review of the countdown and delta remains unverified. Changes are uncommitted for Claude review; no new runtime dependency or car-physics change.
