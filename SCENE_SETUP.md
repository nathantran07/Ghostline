# Ghostline scene setup

## Open the supplied scene

The project uses Unity **6000.6.4f1** and the **Universal 2D template**. Run `git lfs install` before cloning and `git lfs pull` after cloning to retrieve sprite and image assets. Open **Assets/Scenes/Main.unity**, press Play, and click the Game view for keyboard focus. Wait for lights out, then cross the start line to begin the lap clock. The supplied scene and TMP Essential Resources are ready to use; rebuilding is optional.

## Rebuild the scene (optional)

1. Open the project in Unity **6000.6.4f1**. Wait for package resolution and compilation. `Packages/manifest.json` includes **Unity Splines 2.8.4**.
2. If TMP resources are missing, use **Window > TextMeshPro > Import TMP Essential Resources**. The builder validates the font before replacing the scene.
3. Stop Play mode and choose **Tools > Ghostline > Build Scene**. Confirm replacement of `Assets/Scenes/Main.unity`. Save personal scene edits elsewhere before rebuilding.
4. Open **Main** and press Play. The player starts behind the upper-right start/finish line, heading down-right into corner 1. Click the Game view for keyboard input.

The track has a generated racing visual layer: alternating mowed grass ribbons following the track, charcoal asphalt, white edge paint, inside-corner red/white curbs, metal barriers, outside-corner tire walls, outlined grid slots, sector paint, and a two-row checkered finish strip. These meshes add no colliders or race rules. The README hero is an isolated render of the game scene with a posed player and an in-memory ghost fixture.

## Add the minimap to your existing scene

1. Open your edited Main scene and stop Play mode. Use **Tools > Ghostline > Add Minimap To Scene** to preserve the existing track and scene edits. Do not use **Build Scene** for this installation: that command still replaces Main.
2. The installer creates **HUD Canvas/Minimap**, containing a **Track** RawImage with **Ghost Dot** and **Player Dot** Images. It wires the track, car, ghost, race, and UI references automatically. Re-running it reuses the panel/dots, repairs a missing dot, and preserves MinimapView tuning. Installation supports Undo and marks the scene dirty; save Main yourself.
3. Select **HUD Canvas/Minimap** and tune the serialized **MinimapView** fields below. The existing Canvas Scaler controls UI scale. Default placement is top-right, away from the top-left lap time and ghost delta, at 30% of canvas height (1.5 times the original side length). Existing installed panels retain their serialized height: set **Height Fraction = 0.30** to enlarge one that still uses 0.20, then re-run the installer to refresh its Editor layout and save Main.

| MinimapView setting | Default | Meaning |
| --- | --- | --- |
| Height Fraction / Panel Offset | 0.3 / (24, 24) | Square panel height as a fraction of canvas height; positive inset in scaled canvas units from the top and right edges |
| Texture Resolution / Padding | 256 / 16 | Square runtime texture resolution; padding in texture pixels, leaving room for ticks and dots |
| Line Width / Tick Width / Tick Length | 3 / 3 / 12 | Texture pixels; line edges use simple distance-based smoothing |
| Player Dot Size / Ghost Dot Size | 8 / 7 | Dot diameters in scaled canvas units |
| Outline Width | 1.5 | Player outline distance in scaled canvas units |
| Background Color | Dark blue-black, alpha 0.65 | Panel tint and opacity |
| Track Color / Tick Color | Light gray / white | Centerline and start/finish marker colors |
| Player Color / Outline Color | Yellow / dark, alpha 1 | Bright player with a dark outline |
| Ghost Color | Cyan-blue, alpha 0.5 | Distinct translucent ghost dot |

Keep Padding below half the texture resolution and large enough for half the tick length plus its line width. Appearance settings for the generated track texture are read at Play startup; exit and re-enter Play after changing them. Panel and dot settings are applied each frame. The texture is drawn once from `TrackGenerator.Samples`; runtime track edits require restarting Play to rebuild the minimap. Positions outside the track bounds clamp to the padded panel rectangle.

