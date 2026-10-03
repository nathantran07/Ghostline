# Ghostline scene setup

## Build the scene

1. Open the project in Unity **6000.6.4f1**. Wait for package resolution and compilation. `Packages/manifest.json` includes **Unity Splines 2.8.4**.
2. If TMP resources are missing, use **Window > TextMeshPro > Import TMP Essential Resources**. The builder validates the font before replacing the scene.
3. Stop Play mode and choose **Tools > Ghostline > Build Scene**. Confirm replacement of `Assets/Scenes/Main.unity`. Save personal scene edits elsewhere before rebuilding.
4. Open **Main** and press Play. The player starts behind the upper-right start/finish line, heading down-right into corner 1. Click the Game view for keyboard input.

The track has a generated racing visual layer: dark green grass, charcoal asphalt, white edge paint, inside-corner red/white curbs, metal barriers, outside-corner tire walls, outlined grid slots, sector paint, and a two-row checkered finish strip. These meshes add no colliders or race rules. The README preview screenshot predates this track and the Lambo change.

## Add the minimap to your existing scene

1. Open your edited Main scene and stop Play mode. Use **Tools > Ghostline > Add Minimap To Scene** to preserve the existing track and scene edits. Do not use **Build Scene** for this installation: that command still replaces Main.
2. The installer creates **HUD Canvas/Minimap**, containing a **Track** RawImage with **Ghost Dot** and **Player Dot** Images. It wires the track, car, ghost, race, and UI references automatically. Re-running it reuses the panel/dots, repairs a missing dot, and preserves MinimapView tuning. Installation supports Undo and marks the scene dirty; save Main yourself.
3. Select **HUD Canvas/Minimap** and tune the serialized **MinimapView** fields below. The existing Canvas Scaler controls UI scale. Default placement is bottom-left, away from the top-left lap time and ghost delta.

| MinimapView setting | Default | Meaning |
| --- | --- | --- |
| Height Fraction / Panel Offset | 0.2 / (24, 24) | Square panel height as a fraction of canvas height; offset in scaled canvas units from bottom-left |
| Texture Resolution / Padding | 256 / 16 | Square runtime texture resolution; padding in texture pixels, leaving room for ticks and dots |
| Line Width / Tick Width / Tick Length | 3 / 3 / 12 | Texture pixels; line edges use simple distance-based smoothing |
| Player Dot Size / Ghost Dot Size | 8 / 7 | Dot diameters in scaled canvas units |
| Outline Width | 1.5 | Player outline distance in scaled canvas units |
| Background Color | Dark blue-black, alpha 0.65 | Panel tint and opacity |
| Track Color / Tick Color | Light gray / white | Centerline and start/finish marker colors |
| Player Color / Outline Color | Yellow / dark, alpha 1 | Bright player with a dark outline |
| Ghost Color | Cyan-blue, alpha 0.5 | Distinct translucent ghost dot |

Keep Padding below half the texture resolution and large enough for half the tick length plus its line width. Appearance settings for the generated track texture are read at Play startup; exit and re-enter Play after changing them. Panel and dot settings are applied each frame. The texture is drawn once from `TrackGenerator.Samples`; runtime track edits require restarting Play to rebuild the minimap. Positions outside the track bounds clamp to the padded panel rectangle.

With a valid saved ghost, its dot appears at the configured spawn during **3/2/1/GO** and the approach to the start line, then follows playback after the first forward crossing. It hides when the recording ends or the attempt finishes, and returns to spawn after **R**. No recording means no ghost dot. The world ghost still remains hidden before the lap starts. The panel uses Unity's default UI material and built-in dot sprite; its only new texture is runtime-generated. No minimap shaders or material assets need assignment. Fresh **Build Scene** runs include the minimap automatically.

Run **Window > General > Test Runner > EditMode** and select **MinimapViewTests** / **MinimapInstallerTests**, or **Run All**. The 14 new scene cases need no scene installation, saved ghost, batch mode, TMP-resource import, or extra test assembly setup. They clean up their own objects and preview scene. The 23 projection cases live in **MinimapProjectionTests** under the engine-free Core test assembly.

Manual Play review: check that the line/tick match the track orientation, both dots align with the centerline, the player outline stays legible, and the bottom-left panel avoids your HUD edits. Resize the Game view to check scaling. Check no-ghost, countdown spawn, running playback, playback end, finish, and **R** restart. Automated checks do not replace this visual review.

