using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ghostline.Editor;
using Ghostline.Core;
using Ghostline.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.TestTools;

namespace Ghostline.Tests.Scene
{
    public sealed class TrackGeneratorTests
    {
        [Test]
        public void SavedSceneUsesGeneratedCircuitInsteadOfRectangles()
        {
            WithTrack(track =>
            {
                Assert.That(track.GetComponent<SplineContainer>().Spline.Closed, Is.True);
                Assert.That(track.GetComponent<SplineContainer>().Spline.Count, Is.InRange(50, 80));
                Assert.That(track.Length, Is.InRange(600f, 900f));
                Assert.That(track.RoadWidth, Is.EqualTo(3.4f).Within(0.0001f));
                Assert.That(track.WidthLimits, Is.Not.Empty);
                Assert.That(track.MinimumRadius, Is.GreaterThan(0f));
                Assert.That(track.Crossings.Count, Is.EqualTo(1));
                Assert.That(track.transform.Find("Generated Circuit").GetComponentsInChildren<MeshRenderer>(), Has.Length.EqualTo(10));
                MeshFilter road = track.transform.Find("Generated Circuit/Road").GetComponent<MeshFilter>();
                track.enabled = false;
                Assert.That(road.sharedMesh, Is.Null);
                track.enabled = true;
                Assert.That(road.sharedMesh, Is.Not.Null);
                Assert.That(track.GetComponentsInChildren<EdgeCollider2D>().Length, Is.GreaterThan(2));
                Assert.That(track.transform.Find("Infield"), Is.Null);
                TrackSample start = track.GetSample(0f);
                Assert.That(start.Tangent.x, Is.GreaterThan(0f));
                Assert.That(start.Tangent.y, Is.LessThan(0f));
                TestContext.WriteLine($"Length={track.Length:0.00}; minimum radius={track.MinimumRadius:0.00}; required={track.RequiredRadius:0.00}; crossings={track.Crossings.Count}");
            });
        }

        [Test]
        public void SamplesAreEvenlySpacedAndRoadMeshClosesAtFullWidth()
        {
            WithTrack(track =>
            {
                Mesh mesh = track.transform.Find("Generated Circuit/Road").GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh, Is.Not.Null);
                Vector3[] vertices = mesh.vertices;
                Assert.That(vertices, Has.Length.EqualTo((track.Samples.Count + 1) * 2));
                Assert.That(Vector3.Distance(vertices[0], vertices[vertices.Length - 2]), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(vertices[1], vertices[vertices.Length - 1]), Is.LessThan(0.0001f));
                for (int i = 0; i < track.Samples.Count; i++)
                {
                    TrackSample sample = track.Samples[i];
                    TrackSample next = track.Samples[(i + 1) % track.Samples.Count];
                    Assert.That(Vector2.Distance(sample.Position, next.Position),
                        Is.EqualTo(track.SampleSpacing).Within(track.SampleSpacing * 0.08f));
                    Assert.That(Vector2.Dot(sample.Tangent, sample.Normal), Is.Zero.Within(0.0001f));
                    Assert.That(Vector3.Distance(vertices[i * 2], vertices[i * 2 + 1]),
                        Is.EqualTo(track.GetRoadWidth(sample.Distance)).Within(0.0001f));
                    Assert.That(track.GetRoadWidth(sample.Distance), Is.InRange(0.55f, track.RoadWidth));
                }
            });
        }

        [Test]
        public void BothCrossoverPassesAreUnobstructedAndBothArcsRequireGates()
        {
            WithTrack(track =>
            {
                Assert.That(track.GateDistances, Has.Count.EqualTo(12));
                TrackCrossing crossing = track.Crossings.Single();
                Assert.That(track.GateDistances.Any(d => d > crossing.FirstDistance && d < crossing.SecondDistance), Is.True);
                Assert.That(track.GateDistances.Any(d => d < crossing.FirstDistance || d > crossing.SecondDistance), Is.True);
                EdgeCollider2D[] walls = track.GetComponentsInChildren<EdgeCollider2D>();
                foreach (float pass in new[] { crossing.FirstDistance, crossing.SecondDistance })
                {
                    for (float distance = -track.RoadWidth * 2f; distance <= track.RoadWidth * 2f; distance += 0.2f)
                    {
                        TrackSample sample = track.GetSample(pass + distance);
                        foreach (float lateral in new[] { -0.85f, 0f, 0.85f })
                        {
                            Vector2 point = sample.Position + sample.Normal * (track.RoadWidth * 0.5f * lateral);
                            foreach (EdgeCollider2D wall in walls)
                            {
                                Vector2[] points = wall.points;
                                for (int i = 0; i < points.Length - 1; i++)
                                    Assert.That(DistanceToSegment(point, points[i], points[i + 1]),
                                        Is.GreaterThan(wall.edgeRadius), "A wall obstructs a crossover road corridor.");
                            }
                        }
                    }
                }
                foreach (float distance in track.GateDistances)
                    Assert.That(Vector2.Distance(track.GetSample(distance).Position, crossing.Position), Is.GreaterThan(track.RoadWidth));
            });
        }

