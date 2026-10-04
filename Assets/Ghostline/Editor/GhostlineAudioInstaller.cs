using System;
using System.Collections.Generic;
using Ghostline.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ghostline.Editor
{
    public static class GhostlineAudioInstaller
    {
        private const string SfxObjectName = "Audio SFX";

        [MenuItem("Tools/Ghostline/Add Audio To Scene")]
        public static void AddAudioToScene()
        {
            EngineAudio engine = AddToScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = engine.gameObject;
        }

        /// <summary>Adds player-only synthesized audio with Undo; never rebuilds or saves the scene.</summary>
        public static EngineAudio AddToScene(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before adding Ghostline audio.");
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the Ghostline scene before adding audio.");
            CarController car = Single<CarController>(scene);
            RaceManager race = Single<RaceManager>(scene);
            if (race.PlayerCar != car || car.GetComponentInParent<GhostCarView>() != null)
                throw new InvalidOperationException("Audio installation needs the RaceManager's configured player car.");
            var cameras = Find<Camera>(scene);
            cameras.RemoveAll(camera => !camera.CompareTag("MainCamera"));
            if (cameras.Count != 1 || cameras[0].GetComponentInParent<GhostCarView>() != null)
                throw new InvalidOperationException("Audio installation needs exactly one MainCamera-tagged camera outside the ghost.");
            Camera camera = cameras[0];
            foreach (GhostCarView ghost in Find<GhostCarView>(scene))
                if (ghost.GetComponentsInChildren<AudioSource>(true).Length != 0
                    || ghost.GetComponentsInChildren<PlayerAudioSettings>(true).Length != 0
                    || ghost.GetComponentsInChildren<PlayerAudioOutput>(true).Length != 0)
                    throw new InvalidOperationException("The ghost hierarchy must be silent. Remove its authored audio components before installing.");

            EngineAudio engine = Owned<EngineAudio>(scene, car.gameObject);
            SfxPlayer sfx = Owned<SfxPlayer>(scene, car.gameObject);
            PlayerAudioSettings settings = Owned<PlayerAudioSettings>(scene, car.gameObject);
            PlayerAudioOutput output = Owned<PlayerAudioOutput>(scene, camera.gameObject);
            AudioSource[] engineSources = car.GetComponents<AudioSource>();
            if (engineSources.Length > 1 || (engine == null && engineSources.Length != 0))
                throw new InvalidOperationException("The player has conflicting authored AudioSources; installation will not replace them.");
            AudioSource engineSource = engineSources.Length == 0 ? null : engineSources[0];
            AudioSource sfxSource = sfx != null ? sfx.Source : null;
            Transform sfxRoot = sfxSource != null ? sfxSource.transform : car.transform.Find(SfxObjectName);
            if (sfx == null && sfxRoot != null)
                throw new InvalidOperationException("Car/Audio SFX already contains an authored object; rename it before installing.");
            if (sfxRoot != null)
            {
                if (sfxRoot == car.transform || !sfxRoot.IsChildOf(car.transform)
                    || sfxRoot.GetComponentInParent<GhostCarView>() != null
                    || sfxRoot.GetComponent<EngineAudio>() != null)
                    throw new InvalidOperationException("The SFX source must be on a separate child of the player.");
                AudioSource[] sources = sfxRoot.GetComponents<AudioSource>();
                if (sources.Length > 1)
                    throw new InvalidOperationException("The SFX child must have at most one AudioSource.");
                sfxSource = sources.Length == 0 ? null : sources[0];
            }
            foreach (AudioSource source in Find<AudioSource>(scene))
            {
                if (source != engineSource && source != sfxSource)
                    throw new InvalidOperationException("Only the player may make sound; conflicting authored AudioSources were left unchanged.");
                if (source.clip != null)
                    throw new InvalidOperationException("Ghostline audio uses generated clips only; unassign authored clips before installing.");
            }
            List<AudioListener> listeners = Find<AudioListener>(scene);
            AudioListener listener = camera.GetComponent<AudioListener>();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Ghostline Audio");
            try
            {
                settings = settings != null ? settings : Undo.AddComponent<PlayerAudioSettings>(car.gameObject);
                if (engineSource == null)
                {
                    engineSource = Undo.AddComponent<AudioSource>(car.gameObject);
                    ConfigureNewSource(engineSource, 1f, true);
                }
                engine = engine != null ? engine : Undo.AddComponent<EngineAudio>(car.gameObject);
                if (sfxRoot == null)
                {
                    var child = new GameObject(SfxObjectName);
                    // Register after parenting so Undo/Redo retains target-scene membership.
                    child.transform.SetParent(car.transform, false);
                    Undo.RegisterCreatedObjectUndo(child, "Create Ghostline SFX source");
                    sfxRoot = child.transform;
                }
                if (sfxSource == null)
                {
                    sfxSource = Undo.AddComponent<AudioSource>(sfxRoot.gameObject);
                    ConfigureNewSource(sfxSource, settings.SfxVolume, false);
                }
                sfx = sfx != null ? sfx : Undo.AddComponent<SfxPlayer>(car.gameObject);
                if (listener == null)
                    listener = Undo.AddComponent<AudioListener>(camera.gameObject);
                foreach (AudioListener extra in listeners)
                    if (extra != listener)
                        Undo.DestroyObjectImmediate(extra);
                output = output != null ? output : Undo.AddComponent<PlayerAudioOutput>(camera.gameObject);
                Undo.RecordObjects(new UnityEngine.Object[] { engine, sfx, output }, "Wire Ghostline Audio");
                engine.UpgradeFactoryHarmonics();
                engine.Configure(race);
                sfx.Configure(race, sfxSource);
                output.Configure(settings);
                foreach (Component component in new Component[] { settings, engineSource, engine, sfxSource, sfx, listener, output })
                {
                    EditorUtility.SetDirty(component);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undoGroup);
                return engine;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        private static void ConfigureNewSource(AudioSource source, float volume, bool loop)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = volume;
        }

        private static T Single<T>(Scene scene) where T : Component
        {
            List<T> matches = Find<T>(scene);
            if (matches.Count != 1)
                throw new InvalidOperationException($"Audio installation needs exactly one {typeof(T).Name} in the target scene.");
            return matches[0];
        }

        private static T Owned<T>(Scene scene, GameObject owner) where T : Component
        {
            List<T> matches = Find<T>(scene);
            if (matches.Count > 1 || (matches.Count == 1 && matches[0].gameObject != owner))
                throw new InvalidOperationException($"Audio installation found conflicting {typeof(T).Name} components.");
            return matches.Count == 0 ? null : matches[0];
        }

        private static List<T> Find<T>(Scene scene) where T : Component
        {
            var matches = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                matches.AddRange(root.GetComponentsInChildren<T>(true));
            return matches;
        }
    }
}