## Track geometry and tuning

`Assets/Ghostline/Editor/GhostlineSceneBuilder.cs` holds **SuzukaNormalizedKnots**, the sole hand-tunable knot array. It traces the colored centerline in `docs/reference/suzuka-layout.png`, following corners 1-18 from the checkered flag. Corner comments identify each group. X is pixel X divided by 1280; Y is one minus pixel Y divided by 720. The builder restores the image aspect ratio when mapping normalized points to world coordinates.

**Track** has a closed **SplineContainer** and **TrackGenerator**. The generator lives in `Assets/Ghostline/Game/TrackGenerator.cs`; the Game assembly owns sampling, meshes, collision boundaries, radius checks, and checkpoint placement. The builder supplies knots and connects the race objects.

| TrackGenerator setting | Default | Meaning |
| --- | --- | --- |
| Road Width | 3.4 | Requested width; tight corners narrow locally |
| Wall Thickness | 0.2 | Wall mesh width and twice the EdgeCollider2D edge radius |
| Sample Count | 2048 | Even arc-length samples around the closed loop |
| Checkpoint Count | 12 | Ordered checkpoints in addition to start/finish |
| Radius Margin | 0.3 | Clearance between the full inner wall and the local bend radius |
| Offset Smoothing Length | 6 | Distance over which local width restrictions spread into adjacent samples |
| Road Color | RGB 43/51/64 | Serialized dark asphalt tint; existing road geometry is retained |

The normalized image maps to 300 world units and the centerline measures about 724 units. The default curve and drag balance near 17.1 units/s, without clamping speed. Tight corners narrow locally using Core offset safety and closed-loop smoothing. Console messages name each limited arc-length interval and its minimum width. Retained wall intersections receive further local reductions. Actual lap time needs a driven measurement.

The road is one closed mesh strip. Visible walls are mesh strips over **EdgeCollider2D** polylines. All use the project's supplied URP **Sprite-Unlit-Default** material, with flat vertex colors and no lights. Meshes are transient and rebuilt on scene load; colliders and gates remain serialized in Main. Sample spacing must be no greater than a quarter road width.

**TrackVisuals**, in `Assets/Ghostline/Game/TrackVisuals.cs`, owns presentation only and is called by `TrackGenerator.RebuildMeshes`. It combines each visual type into one static mesh, including disconnected strips on both sides. It uses `TrackGenerator.InsideOtherRoad` directly for crossover clearance; a conservative spatial index narrows candidate segments without changing the wall corridor or adjacency rule. Runs wrap across knot zero when appropriate, split at gaps or turn-direction changes, and must satisfy minimum arc length. Stripes are subdivided at exact distances rather than rounded to sample indices.

| TrackVisuals setting | Default | Meaning |
| --- | --- | --- |
| Grass Color / Margin / Screen Height | RGB 9/24/13 / 18 / 18 | One ground quad; margin is at least the larger of the configured height and the Main Camera's current orthographic height, on every side |
| White / Red / Black | White / RGB 217/11/10 / RGB 4/4/4 | Paint, alternating curb/tire blocks, and checker colors |
| Edge Width / Inset | 0.15 / 0.05 | White edge strip fully on the asphalt |
| Curb Width / Curvature Threshold | 0.5 / 0.035 | Inner-side curb; signed curvature magnitude must exceed threshold, in inverse world units |
| Curb Minimum Run Length / Stripe Length | 1.5 / 1 | Drop short runs; alternate red/white every unit of arc length |
| Barrier Color / Center Color | RGB 166/173/179 / RGB 64/71/77 | Metal strip with a dark center stripe |
| Barrier Gap / Width / Center Width | 0.05 / 0.4 / 0.04 | Begins beyond the existing collider's outer radius |
| Tire Width / Curvature Threshold | 0.8 / 0.13 | Outer side of tighter turns, immediately outside the barrier; threshold must exceed the curb threshold |
| Tire Minimum Run Length / Block Length | 1.5 / 1.2 | Drop short runs; alternate white/red blocks by arc length |
| Checker Depth | 1 | Two rows of square checks, clipped at the road edges |
| Grid Slot Length / Width / Outline Width | 1.4 / 0.7 / 0.04 | Car-sized outlined rectangles; paint stays within these footprints |
| Grid Row Spacing / Stagger | 1.5 / 0.75 | Multiples of car length for same-column spacing and the second column's offset |
| Front Grid Distance / Curvature Limit | 4 / 0.015 | Begin behind finish; stop at the first corner, wall conflict, or crossover |
| Sector 1 / 2 / 3 Color | RGB 228/17/115 / 255/218/0 / 67/148/221 | Pink/yellow/blue sampled from `docs/reference/suzuka-layout.png` |
| Sector Line Width / Corner 8 Knot / Corner 15 Knot | 0.15 / 30 / 65 | Thin full-width paint at start and the nearest existing gates to those corner anchors |

