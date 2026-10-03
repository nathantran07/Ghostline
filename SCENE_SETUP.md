# Ghostline scene setup

Use the automatic builder first. The manual steps below produce the same scene without the builder. All coordinates and sizes are in Unity world units unless they describe the HUD.

## Automatic setup

1. In Unity Hub, open `X:\Unity Projects\Ghostline` with **6000.6.4f1**, the version recorded in `ProjectSettings/ProjectVersion.txt`. Wait for compilation. Open **Window > General > Console** and resolve any red compilation errors before continuing.
2. Open **Edit > Project Settings > Player > Other Settings > Configuration**. Verify **Active Input Handling** is **Input System Package (New)** or **Both**. Accept an Editor restart if Unity requests one. The scripts use `UnityEngine.InputSystem.Keyboard.current`, without action assets.
3. Open **Window > TextMeshPro > Import TMP Essential Resources**. Click **Import** in the package window and wait for the progress indicator to finish. Confirm `Assets/TextMesh Pro/Resources/TMP Settings.asset` and `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` exist. Skipping this step causes missing/broken HUD text; the builder refuses to build until the settings and default font are available. TMP Examples & Extras are unnecessary.
4. Stop Play mode if it is active. Choose **Tools > Ghostline > Build Scene**. Save your current scene if prompted. If Main already exists, **Rebuild** replaces its previous objects and Inspector edits. The result opens as `Assets/Scenes/Main.unity` with all script fields assigned.
5. Inspect the rectangles. If they are black or invisible because of a light-dependent default, apply the **Sprite-Unlit-Default** fallback below. Do not add lights or create materials.
6. Press **Ctrl+S** to preserve any manual material changes. Press **Play**, click inside **Game**, then hold W/Up to cross the white start line. Follow the Play checks at the end of this guide.

## Manual fallback

1. Perform automatic steps **1–3** first, including TMP Essential Resources. Check the Console before creating objects.
2. Choose **File > New Scene**, choose **Empty**, and create it. An empty scene prevents template lights from being included. Save it as **`Assets/Scenes/Main.unity`** with **File > Save As**; create the Scenes folder in Assets if needed.
3. In the Hierarchy, choose **Create Empty**, name it **Ghostline**, and use the Transform component's menu > **Reset**. Keep this parent at position `(0,0,0)`, rotation `(0,0,0)`, and scale `(1,1,1)`. Create the following hierarchy using empty objects, except the camera and Canvas described later:

```text
Ghostline
  Track
    Road
    Infield
    Walls
      Outer Top
      Outer Bottom
      Outer Left
      Outer Right
      Inner Top
      Inner Bottom
      Inner Left
      Inner Right
  Checkpoints
    Start Finish
    Checkpoint 1
    Checkpoint 2
    Checkpoint 3
    Checkpoint 4
  Car
  Ghost
  Main Camera
  RaceManager
  HUD Canvas
    Current Lap
    Best Lap
    Status
```

4. Reset **Track**, **Walls**, **Checkpoints**, and **RaceManager** transforms as well. Keep their scale at 1 so child sizes stay correct. Gameplay objects stay at **Z = 0**; the camera alone is at **Z = -10**.
5. For each rectangle (**Road**, **Infield**, eight walls, five gates, **Car**, **Ghost**), select the empty object and use **Add Component > Sprite Renderer**, then **Add Component > Solid Sprite**. The script is `Assets/Ghostline/Game/SolidSprite.cs`. Do not choose Unity's Square sprite asset or assign a sprite file. SolidSprite generates its Sprite automatically in Edit mode and Play mode.
6. Set rectangle color, **Size**, and **Sorting Order** on **Solid Sprite**, using the tables below. Use its Size field rather than editing Transform Scale; the component owns scale. Keep **Sprite Renderer > Sorting Layer** as **Default** and preserve the supplied material. Color hex values below are approximations of the builder's float colors; set the indicated alpha separately in the color picker (A is shown as either 0–1 or 0–255 depending on the Editor).
7. Configure the road and infield:

| Object | Local position XYZ | Solid Sprite Size XY | Color | Alpha | Sorting Order |
| --- | --- | --- | --- | --- | --- |
| Road | `(0,0,0)` | `(24,16)` | `#2B3340` | 1 | 0 |
| Infield | `(0,0,0)` | `(16,8)` | `#0E1217` | 1 | 1 |

Road and Infield need no colliders. The inner wall perimeter encloses the infield.

8. Configure all eight walls with color **`#6B788A`**, alpha **1**, and Sorting Order **3**:

| Wall | Local position XYZ | Solid Sprite Size XY |
| --- | --- | --- |
| Outer Top | `(0,8,0)` | `(24.7,0.5)` |
| Outer Bottom | `(0,-8,0)` | `(24.7,0.5)` |
| Outer Left | `(-12,0,0)` | `(0.5,16.5)` |
| Outer Right | `(12,0,0)` | `(0.5,16.5)` |
| Inner Top | `(0,4,0)` | `(16.5,0.5)` |
| Inner Bottom | `(0,-4,0)` | `(16.5,0.5)` |
| Inner Left | `(-8,0,0)` | `(0.5,8.5)` |
| Inner Right | `(8,0,0)` | `(0.5,8.5)` |

9. Add **Box Collider 2D** to every wall. Set collider **Size `(1,1)`**, **Offset `(0,0)`**, and leave **Is Trigger unchecked**. SolidSprite's scale expands the unit collider to the rectangle dimensions. Do not add Rigidbody2D to walls: a collider with no rigidbody is a static wall. The overlapping wall ends close the corners.
10. Configure **Car**: position **`(-2,-6,0)`**, rotation **`(0,0,-90)`**, Solid Sprite Size **`(0.8,1.4)`**, color **`#1ACCF2`**, alpha **1**, Sorting Order **5**. Local up is the car's forward axis; Z rotation -90 makes it face right along the bottom lane.
11. Add **Box Collider 2D** to Car, with Size **`(1,1)`**, Offset **`(0,0)`**, and **Is Trigger unchecked**. Add **Rigidbody 2D** and set:

| Rigidbody 2D field | Value |
| --- | --- |
| Body Type | Dynamic |
| Simulated | Checked |
| Gravity Scale | 0 |
| Mass | 1 |
| Linear Damping | 1.2 |
| Angular Damping | 8 |
| Interpolate | Interpolate |
| Collision Detection | Continuous |
| Constraints | None |

12. Add **Car Controller** (`Assets/Ghostline/Game/CarController.cs`) to Car. Keep **Speed 12**, **Acceleration 18**, **Steering 150**, **Grip 12**, **Linear Damping 1.2**, **Angular Damping 8** initially. Higher Grip removes sideways sliding faster. Lower Speed or higher Steering makes corners easier. Change the script's damping fields when tuning, because it applies those values to the rigidbody in Awake.
13. Configure **Ghost** at **`(-2,-6,0)`**, rotation **`(0,0,-90)`**, Solid Sprite Size **`(0.8,1.4)`**, color **`#CCF2FF`**, alpha **0.3** (about **77/255**), Sorting Order **4**. Add **Ghost Car View** (`Assets/Ghostline/Game/GhostCarView.cs`). Give Ghost **no collider and no Rigidbody2D**. It may be visible in Edit mode; GhostCarView hides it at Play startup until a valid saved replay begins.
14. Configure the five gates. All use Sorting Order **2**, Z position **0**, Z rotation **0**. Start Finish is white with alpha **1**; checkpoint gates use **`#FFA626`**, alpha **0.7** (about **179/255**):

| Gate | Local position XYZ | Solid Sprite Size XY | Is Start Finish | Checkpoint Index | Forward Direction XY |
| --- | --- | --- | --- | --- | --- |
| Start Finish | `(0,-6,0)` | `(0.25,4)` | Checked | 0 (unused) | `(1,0)` |
| Checkpoint 1 | `(10,0,0)` | `(4,0.25)` | Unchecked | 0 | `(0,1)` |
| Checkpoint 2 | `(0,6,0)` | `(0.25,4)` | Unchecked | 1 | `(-1,0)` |
| Checkpoint 3 | `(-10,0,0)` | `(4,0.25)` | Unchecked | 2 | `(0,-1)` |
| Checkpoint 4 | `(-5,-6,0)` | `(0.25,4)` | Unchecked | 3 | `(1,0)` |

