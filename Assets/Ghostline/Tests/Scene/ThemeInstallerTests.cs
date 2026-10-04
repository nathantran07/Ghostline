using System;
using System.Collections;
using System.IO;
using System.Linq;
using Ghostline.Editor;
using Ghostline.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ghostline.Tests.Scene
{
    public sealed class ThemeInstallerTests
    {
        [Test]
        public void InstallerAddsVisibleDecorUnderGeneratedWithoutSavingOrChangingTrackPhysics()
        {
            WithTrack(track =>
            {
                byte[] saved = File.ReadAllBytes(GhostlineSceneBuilder.ScenePath);
                string settings = EditorJsonUtility.ToJson(track);
                Collider2D[] physics = track.GetComponentsInChildren<Collider2D>();
                string[] colliderSettings = physics.Select(EditorJsonUtility.ToJson).ToArray();
                RaceManager race = track.transform.parent.Find("RaceManager").GetComponent<RaceManager>();
                string raceSettings = EditorJsonUtility.ToJson(race);
                DecorGenerator decor = Install(track.gameObject.scene);
                Assert.That(decor.gameObject, Is.SameAs(track.gameObject));
                Assert.That(decor.GeneratedRoot, Is.SameAs(track.transform.Find("Generated")));
                Assert.That(decor.Footprints, Is.Not.Empty);
                Assert.That(decor.GeneratedRoot.GetComponentsInChildren<MeshRenderer>(), Has.Length.EqualTo(4));
                Assert.That(track.gameObject.scene.isDirty, Is.True);
                CollectionAssert.AreEqual(saved, File.ReadAllBytes(GhostlineSceneBuilder.ScenePath));
                Assert.That(EditorJsonUtility.ToJson(track), Is.EqualTo(settings));
                Assert.That(EditorJsonUtility.ToJson(race), Is.EqualTo(raceSettings));
                CollectionAssert.AreEqual(physics, track.GetComponentsInChildren<Collider2D>());
                CollectionAssert.AreEqual(colliderSettings, physics.Select(EditorJsonUtility.ToJson));
            });
        }

        [Test]
        public void ReinstallationPreservesSeedTogglesDensityPaletteAndOtherObjects()
        {
            WithTrack(track =>
            {
                DecorGenerator first = Install(track.gameObject.scene);
                var settings = new SerializedObject(first);
                settings.FindProperty("_seed").intValue = 2468;
                settings.FindProperty("_showBanners").boolValue = false;
                settings.FindProperty("_treeDensity").floatValue = 0.4f;
                settings.FindProperty("_treePalette").GetArrayElementAtIndex(0).colorValue = Color.green;
                settings.ApplyModifiedPropertiesWithoutUndo();
                first.Rebuild();
                string before = EditorJsonUtility.ToJson(first);
                DecorFootprint[] layout = first.Footprints.ToArray();
                var authored = new GameObject("Authored track object");
                authored.transform.SetParent(track.transform, false);
                try
                {
                    Assert.That(Install(track.gameObject.scene), Is.SameAs(first));
                    Assert.That(EditorJsonUtility.ToJson(first), Is.EqualTo(before));
                    CollectionAssert.AreEqual(layout, first.Footprints);
                    Assert.That(track.GetComponents<DecorGenerator>(), Has.Length.EqualTo(1));
                    Assert.That(track.transform.Cast<Transform>().Count(child => child.name == "Generated"), Is.EqualTo(1));
                    Assert.That(track.transform.Find("Authored track object"), Is.SameAs(authored.transform));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(authored);
                }
            });
        }

        [Test]
        public void AddingThemeIsOneUndoOperationAndRedoRestoresVisibleDecor()
        {
            WithTrack(track =>
            {
                Install(track.gameObject.scene);
                Undo.PerformUndo();
                Assert.That(track.GetComponent<DecorGenerator>(), Is.Null);
                Assert.That(track.transform.Find("Generated"), Is.Null);
                Assert.That(track.transform.Find("Generated Circuit"), Is.Not.Null);
                Undo.PerformRedo();
                DecorGenerator decor = track.GetComponent<DecorGenerator>();
                Assert.That(decor, Is.Not.Null);
                Assert.That(decor.GeneratedRoot, Is.SameAs(track.transform.Find("Generated")));
                Assert.That(decor.Footprints, Is.Not.Empty);
            });
        }

        [Test]
        public void ReinstallationPreservesDisabledDecor()
        {
            WithTrack(track =>
            {
                DecorGenerator decor = Install(track.gameObject.scene);
                decor.enabled = false;
                string before = EditorJsonUtility.ToJson(decor);
                Assert.That(Install(track.gameObject.scene), Is.SameAs(decor));
                Assert.That(EditorJsonUtility.ToJson(decor), Is.EqualTo(before));
                Assert.That(decor.GeneratedRoot, Is.Null);
            });
        }

        [Test]
        public void InactiveUnbuiltTrackCanBeWiredWithoutGeneratingPhysicsOrEnablingIt()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Unbuilt theme track", typeof(TrackGenerator));
            SceneManager.MoveGameObjectToScene(root, scene);
            TrackGenerator track = root.GetComponent<TrackGenerator>();
            track.enabled = false;
            root.SetActive(false);
            try
            {
                string before = EditorJsonUtility.ToJson(track);
                DecorGenerator decor = Install(scene);
                Assert.That(Install(scene), Is.SameAs(decor));
                Assert.That(decor.gameObject, Is.SameAs(root));
                Assert.That(EditorJsonUtility.ToJson(track), Is.EqualTo(before));
                Assert.That(track.enabled, Is.False);
                Assert.That(root.activeSelf, Is.False);
                Assert.That(track.Samples, Is.Empty);
                Assert.That(track.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(decor.GeneratedRoot, Is.Null);
                Assert.That(scene.isDirty, Is.True);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void MissingOrAmbiguousTrackAndAuthoredGeneratedRootFailWithoutAddingDecor()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Theme installer test");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                Assert.Throws<InvalidOperationException>(() => Install(scene));
                root.AddComponent<TrackGenerator>();
                var second = new GameObject("Second track", typeof(TrackGenerator));
                second.transform.SetParent(root.transform, false);
                Assert.Throws<InvalidOperationException>(() => Install(scene));
                UnityEngine.Object.DestroyImmediate(second);
                var authored = new GameObject("Generated");
                authored.transform.SetParent(root.transform, false);
                Assert.Throws<InvalidOperationException>(() => Install(scene));
                Assert.That(root.GetComponent<DecorGenerator>(), Is.Null);
                Assert.That(root.transform.Find("Generated"), Is.SameAs(authored.transform));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            Assert.Throws<InvalidOperationException>(() => Install(default));
        }

        [Test]
        public void TrackMeshReloadAndRegenerationRebuildDecorAndRespectDisabledComponent()
        {
            WithTrack(track =>
            {
                DecorGenerator decor = track.gameObject.AddComponent<DecorGenerator>();
                Assert.That(track.transform.Find("Generated"), Is.SameAs(decor.GeneratedRoot));
                DecorFootprint[] layout = decor.Footprints.ToArray();
                Mesh oldMesh = decor.GeneratedRoot.GetComponentInChildren<MeshFilter>().sharedMesh;
                Collider2D[] physics = track.GetComponentsInChildren<Collider2D>();
                string[] before = physics.Select(EditorJsonUtility.ToJson).ToArray();
                track.enabled = false;
                Assert.That(decor.GeneratedRoot, Is.Null);
                Assert.That(oldMesh == null, Is.True);
                track.enabled = true;
                CollectionAssert.AreEqual(layout, decor.Footprints);
                CollectionAssert.AreEqual(physics, track.GetComponentsInChildren<Collider2D>());
                CollectionAssert.AreEqual(before, physics.Select(EditorJsonUtility.ToJson));
                Transform previous = decor.GeneratedRoot;
                track.Regenerate();
                Assert.That(previous == null, Is.True);
                CollectionAssert.AreEqual(layout, decor.Footprints);
                Assert.That(track.transform.Cast<Transform>().Count(child => child.name == "Generated"), Is.EqualTo(1));
                decor.enabled = false;
                track.Regenerate();
                Assert.That(decor.GeneratedRoot, Is.Null);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidDecorSettingsRejectRegenerationBeforeReplacingPhysicsOrGates(bool exceedBudget)
        {
            WithTrack(track =>
            {
                DecorGenerator decor = Install(track.gameObject.scene);
                Transform circuit = track.transform.Find("Generated Circuit");
                Collider2D[] physics = track.GetComponentsInChildren<Collider2D>();
                string[] before = physics.Select(EditorJsonUtility.ToJson).ToArray();
                var settings = new SerializedObject(decor);
                if (exceedBudget)
                {
                    settings.FindProperty("_bannerSpacing").floatValue = 0.1f;
                    settings.FindProperty("_bannerDensity").floatValue = 4f;
                }
                else
                    settings.FindProperty("_grandstandSize").vector2Value = new Vector2(0.1f, 3.4f);
                settings.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(track.Regenerate);
                Assert.That(track.transform.Find("Generated Circuit"), Is.SameAs(circuit));
                CollectionAssert.AreEqual(physics, track.GetComponentsInChildren<Collider2D>());
                CollectionAssert.AreEqual(before, physics.Select(EditorJsonUtility.ToJson));
                Assert.That(track.GetComponentsInChildren<CheckpointTrigger>(), Has.Length.EqualTo(track.CheckpointCount + 1));
            });
        }

        [UnityTest]
        public IEnumerator RuntimeSameFrameRebuildToggleAndRegenerationUseOnlyCurrentGeometry()
        {
            if (!Application.isBatchMode)
                Assert.Ignore("Run this Main-scene Play regression in the isolated batch copy.");
            EditorSceneManager.OpenScene(GhostlineSceneBuilder.ScenePath, OpenSceneMode.Single);
            Install(SceneManager.GetActiveScene());
            yield return new EnterPlayMode();
            yield return null;
            string failure = null;
            TrackGenerator track = null;
            DecorGenerator decor = null;
            DecorFootprint[] layout = null;
            try
            {
                track = GameObject.Find("Ghostline/Track").GetComponent<TrackGenerator>();
                decor = track.GetComponent<DecorGenerator>();
                layout = decor.Footprints.ToArray();
                Assert.That(layout, Is.Not.Empty);
                MeshRenderer road = track.GeneratedRoot.Find("Road").GetComponent<MeshRenderer>();
                if (!road.isPartOfStaticBatch)
                    StaticBatchingUtility.Combine(track.gameObject);
                Assert.That(road.isPartOfStaticBatch, Is.True);
                Assert.That(road.GetComponent<MeshFilter>().sharedMesh, Is.Not.SameAs(track.GeneratedRoadMesh));
                decor.Rebuild();
                decor.Rebuild();
                CollectionAssert.AreEqual(layout, decor.Footprints, "Repeated rebuilds must preserve the layout.");
                decor.enabled = false;
                decor.enabled = true;
            }
            catch (Exception exception)
            {
                failure = exception.ToString();
            }
            yield return null;
            if (failure == null)
                try
                {
                    CollectionAssert.AreEqual(layout, decor.Footprints, "Re-enable must restore the layout.");
                    track.Regenerate();
                    track.Regenerate();
                    CollectionAssert.AreEqual(layout, decor.Footprints, "Repeated regeneration must preserve the layout.");
                    Assert.That(track.transform.Cast<Transform>().Count(t => t.name == "Generated" && t.gameObject.activeSelf), Is.EqualTo(1));
                    Assert.That(track.GetComponentsInChildren<CheckpointTrigger>(), Has.Length.EqualTo(track.CheckpointCount + 1));
                }
                catch (Exception exception)
                {
                    failure = exception.ToString();
                }
            yield return new ExitPlayMode();
            Assert.That(failure, Is.Null);
        }

        [Test]
        public void FreshBuilderIncludesDecorAndReloadRestoresGeneratedMeshes()
        {
            if (!Application.isBatchMode)
                Assert.Ignore("Run the destructive fresh-scene builder in the isolated batch copy.");
            byte[] saved = File.ReadAllBytes(GhostlineSceneBuilder.ScenePath);
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                GhostlineSceneBuilder.BuildScene();
                TrackGenerator track = SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "Ghostline")
                    .transform.Find("Track").GetComponent<TrackGenerator>();
                Assert.That(track.GetComponent<DecorGenerator>(), Is.Not.Null);
                EditorSceneManager.OpenScene(GhostlineSceneBuilder.ScenePath, OpenSceneMode.Single);
                track = SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "Ghostline")
                    .transform.Find("Track").GetComponent<TrackGenerator>();
                DecorGenerator decor = track.GetComponent<DecorGenerator>();
                Assert.That(decor.GeneratedRoot, Is.SameAs(track.transform.Find("Generated")));
                Assert.That(decor.Footprints, Is.Not.Empty);
                Assert.That(decor.GeneratedRoot.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh != null), Is.True);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                File.WriteAllBytes(GhostlineSceneBuilder.ScenePath, saved);
                AssetDatabase.ImportAsset(GhostlineSceneBuilder.ScenePath);
                foreach (SceneSetup entry in setup)
                    if (entry.isLoaded && entry.isActive && !string.IsNullOrEmpty(entry.path))
                    {
                        EditorSceneManager.RestoreSceneManagerSetup(setup);
                        break;
                    }
            }
        }

        private static DecorGenerator Install(UnityEngine.SceneManagement.Scene scene)
        {
            return GhostlineThemeInstaller.AddToScene(scene);
        }

        private static void WithTrack(Action<TrackGenerator> assertion)
        {
            var scene = SceneManager.GetSceneByPath(GhostlineSceneBuilder.ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(GhostlineSceneBuilder.ScenePath, OpenSceneMode.Additive);
            var preview = EditorSceneManager.NewPreviewScene();
            var holder = new GameObject("Theme integration fixture");
            SceneManager.MoveGameObjectToScene(holder, preview);
            holder.SetActive(false);
            try
            {
                GameObject source = scene.GetRootGameObjects().Single(g => g.name == "Ghostline");
                GameObject copy = UnityEngine.Object.Instantiate(source, holder.transform, false);
                copy.name = source.name;
                TrackGenerator track = copy.transform.Find("Track").GetComponent<TrackGenerator>();
                DecorGenerator sourceDecor = source.transform.Find("Track").GetComponent<DecorGenerator>();
                DecorGenerator copiedDecor = track.GetComponent<DecorGenerator>();
                if (sourceDecor != null && sourceDecor.GeneratedRoot != null)
                {
                    Transform generated = track.transform.Find(sourceDecor.GeneratedRoot.name);
                    if (generated != null)
                        UnityEngine.Object.DestroyImmediate(generated.gameObject);
                }
                if (copiedDecor != null)
                    UnityEngine.Object.DestroyImmediate(copiedDecor);
                holder.SetActive(true);
                assertion(track);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                if (!wasLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