Sorting orders: grass **-7**; barriers/tire walls **-6**; road **-5**; edge lines **-4**; curbs **-3**; grid **-2**; sectors **-1**; checker **0**. Existing checkpoint sprites **2**, wall surface **3**, ghost **4**, player **5**, and screen-space HUD retain their orders. The plain start sprite and the two replaced sector-boundary sprites are hidden; their transforms, trigger dimensions, indices, and wiring are retained. The pink start sector line has a visible leading edge beside the checker.

The crossover is a flat intersection. Both spline passes continue straight. Wall samples inside another road corridor are omitted when their separation along the loop exceeds three road widths. Each omission splits the wall into open polylines; collider ends cannot bridge the intersection.

Checkpoints are evenly spaced by arc length, then nudged clear of the crossover. Both arcs between the crossover passes contain at least one required gate. Each gate spans the full road width, is perpendicular to its tangent, and filters backward crossings using that tangent. Start/finish sits at knot zero. Player and ghost spawn in the front grid slot four units behind it, laterally offset into the first column; the race reset pose and camera snap use the same pose. The current short approach fits **one slot** before the previous bend at the default curvature limit, so generation logs the required warning. The generator paints up to ten slots when the available straight permits them and checks the full rectangle, not just its center. If none fit, it warns and uses the existing centerline fallback behind the start.

For spline geometry, edit builder knots and rebuild. For width, edit TrackGenerator's Inspector settings and use its **Regenerate Track** context menu; width changes also regenerate walls and gates when the component enables. Road meshes, walls, triggers, grid columns, and reset spawn share effective width. Gate-count changes still require matching RaceManager configuration. Core remains free of Unity dependencies.

## Car sprite and physics

Keep `Assets/Ghostline/Game/Art/LAMBO.png` as a single centered Sprite: Full Rect, Bilinear, Clamp, no mipmaps, no compression, Max Size 2048, and Pixels Per Unit equal to source height divided by **1.4**. Build Scene reapplies those settings.

Each car root has unit scale and a **Visual** child at local position zero, unit scale, and local Z rotation **180**. The supplied PNG points down; `CarController.FixedUpdate()` drives along the root's local up. The root rotation follows the spawn tangent; it is no longer fixed at -90 degrees.

| Visual | Color / alpha | Sorting order | Material |
| --- | --- | --- | --- |
| Car/Visual | White / 1, preserving the yellow artwork | 5 | URP Sprite-Unlit-Default |
| Ghost/Visual | Light blue / 0.4 | 4 | `Assets/Ghostline/Game/Art/GhostSprite.mat` |

The existing ghost shader recolors the yellow artwork blue while preserving details. No car uses SolidSprite. Car retains a **BoxCollider2D** size `(0.55,1.18)` with offset `(0,0.08)`, plus a dynamic **Rigidbody2D**: gravity 0, damping 0/8, Interpolate, Continuous. The ghost has no collider or rigidbody.

`CarController` defaults: Top Speed reference 18, Max Acceleration 18, Throttle Ramp Time 0.4, Drag 0.85, Engine Braking 1.5, Brake Acceleration 6, Reverse Acceleration 8, Reverse Threshold 0.3, Steering 150, Minimum Steering Speed 2, Grip 12, Angular Damping 8, and Wall Speed Loss 0.35. Rigidbody linear damping is zero because the controller supplies drag force. Accel Curve uses speed / Top Speed on X and acceleration multiplier on Y; Steering Curve uses absolute forward speed / Top Speed and turn-rate multiplier. W/Up ramps throttle up; releasing ramps it down. S/Down takes braking priority and powers reverse near rest. Wall contacts retain 65% of incoming speed by default, including head-on impacts.

