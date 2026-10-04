using System;
using System.Collections.Generic;
using Ghostline.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ghostline.Editor
{
    public static class GhostlineThemeInstaller
    {
        [MenuItem("Tools/Ghostline/Add Theme To Scene")]
        public static void AddThemeToScene()
        {
            DecorGenerator decor = AddToScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = decor.gameObject;
        }

        /// <summary>Attaches presentation to the existing track; never regenerates physics or saves the scene.</summary>
        public static DecorGenerator AddToScene(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before adding the Ghostline theme.");
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the Ghostline scene before adding its theme.");
            var tracks = new List<TrackGenerator>();
            foreach (GameObject root in scene.GetRootGameObjects())
                tracks.AddRange(root.GetComponentsInChildren<TrackGenerator>(true));
            if (tracks.Count != 1)
                throw new InvalidOperationException("Theme installation needs exactly one TrackGenerator in the target scene.");
            TrackGenerator track = tracks[0];
            DecorGenerator[] components = track.GetComponents<DecorGenerator>();
            if (components.Length > 1)
                throw new InvalidOperationException("Theme installation needs at most one DecorGenerator on the track.");
            DecorGenerator existing = components.Length == 0 ? null : components[0];
            Transform generated = track.transform.Find("Generated");
            if (generated != null && (existing == null || existing.GeneratedRoot != generated))
                throw new InvalidOperationException("Track/Generated already contains an authored object; rename it before installing the theme.");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Ghostline Theme");
            try
            {
                DecorGenerator decor = existing != null ? existing : Undo.AddComponent<DecorGenerator>(track.gameObject);
                Transform road = track.GeneratedRoot != null ? track.GeneratedRoot.Find("Road") : null;
                if (decor.isActiveAndEnabled && track.isActiveAndEnabled && road != null
                    && track.GeneratedRoadMesh != null
                    && (existing != null || decor.GeneratedRoot == null))
                    decor.Rebuild();
                // Track/material references are resolved on the same object by DecorGenerator.
                // Existing seeds, toggles, densities, palettes and disabled states are retained.
                EditorUtility.SetDirty(decor);
                PrefabUtility.RecordPrefabInstancePropertyModifications(decor);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undoGroup);
                return decor;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }
    }
}
