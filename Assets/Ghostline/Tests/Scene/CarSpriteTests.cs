using System.IO;
using Ghostline.Core;
using Ghostline.Editor;
using Ghostline.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace Ghostline.Tests.Scene
{
    public sealed class CarSpriteTests
    {
        private const string SpritePath = "Assets/Ghostline/Game/Art/LAMBO.png";

        [Test]
        public void SpriteImportMatchesTheOriginalCarLength()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.rect.height / sprite.pixelsPerUnit, Is.EqualTo(1.4f).Within(0.0001f));
        }

        [Test]
        public void SavedSceneCarsAlignWithMovementAndKeepTheirPhysics()
        {
            UnityScene scene = SceneManager.GetSceneByPath(GhostlineSceneBuilder.ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(GhostlineSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                AssertCars(scene);
            }
            finally
            {
                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void BuilderProducesTheSameCarPresentation()
        {
            if (!Application.isBatchMode)
                Assert.Ignore("The builder's replacement confirmation is interactive; run this test in batch mode.");
            byte[] savedScene = File.ReadAllBytes(GhostlineSceneBuilder.ScenePath);
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                GhostlineSceneBuilder.BuildScene();
                AssertCars(SceneManager.GetActiveScene());
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                File.WriteAllBytes(GhostlineSceneBuilder.ScenePath, savedScene);
                AssetDatabase.ImportAsset(GhostlineSceneBuilder.ScenePath);
                foreach (SceneSetup saved in setup)
                {
                    if (saved.isLoaded && saved.isActive && !string.IsNullOrEmpty(saved.path))
                    {
                        EditorSceneManager.RestoreSceneManagerSetup(setup);
                        break;
                    }
                }
            }
        }

        [Test]
        public void GhostMaterialRendersBlueWithoutLights()
        {
            UnityScene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(1024, 768, 24);
            var image = new Texture2D(1024, 768, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
                CreatePreviewCar(scene, "Player", sprite, new Vector3(1000f, 999.4f, 0f), Color.white,
                    AssetDatabase.LoadAssetAtPath<Material>(
                        "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat"));
                CreatePreviewCar(scene, "Ghost", sprite, new Vector3(1000f, 1000.6f, 0f),
                    new Color(0.8f, 0.95f, 1f, 0.4f),
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/Ghostline/Game/Art/GhostSprite.mat"));
                var cameraObject = new GameObject("Car preview camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.transform.position = new Vector3(1000f, 1000f, -10f);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.cameraType = CameraType.Preview;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 1.6f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.17f, 0.2f, 0.25f);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, 1024f, 768f), 0, 0);
                image.Apply();
                Color ghostBody = image.GetPixel(536, 528);
                Assert.That(ghostBody.b, Is.GreaterThan(image.GetPixel(20, 20).b + 0.1f));
                Assert.That(ghostBody.b, Is.GreaterThan(ghostBody.g));
                Assert.That(ghostBody.g, Is.GreaterThan(ghostBody.r));
                Color playerBody = image.GetPixel(536, 240);
                Assert.That(playerBody.r, Is.GreaterThan(playerBody.b + 0.2f));
                if (Application.isBatchMode)
                {
                    Directory.CreateDirectory("Logs");
                    File.WriteAllBytes("Logs/LamboPreview.png", image.EncodeToPNG());
                }
                camera.targetTexture = null;
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void ReplayShowsAndHidesTheChildWithoutChangingItsRotation()
        {
            var root = new GameObject("Ghost replay test");
            var visual = new GameObject("Visual", typeof(SpriteRenderer));
            visual.transform.SetParent(root.transform, false);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            try
            {
                GhostCarView view = root.AddComponent<GhostCarView>();
                view.Hide();
                Assert.That(renderer.enabled, Is.False);
                view.ShowAt(0f);
                Assert.That(renderer.enabled, Is.False);
                var data = new BestLapData { LapTime = 1f };
                data.Samples.Add(new GhostSample(0f, 3f, 4f, -45f));
                view.SetLap(data);
                view.ShowAt(0f);
                Assert.That(renderer.enabled, Is.True);
                Assert.That(root.transform.position, Is.EqualTo(new Vector3(3f, 4f, 0f)));
                Assert.That(Vector3.Dot(-visual.transform.up, root.transform.up), Is.GreaterThan(0.999f));
                view.SetLap(null);
                Assert.That(renderer.enabled, Is.False);
                Assert.That(root.GetComponent<SpriteRenderer>(), Is.Null);
                Assert.That(root.GetComponent<SolidSprite>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreatePreviewCar(UnityScene scene, string name, Sprite sprite, Vector3 position,
            Color color, Material material)
        {
            var gameObject = new GameObject(name, typeof(SpriteRenderer));
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            gameObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, 90f));
            SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sharedMaterial = material;
        }

        private static void AssertCars(UnityScene scene)
        {
            Transform root = null;
            foreach (GameObject gameObject in scene.GetRootGameObjects())
            {
                if (gameObject.name == "Ghostline")
                    root = gameObject.transform;
            }
            Assert.That(root, Is.Not.Null);
            Transform car = root.Find("Car");
            Transform ghost = root.Find("Ghost");
            AssertCarVisual(car, 5, Color.white);
            AssertCarVisual(ghost, 4, new Color(0.8f, 0.95f, 1f, 0.4f));
            TrackGenerator track = root.Find("Track").GetComponent<TrackGenerator>();
            TrackSample spawn = track.GetComponent<TrackVisuals>().GetSpawnSample(track);
            Assert.That(Vector2.Distance(car.position, spawn.Position), Is.LessThan(0.001f));
            Assert.That(Vector2.Distance(ghost.position, spawn.Position), Is.LessThan(0.001f));
            Assert.That(Vector2.Dot(car.up, spawn.Tangent), Is.GreaterThan(0.999f));
            Assert.That(Vector2.Dot(ghost.up, spawn.Tangent), Is.GreaterThan(0.999f));
            Assert.That(car.GetComponent<Rigidbody2D>(), Is.Not.Null);
            BoxCollider2D collider = car.GetComponent<BoxCollider2D>();
            Assert.That(collider.isTrigger, Is.False);
            Assert.That(collider.size.x, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(collider.offset.y - collider.size.y * 0.5f, Is.GreaterThan(-0.52f));
            Assert.That(collider.offset.y + collider.size.y * 0.5f, Is.LessThan(0.7f));
            Assert.That(ghost.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(ghost.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
            Material material = ghost.GetComponentInChildren<SpriteRenderer>(true).sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("Ghostline/Ghost Sprite"));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
        }

        private static void AssertCarVisual(Transform root, int sortingOrder, Color color)
        {
            Assert.That(root, Is.Not.Null);
            Assert.That(root.localScale, Is.EqualTo(Vector3.one));
            Assert.That(root.GetComponent<SpriteRenderer>(), Is.Null);
            Assert.That(root.GetComponentsInChildren<SolidSprite>(true), Is.Empty);
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            Assert.That(renderers, Has.Length.EqualTo(1));
            SpriteRenderer renderer = renderers[0];
            Assert.That(renderer.transform.parent, Is.EqualTo(root));
            Assert.That(renderer.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(renderer.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Vector3.Dot(-renderer.transform.up, root.up), Is.GreaterThan(0.999f));
            Assert.That(renderer.sprite, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath)));
            Assert.That(renderer.sprite.rect.height / renderer.sprite.pixelsPerUnit,
                Is.EqualTo(1.4f).Within(0.0001f));
            Assert.That(renderer.sortingOrder, Is.EqualTo(sortingOrder));
            Assert.That(renderer.color, Is.EqualTo(color));
        }
    }
}
