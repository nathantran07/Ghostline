using System;
using System.Collections.Generic;
using Ghostline.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ghostline.Editor
{
    public static class GhostlineMinimapInstaller
    {
        [MenuItem("Tools/Ghostline/Add Minimap To Scene")]
        public static void AddMinimapToScene()
        {
            MinimapView view = AddToScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = view.gameObject;
        }

        /// <summary>Adds or repairs only minimap UI in the supplied loaded scene; never regenerates or saves it.</summary>
        public static MinimapView AddToScene(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before adding the Ghostline minimap.");
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the Ghostline scene before adding its minimap.");

            TrackGenerator track = FindSingle<TrackGenerator>(scene);
            CarController car = FindSingle<CarController>(scene);
            GhostCarView ghost = FindSingle<GhostCarView>(scene, false);
            RaceManager race = FindSingle<RaceManager>(scene);
            HudView hud = FindSingle<HudView>(scene);
            MinimapView existing = FindSingle<MinimapView>(scene, false);
            Canvas canvas = hud.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.GetComponent<CanvasScaler>() == null)
                throw new InvalidOperationException("Ghostline's HUD needs a Canvas with a Canvas Scaler.");
            Sprite dotSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (dotSprite == null)
                throw new InvalidOperationException("Unity's default UI dot sprite could not be loaded.");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Ghostline Minimap");
            GameObject panel = existing == null ? GetOrCreate("Minimap", canvas.transform) : existing.gameObject;
            if (panel.transform.parent != canvas.transform)
                Undo.SetTransformParent(panel.transform, canvas.transform, "Move Ghostline Minimap");
            Image background = GetOrAdd<Image>(panel);
            MinimapView view = GetOrAdd<MinimapView>(panel);
            GameObject mapObject = GetOrCreate("Track", panel.transform);
            RawImage map = GetOrAdd<RawImage>(mapObject);
            Image ghostDot = GetOrAdd<Image>(GetOrCreate("Ghost Dot", mapObject.transform));
            Image playerDot = GetOrAdd<Image>(GetOrCreate("Player Dot", mapObject.transform));
            Outline outline = GetOrAdd<Outline>(playerDot.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[]
            {
                view, background, map, playerDot, ghostDot, outline, panel.transform,
                mapObject.transform, playerDot.transform, ghostDot.transform
            }, "Wire Ghostline Minimap");
            background.material = null;
            map.material = null;
            map.color = Color.white;
            ghostDot.material = null;
            playerDot.material = null;
            ghostDot.sprite = dotSprite;
            playerDot.sprite = dotSprite;
            // The translucent ghost must remain visible when both dots share the countdown spawn.
            ghostDot.transform.SetAsLastSibling();
            view.Configure(track, car.transform, ghost, race, background, map, playerDot, ghostDot);
            EditorUtility.SetDirty(view);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            return view;
        }

        private static T FindSingle<T>(Scene scene, bool required = true) where T : Component
        {
            var matches = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                matches.AddRange(root.GetComponentsInChildren<T>(true));
            if (matches.Count > 1 || (required && matches.Count == 0))
                throw new InvalidOperationException($"Minimap installation needs exactly one {typeof(T).Name} in the target scene.");
            return matches.Count == 0 ? null : matches[0];
        }

        private static GameObject GetOrCreate(string name, Transform parent)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                if (!(existing is RectTransform))
                    throw new InvalidOperationException($"Minimap UI object {name} must have a RectTransform.");
                return existing.gameObject;
            }
            var child = new GameObject(name, typeof(RectTransform));
            // Parenting before registration keeps Undo recreation inside the correct scene.
            child.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(child, "Create Ghostline Minimap UI");
            return child;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component == null ? Undo.AddComponent<T>(gameObject) : component;
        }
    }
}
