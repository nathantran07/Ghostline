# Ghostline

A top-down Unity 2D time-trial game with a Suzuka-inspired circuit and a ghost replay of your best lap.

![Ghostline gameplay](docs/images/gameplay.gif)

[Watch the demo video (with sound)](https://youtu.be/h-XSCS6nqCQ)

**Build placeholder:** Download a playable build (GitHub Release link goes here).

## Features

- Spline-generated, Suzuka-inspired figure-8 track with kerbs and grass ribbons.
- Translucent ghost replay of the best completed lap.
- Minimap with player and ghost markers.
- F1-style five-light start sequence.
- Synthesized Aventador-inspired V12 engine audio.
- Generated trackside decor, including trees, grandstands, tire stacks, and banners.
- Local best-lap persistence, including replay samples and checkpoint splits.
- Best lap and ghost clearing with hold progress, a centered prompt, and timed result messages.

## Screenshots

![Render of the Ghostline start/finish area with trackside decor, player car, ghost, HUD, and minimap](docs/images/hero.png)

*Render of the game scene in an isolated project copy, with a posed player and an in-memory ghost fixture. This is not live gameplay footage.*

## Controls and how to run

| Key | Action |
| --- | --- |
| W / Up | Accelerate |
| S / Down | Brake, then reverse |
| A / Left | Steer left |
| D / Right | Steer right |
| R | Restart the attempt and start sequence; retain the saved best lap |
| Delete (hold 1 second) | Open the clear-best confirmation prompt; releasing early cancels the hold |
| Y | Confirm clearing the saved best lap and ghost while the prompt is open |
| N / Esc | Cancel the clear-best prompt; it also cancels automatically after 5 seconds |
| M | Mute or unmute player audio |

Holding Delete shows a progress bar near the center of the screen. Once the prompt opens, a thin bar shows the remaining confirmation time. Success or failure appears as a brief centered message. The attempt continues underneath, and a failed delete keeps the saved best and ghost.

Use **Unity 6000.6.4f1**, as recorded in `ProjectSettings/ProjectVersion.txt`. The project uses the **Universal 2D template** and includes its package and rendering settings.

The repo uses **Git LFS for sprite and image assets**. Install Git LFS and run `git lfs install` **before cloning**. Without LFS, Git can leave text pointer files in place of the images, so the art appears missing. Alternatively, use a playable release build once the build link above is filled in.

```shell
git lfs install
git clone https://github.com/nathantran07/Ghostline.git
cd Ghostline
git lfs pull
```

1. In Unity Hub, choose **Add > Add project from disk**, select the cloned project folder, and open it with Unity **6000.6.4f1**. Wait for package resolution and compilation.
2. Open **Assets/Scenes/Main.unity**. The playable scene and TMP Essential Resources are included; no scene rebuild is needed.
3. Press **Play** and click the **Game** view for keyboard focus. Wait for the five red lamps to go out, then cross the start line to begin the lap clock.
4. Pass all twelve checkpoints in order and cross the finish line. The attempt stops at the finish; press **R** to race again against your saved best.

The supplied project uses **Input System Package (New)**. If changing project settings, keep **Active Input Handling** set to **Input System Package (New)** or **Both**. See [SCENE_SETUP.md](SCENE_SETUP.md) for optional scene rebuilding, installers, and Inspector settings. Rebuilding replaces saved scene edits after confirmation.

## Architecture

**Ghostline.Core** (`Assets/Ghostline/Core`) contains pure C# game logic: lap timing, ordered checkpoints, ghost recording and interpolation, race state, best-lap validation, checkpoint deltas, start sequencing, driving calculations, track offset geometry, minimap projection, and audio synthesis. Its assembly has `noEngineReferences: true`, so this logic can be tested without Unity objects or the engine. Storage is accessed through `IBestLapStorage`, keeping persistence rules separate from the file adapter.

**Ghostline.Game** (`Assets/Ghostline/Game`) connects Core to Unity. It contains input and Rigidbody2D integration in `CarController`, race coordination in `RaceManager`, ghost and HUD views, camera following, spline sampling and generated track/decor meshes, minimap UI, audio playback, and the JSON file storage adapter. Unity-specific rendering, physics, and lifecycle code stays in this assembly.

**Ghostline.Editor** (`Assets/Ghostline/Editor`) contains the scene builder and additive audio, minimap, and theme installers exposed under **Tools > Ghostline**. These editor tools build and wire the Unity components; the additive installers support updating an existing scene without replacing it.

**Ghostline.Tests.EditMode** (`Assets/Ghostline/Tests/EditMode`) contains engine-free NUnit tests against Core, following the same `noEngineReferences: true` boundary. **Ghostline.Tests.Storage** (`Assets/Ghostline/Tests/EditMode/Storage`) tests the Unity JSON file adapter, while **Ghostline.Tests.Scene** (`Assets/Ghostline/Tests/Scene`) tests scene wiring, component integration, installers, geometry, and rendered output. All three appear in Unity's EditMode Test Runner.

## Testing

The suite contains **475 EditMode test cases**. Open **Window > General > Test Runner > EditMode > Run All**. Some scene-builder and Play integration cases require batch mode and are skipped in an interactive run; use the command below for the complete suite. Graphics must remain enabled because several tests render previews.

From the project root in PowerShell, using the default Windows Unity Hub installation path:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe' `
  -batchmode -projectPath (Get-Location).Path `
  -runTests -testPlatform EditMode `
  -testResults "$env:TEMP\ghostline-editmode.xml" `
  -logFile "$env:TEMP\ghostline-editmode.log"
```

Adjust the Editor path for your installation. Run this in a disposable project copy to keep generated previews and scene-building checks separate from your working scene. Do not add `-nographics`.

Tests cover timing and checkpoint rules, replay interpolation, save validation and compatibility, best-lap clearing and confirmation controls, driving calculations, start sequencing and deltas, minimap projection and wiring, generated track/decor clearance, scene installers, car and gantry rendering, and synthesized audio.

## Credits

Vehicle sprites: '2D Top-Down Vehicles Assets' by Turbo Developement Team (https://turbo-developement-team.itch.io/2d-top-down-vehicles), free to use per the author's page.

All audio is synthesized in code. No recorded audio is included.