## Scene wiring

Main contains **Ghostline/Track/Generated Circuit** (Road, Walls, Checkpoints, Grass, Barriers, Tire Walls, Edge Lines, Curbs, Grid, Sector Lines, Start Finish Checker), **Car**, **Ghost**, **Main Camera**, **HUD Canvas**, and **RaceManager**. There is no rectangular Infield or old box wall perimeter.

The orthographic camera uses Size 9, Z -10, the existing 2D renderer, and CameraFollow targeting Car. HUD remains screen-space overlay with LiberationSans SDF. RaceManager references the car, ghost, HUD, and camera; its serialized count and reset pose match the generated track. Gate indices are zero-based and reference that same manager. Use the Default layer and default 2D collision matrix; no new tags or layers are required.

**HUD Canvas** contains five TMP objects wired to HudView: **Current Lap** (`_currentTimeText`), **Best Lap** (`_bestTimeText`), **Status** (`_statusText`), **Countdown** (`_countdownText`), and **Ghost Delta** (`_deltaText`). The builder creates Countdown at the canvas center with centered alignment, font size 96, and a 400 x 160 rectangle. Ghost Delta sits beside Current Lap at top-left offset `(280,-24)`, font size 28, and starts hidden. Countdown shows 3/2/1/GO and hides when Done. Delta uses signed three-decimal seconds, green ahead, red behind, white at zero, and fades during the final second of a three-second display. Rebuild with **Tools > Ghostline > Build Scene** to add these objects to a scene created by an earlier builder.

If a road or gate is black, check its renderer material is **Sprite-Unlit-Default**, without adding lights. Car/Visual uses that material; Ghost/Visual uses GhostSprite. Generated road meshes are recreated by TrackGenerator.OnEnable. A missing mesh or radius error should be corrected in the generator or knots, rather than hidden with a material or collider workaround.

## Play and persistence checks

1. Start Play while holding throttle/steering: the car stays still through **3, 2, 1** (one second each). At **GO**, driving unlocks; GO hides after 0.75 seconds. The timer and world ghost remain inactive until the first forward checker crossing; the minimap ghost dot is already at spawn when a saved recording exists. The timer then starts and the HUD requests checkpoint 1 / 12.
2. Follow corners 1-18 and pass gates in order. Continue straight at both crossover visits. Turning there to skip a loop must leave an expected gate unpassed; backward and out-of-order entries must not advance progress.
3. Finish after all twelve gates. Time freezes, the best updates if faster, and driving stops. Press **R**: position, heading, velocity, timer, splits, and camera reset together; delta clears and the countdown restarts with driving locked.
4. Start another attempt. A valid saved best appears as a translucent blue Lambo, without collisions, only after the timer starts. Each accepted gate shows current split minus best split. Check signed three-decimal values and green/red colors, fading by about three seconds; rejected gate entries must not refresh the display. At finish, the delta uses the previous best even if the lap sets a new record. The first lap has no delta. A slower lap retains the best. Re-enter Play and check disk persistence.
5. Save format **6** includes version, track ID (`suzuka`), and ordered splits. Missing or mismatched identity/version, including version 5, quietly produces no best, ghost, or delta until a new valid lap is saved. Invalid splits and poses are also ignored. The default save path is `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Ghostline\ghostline-best-lap.json`; Company/Product settings can change it.
6. Run **Window > General > Test Runner > EditMode > Run All**. Builder tests require batch mode to bypass interactive scene-replacement dialogs. GPU tests require graphics; omit `-nographics`. Expect zero failed tests. Batch tests export ignored `Logs/LamboPreview.png`, `Logs/SuzukaPreview.png`, `Logs/RacingStartPreview.png`, and `Logs/RacingCornerPreview.png` for visual checks. Geometry tests also cover visual sorting, curvature-side selection, short-run rejection, crossover paint clearance, spatial-index equivalence to the exhaustive wall rule, complete grid footprints, a ten-slot clear approach, sector anchors, and mesh regeneration without collider/gate changes.
7. In **File > Build Profiles**, place Main first in the enabled Scene List and remove SampleScene. Build to an ignored Build/ or Builds/ directory. The builder leaves build profiles unchanged.

Commit source, `.meta` files, the rebuilt Main scene, and package manifest/lock changes. Preserve Git LFS and the existing ignore rules; omit caches, logs, and generated project files.