15. Add **Box Collider 2D** to each gate. Set Size **`(1,1)`**, Offset **`(0,0)`**, and **check Is Trigger**. Add **Checkpoint Trigger** (`Assets/Ghostline/Game/CheckpointTrigger.cs`) and set the fields from the gate table. The Forward Direction is a world-space movement direction; it prevents backward entries from advancing the race. Leave the **Race Manager** reference empty temporarily; assign it in step 22.
16. Create a camera with **GameObject > Camera**, name it **Main Camera**, and parent it under Ghostline. Remove any other cameras from this scene. Set its tag to Unity's built-in **MainCamera**. Keep just one **Audio Listener**, on this camera. Set Transform position **`(-2,-6,-10)`** and rotation **`(0,0,0)`**. On **Camera**, set **Projection: Orthographic**, **Size: 9**, and **Background Type: Solid Color** (shown as **Clear Flags: Solid Color** in some versions). Set background color **`#090B11`**, alpha 1. Use the project's default renderer, the template's 2D Renderer; do not change rendering assets.
17. Add **Camera Follow** (`Assets/Ghostline/Game/CameraFollow.cs`) to Main Camera. Drag **Car** from the Hierarchy into **Target**. Keep **Smoothing 5**. The component maintains Z = -10 while following the car; an initial snap occurs when the attempt resets.
18. Create **GameObject > UI > Canvas**, name it **HUD Canvas**, and parent it under Ghostline. Set **Canvas > Render Mode: Screen Space - Overlay**. Set **Canvas Scaler > UI Scale Mode: Scale With Screen Size**, **Reference Resolution `(1600,900)`**, and **Match 0.5**. If Unity also created an **EventSystem**, delete that object: this HUD has no buttons and needs no UI input module or action assets. A Graphic Raycaster is unnecessary and can be removed.
19. Under HUD Canvas, create three **UI > Text - TextMeshPro** objects and name them **Current Lap**, **Best Lap**, and **Status**. They must be UI text (`TextMeshProUGUI`), not 3D text. On each Rect Transform, set **Anchor Min `(0,1)`**, **Anchor Max `(0,1)`**, and **Pivot `(0,1)`**. Set **Width 1550**, **Height 42**, and Z **0**. Use the following X/Y anchored positions and text settings:

| Object | Anchored position XY | Font Size | Initial text |
| --- | --- | --- | --- |
| Current Lap | `(24,-24)` | 28 | `Lap  0.00 s` |
| Best Lap | `(24,-68)` | 28 | `Best  --` |
| Status | `(24,-112)` | 22 | `Cross the white line to start \| WASD / arrows \| R: restart` |

Assign **LiberationSans SDF** as Font Asset on all three. Set text color to white, alignment **Top Left**, wrapping **No Wrap/disabled**, and **Raycast Target unchecked**. The font is supplied by TMP Essential Resources. If it is unavailable, return to the TMP import step.

20. Add **Hud View** (`Assets/Ghostline/Game/HudView.cs`) to **HUD Canvas**. Drag **Current Lap** into **Current Time Text**, **Best Lap** into **Best Time Text**, and **Status** into **Status Text**. Unity selects the TMP component from each dragged object.
21. Add **Race Manager** (`Assets/Ghostline/Game/RaceManager.cs`) to the **RaceManager** object. Assign every field:

| Race Manager Inspector field | Drag or enter |
| --- | --- |
| Car | Car object with CarController |
| Ghost | Ghost object with GhostCarView |
| Hud | HUD Canvas object with HudView |
| Camera Follow | Main Camera object with CameraFollow |
| Spawn Position | `(-2,-6)` |
| Spawn Rotation | `-90` |