With a valid saved ghost, its dot appears at the configured spawn during the **start gantry and lights-out signal** and the approach to the start line, then follows playback after the first forward crossing. It hides when the recording ends or the attempt finishes, and returns to spawn after **R**. No recording means no ghost dot. The world ghost still remains hidden before the lap starts. The panel uses Unity's default UI material and built-in dot sprite; its only new texture is runtime-generated. No minimap shaders or material assets need assignment. Fresh **Build Scene** runs include the minimap automatically.

Run **Window > General > Test Runner > EditMode** and select **MinimapViewTests** / **MinimapInstallerTests**, or **Run All**. The 14 minimap scene cases need no scene installation, saved ghost, batch mode, TMP-resource import, or extra test assembly setup. They clean up their own objects and preview scene. The 23 projection cases live in **MinimapProjectionTests** under the engine-free Core test assembly.

Manual Play review: check that the line/tick match the track orientation, both dots align with the centerline, the player outline stays legible, and the larger top-right panel avoids your HUD edits. Resize the Game view to check scaling. Check no-ghost, countdown spawn, running playback, playback end, finish, and **R** restart. Automated checks do not replace this visual review.

## Synthesized player audio

The supplied scene includes synthesized Aventador-inspired V12 engine audio and procedural collision, countdown, and lap cues. All audio is generated in code; no recorded audio is included. **M** toggles mute while retaining configured volume levels. The ghost has no audio.

For a scene missing audio, stop Play and use **Tools > Ghostline > Add Audio To Scene**. The additive installer supports Undo, reuses existing player components, preserves custom audio settings, and marks the scene dirty without saving. Fresh **Build Scene** runs include audio. Keep exactly one AudioListener on the main camera.

Player audio settings live on **PlayerAudioSettings**, **PlayerAudioOutput**, **EngineAudio**, and **SfxPlayer**. The default engine uses the pulse voice; **Use Additive Voice** on EngineAudio provides a live A/B comparison with the previous harmonic voice. Other voice settings are captured on enable, so re-enable the component or restart Play after changing them.

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
| Show Grass / Show Edge Lines / Show Curbs | All on | Independent presentation toggles; disabled categories retain empty meshes and existing objects |
| Grass Band Color / Grass Alternate Color / Grass Base Color | #2F6B2F / #3A7D3A / #285C2A | Track-following ribbons over flat base grass; replaces obsolete horizontal-stripe fields without editing Main |
| Band Length / Grass Depth | 6 / 14 | Alternate by centerline arc length from distance zero; extend outward from each wall, reducing depth for tight corners and clearance |
| Grass Margin / Screen Height | 18 / 18 | One combined ground mesh; margin is at least the larger of the configured height and the Main Camera's current orthographic height, on every side |
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

For grass ribbons, keep your existing Main scene open, select **Ghostline/Track > TrackVisuals**, and enter Play. Adjust Band Length, Grass Depth, the three grass colors, and category toggles outside Play, then restart Play to regenerate transient visuals. Do not use **Build Scene** on your edited scene. New grass fields supply the new defaults without rewriting the scene's old stripe settings. Band length must be finite and positive, with at most 4096 bands around the lap; depth must be finite and nonnegative. Zero depth leaves only the base grass.

Grass uses the wall geometry helpers on separate offsets: inner-corner clamping, closed-profile smoothing using the existing track margin/smoothing settings, and local intersection reductions. Ribbons split at exact global arc-length boundaries, shorten the last band at the loop seam, and leave flat-base gaps where geometry cannot clear the crossover or would fold/overlap. Base and ribbons share one UInt32 mesh/submesh, at local Z 0.02 and 0.01 respectively. Decor remains at Z 0 with its existing sorting: trees, grandstands, tire stacks, and banners at **1**, and trackside barriers/tire walls at **-6**, all above grass **-7**. Layouts and seeds are unchanged.

