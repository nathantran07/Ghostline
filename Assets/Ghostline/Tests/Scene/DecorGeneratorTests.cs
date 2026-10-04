using System;
using System.IO;
using System.Linq;
using Ghostline.Editor;
using Ghostline.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ghostline.Tests.Scene
{
    public sealed class DecorGeneratorTests
    {
        [Test]
        public void TracksideDecorHasDedicatedGeneratorAndGeometryBuilders()
        {
            foreach (string name in new[] { "DecorGenerator", "DecorPlacementGeometry", "DecorMeshBuilder" })
                Assert.That(typeof(TrackGenerator).Assembly.GetType("Ghostline.Game." + name), Is.Not.Null,
                    name + " must implement the approved trackside decor layer.");
        }

        [Test]
        public void EveryCategoryIsNonemptyAndFullFootprintsClearAllRoadWallsAndDecor()
        {
            WithDecor((track, decor) =>
            {
                Assert.That(track.WidthLimits, Is.Not.Empty, "Exercise locally narrowed corners.");
                Assert.That(track.Crossings, Is.Not.Empty, "Exercise both crossover passes.");
                var geometry = new DecorPlacementGeometry(track);
                foreach (DecorCategory category in Enum.GetValues(typeof(DecorCategory)))
                    Assert.That(decor.Footprints.Count(p => p.Category == category), Is.GreaterThan(0), category.ToString());
                for (int i = 0; i < decor.Footprints.Count; i++)
                {
                    DecorFootprint footprint = decor.Footprints[i];
                    Assert.That(geometry.ClearsTrack(footprint, decor.PlacementMargin), Is.True);
                    AssertExhaustiveMeshClear(track, footprint, decor.PlacementMargin);
                    foreach (EdgeCollider2D wall in track.GetComponentsInChildren<EdgeCollider2D>())
                    {
                        Vector2[] points = wall.points;
                        for (int segment = 1; segment < points.Length; segment++)
                        {
                            Vector2 a = track.transform.InverseTransformPoint(wall.transform.TransformPoint(points[segment - 1] + wall.offset));
                            Vector2 b = track.transform.InverseTransformPoint(wall.transform.TransformPoint(points[segment] + wall.offset));
                            Assert.That(DecorPlacementGeometry.DistanceSquared(footprint.Corners, new[] { a, b }),
                                Is.GreaterThanOrEqualTo(Mathf.Pow(wall.edgeRadius + decor.PlacementMargin, 2f) - 0.00001f));
                        }
                    }
                    for (int j = 0; j < i; j++)
                        Assert.That(DecorPlacementGeometry.DistanceSquared(footprint.Corners, decor.Footprints[j].Corners),
                            Is.GreaterThanOrEqualTo(decor.PlacementMargin * decor.PlacementMargin - 0.00001f));
                }
                TestContext.WriteLine(string.Join(", ", Enum.GetValues(typeof(DecorCategory)).Cast<DecorCategory>()
                    .Select(category => category + "=" + decor.Footprints.Count(p => p.Category == category))));
            });
        }

        [Test]
        public void SameSeedRebuildsIdenticalFootprintsVerticesIndicesAndColors()
        {
            WithDecor((track, decor) =>
            {
                DecorFootprint[] before = decor.Footprints.ToArray();
                MeshFilter[] filters = decor.GeneratedRoot.GetComponentsInChildren<MeshFilter>();
                Vector3[][] vertices = filters.Select(f => f.sharedMesh.vertices).ToArray();
                int[][] triangles = filters.Select(f => f.sharedMesh.triangles).ToArray();
                Color[][] colors = filters.Select(f => f.sharedMesh.colors).ToArray();
                decor.Rebuild();
                CollectionAssert.AreEqual(before, decor.Footprints);
                filters = decor.GeneratedRoot.GetComponentsInChildren<MeshFilter>();
                for (int i = 0; i < filters.Length; i++)
                {
                    CollectionAssert.AreEqual(vertices[i], filters[i].sharedMesh.vertices);
                    CollectionAssert.AreEqual(triangles[i], filters[i].sharedMesh.triangles);
                    CollectionAssert.AreEqual(colors[i], filters[i].sharedMesh.colors);
                }
            });
        }

        [TestCase("_showTrees", DecorCategory.Trees)]
        [TestCase("_showGrandstands", DecorCategory.Grandstands)]
        [TestCase("_showTires", DecorCategory.Tires)]
        [TestCase("_showBanners", DecorCategory.Banners)]
        public void CategoryTogglePreservesOtherLayoutsAndMeshes(string field, DecorCategory category)
        {
            WithDecor((track, decor) =>
            {
                DecorFootprint[] before = decor.Footprints.ToArray();
                var meshes = decor.GeneratedRoot.GetComponentsInChildren<MeshFilter>()
                    .ToDictionary(f => f.name, f => f.sharedMesh.vertices);
                var settings = new SerializedObject(decor);
                settings.FindProperty(field).boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                decor.Rebuild();
                CollectionAssert.AreEqual(before.Where(p => p.Category != category), decor.Footprints);
                foreach (MeshFilter filter in decor.GeneratedRoot.GetComponentsInChildren<MeshFilter>())
                    if (filter.name == category.ToString())
                        Assert.That(filter.sharedMesh.vertexCount, Is.Zero);
                    else
                        CollectionAssert.AreEqual(meshes[filter.name], filter.sharedMesh.vertices);
                settings.FindProperty(field).boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
                decor.Rebuild();
                CollectionAssert.AreEqual(before, decor.Footprints);
            });
        }

        [Test]
        public void MeshesUseExistingMaterialStayBelowCarsAndPreservePhysicsGatesAndSpawn()
        {
            WithDecor((track, decor) =>
            {
                Material material = track.transform.Find("Generated Circuit/Road").GetComponent<MeshRenderer>().sharedMaterial;
                string before = EditorJsonUtility.ToJson(track.transform.parent.Find("RaceManager").GetComponent<RaceManager>());
                Collider2D[] colliders = track.GetComponentsInChildren<Collider2D>();
                string[] colliderSettings = colliders.Select(EditorJsonUtility.ToJson).ToArray();
                float[] gates = track.GateDistances.ToArray();
                decor.Rebuild();
                Assert.That(decor.GeneratedRoot.GetComponentsInChildren<MeshRenderer>(), Has.Length.EqualTo(4));
                Assert.That(decor.GeneratedRoot.GetComponentsInChildren<Collider2D>(), Is.Empty);
                Assert.That(decor.GeneratedRoot.GetComponentsInChildren<MonoBehaviour>(), Is.Empty);
                foreach (MeshRenderer renderer in decor.GeneratedRoot.GetComponentsInChildren<MeshRenderer>())
                {
                    Assert.That(renderer.sharedMaterial, Is.SameAs(material));
                    Assert.That(renderer.sortingOrder, Is.InRange(-6, 3));
                    Assert.That(renderer.gameObject.isStatic, Is.True);
                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh.subMeshCount, Is.EqualTo(1));
                    Assert.That(mesh.indexFormat, Is.EqualTo(IndexFormat.UInt32));
                    Vector2[][] footprints = decor.Footprints.Where(p => p.Category.ToString() == renderer.name)
                        .Select(p => p.Corners).ToArray();
                    foreach (Vector3 vertex in mesh.vertices)
                        if (!footprints.Any(corners => DecorPlacementGeometry.Contains(corners, vertex)))
                            Assert.Fail(renderer.name + " vertex " + vertex.ToString("F6") + " must fit its reserved footprint. Nearest local offset: "
                            + string.Join(",", decor.Footprints.Where(p => p.Category.ToString() == renderer.name)
                                .OrderBy(p => (p.Center - (Vector2)vertex).sqrMagnitude).Take(1)
                                .Select(p => new Vector2(Vector2.Dot((Vector2)vertex - p.Center, p.Tangent),
                                    Vector2.Dot((Vector2)vertex - p.Center, new Vector2(-p.Tangent.y, p.Tangent.x))).ToString("F6")
                                    + " / " + p.HalfSize.ToString("F6"))));
                }
                CollectionAssert.AreEqual(colliders, track.GetComponentsInChildren<Collider2D>());
                CollectionAssert.AreEqual(colliderSettings, colliders.Select(EditorJsonUtility.ToJson));
                CollectionAssert.AreEqual(gates, track.GateDistances);
                Assert.That(EditorJsonUtility.ToJson(track.transform.parent.Find("RaceManager").GetComponent<RaceManager>()), Is.EqualTo(before));
                Mesh[] old = decor.GeneratedRoot.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                decor.enabled = false;
                Assert.That(decor.GeneratedRoot, Is.Null);
                Assert.That(old.All(mesh => mesh == null), Is.True);
                decor.enabled = true;
                Assert.That(decor.Footprints, Is.Not.Empty);
            });
        }

        [Test]
        public void ClearanceChecksPolygonInteriorsEdgesAndWallEndCaps()
        {
            Vector2[] box = DecorPlacementGeometry.Rectangle(Vector2.zero, Vector2.right, Vector2.one);
            Assert.That(DecorPlacementGeometry.DistanceSquared(box,
                DecorPlacementGeometry.Rectangle(Vector2.zero, Vector2.right, Vector2.one * 0.2f)), Is.Zero);
            Assert.That(DecorPlacementGeometry.DistanceSquared(box,
                new[] { new Vector2(-2f, 0f), new Vector2(2f, 0f) }), Is.Zero);
            Assert.That(DecorPlacementGeometry.DistanceSquared(box,
                new[] { new Vector2(1.2f, 1.2f), new Vector2(2f, 2f) }), Is.EqualTo(0.08f).Within(0.00001f));
            Assert.That(DecorPlacementGeometry.DistanceSquared(box,
                DecorPlacementGeometry.Rectangle(new Vector2(2.5f, 0f), Vector2.right, Vector2.one)),
                Is.EqualTo(0.25f).Within(0.00001f));
            Vector2 center = new Vector2(122f, -46f);
            Vector2 tangent = new Vector2(0.6f, 0.8f);
            Vector2[] rotated = DecorPlacementGeometry.Rectangle(center, tangent, new Vector2(1.8f, 0.35f));
            Vector2 edge = center + tangent * -0.9f + new Vector2(-tangent.y, tangent.x) * 0.35f;
            Assert.That(DecorPlacementGeometry.Contains(rotated, edge), Is.True,
                "Long rotated edges use distance-scaled floating-point tolerance.");
        }

        [Test]
        public void CombinedGeometrySupportsMoreThan65535Vertices()
        {
            var builder = new DecorMeshBuilder();
            Color[] palette = Enumerable.Repeat(Color.white, 7).ToArray();
            for (int i = 0; i < 1000; i++)
                builder.Add(new DecorFootprint(DecorCategory.Trees, Vector2.zero, Vector2.right,
                    Vector2.one * 1.6f, 0f, 1, i), palette, 0.18f);
            Mesh mesh = builder.Create("Large decor test");
            try
            {
                Assert.That(mesh.vertexCount, Is.GreaterThan(65535));
                Assert.That(mesh.indexFormat, Is.EqualTo(IndexFormat.UInt32));
                Assert.That(mesh.triangles.Max(), Is.GreaterThan(65535));
                Assert.That(mesh.triangles.Max(), Is.LessThan(mesh.vertexCount));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [TestCase("_placementMargin", -1f)]
        [TestCase("_placementMargin", float.NaN)]
        [TestCase("_treeDensity", float.PositiveInfinity)]
        [TestCase("_bannerSpacing", 0f)]
        public void InvalidSettingsFailBeforeReplacingExistingMeshes(string field, float value)
        {
            WithDecor((track, decor) =>
            {
                Mesh original = decor.GeneratedRoot.GetComponentInChildren<MeshFilter>().sharedMesh;
                var settings = new SerializedObject(decor);
                settings.FindProperty(field).floatValue = value;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(decor.Rebuild);
                Assert.That(decor.GeneratedRoot.GetComponentInChildren<MeshFilter>().sharedMesh, Is.SameAs(original));
            });
        }

        [Test]
        public void ZeroDensitiesAndBoundedAttemptsAllowAnEmptyLayout()
        {
            WithDecor((track, decor) =>
            {
                var settings = new SerializedObject(decor);
                foreach (string field in new[] { "_treeDensity", "_grandstandDensity", "_tireDensity", "_bannerDensity" })
                    settings.FindProperty(field).floatValue = 0f;
                settings.FindProperty("_maxPlacementAttempts").intValue = 1;
                settings.ApplyModifiedPropertiesWithoutUndo();
                decor.Rebuild();
                Assert.That(decor.Footprints, Is.Empty);
            });
        }

        [Test]
        public void DecorRendersAndExportsStartAndCornerPreviews()
        {
            WithDecor((track, decor) =>
            {
                var cameraObject = new GameObject("Decor preview camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, track.gameObject.scene);
                var target = new RenderTexture(1600, 900, 24);
                var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    Camera camera = cameraObject.GetComponent<Camera>();
                    camera.scene = track.gameObject.scene;
                    camera.cameraType = CameraType.Preview;
                    camera.enabled = false;
                    camera.orthographic = true;
                    camera.orthographicSize = 10f;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.targetTexture = target;
                    DecorFootprint stand = decor.Footprints.First(p => p.Category == DecorCategory.Grandstands);
                    DecorFootprint tire = decor.Footprints.First(p => p.Category == DecorCategory.Tires);
                    Vector2[] centers = { (track.GetSample(0f).Position + stand.Center) * 0.5f,
                        track.GetSample(tire.TrackDistance).Position };
                    string[] paths = { "Logs/DecorStartPreview.png", "Logs/DecorCornerPreview.png" };
                    for (int i = 0; i < centers.Length; i++)
                    {
                        Vector3 world = track.transform.TransformPoint(centers[i]);
                        cameraObject.transform.position = new Vector3(world.x, world.y, -10f);
                        foreach (Vector2 corner in (i == 0 ? stand : tire).Corners)
                        {
                            Vector3 viewport = camera.WorldToViewportPoint(track.transform.TransformPoint(corner));
                            Assert.That(viewport.x, Is.InRange(0f, 1f));
                            Assert.That(viewport.y, Is.InRange(0f, 1f), "The reviewed decor must fit completely in the preview.");
                        }
                        camera.Render();
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
                        image.Apply();
                        Color expected = i == 0 ? new Color(0.48f, 0.51f, 0.55f) : new Color(33f / 255f, 33f / 255f, 33f / 255f);
                        Assert.That(image.GetPixels().Count(c => Mathf.Abs(c.r - expected.r) < 0.02f
                            && Mathf.Abs(c.g - expected.g) < 0.02f && Mathf.Abs(c.b - expected.b) < 0.02f),
                            Is.GreaterThan(40), "Expected solid decor fill must appear in the rendered preview.");
                        if (Application.isBatchMode)
                        {
                            Directory.CreateDirectory("Logs");
                            File.WriteAllBytes(paths[i], image.EncodeToPNG());
                        }
                    }
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

        private static void AssertExhaustiveMeshClear(TrackGenerator track, DecorFootprint footprint, float margin)
        {
            foreach (string path in new[] { "Generated Circuit/Road", "Generated Circuit/Walls/Wall Surface" })
            {
                Mesh mesh = track.transform.Find(path).GetComponent<MeshFilter>().sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                Vector2[] corners = footprint.Corners;
                Vector2 minimum = corners.Aggregate(Vector2.Min) - Vector2.one * margin;
                Vector2 maximum = corners.Aggregate(Vector2.Max) + Vector2.one * margin;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector2[] triangle = { vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]] };
                    Vector2 low = triangle.Aggregate(Vector2.Min);
                    Vector2 high = triangle.Aggregate(Vector2.Max);
                    if (high.x < minimum.x || low.x > maximum.x || high.y < minimum.y || low.y > maximum.y)
                        continue;
                    Assert.That(DecorPlacementGeometry.DistanceSquared(corners, triangle),
                        Is.GreaterThanOrEqualTo(margin * margin - 0.00001f), path + " full triangle clearance");
                }
            }
        }

        private static void WithDecor(Action<TrackGenerator, DecorGenerator> assertion)
        {
            var scene = SceneManager.GetSceneByPath(GhostlineSceneBuilder.ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(GhostlineSceneBuilder.ScenePath, OpenSceneMode.Additive);
            DecorGenerator decor = null;
            try
            {
                TrackGenerator track = scene.GetRootGameObjects().Single(g => g.name == "Ghostline")
                    .transform.Find("Track").GetComponent<TrackGenerator>();
                Collider2D[] physics = track.GetComponentsInChildren<Collider2D>();
                string[] physicsBefore = physics.Select(EditorJsonUtility.ToJson).ToArray();
                string trackBefore = EditorJsonUtility.ToJson(track);
                RaceManager race = track.transform.parent.Find("RaceManager").GetComponent<RaceManager>();
                string raceBefore = EditorJsonUtility.ToJson(race);
                decor = track.gameObject.AddComponent<DecorGenerator>();
                assertion(track, decor);
                CollectionAssert.AreEqual(physics, track.GetComponentsInChildren<Collider2D>());
                CollectionAssert.AreEqual(physicsBefore, physics.Select(EditorJsonUtility.ToJson));
                Assert.That(EditorJsonUtility.ToJson(track), Is.EqualTo(trackBefore));
                Assert.That(EditorJsonUtility.ToJson(race), Is.EqualTo(raceBefore));
            }
            finally
            {
                if (decor != null)
                    UnityEngine.Object.DestroyImmediate(decor);
                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
