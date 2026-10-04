using System;
using System.IO;
using System.Linq;
using Ghostline.Editor;
using Ghostline.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace Ghostline.Tests.Scene
{
    public sealed class AudioInstallerTests
    {
        private UnityScene _scene;
        private GameObject _root;
        private CarController _car;
        private GhostCarView _ghost;
        private RaceManager _race;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            _root = new GameObject("Audio installer test");
            SceneManager.MoveGameObjectToScene(_root, _scene);
            _car = Child("Car", _root.transform, typeof(CarController)).GetComponent<CarController>();
            GameObject ghost = Child("Ghost", _root.transform);
            Child("Visual", ghost.transform, typeof(SpriteRenderer));
            _ghost = ghost.AddComponent<GhostCarView>();
            _race = _root.AddComponent<RaceManager>();
            _race.Configure(_car, _ghost, _root.AddComponent<HudView>(), null);
            _camera = Child("Main Camera", _root.transform, typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            _camera.tag = "MainCamera";
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
                EditorSceneManager.ClosePreviewScene(_scene);
        }

        [Test]
        public void InstallationIsPlayerOnlyAndIdempotentWithoutChangingPhysics()
        {
            string carBefore = EditorJsonUtility.ToJson(_car);
            string bodyBefore = EditorJsonUtility.ToJson(_car.GetComponent<Rigidbody2D>());
            string colliderBefore = EditorJsonUtility.ToJson(_car.GetComponent<BoxCollider2D>());
            string raceBefore = EditorJsonUtility.ToJson(_race);
            string ghostBefore = EditorJsonUtility.ToJson(_ghost);
            EngineAudio first = Install();
            Assert.That(Install(), Is.SameAs(first));
            AssertCounts(_scene);
            Assert.That(EditorJsonUtility.ToJson(_car), Is.EqualTo(carBefore));
            Assert.That(EditorJsonUtility.ToJson(_car.GetComponent<Rigidbody2D>()), Is.EqualTo(bodyBefore));
            Assert.That(EditorJsonUtility.ToJson(_car.GetComponent<BoxCollider2D>()), Is.EqualTo(colliderBefore));
            Assert.That(EditorJsonUtility.ToJson(_race), Is.EqualTo(raceBefore));
            Assert.That(EditorJsonUtility.ToJson(_ghost), Is.EqualTo(ghostBefore));
            Assert.That(_scene.isDirty, Is.True);
            Assert.That(_car.GetComponent<AudioSource>().loop, Is.True);
            Assert.That(_car.GetComponent<AudioSource>().playOnAwake, Is.False);
            Assert.That(_car.GetComponent<SfxPlayer>().Source.playOnAwake, Is.False);
            Assert.That(_car.GetComponent<SfxPlayer>().Source.volume, Is.EqualTo(0.6f));
            Assert.That(_car.GetComponent<AudioSource>().clip, Is.Null, "No runtime audio assets may be serialized.");
        }

        [Test]
        public void ReinstallationPreservesTuningDisabledStatesAndAuthoredChildren()
        {
            EngineAudio engine = Install();
            PlayerAudioSettings settings = _car.GetComponent<PlayerAudioSettings>();
            var serialized = new SerializedObject(engine);
            serialized.FindProperty("_bankDetune").floatValue = 0.001f;
            serialized.FindProperty("_shiftGap").floatValue = 0.12f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var volumes = new SerializedObject(settings);
            volumes.FindProperty("_engineVolume").floatValue = 0.2f;
            volumes.ApplyModifiedPropertiesWithoutUndo();
            engine.enabled = false;
            settings.enabled = false;
            SfxPlayer sfx = _car.GetComponent<SfxPlayer>();
            sfx.enabled = false;
            PlayerAudioOutput output = _camera.GetComponent<PlayerAudioOutput>();
            output.enabled = false;
            _car.GetComponent<AudioSource>().enabled = false;
            var authored = Child("Authored dashboard", _car.transform);
            authored.transform.localPosition = Vector3.one * 3f;
            string before = EditorJsonUtility.ToJson(engine);
            string settingsBefore = EditorJsonUtility.ToJson(settings);
            string sfxBefore = EditorJsonUtility.ToJson(sfx);
            string outputBefore = EditorJsonUtility.ToJson(output);
            string sourceBefore = EditorJsonUtility.ToJson(_car.GetComponent<AudioSource>());
            Assert.That(Install(), Is.SameAs(engine));
            Assert.That(EditorJsonUtility.ToJson(engine), Is.EqualTo(before));
            Assert.That(EditorJsonUtility.ToJson(settings), Is.EqualTo(settingsBefore));
            Assert.That(EditorJsonUtility.ToJson(sfx), Is.EqualTo(sfxBefore));
            Assert.That(EditorJsonUtility.ToJson(output), Is.EqualTo(outputBefore));
            Assert.That(EditorJsonUtility.ToJson(_car.GetComponent<AudioSource>()), Is.EqualTo(sourceBefore));
            Assert.That(authored.transform.localPosition, Is.EqualTo(Vector3.one * 3f));
        }

        [Test]
        public void InstallationIsOneUndoOperationAndRedoRestoresWiring()
        {
            Install();
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(_car.GetComponent<EngineAudio>(), Is.Null);
            Assert.That(_car.GetComponent<SfxPlayer>(), Is.Null);
            Assert.That(_car.GetComponent<PlayerAudioSettings>(), Is.Null);
            Assert.That(_car.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(_car.transform.Find("Audio SFX"), Is.Null);
            Assert.That(_camera.GetComponent<PlayerAudioOutput>(), Is.Null);
            Assert.That(_camera.GetComponent<AudioListener>(), Is.Not.Null);
            Undo.PerformRedo();
            AssertCounts(_scene);
            Assert.That(_car.GetComponent<SfxPlayer>().Source,
                Is.SameAs(_car.transform.Find("Audio SFX").GetComponent<AudioSource>()));
            Assert.That(Install(), Is.SameAs(_car.GetComponent<EngineAudio>()));
        }

        [Test]
        public void ExtraListenersAreRemovedWithUndoButTheirObjectsArePreserved()
        {
            GameObject extra = Child("Authored listener", _root.transform, typeof(AudioListener));
            Install();
            Assert.That(extra, Is.Not.Null);
            Assert.That(extra.GetComponent<AudioListener>(), Is.Null);
            Assert.That(_root.GetComponentsInChildren<AudioListener>(true), Has.Length.EqualTo(1));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(extra.GetComponent<AudioListener>(), Is.Not.Null);
            Assert.That(_camera.GetComponent<AudioListener>(), Is.Not.Null);
        }

        [Test]
        public void MissingCameraListenerIsAddedAndInactiveObjectsStayInactive()
        {
            Object.DestroyImmediate(_camera.GetComponent<AudioListener>());
            _root.SetActive(false);
            Install();
            Assert.That(_root.activeSelf, Is.False);
            Assert.That(_camera.GetComponent<AudioListener>(), Is.Not.Null);
            AssertCounts(_scene);
        }

        [Test]
        public void GhostSourcesFailPreflightWithoutChangingAnyObjects()
        {
            AudioSource source = Child("Authored ghost sound", _ghost.transform, typeof(AudioSource)).GetComponent<AudioSource>();
            string before = EditorJsonUtility.ToJson(source);
            Assert.Throws<InvalidOperationException>(() => Install());
            Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(before));
            Assert.That(_car.GetComponent<EngineAudio>(), Is.Null);
            Assert.That(_car.transform.Find("Audio SFX"), Is.Null);
        }

        [TestCase("player")]
        [TestCase("child")]
        [TestCase("elsewhere")]
        public void ConflictingAuthoredSourcesFailWithoutReplacingThem(string location)
        {
            GameObject owner = location == "player" ? _car.gameObject
                : Child("Authored audio", location == "child" ? _car.transform : _root.transform);
            AudioSource source = owner.AddComponent<AudioSource>();
            Assert.Throws<InvalidOperationException>(() => Install());
            Assert.That(owner.GetComponent<AudioSource>(), Is.SameAs(source));
            Assert.That(_car.GetComponent<EngineAudio>(), Is.Null);
        }

        [Test]
        public void AuthoredSfxObjectIsPreservedAndMissingInputsAreRejected()
        {
            GameObject authored = Child("Audio SFX", _car.transform);
            Assert.Throws<InvalidOperationException>(() => Install());
            Assert.That(_car.transform.Find("Audio SFX"), Is.SameAs(authored.transform));
            Object.DestroyImmediate(authored);
            _camera.tag = "Untagged";
            Assert.Throws<InvalidOperationException>(() => Install());
            _camera.tag = "MainCamera";
            Object.DestroyImmediate(_race);
            Assert.Throws<InvalidOperationException>(() => Install());
            Assert.Throws<InvalidOperationException>(() => GhostlineAudioInstaller.AddToScene(default));
            Assert.That(_car.GetComponent<EngineAudio>(), Is.Null);
        }

        [Test]
        public void DuplicatePlayersAndMainCamerasFailBeforeMutation()
        {
            GameObject other = Child("Other car", _root.transform, typeof(CarController));
            Assert.Throws<InvalidOperationException>(() => Install());
            Object.DestroyImmediate(other);
            other = Child("Other main camera", _root.transform, typeof(Camera));
            other.tag = "MainCamera";
            Assert.Throws<InvalidOperationException>(() => Install());
            Assert.That(_car.GetComponent<EngineAudio>(), Is.Null);
        }

        [Test]
        public void SavedSceneCopyCanBeInstalledWithoutSavingOrChangingCarAndTrackConfiguration()
        {
            string path = "Assets/GhostlineAudioVerification-" + Guid.NewGuid().ToString("N") + ".unity";
            Assert.That(AssetDatabase.CopyAsset(GhostlineSceneBuilder.ScenePath, path), Is.True);
            UnityScene scene = default;
            try
            {
                byte[] saved = File.ReadAllBytes(path);
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                GameObject root = scene.GetRootGameObjects().Single(gameObject => gameObject.name == "Ghostline");
                CarController car = root.GetComponentInChildren<CarController>(true);
                TrackGenerator track = root.GetComponentInChildren<TrackGenerator>(true);
                RaceManager race = root.GetComponentInChildren<RaceManager>(true);
                string carBefore = EditorJsonUtility.ToJson(car);
                string trackBefore = EditorJsonUtility.ToJson(track);
                string raceBefore = EditorJsonUtility.ToJson(race);
                GhostlineAudioInstaller.AddToScene(scene);
                AssertCounts(scene);
                Assert.That(EditorJsonUtility.ToJson(car), Is.EqualTo(carBefore));
                Assert.That(EditorJsonUtility.ToJson(track), Is.EqualTo(trackBefore));
                Assert.That(EditorJsonUtility.ToJson(race), Is.EqualTo(raceBefore));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(saved));
                Assert.That(scene.isDirty, Is.True);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void FreshBuilderIncludesAudioAndSerializedReferencesSurviveReload()
        {
            if (!Application.isBatchMode)
                Assert.Ignore("Run the destructive builder in the isolated batch copy.");
            byte[] saved = File.ReadAllBytes(GhostlineSceneBuilder.ScenePath);
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                GhostlineSceneBuilder.BuildScene();
                AssertCounts(SceneManager.GetActiveScene());
                UnityScene scene = EditorSceneManager.OpenScene(GhostlineSceneBuilder.ScenePath, OpenSceneMode.Single);
                AssertCounts(scene);
                CarController car = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CarController>(true)).Single();
                Assert.That(car.GetComponent<SfxPlayer>().Source.transform.parent, Is.SameAs(car.transform));
                Assert.That(car.GetComponent<PlayerAudioSettings>().MasterVolume, Is.EqualTo(0.5f));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                File.WriteAllBytes(GhostlineSceneBuilder.ScenePath, saved);
                AssetDatabase.ImportAsset(GhostlineSceneBuilder.ScenePath);
                if (setup.Any(entry => entry.isLoaded && entry.isActive && !string.IsNullOrEmpty(entry.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private EngineAudio Install()
        {
            return GhostlineAudioInstaller.AddToScene(_scene);
        }

        private static GameObject Child(string name, Transform parent, params Type[] components)
        {
            var child = new GameObject(name, components);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void AssertCounts(UnityScene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            CarController car = roots.SelectMany(root => root.GetComponentsInChildren<CarController>(true)).Single();
            Assert.That(car.GetComponents<EngineAudio>(), Has.Length.EqualTo(1));
            Assert.That(car.GetComponents<SfxPlayer>(), Has.Length.EqualTo(1));
            Assert.That(car.GetComponents<PlayerAudioSettings>(), Has.Length.EqualTo(1));
            Assert.That(car.GetComponents<AudioSource>(), Has.Length.EqualTo(1));
            Assert.That(car.GetComponentsInChildren<AudioSource>(true), Has.Length.EqualTo(2));
            AudioListener[] listeners = roots.SelectMany(root => root.GetComponentsInChildren<AudioListener>(true)).ToArray();
            Assert.That(listeners, Has.Length.EqualTo(1));
            Assert.That(listeners[0].GetComponent<Camera>().CompareTag("MainCamera"), Is.True);
            Assert.That(listeners[0].GetComponent<PlayerAudioOutput>(), Is.Not.Null);
            foreach (GhostCarView ghost in roots.SelectMany(root => root.GetComponentsInChildren<GhostCarView>(true)))
            {
                Assert.That(ghost.GetComponentsInChildren<AudioSource>(true), Is.Empty);
                Assert.That(ghost.GetComponentsInChildren<EngineAudio>(true), Is.Empty);
                Assert.That(ghost.GetComponentsInChildren<SfxPlayer>(true), Is.Empty);
            }
        }
    }
}