Run **TrackVisualGeometryTests** in EditMode for arc-length colors, normal alignment, depth limits, folded/overlapping strips, crossover clearance, independent toggles, and unchanged decor geometry. The decor visibility test renders every decor mesh alone and with grass and compares its visible pixels. Batch tests export **Logs/GrassStartFinishPreview.png**, **Logs/GrassTightCornerPreview.png**, and **Logs/GrassCrossoverPreview.png** for visual review.

### Generated trackside decor

Stop Play, open your existing Main, and run **Tools > Ghostline > Add Theme To Scene**. The installer attaches **DecorGenerator** to **Ghostline/Track** and builds decor from the available generated road and walls. Its four mesh objects appear under **Track > Generated**. Repeating the menu reuses the same component, preserves its settings and enabled state, and creates no duplicate roots. Installation is one Undo operation; Redo restores the component and visible geometry. The installer marks the scene dirty and never saves it, replaces authored objects, or regenerates physics. Save Main yourself after visual review to retain the component/settings. An inactive or unbuilt track is attached without enabling it; decor builds once the track geometry is available.

Fresh **Tools > Ghostline > Build Scene** includes decor. For your edited Main, use **Add Theme To Scene**. `TrackGenerator.RebuildMeshes()` rebuilds enabled decor automatically after road/wall visuals, and track disable/re-enable releases/recreates its meshes. Use **Rebuild Decor** after changing decor Inspector fields. Generated meshes remain transient and regenerate on scene reload. An unrelated authored object named **Track/Generated** causes a clear error without replacement; rename that object before installing.

Tune **Tree Density**, **Blossom Fraction**, **Placement Margin**, **Banner Spacing**, and the palettes first. Then try **Seed** for a different grove arrangement or **Grandstand Size** for roof proportions. All dimensions are in track-local units. Every category has an independent Show toggle, density, and opaque palette. A visibility toggle keeps that category's footprints reserved, preserving all other layouts; density/dimension changes can change reservations. Zero density removes a category's reservations.

| Inspector field | Default | Effect |
|---|---|---|
| Seed / Placement Margin / Max Placement Attempts | 271828 / 0.8 / 32 | Deterministic category streams; clearance around whole footprints; 1–128 attempts per placement |
| Show Trees / Tree Density | true / 1 | Target 0.3 canopy clusters per unit of track length |
| Grove Spacing / Tree Falloff / Tree Reach | 18 / 5 / 16 | Spaced groves, exponential falloff away from the wall, maximum extra scatter distance |
| Blossom Fraction | 0.18 | Mostly dark green clusters, with pink accents |
| Tree Palette | 7 entries | Dark greens `#1F5A2B`, `#2A6B33`; blossoms `#F8BBD0`, `#F48FB1`; green/pink highlights; darker offset shadow |
| Show Grandstands / Grandstand Density / Grandstand Size | true / 1 / (12, 3.4) | Up to two stands per density unit on the main straight just beyond the finish; crowd rows face the track |
| Grandstand Palette | 6 entries | Gray roof, seating base, four crowd colors |
| Show Tires / Tire Density / Exit Curvature Threshold | true / 1 / 0.06 | Select exits after qualifying curved runs; place stacks on the outer side |
| Tire Palette | 4 entries | Rubber `#212121`, lighter inner ring, red pad, white pad |
| Show Banners / Banner Density / Banner Spacing | true / 1 / 10 | Even arc-length anchors on straight sections; effective spacing is spacing divided by density |
| Straight Curvature Limit | 0.025 | Whole-length straightness check for stands and banners |
| Banner Palette | 4 entries | Invented red/blue/yellow/purple blocks and stripes, no glyphs or logos |

`DecorPlacementGeometry` indexes the generator-owned road and wall meshes and actual wall-collider capsules, including their rounded ends. It uses original surface meshes rather than renderer buffers that static batching may combine with grass and other decor. It checks the entire oriented footprint against every nearby segment, including the other crossover pass and narrowed corners, then against every reserved decor footprint. `DecorMeshBuilder` uses 3–6 overlapping circles per canopy, plus one highlight and an offset shadow. Each category is one static mesh/submesh using the existing road material, solid vertex fills with Linear-color conversion, and 32-bit indices. All decor sorts at **1**, below walls **3**, ghost **4** and car **5**. There are no colliders, frame callbacks, custom shaders, or material assets.

