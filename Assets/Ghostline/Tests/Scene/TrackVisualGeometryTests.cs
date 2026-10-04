using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Ghostline.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.TestTools;
using Ghostline.Editor;

namespace Ghostline.Tests.Scene
{
    public sealed class TrackVisualGeometryTests
    {
        [Test]
        public void SavedCircuitContainsStaticUnlitVisualLayers()
        {
            WithTrack(track =>
            {
                Transform circuit = track.transform.Find("Generated Circuit");
                string[] layers = { "Grass", "Barriers", "Tire Walls", "Edge Lines", "Curbs", "Grid", "Sector Lines", "Start Finish Checker" };
                foreach (string layer in layers)
                {
                    Transform visual = circuit.Find(layer);
                    Assert.That(visual, Is.Not.Null, layer + " must be generated from the spline.");
                    Assert.That(visual.gameObject.isStatic, Is.True);
                    Assert.That(visual.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
                    Assert.That(visual.GetComponent<MeshRenderer>().sharedMaterial.shader.name,
                        Is.EqualTo("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                    Assert.That(visual.GetComponentsInChildren<Collider2D>(), Is.Empty);
                }
                string[] sorted = { "Grass", "Barriers", "Road", "Edge Lines", "Curbs", "Grid", "Sector Lines", "Start Finish Checker" };
                for (int i = 1; i < sorted.Length; i++)
                    Assert.That(circuit.Find(sorted[i]).GetComponent<MeshRenderer>().sortingOrder,
                        Is.GreaterThan(circuit.Find(sorted[i - 1]).GetComponent<MeshRenderer>().sortingOrder));
                Assert.That(circuit.Find("Start Finish Checker").GetComponent<MeshRenderer>().sortingOrder, Is.LessThan(4));
                Assert.That(circuit.Find("Tire Walls").GetComponent<MeshRenderer>().sortingOrder,
                    Is.EqualTo(circuit.Find("Barriers").GetComponent<MeshRenderer>().sortingOrder));
                Bounds grass = circuit.Find("Grass").GetComponent<MeshFilter>().sharedMesh.bounds;
                Bounds road = circuit.Find("Road").GetComponent<MeshFilter>().sharedMesh.bounds;
                Assert.That(circuit.Find("Grass").GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(4));
                Assert.That(road.min.x - grass.min.x, Is.GreaterThanOrEqualTo(18f));
                Assert.That(road.min.y - grass.min.y, Is.GreaterThanOrEqualTo(18f));
                Assert.That(grass.max.x - road.max.x, Is.GreaterThanOrEqualTo(18f));
                Assert.That(grass.max.y - road.max.y, Is.GreaterThanOrEqualTo(18f));
            });
        }

        [Test]
        public void GrassBandsAlternateSerializedColorsWithoutGapsAndClipToGroundBounds()
        {
            WithTrack(track =>
            {
                TrackVisuals visuals = track.GetComponent<TrackVisuals>();
                var settings = new SerializedObject(visuals);
                Assert.That(settings.FindProperty("_grassStripeColor"), Is.Not.Null);
                Assert.That(settings.FindProperty("_grassStripeWidth"), Is.Not.Null);
                float originalWidth = settings.FindProperty("_grassStripeWidth").floatValue;
                Color originalColor = settings.FindProperty("_grassColor").colorValue;
                Color originalStripeColor = settings.FindProperty("_grassStripeColor").colorValue;
                try
                {
                    settings.FindProperty("_grassColor").colorValue = new Color(0.15f, 0.3f, 0.1f);
                    settings.FindProperty("_grassStripeColor").colorValue = new Color(0.25f, 0.45f, 0.2f);
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    foreach (float width in new[] { 8f, 12.5f, 1000f })
                    {
                        SetFloat(visuals, "_grassStripeWidth", width);
                        RebuildVisuals(track);
                        Mesh grass = VisualMesh(track, "Grass");
                        Vector3[] vertices = grass.vertices;
                        Color[] colors = grass.colors;
                        int firstBand = Mathf.FloorToInt(grass.bounds.min.y / width);
                        int lastBand = Mathf.CeilToInt(grass.bounds.max.y / width);
                        Assert.That(vertices.Length, Is.EqualTo((lastBand - firstBand) * 4));
                        Assert.That(grass.triangles.Length, Is.EqualTo((lastBand - firstBand) * 6));
                        Assert.That(grass.subMeshCount, Is.EqualTo(1));
                        float previousTop = grass.bounds.min.y;
                        settings.Update();
                        for (int band = firstBand; band < lastBand; band++)
                        {
                            int offset = (band - firstBand) * 4;
                            Assert.That(vertices[offset].x, Is.EqualTo(grass.bounds.min.x));
                            Assert.That(vertices[offset + 1].x, Is.EqualTo(grass.bounds.max.x));
                            Assert.That(vertices[offset].y, Is.EqualTo(previousTop).Within(0.0001f));
                            Assert.That(vertices[offset + 1].y, Is.EqualTo(previousTop).Within(0.0001f));
                            float top = Mathf.Min(grass.bounds.max.y, (band + 1) * width);
                            Assert.That(vertices[offset + 2].y, Is.EqualTo(top).Within(0.0001f));
                            Assert.That(vertices[offset + 3].y, Is.EqualTo(top).Within(0.0001f));
                            Assert.That(top, Is.GreaterThan(previousTop));
                            Color expected = settings.FindProperty(band % 2 == 0 ? "_grassColor" : "_grassStripeColor").colorValue;
                            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                                expected = expected.linear;
                            for (int i = 0; i < 4; i++)
                                Assert.That(colors[offset + i], Is.EqualTo(expected));
                            previousTop = top;
                        }
                        Assert.That(previousTop, Is.EqualTo(grass.bounds.max.y));
                    }
                }
                finally
                {
                    settings.Update();
                    settings.FindProperty("_grassColor").colorValue = originalColor;
                    settings.FindProperty("_grassStripeColor").colorValue = originalStripeColor;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    SetFloat(visuals, "_grassStripeWidth", originalWidth);
                    RebuildVisuals(track);
                }
            });
        }

        [TestCase("_showGrass", "Grass")]
        [TestCase("_showEdgeLines", "Edge Lines")]
        [TestCase("_showCurbs", "Curbs")]
        public void ThemeTogglesHideOnlyTheirCategoryAndRestoreIdenticalGeometry(string field, string layer)
        {
            WithTrack(track =>
            {
                TrackVisuals visuals = track.GetComponent<TrackVisuals>();
                var settings = new SerializedObject(visuals);
                Assert.That(settings.FindProperty(field), Is.Not.Null);
                bool original = settings.FindProperty(field).boolValue;
                string[] layers = { "Grass", "Barriers", "Tire Walls", "Edge Lines", "Curbs", "Grid", "Sector Lines", "Start Finish Checker" };
                var vertices = layers.ToDictionary(name => name, name => VisualMesh(track, name).vertices);
                Mesh road = VisualMesh(track, "Road");
                EdgeCollider2D[] walls = track.GetComponentsInChildren<EdgeCollider2D>();
                Vector2[][] wallPoints = walls.Select(w => w.points).ToArray();
                int rendererCount = track.GetComponentsInChildren<MeshRenderer>().Length;
                try
                {
                    settings.FindProperty(field).boolValue = false;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    RebuildVisuals(track);
                    Assert.That(VisualMesh(track, layer).vertexCount, Is.Zero);
                    if (layer == "Curbs")
                        Assert.That(visuals.CurbRuns, Is.Empty);
                    Assert.That(visuals.RibbonQuads.Any(q => q.Layer == layer), Is.False);
                    foreach (string other in layers.Where(name => name != layer))
                        Assert.That(VisualMesh(track, other).vertices, Is.EqualTo(vertices[other]), other);
                    Assert.That(VisualMesh(track, "Road"), Is.SameAs(road));
                    for (int i = 0; i < walls.Length; i++)
                        Assert.That(walls[i].points, Is.EqualTo(wallPoints[i]));
                    settings.Update();
                    settings.FindProperty(field).boolValue = true;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    RebuildVisuals(track);
                    Assert.That(VisualMesh(track, layer).vertices, Is.EqualTo(vertices[layer]));
                    Assert.That(track.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(rendererCount));
                }
                finally
                {
                    settings.Update();
                    settings.FindProperty(field).boolValue = original;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    RebuildVisuals(track);
                }
            });
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0.000001f)]
        public void InvalidOrExcessiveGrassStripeWidthsFailExplicitly(float width)
        {
            WithTrack(track =>
            {
                TrackVisuals visuals = track.GetComponent<TrackVisuals>();
                var settings = new SerializedObject(visuals);
                Assert.That(settings.FindProperty("_grassStripeWidth"), Is.Not.Null);
                float original = settings.FindProperty("_grassStripeWidth").floatValue;
                try
                {
                    SetFloat(visuals, "_grassStripeWidth", width);
                    Assert.Throws<InvalidOperationException>(() => RebuildVisuals(track));
                }
                finally
                {
                    SetFloat(visuals, "_grassStripeWidth", original);
                    RebuildVisuals(track);
                }
            });
        }

        [Test]
        public void CurbsAndTiresUseOppositeSidesAndDropShortRuns()
        {
            WithTrack(track =>
            {
                TrackVisuals visuals = track.GetComponent<TrackVisuals>();
                var settings = new SerializedObject(visuals);
                Assert.That(visuals.CurbRuns, Is.Not.Empty);
                Assert.That(visuals.TireRuns, Is.Not.Empty);
                AssertRuns(track, visuals.CurbRuns, settings.FindProperty("_curbCurvatureThreshold").floatValue,
                    settings.FindProperty("_curbMinimumRunLength").floatValue, 1f);
                AssertRuns(track, visuals.TireRuns, settings.FindProperty("_tireCurvatureThreshold").floatValue,
                    settings.FindProperty("_tireMinimumRunLength").floatValue, -1f);
                float curbMinimum = settings.FindProperty("_curbMinimumRunLength").floatValue;
                float tireMinimum = settings.FindProperty("_tireMinimumRunLength").floatValue;
                try
                {
                    SetFloat(visuals, "_curbMinimumRunLength", track.Length + 1f);
                    SetFloat(visuals, "_tireMinimumRunLength", track.Length + 1f);
                    RebuildVisuals(track);
                    Assert.That(visuals.CurbRuns, Is.Empty);
                    Assert.That(visuals.TireRuns, Is.Empty);
                    Assert.That(VisualMesh(track, "Curbs").vertexCount, Is.Zero);
                    Assert.That(VisualMesh(track, "Tire Walls").vertexCount, Is.Zero);
                }
                finally
                {
                    SetFloat(visuals, "_curbMinimumRunLength", curbMinimum);
                    SetFloat(visuals, "_tireMinimumRunLength", tireMinimum);
                    RebuildVisuals(track);
                }
            });
        }

        [Test]
        public void EveryRibbonVertexAndTriangleCenterIsOutsideTheOtherRoadCorridor()
        {
            WithTrack(track =>
            {
                TrackVisuals visuals = track.GetComponent<TrackVisuals>();
                var vertices = new Dictionary<string, Vector3[]>();
                string[] layers = { "Edge Lines", "Curbs", "Barriers", "Tire Walls" };
                foreach (string layer in layers)
                    vertices.Add(layer, VisualMesh(track, layer).vertices);
                foreach (TrackVisualQuad quad in visuals.RibbonQuads)
                {
                    Vector3[] points = vertices[quad.Layer];
                    int offset = quad.VertexOffset;
                    for (int i = 0; i < 4; i++)
                    {
                        float distance = i < 2 ? quad.FromDistance : quad.ToDistance;
                        Assert.That(track.InsideOtherRoad(points[offset + i], Mathf.Repeat(distance, track.Length)),
                            Is.False, quad.Layer + " intrudes into another road corridor.");
                        TrackSample source = track.GetSample(distance);
                        float lateral = Mathf.Abs(Vector2.Dot((Vector2)points[offset + i] - source.Position, source.Normal));
                        if (quad.Layer == "Barriers" || quad.Layer == "Tire Walls")
                            Assert.That(lateral, Is.GreaterThanOrEqualTo(track.GetRoadWidth(distance) * 0.5f + track.WallThickness - 0.0001f));
                        else
                            Assert.That(lateral, Is.LessThanOrEqualTo(track.GetRoadWidth(distance) * 0.5f - 0.0499f));
                    }
                    Vector2 center = (points[offset] + points[offset + 1] + points[offset + 2] + points[offset + 3]) * 0.25f;
                    Assert.That(track.InsideOtherRoad(center, Mathf.Repeat((quad.FromDistance + quad.ToDistance) * 0.5f, track.Length)), Is.False);
                    Vector2 firstTriangle = (points[offset] + points[offset + 1] + points[offset + 2]) / 3f;
                    Vector2 secondTriangle = (points[offset] + points[offset + 2] + points[offset + 3]) / 3f;
                    Assert.That(track.InsideOtherRoad(firstTriangle, Mathf.Repeat((quad.FromDistance * 2f + quad.ToDistance) / 3f, track.Length)), Is.False);
                    Assert.That(track.InsideOtherRoad(secondTriangle, Mathf.Repeat((quad.FromDistance + quad.ToDistance * 2f) / 3f, track.Length)), Is.False);
                }
                foreach (string layer in layers)
                    Assert.That(visuals.RibbonQuads.Count(q => q.Layer == layer) * 4, Is.EqualTo(vertices[layer].Length));
                TrackCrossing crossing = track.Crossings.Single();
                Assert.That(track.InsideOtherRoad(track.GetSample(crossing.FirstDistance).Position, crossing.FirstDistance), Is.True);
                Assert.That(track.InsideOtherRoad(track.GetSample(crossing.SecondDistance).Position, crossing.SecondDistance), Is.True);
                Assert.That(visuals.RibbonQuads.Any(q => q.Layer == "Edge Lines"
                    && q.FromDistance < crossing.FirstDistance && q.ToDistance > crossing.FirstDistance), Is.False);
            });
        }

        [Test]
        public void CrossoverIndexPreservesTheExhaustiveWallRule()
        {
            WithTrack(track =>
            {
                foreach (TrackCrossing crossing in track.Crossings)
                    foreach (float pass in new[] { crossing.FirstDistance, crossing.SecondDistance })
                        for (float along = -5f; along <= 5f; along += 0.5f)
                            for (float offset = -3f; offset <= 3f; offset += 0.25f)
                            {
                                TrackSample sample = track.GetSample(pass + along);
                                Vector2 point = sample.Position + sample.Normal * offset;
                                Assert.That(track.InsideOtherRoad(point, sample.Distance), Is.EqualTo(ExhaustiveCorridor(track, point, sample.Distance)));
                            }
            });
        }

        [Test]
        public void GridFootprintsRemainClearOfWallsAndCrossoverAndSpawnUsesFrontSlot()
        {
            WithTrack(track =>
            {
                TrackVisuals visuals = track.GetComponent<TrackVisuals>();
                Assert.That(visuals.GridSlots.Count, Is.InRange(1, 10));
                TrackGridSlot front = visuals.GridSlots[0];
                TrackSample spawn = visuals.GetSpawnSample(track);
                Assert.That(spawn.Position, Is.EqualTo(front.Center));
                Assert.That(spawn.Tangent, Is.EqualTo(front.Tangent));
                Transform car = track.transform.parent.Find("Car");
                Assert.That(Vector2.Distance(car.position, front.Center), Is.LessThan(0.001f));
                EdgeCollider2D[] walls = track.GetComponentsInChildren<EdgeCollider2D>();
                foreach (TrackGridSlot slot in visuals.GridSlots)
                {
                    Vector2 normal = new Vector2(-slot.Tangent.y, slot.Tangent.x);
                    for (int i = 0; i <= 16; i++)
                        for (int side = -1; side <= 1; side++)
                        {
                            float along = Mathf.Lerp(-slot.Length * 0.5f, slot.Length * 0.5f, i / 16f);
                            Vector2 point = slot.Center + slot.Tangent * along + normal * (slot.Width * 0.5f * side);
                            Assert.That(track.InsideOtherRoad(point, track.GetSample(slot.Distance + along).Distance), Is.False);
                            foreach (EdgeCollider2D wall in walls)
                            {
                                Vector2[] points = wall.points;
                                for (int j = 1; j < points.Length; j++)
                                    Assert.That(DistanceToSegment(point, points[j - 1], points[j]), Is.GreaterThan(wall.edgeRadius));
                            }
                        }
                }
                if (visuals.GridSlots.Count < 10)
                    LogAssert.Expect(LogType.Warning, new Regex("Track grid fits " + visuals.GridSlots.Count + " of 10 slots"));
                RebuildVisuals(track);
            });
        }

        [Test]
        public void LongClearApproachFitsTenStaggeredSlotsAndNarrowRoadFitsNone()
        {
            var root = new GameObject("Grid test", typeof(SplineContainer), typeof(TrackGenerator), typeof(TrackVisuals));
            try
            {
                root.GetComponent<SplineContainer>().Spline = new Spline(new[]
                {
                    new Unity.Mathematics.float3(0f, 0f, 0f), new Unity.Mathematics.float3(100f, 0f, 0f),
                    new Unity.Mathematics.float3(100f, 100f, 0f), new Unity.Mathematics.float3(-100f, 100f, 0f),
                    new Unity.Mathematics.float3(-100f, 0f, 0f), new Unity.Mathematics.float3(-50f, 0f, 0f),
                    new Unity.Mathematics.float3(-25f, 0f, 0f)
                }, TangentMode.AutoSmooth, true);
                TrackGenerator track = root.GetComponent<TrackGenerator>();
                Material material = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
                track.Configure(material, sampleCount: 4096);
                TrackVisuals visuals = root.GetComponent<TrackVisuals>();
                visuals.Configure(18f, 1.4f, 0.55f, 1, 2);
                TrackSample spawn = visuals.GetSpawnSample(track);
                for (float behind = 0.5f; behind < 16f; behind += 0.5f)
                {
                    TrackSample probe = track.GetSample(-behind);
                    int index = Mathf.FloorToInt(probe.Distance / track.SampleSpacing);
                    Assert.That(track.GetSignedCurvature(index), Is.Zero.Within(0.00001f), "The fixture must provide a straight approach.");
                    Assert.That(track.InsideOtherRoad(probe.Position, probe.Distance), Is.False);
                }
                Assert.That(visuals.GridSlots, Has.Count.EqualTo(10));
                Assert.That(spawn.Position, Is.EqualTo(visuals.GridSlots[0].Center));
                for (int i = 2; i < 10; i++)
                    Assert.That(visuals.GridSlots[i - 2].Distance - visuals.GridSlots[i].Distance, Is.EqualTo(2.1f).Within(0.001f));
                SetFloat(visuals, "_gridSlotWidth", track.RoadWidth);
                visuals.GetSpawnSample(track);
                Assert.That(visuals.GridSlots, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SectorAnchorsAndCheckerUseExistingGatesWithoutChangingTriggers()
        {
            WithTrack(track =>
            {
                TrackVisuals visuals = track.GetComponent<TrackVisuals>();
                Assert.That(visuals.SectorDistances, Has.Count.EqualTo(3));
                Assert.That(visuals.SectorDistances[0], Is.Zero);
                Assert.That(visuals.SectorDistances[1], Is.LessThan(visuals.SectorDistances[2]));
                foreach (int knot in new[] { 30, 65 })
                {
                    Vector3 point = track.GetComponent<SplineContainer>().Spline[knot].Position;
                    TrackSample sample = track.Samples.OrderBy(s => Vector2.SqrMagnitude(s.Position - (Vector2)point)).First();
                    float expected = track.GateDistances.OrderBy(d => Mathf.Abs(d - sample.Distance)).First();
                    Assert.That(visuals.SectorDistances[knot == 30 ? 1 : 2], Is.EqualTo(expected));
                }
                Assert.That(VisualMesh(track, "Sector Lines").vertexCount, Is.EqualTo(12));
                Mesh checker = VisualMesh(track, "Start Finish Checker");
                var settings = new SerializedObject(visuals);
                float depth = settings.FindProperty("_checkerDepth").floatValue;
                Assert.That(checker.vertexCount, Is.EqualTo(Mathf.CeilToInt(track.RoadWidth / (depth * 0.5f)) * 8));
                TrackSample start = track.GetSample(0f);
                Vector3[] points = checker.vertices;
                Assert.That(points.Max(p => Vector2.Dot((Vector2)p - start.Position, start.Tangent))
                    - points.Min(p => Vector2.Dot((Vector2)p - start.Position, start.Tangent)), Is.EqualTo(depth).Within(0.001f));
                Assert.That(checker.colors.Distinct().Count(), Is.EqualTo(2));
                Transform gate = track.transform.Find("Generated Circuit/Checkpoints/Start Finish");
                Assert.That(gate.GetComponent<SpriteRenderer>().enabled, Is.False);
                Assert.That(gate.GetComponent<BoxCollider2D>().size, Is.EqualTo(Vector2.one));
                Assert.That(gate.localScale, Is.EqualTo(new Vector3(0.25f, track.RoadWidth, 1f)));
                Assert.That(gate.GetComponent<BoxCollider2D>().isTrigger, Is.True);
                foreach (TrackVisualQuad quad in visuals.RibbonQuads.Where(q => q.Layer == "Curbs" || q.Layer == "Tire Walls"))
                {
                    float stripe = settings.FindProperty(quad.Layer == "Curbs" ? "_curbStripeLength" : "_tireBlockLength").floatValue;
                    Assert.That(quad.ToDistance - quad.FromDistance, Is.LessThanOrEqualTo(stripe + 0.0001f));
                    IReadOnlyList<TrackVisualRun> runs = quad.Layer == "Curbs" ? visuals.CurbRuns : visuals.TireRuns;
                    TrackVisualRun run = runs.Single(r => quad.FromDistance >= r.Distance - 0.0001f
                        && quad.ToDistance <= r.Distance + r.Length + 0.0001f);
                    int block = Mathf.FloorToInt(((quad.FromDistance + quad.ToDistance) * 0.5f - run.Distance) / stripe);
                    string colorField = (block % 2 == 0) == (quad.Layer == "Curbs") ? "_red" : "_white";
                    Color expectedColor = settings.FindProperty(colorField).colorValue;
                    if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                        expectedColor = expectedColor.linear;
                    Color[] colors = VisualMesh(track, quad.Layer).colors;
                    for (int i = 0; i < 4; i++)
                        Assert.That(colors[quad.VertexOffset + i], Is.EqualTo(expectedColor));
                }
            });
        }

        [Test]
        public void VisualRebuildAndSceneReloadPreserveColliderAndGateGeometry()
        {
            WithTrack(track =>
            {
                EdgeCollider2D[] walls = track.GetComponentsInChildren<EdgeCollider2D>();
                Vector2[][] wallPoints = walls.Select(w => w.points).ToArray();
                float[] radii = walls.Select(w => w.edgeRadius).ToArray();
                BoxCollider2D[] gates = track.GetComponentsInChildren<BoxCollider2D>();
                Vector3[] positions = gates.Select(g => g.transform.localPosition).ToArray();
                Vector3[] scales = gates.Select(g => g.transform.localScale).ToArray();
                Quaternion[] rotations = gates.Select(g => g.transform.localRotation).ToArray();
                Vector2[] sizes = gates.Select(g => g.size).ToArray();
                RebuildVisuals(track);
                track.enabled = false;
                Assert.That(track.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh == null), Is.True);
                track.enabled = true;
                Assert.That(track.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh != null), Is.True);
                for (int i = 0; i < walls.Length; i++)
                {
                    Assert.That(walls[i].points, Is.EqualTo(wallPoints[i]));
                    Assert.That(walls[i].edgeRadius, Is.EqualTo(radii[i]));
                }
                for (int i = 0; i < gates.Length; i++)
                {
                    Assert.That(gates[i].transform.localPosition, Is.EqualTo(positions[i]));
                    Assert.That(gates[i].transform.localScale, Is.EqualTo(scales[i]));
                    Assert.That(gates[i].transform.localRotation, Is.EqualTo(rotations[i]));
                    Assert.That(gates[i].size, Is.EqualTo(sizes[i]));
                    Assert.That(gates[i].isTrigger, Is.True);
                }
                Assert.That(track.transform.Find("Generated Circuit").GetComponentsInChildren<MeshRenderer>(), Has.Length.EqualTo(10));
            });
        }

        [Test]
        public void InvalidVisualThresholdsFailExplicitly()
        {
            WithTrack(track =>
            {
                TrackVisuals visuals = track.GetComponent<TrackVisuals>();
                SetFloat(visuals, "_tireCurvatureThreshold", 0.01f);
                Assert.Throws<InvalidOperationException>(() => visuals.GetSpawnSample(track));
                SetFloat(visuals, "_tireCurvatureThreshold", 0.13f);
            });
        }

        private static void AssertRuns(TrackGenerator track, IReadOnlyList<TrackVisualRun> runs, float threshold, float minimum, float turnSide)
        {
            foreach (TrackVisualRun run in runs)
            {
                Assert.That(run.Length, Is.GreaterThanOrEqualTo(minimum));
                for (int i = 0; i <= run.SegmentCount; i++)
                    Assert.That(track.GetSignedCurvature((run.FirstSample + i) % track.Samples.Count) * run.Side * turnSide,
                        Is.GreaterThan(threshold));
            }
        }

        private static bool ExhaustiveCorridor(TrackGenerator track, Vector2 point, float distance)
        {
            float radius = track.RoadWidth * 0.5f + track.WallThickness + track.SampleSpacing * 0.5f;
            for (int i = 0; i < track.Samples.Count; i++)
            {
                Vector2 a = track.Samples[i].Position;
                Vector2 b = track.Samples[(i + 1) % track.Samples.Count].Position;
                float fraction = Mathf.Clamp01(Vector2.Dot(point - a, b - a) / (b - a).sqrMagnitude);
                float separation = Mathf.Abs(distance - (i + fraction) * track.SampleSpacing);
                if (Mathf.Min(separation, track.Length - separation) > track.RoadWidth * 3f
                    && Vector2.Distance(point, Vector2.Lerp(a, b, fraction)) <= radius)
                    return true;
            }
            return false;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            float fraction = Mathf.Clamp01(Vector2.Dot(point - a, b - a) / (b - a).sqrMagnitude);
            return Vector2.Distance(point, Vector2.Lerp(a, b, fraction));
        }

        private static void SetFloat(TrackVisuals visuals, string field, float value)
        {
            var settings = new SerializedObject(visuals);
            settings.FindProperty(field).floatValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Mesh VisualMesh(TrackGenerator track, string name)
        {
            return track.transform.Find("Generated Circuit/" + name).GetComponent<MeshFilter>().sharedMesh;
        }

        private static void RebuildVisuals(TrackGenerator track)
        {
            track.GetComponent<TrackVisuals>().Build(track, track.transform.Find("Generated Circuit"),
                track.transform.Find("Generated Circuit/Road").GetComponent<MeshRenderer>().sharedMaterial);
        }

        private static void WithTrack(Action<TrackGenerator> assertion)
        {
            var scene = SceneManager.GetSceneByPath(GhostlineSceneBuilder.ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(GhostlineSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform root = scene.GetRootGameObjects().Single(g => g.name == "Ghostline").transform;
                assertion(root.Find("Track").GetComponent<TrackGenerator>());
            }
            finally
            {
                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