22. Select each of the five gates and drag the **RaceManager** object into **Checkpoint Trigger > Race Manager**. All five must reference the same RaceManager. Recheck the index, start/finish flag, trigger checkbox, and Forward Direction against the table.
23. Leave gameplay objects **Untagged** on the **Default** layer, except the MainCamera tag on the camera. No new tags or layers are required. Keep the default 2D collision matrix with Default colliding with Default (**Edit > Project Settings > Physics 2D**). The car is identified by its component, not a Player tag. There should be **one Rigidbody2D**, **eight solid wall colliders**, **one solid car collider**, and **five trigger colliders**.
24. Confirm **no Light 2D component exists in Main**. Do not add a Global Light 2D or other lights. Inspect all rectangle materials using the fallback below if necessary. Press **Ctrl+S** to save the scene, then run the Play checks.

## Black or missing rectangles: approved material fallback

The scripts and builder never assign sprite materials. A URP 2D default may use lighting and display rectangles black when there are no lights. Correct this in the Inspector with Unity's supplied unlit sprite material:

1. Stop Play mode so the change is saved to the scene. Select a rectangle such as **Car** in the Hierarchy.
2. Expand its **Sprite Renderer > Materials** section. Beside **Element 0**, click the small circle to open the material picker. Some Inspector layouts display a single **Material** field instead of the Materials list.
3. Search **`Sprite-Unlit-Default`**. Select the existing material supplied by **Universal Render Pipeline / 2D**. Use the picker tab that includes package/project assets if it is hidden by a filter. Do not choose the Built-in Render Pipeline's `Sprites/Default`, and do not create a new material or shader.
4. Repeat for Road, Infield, all eight walls, all five gates, Car, and Ghost. You can select multiple rectangle objects in the Hierarchy and change their common SpriteRenderer material together. Tint and ghost transparency still come from SolidSprite.
5. Press **Ctrl+S** and enter Play. Keep lights absent. Rebuilding Main recreates the objects with defaults, so apply this fallback again after a rebuild when needed.

If a rectangle is missing rather than black, check that SolidSprite and SpriteRenderer are enabled, Size is positive, color alpha is nonzero, and the camera can see the object's position. Do not fix a missing sprite by assigning an asset: SolidSprite should recreate it automatically after loading and on Play. Ghost is intentionally hidden before a saved attempt begins.

## Play and persistence checks

1. Press **Play** and click the **Game** view. Confirm the HUD is legible and the car and track are visible. Current time should read zero before crossing the white line. On the first run, best should read `--` and the ghost should be hidden.
2. Hold **W/Up** to drive right across the white line. The timer should start. Hold **A/Left** while moving to steer upward at the right corner; slow down before turning. Drive up the right lane, left along the top, down the left, then right along the bottom.
3. Cross gates **1, 2, 3, 4** in that order. Check the HUD's next-checkpoint status after each. Out-of-order or backward gate entries should not advance it. Each corner along this route is a left turn; steering reverses when backing up.
4. Cross the white finish line moving right after all four gates. Time should freeze, best should update if faster, and the car should stop. Press **R**: the car returns to spawn with zero velocity, current time resets, and best remains.
5. Begin the next attempt. The translucent ghost should appear at the start and move along the saved best. It has no collider and cannot push the player. A slower lap should leave best unchanged.
6. Stop Play, then press Play again and begin an attempt. Best and the ghost should survive because the save is on disk. A corrupt file should result in best `--` and no ghost, rather than a game crash. Read the Console if a save fails; the HUD also reports save failure.
7. With Play stopped, the current save is `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Ghostline\ghostline-best-lap.json`. To clear best, delete that file only. Changing Company Name or Product Name changes the persistent data folder. Do not edit or delete project caches to reset laps.
8. Stop Play and run **Window > General > Test Runner > EditMode > Run All**. Expect **24 passed, 0 failed**. These tests verify Core rules; the preceding checks verify scene/input/physics presentation.
9. For an executable, open **File > Build Profiles**. Add the open **Main** scene to the profile's **Scene List** using **Add Open Scenes**, make Main the first enabled scene, and disable/remove SampleScene from that profile's list. Build into an ignored **Build/** or **Builds/** folder. The builder does not modify your build profile.
10. When committing Editor-created content, include `Assets/Scenes/Main.unity`, its `.meta`, and imported `Assets/TextMesh Pro` resources with their `.meta` files. Keep the repository's existing `.gitignore` and Git LFS `.gitattributes`; there is no need to add cache files or generated `.csproj`/`.sln` files.