Settings fail explicitly on nonfinite/invalid values, altered palette sizes, translucent colors, or more than 4096 candidates/reserved footprints. Track regeneration validates enabled decor before replacing the circuit, so invalid decor settings preserve existing walls and gates. Densities range from 0–4, margin from 0–50, and dimensions/spacing/thresholds must be positive and at most 100. Grandstands must be at least 0.8 by 0.8 units to contain the crowd dots and stay within 50 units (or 10% of a shorter circuit) beyond the finish. Crowded placements can exhaust their bounded attempts and be omitted; the default circuit must still produce every category.

Run **DecorGeneratorTests**, **ThemeInstallerTests**, or the full EditMode suite with graphics enabled. Tests check exhaustive triangle and wall-end clearance, pairwise decor clearance, containment of every generated vertex, nonempty defaults, identical seed regeneration, visibility-toggle stability, invalid inputs, empty densities, mesh cleanup and unchanged physics/gates/spawn. Installer tests cover repeated installation, Undo/Redo, preserved settings and disabled states, inactive/unbuilt tracks, invalid targets, automatic track-triggered rebuilds, and fresh-scene reload. Regressions verify invalid decor settings leave the circuit intact and same-frame Play rebuilds/toggles/regeneration use current geometry despite deferred destruction. The integration fixtures use disposable preview copies; the fresh-scene builder and Play regression run only in batch mode. Render tests exercise the installer and export `Logs/DecorStartPreview.png` and `Logs/DecorCornerPreview.png`; wiring-review copies are saved under ignored `Logs/ThemeWiring/`. Inspect these renders alongside a manual Play review of your scene.

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

`CarController` defaults: Top Speed reference 18, Max Acceleration 18, Throttle Ramp Time 0.4, Drag 0.85, Engine Braking 1.5, Brake Acceleration 6, Reverse Acceleration 8, Reverse Threshold 0.3, Steering 160, Minimum Steering Speed 2, Grip 12, Angular Damping 8, and Wall Speed Loss 0.35. Rigidbody linear damping is zero because the controller supplies drag force. Accel Curve uses speed / Top Speed on X and acceleration multiplier on Y; Steering Curve uses absolute forward speed / Top Speed and turn-rate multiplier. W/Up ramps throttle up; releasing ramps it down. S/Down takes braking priority and powers reverse near rest. Wall contacts retain 65% of incoming speed by default, including head-on impacts.

## Scene wiring

Main contains **Ghostline/Track/Generated Circuit** (Road, Walls, Checkpoints, Grass, Barriers, Tire Walls, Edge Lines, Curbs, Grid, Sector Lines, Start Finish Checker), **Car**, **Ghost**, **Main Camera**, **HUD Canvas**, and **RaceManager**. There is no rectangular Infield or old box wall perimeter.

The orthographic camera uses Size 9, Z -10, the existing 2D renderer, and CameraFollow targeting Car. HUD remains screen-space overlay with LiberationSans SDF. RaceManager references the car, ghost, HUD, and camera; its serialized count and reset pose match the generated track. Gate indices are zero-based and reference that same manager. Use the Default layer and default 2D collision matrix; no new tags or layers are required.

**HUD Canvas** contains five TMP objects wired to HudView: **Current Lap** (`_currentTimeText`), **Best Lap** (`_bestTimeText`), **Status** (`_statusText`), **Countdown** (`_countdownText`), and **Ghost Delta** (`_deltaText`). The builder retains Countdown as a centered 400 x 160 placement reference. At runtime, `HudView.RenderCountdown` clears and disables that text, then creates one **Start Gantry** child (360 x 96) using `StartGantryView` and Unity's default UI material. Five dark gray housings contain circular lamps, bright red when lit and dim red when off. Lamps light cumulatively at 0, 0.6, 1.2, 1.8, and 2.4 seconds; all go dim at 3.0 seconds, and the gantry hides at 3.75 seconds. No new scene objects need authoring or assignment, no new assets are required, and existing scenes work without rebuilding. Ghost Delta sits beside Current Lap at top-left offset `(280,-24)`, font size 28, and starts hidden. Delta uses signed three-decimal seconds, green ahead, red behind, white at zero, and fades during the final second of a three-second display. Rebuild with **Tools > Ghostline > Build Scene** to add these objects to a scene created by an earlier builder.

