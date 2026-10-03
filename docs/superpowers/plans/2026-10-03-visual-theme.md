# Ghostline Visual Theme Implementation Plan

> Execute inline using superpowers:executing-plans. The user approved the plan and requires a stop after commit 1 for visual review.

**Goal:** Add a deterministic Suzuka-inspired visual theme without changing physics, tuning, persistence, or authored scene objects.

**Architecture:** Extend the existing combined-mesh TrackVisuals layer for grass and road paint. Later, add a seeded DecorGenerator and an additive, undoable theme installer. Reuse Unity's supplied unlit material and runtime shape geometry.

**Tech Stack:** Unity 6000.6.4f1, C#, existing Unity Splines, NUnit EditMode tests.

## Commit 1: Grass stripes, edge lines, kerbs

- [x] Add failing cases to `Assets/Ghostline/Tests/Scene/TrackVisualGeometryTests.cs` for alternating serialized grass colors, band continuity/clipping, category toggles, and invalid stripe widths. Baseline: 9 existing cases passed; 9 new cases failed on missing fields.
- [x] Extend `Assets/Ghostline/Game/TrackVisuals.cs:BuildGrass` with origin-anchored horizontal bands, serialized alternate color and 8-unit width, one mesh/submesh, and a 4096-band allocation guard. Preserve the serialized base grass color and existing ground bounds.
- [x] Extend `TrackVisuals.BuildRibbons` with independent edge-line/curb toggles; preserve signed curvature selection, local width adjustment, and crossover clearance. Grass has its own toggle. Disabled layers retain empty meshes so object counts and references stay stable.
- [x] Update `README.md` and `SCENE_SETUP.md`; review diffs and hash protected source/scene/settings files against the pre-change snapshot. All 13 protected files match.
- [x] Run the command below with graphics: exit 0, 219/219 Passed, zero failures/skips. Inspect exported start/finish and corner previews; grass bands and existing paint render correctly.
- [x] Ready for commit 1 and user visual review. Commit only these source, test, and documentation files. STOP; do not start decor or installer implementation.

```powershell
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe'
$verificationProject = 'C:\Users\Nate Tran\AppData\Local\Temp\Ghostline-Theme-20261003-164345'
$arguments = @('-batchmode', '-runTests', '-testPlatform', 'EditMode',
    '-projectPath', ('"' + $verificationProject + '"'),
    '-testResults', ('"' + $verificationProject + '/theme-green.xml"'),
    '-logFile', ('"' + $verificationProject + '/theme-green.log"'))
Start-Process -FilePath $unityEditor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
```

Use the full Windows project path: launching this snapshot via its `NATETR~1` short-path alias caused MonoScript/scene reference failures. The full-path baseline and final verification imported correctly. Workspace `Logs/ThemeCommit1/` retains the final test XML and rendered previews for review; these are ignored verification artifacts, not runtime art assets.

## Later commits: Await visual-review approval

2. Seeded blossom clusters, finish-area grandstands, dark circular tire stacks at selected exits, and colored banner blocks with simple stripe patterns. No text or glyph rendering. Full footprints must clear every road pass and actual wall geometry; category toggles must preserve other layouts. Bounded attempts and combined meshes.
3. Slowly rotating Ferris wheel with its entire swept footprint clear of road/walls; decor below ghost/car. Add idempotent Undo-capable installer that preserves tuning and authored objects, marks dirty, and never saves or rebuilds an existing scene. Minimap is a UI overlay: no exclusion logic.
4. Clearance/determinism/toggle/installer/rotation tests and documentation. Compare physics, gates, spawn, and save format against baseline; report manual experience review separately.

Do not edit or stage `Assets/Scenes/Main.unity`, existing local changes, `Library/`, `Temp/`, or `UserSettings/`. Unity verification uses a temporary snapshot of Assets, Packages, and ProjectSettings only; its generated caches are outside the workspace.