        [Test]
        public void GatesAndSpawnMatchSplineDirectionsAndRaceConfiguration()
        {
            WithTrack(track =>
            {
                Transform root = track.transform.parent;
                RaceManager race = root.Find("RaceManager").GetComponent<RaceManager>();
                var raceFields = new SerializedObject(race);
                Assert.That(raceFields.FindProperty("_checkpointCount").intValue, Is.EqualTo(track.CheckpointCount));
                TrackSample spawn = track.GetComponent<TrackVisuals>().GetSpawnSample(track);
                Assert.That(Vector2.Distance(raceFields.FindProperty("_spawnPosition").vector2Value, spawn.Position), Is.LessThan(0.001f));
                float rotation = Mathf.Atan2(spawn.Tangent.y, spawn.Tangent.x) * Mathf.Rad2Deg - 90f;
                Assert.That(Mathf.DeltaAngle(raceFields.FindProperty("_spawnRotation").floatValue, rotation), Is.Zero.Within(0.001f));
                Assert.That(Vector2.Distance(root.Find("Main Camera").position, spawn.Position), Is.LessThan(0.001f));
                HudView hud = root.Find("HUD Canvas").GetComponent<HudView>();
                var hudFields = new SerializedObject(hud);
                var countdown = hudFields.FindProperty("_countdownText").objectReferenceValue as TMPro.TMP_Text;
                var delta = hudFields.FindProperty("_deltaText").objectReferenceValue as TMPro.TMP_Text;
                Assert.That(countdown, Is.Not.Null);
                Assert.That(delta, Is.Not.Null);
                Assert.That(countdown.name, Is.EqualTo("Countdown"));
                Assert.That(delta.name, Is.EqualTo("Ghost Delta"));
                Assert.That(countdown.rectTransform.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(countdown.rectTransform.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(countdown.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                CheckpointTrigger[] gates = track.GetComponentsInChildren<CheckpointTrigger>();
                Assert.That(gates, Has.Length.EqualTo(track.CheckpointCount + 1));
                foreach (CheckpointTrigger gate in gates)
                {
                    var fields = new SerializedObject(gate);
                    Assert.That(fields.FindProperty("_raceManager").objectReferenceValue, Is.EqualTo(race));
                    bool start = fields.FindProperty("_isStartFinish").boolValue;
                    int index = fields.FindProperty("_checkpointIndex").intValue;
                    TrackSample sample = track.GetSample(start ? 0f : track.GateDistances[index]);
                    Assert.That(Vector2.Distance(gate.transform.position, sample.Position), Is.LessThan(0.001f));
                    Assert.That(Vector2.Dot(gate.transform.right, sample.Tangent), Is.GreaterThan(0.999f));
                    Assert.That(Vector2.Dot(fields.FindProperty("_forwardDirection").vector2Value, sample.Tangent), Is.GreaterThan(0.999f));
                    BoxCollider2D collider = gate.GetComponent<BoxCollider2D>();
                    Assert.That(collider.isTrigger, Is.True);
                    Assert.That(collider.size.y * gate.transform.localScale.y, Is.EqualTo(track.GetRoadWidth(sample.Distance)).Within(0.001f));
                }
            });
        }

        [Test]
        public void RegenerationUsesConfiguredCountAndRemovesPreviousGeometry()
        {
            WithTrack(track =>
            {
                Material material = track.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                RaceManager race = track.transform.parent.Find("RaceManager").GetComponent<RaceManager>();
                Transform root = track.transform.parent;
                TrackSample spawn = track.GetComponent<TrackVisuals>().GetSpawnSample(track);
                float rotation = Mathf.Atan2(spawn.Tangent.y, spawn.Tangent.x) * Mathf.Rad2Deg - 90f;
                try
                {
                    track.Configure(material, 24);
                    Assert.Throws<InvalidOperationException>(() => track.Generate(race));
                    race.Configure(root.Find("Car").GetComponent<CarController>(), root.Find("Ghost").GetComponent<GhostCarView>(),
                        root.Find("HUD Canvas").GetComponent<HudView>(), root.Find("Main Camera").GetComponent<CameraFollow>(),
                        24, spawn.Position, rotation);
                    track.Generate(race);
                    Assert.That(track.transform.childCount, Is.EqualTo(track.GetComponent<DecorGenerator>()?.isActiveAndEnabled == true ? 2 : 1));
                    Assert.That(track.GetComponentsInChildren<CheckpointTrigger>(), Has.Length.EqualTo(25));
                    Assert.That(track.GateDistances, Has.Count.EqualTo(24));
                    Assert.That(track.transform.Find("Generated Circuit").GetComponentsInChildren<MeshRenderer>(), Has.Length.EqualTo(10));
                    foreach (float distance in track.GateDistances)
                        foreach (TrackCrossing crossing in track.Crossings)
                            Assert.That(Vector2.Distance(track.GetSample(distance).Position, crossing.Position), Is.GreaterThan(track.RoadWidth));
                }
                finally
                {
                    track.Configure(material);
                    race.Configure(root.Find("Car").GetComponent<CarController>(), root.Find("Ghost").GetComponent<GhostCarView>(),
                        root.Find("HUD Canvas").GetComponent<HudView>(), root.Find("Main Camera").GetComponent<CameraFollow>(),
                        12, spawn.Position, rotation);
                    track.Generate(race);
                }
            });
        }

        [Test]
        public void RoadRendersWithoutLightsAndExportsOverview()
        {
            WithTrack(track =>
            {
                MeshRenderer road = track.transform.Find("Generated Circuit/Road").GetComponent<MeshRenderer>();
                Bounds bounds = road.bounds;
                var cameraObject = new GameObject("Track preview camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, track.gameObject.scene);
                var target = new RenderTexture(1600, 900, 24);
                var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    cameraObject.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
                    Camera camera = cameraObject.GetComponent<Camera>();
                    camera.scene = track.gameObject.scene;
                    camera.cameraType = CameraType.Preview;
                    camera.enabled = false;
                    camera.orthographic = true;
                    camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x * 900f / 1600f) * 1.12f;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f);
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
                    image.Apply();
                    TrackSample sample = track.GetSample(20f);
                    Vector3 viewport = camera.WorldToViewportPoint(sample.Position);
                    Color roadColor = image.GetPixel(Mathf.RoundToInt(viewport.x * 1599f), Mathf.RoundToInt(viewport.y * 899f));
                    Assert.That(roadColor.r, Is.GreaterThan(image.GetPixel(10, 10).r + 0.03f));
                    Assert.That(roadColor.g, Is.GreaterThan(roadColor.r));
                    Assert.That(roadColor.b, Is.GreaterThan(roadColor.g));
                    Assert.That(roadColor.r, Is.EqualTo(0.17f).Within(0.02f), "The road must render dark gray, including in Linear color space.");
                    if (Application.isBatchMode)
                    {
                        Directory.CreateDirectory("Logs");
                        File.WriteAllBytes("Logs/SuzukaPreview.png", image.EncodeToPNG());
                        TrackVisuals visuals = track.GetComponent<TrackVisuals>();
                        TrackVisualRun corner = visuals.TireRuns.OrderByDescending(run => run.Length).First();
                        TrackSample[] details = { track.GetSample(0f), track.GetSample(corner.Distance + corner.Length * 0.5f) };
                        string[] paths = { "Logs/RacingStartPreview.png", "Logs/RacingCornerPreview.png" };
                        for (int i = 0; i < details.Length; i++)
                        {
                            cameraObject.transform.position = new Vector3(details[i].Position.x, details[i].Position.y, -10f);
                            camera.orthographicSize = 6f;
                            camera.Render();
                            RenderTexture.active = target;
                            image.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
                            image.Apply();
                            File.WriteAllBytes(paths[i], image.EncodeToPNG());
                        }
                    }
                    camera.targetTexture = null;
                }
                finally
                {
                    RenderTexture.active = previous;
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                    UnityEngine.Object.DestroyImmediate(image);
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
            });
        }

        [Test]
        public void InvalidSettingsAreRejectedAndTightCornersReportLimitsWithoutErrors()
        {
            WithTrack(track =>
            {
                Material material = track.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Assert.Throws<ArgumentOutOfRangeException>(() => track.Configure(material, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => track.Configure(material, roadWidth: float.NaN));
                var fields = new SerializedObject(track);
                float originalWidth = track.RoadWidth;
                Assert.That(track.RequiredRadius, Is.EqualTo(track.RoadWidth * 0.5f + track.WallThickness
                    + fields.FindProperty("_radiusMargin").floatValue).Within(0.0001f));
                fields.FindProperty("_roadWidth").floatValue = track.Length;
                fields.ApplyModifiedPropertiesWithoutUndo();
                try
                {
                    Assert.That(track.ValidateRadius(), Is.False);
                }
                finally
                {
                    fields.FindProperty("_roadWidth").floatValue = originalWidth;
                    fields.ApplyModifiedPropertiesWithoutUndo();
                }
            });
        }

        [Test]
        public void InspectorWidthChangeRegeneratesWallsAndGatesOnEnable()
        {
            WithTrack(track =>
            {
                var settings = new SerializedObject(track);
                float original = track.RoadWidth;
                try
                {
                    settings.FindProperty("_roadWidth").floatValue = 4f;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    track.enabled = false;
                    track.enabled = true;
                    settings.Update();
                    foreach (CheckpointTrigger gate in track.GetComponentsInChildren<CheckpointTrigger>())
                    {
                        var fields = new SerializedObject(gate);
                        float distance = fields.FindProperty("_isStartFinish").boolValue ? 0f
                            : track.GateDistances[fields.FindProperty("_checkpointIndex").intValue];
                        Assert.That(gate.transform.localScale.y, Is.EqualTo(track.GetRoadWidth(distance)).Within(0.001f));
                    }
                    Assert.That(settings.FindProperty("_builtRoadWidth").floatValue, Is.EqualTo(4f));
                }
                finally
                {
                    settings.Update();
                    settings.FindProperty("_roadWidth").floatValue = original;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    track.Regenerate();
                }
            });
        }

        [TestCase(3.4f)]
        [TestCase(8f)]
        public void WiderOffsetsAreSmoothedAndWallSegmentsNeverSelfIntersect(float width)
        {
            WithTrack(track =>
            {
                Material material = track.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                RaceManager race = track.transform.parent.Find("RaceManager").GetComponent<RaceManager>();
                float original = track.RoadWidth;
                try
                {
                    track.Configure(material, roadWidth: width);
                    track.Generate(race);
                    Assert.That(track.WidthLimits, Is.Not.Empty);
                    var settings = new SerializedObject(track);
                    float slope = track.RoadWidth * 0.5f / settings.FindProperty("_offsetSmoothingLength").floatValue;
                    for (int i = 0; i < track.Samples.Count; i++)
                    {
                        float a = track.GetSampleRoadWidth(i);
                        float b = track.GetSampleRoadWidth((i + 1) % track.Samples.Count);
                        Assert.That(Mathf.Abs(a - b), Is.LessThanOrEqualTo(2f * slope * track.SampleSpacing + 0.0001f));
                    }
                    var points = new System.Collections.Generic.List<TrackPoint>();
                    var allowed = new System.Collections.Generic.List<bool>();
                    var closurePairs = new System.Collections.Generic.HashSet<(int First, int Last)>();
                    foreach (EdgeCollider2D wall in track.GetComponentsInChildren<EdgeCollider2D>())
                    {
                        Vector2[] path = wall.points;
                        bool closed = path[0] == path[path.Length - 1];
                        if (closed)
                            closurePairs.Add((points.Count, points.Count + path.Length - 2));
                        for (int i = 0; i < path.Length; i++)
                        {
                            points.Add(new TrackPoint(path[i].x, path[i].y));
                            allowed.Add(i < path.Length - 1);
                        }
                    }
                    TrackPoint[] vertices = points.ToArray();
                    foreach (TrackIntersection hit in TrackOffsetGeometry.FindIntersections(vertices, allowed.ToArray(), vertices,
                        new bool[vertices.Length], track.RoadWidth))
                        Assert.That(closurePairs.Contains((Mathf.Min(hit.FirstIndex, hit.SecondIndex),
                            Mathf.Max(hit.FirstIndex, hit.SecondIndex))), Is.True, "Non-adjacent wall segments intersect.");
                    foreach (TrackWidthLimit limit in track.WidthLimits)
                        TestContext.WriteLine($"Limited {limit.FromDistance:0.00}-{limit.ToDistance:0.00}: {limit.MinimumWidth:0.00}");
                }
                finally
                {
                    track.Configure(material, roadWidth: original);
                    track.Generate(race);
                }
            });
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
            return Vector2.Distance(point, a + delta * t);
        }

        private static void WithTrack(Action<TrackGenerator> assertion)
        {
            var scene = SceneManager.GetSceneByPath(GhostlineSceneBuilder.ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(GhostlineSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform root = null;
                foreach (GameObject gameObject in scene.GetRootGameObjects())
                    if (gameObject.name == "Ghostline")
                        root = gameObject.transform;
                Assert.That(root, Is.Not.Null);
                TrackGenerator track = root.Find("Track").GetComponent<TrackGenerator>();
                Assert.That(track, Is.Not.Null,
                    "The rectangular prototype must be replaced by a generated Suzuka circuit.");
                assertion(track);
            }
            finally
            {
                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