If a road or gate is black, check its renderer material is **Sprite-Unlit-Default**, without adding lights. Car/Visual uses that material; Ghost/Visual uses GhostSprite. Generated road meshes are recreated by TrackGenerator.OnEnable. A missing mesh or radius error should be corrected in the generator or knots, rather than hidden with a material or collider workaround.

## Play and persistence checks

1. Start Play while holding throttle/steering: the car stays still as red lamps light one at a time at **0, 0.6, 1.2, 1.8, and 2.4 seconds**. At **lights out (3.0 seconds / 150 fixed ticks)**, driving unlocks; the dim gantry hides 0.75 seconds later. There is no random delay. Existing countdown beeps remain at 0, 1, and 2 seconds, with the start cue at 3 seconds. One beep per lamp would require shifting to five cues at 0.6-second intervals; that audio change is not included. The timer and world ghost remain inactive until the first forward checker crossing; the minimap ghost dot is already at spawn when a saved recording exists. The timer then starts and the HUD requests checkpoint 1 / 12.
2. Follow corners 1-18 and pass gates in order. Continue straight at both crossover visits. Turning there to skip a loop must leave an expected gate unpassed; backward and out-of-order entries must not advance progress.
3. Finish after all twelve gates. Time freezes, the best updates if faster, and driving stops. Press **R**: position, heading, velocity, timer, splits, and camera reset together; delta clears and the gantry restarts with one red lamp lit and driving locked.
4. Start another attempt. A valid saved best appears as a translucent blue Lambo, without collisions, only after the timer starts. Each accepted gate shows current split minus best split. Check signed three-decimal values and green/red colors, fading by about three seconds; rejected gate entries must not refresh the display. At finish, the delta uses the previous best even if the lap sets a new record. The first lap has no delta. A slower lap retains the best. Re-enter Play and check disk persistence.
5. Save format **6** includes version, track ID (`suzuka`), and ordered splits. Missing or mismatched identity/version, including version 5, quietly produces no best, ghost, or delta until a new valid lap is saved. Invalid splits and poses are also ignored. The default save path is `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Ghostline\ghostline-best-lap.json`; Company/Product settings can change it.
6. Run **Window > General > Test Runner > EditMode > Run All**. Builder tests require batch mode to bypass interactive scene-replacement dialogs. GPU tests require graphics; omit `-nographics`. Expect zero failed tests. Batch tests export ignored `Logs/LamboPreview.png`, `Logs/SuzukaPreview.png`, `Logs/RacingStartPreview.png`, and `Logs/RacingCornerPreview.png` for visual checks. Gantry tests also export `Logs/GantryLamp1.png`, `Logs/GantryLamp3.png`, `Logs/GantryLamp5.png`, and `Logs/GantryLightsOut.png` from a disposable scene copy; they check rendered bright/off lamps without editing Main. Geometry tests also cover visual sorting, curvature-side selection, short-run rejection, crossover paint clearance, spatial-index equivalence to the exhaustive wall rule, complete grid footprints, a ten-slot clear approach, sector anchors, and mesh regeneration without collider/gate changes.
7. In **File > Build Profiles**, place Main first in the enabled Scene List and remove SampleScene. Build to an ignored Build/ or Builds/ directory. The builder leaves build profiles unchanged.

Commit source, `.meta` files, the rebuilt Main scene, and package manifest/lock changes. Preserve Git LFS and the existing ignore rules; omit caches, logs, and generated project files.
