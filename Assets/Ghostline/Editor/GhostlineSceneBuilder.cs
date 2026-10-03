using System;
using System.IO;
using Ghostline.Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ghostline.Editor
{
    /// <summary>Builds the asset-free game scene using the project's existing URP configuration.</summary>
    public static class GhostlineSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Tools/Ghostline/Build Scene")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the Ghostline scene.");
            const string settingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath) == null
                || TMP_Settings.defaultFontAsset == null)
            {
                const string message = "Import TMP Essential Resources first: Window > TextMeshPro > "
                    + "Import TMP Essential Resources. Then run Tools > Ghostline > Build Scene again.";
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Ghostline needs TMP resources", message, "OK");
                throw new InvalidOperationException(message);
            }
            if (!Application.isBatchMode)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Replace Ghostline scene?",
                    "This rebuilds Assets/Scenes/Main.unity and replaces edits to that scene.", "Rebuild", "Cancel"))
                    return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Ghostline");
            var track = new GameObject("Track");
            track.transform.SetParent(root.transform, false);
            CreateRectangle("Road", track.transform, Vector2.zero, new Vector2(24f, 16f),
                new Color(0.17f, 0.2f, 0.25f), 0);
            CreateRectangle("Infield", track.transform, Vector2.zero, new Vector2(16f, 8f),
                new Color(0.055f, 0.07f, 0.09f), 1);
            var walls = new GameObject("Walls");
            walls.transform.SetParent(track.transform, false);
            CreateWall("Outer Top", walls.transform, new Vector2(0f, 8f), new Vector2(24.7f, 0.5f));
            CreateWall("Outer Bottom", walls.transform, new Vector2(0f, -8f), new Vector2(24.7f, 0.5f));
            CreateWall("Outer Left", walls.transform, new Vector2(-12f, 0f), new Vector2(0.5f, 16.5f));
            CreateWall("Outer Right", walls.transform, new Vector2(12f, 0f), new Vector2(0.5f, 16.5f));
            CreateWall("Inner Top", walls.transform, new Vector2(0f, 4f), new Vector2(16.5f, 0.5f));
            CreateWall("Inner Bottom", walls.transform, new Vector2(0f, -4f), new Vector2(16.5f, 0.5f));
            CreateWall("Inner Left", walls.transform, new Vector2(-8f, 0f), new Vector2(0.5f, 8.5f));
            CreateWall("Inner Right", walls.transform, new Vector2(8f, 0f), new Vector2(0.5f, 8.5f));

            GameObject carObject = CreateRectangle("Car", root.transform, new Vector2(-2f, -6f),
                new Vector2(0.8f, 1.4f), new Color(0.1f, 0.8f, 0.95f), 5);
            carObject.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
            AddUnitCollider(carObject, false);
            Rigidbody2D body = carObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearDamping = 1.2f;
            body.angularDamping = 8f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CarController car = carObject.AddComponent<CarController>();

            GameObject ghostObject = CreateRectangle("Ghost", root.transform, new Vector2(-2f, -6f),
                new Vector2(0.8f, 1.4f), new Color(0.8f, 0.95f, 1f, 0.3f), 4);
            ghostObject.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
            GhostCarView ghost = ghostObject.AddComponent<GhostCarView>();

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(-2f, -6f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f);
            CameraFollow cameraFollow = cameraObject.AddComponent<CameraFollow>();
            cameraFollow.Configure(car.transform);

            HudView hud = CreateHud(root.transform);
            var raceObject = new GameObject("RaceManager", typeof(RaceManager));
            raceObject.transform.SetParent(root.transform, false);
            RaceManager race = raceObject.GetComponent<RaceManager>();
            race.Configure(car, ghost, hud, cameraFollow);
            var checkpoints = new GameObject("Checkpoints");
            checkpoints.transform.SetParent(root.transform, false);
            CreateCheckpoint("Start Finish", checkpoints.transform, race, true, 0,
                new Vector2(0f, -6f), new Vector2(0.25f, 4f), Vector2.right);
            CreateCheckpoint("Checkpoint 1", checkpoints.transform, race, false, 0,
                new Vector2(10f, 0f), new Vector2(4f, 0.25f), Vector2.up);
            CreateCheckpoint("Checkpoint 2", checkpoints.transform, race, false, 1,
                new Vector2(0f, 6f), new Vector2(0.25f, 4f), Vector2.left);
            CreateCheckpoint("Checkpoint 3", checkpoints.transform, race, false, 2,
                new Vector2(-10f, 0f), new Vector2(4f, 0.25f), Vector2.down);
            CreateCheckpoint("Checkpoint 4", checkpoints.transform, race, false, 3,
                new Vector2(-5f, -6f), new Vector2(0.25f, 4f), Vector2.right);

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Ghostline could not save " + ScenePath);
            Selection.activeGameObject = raceObject;
            Debug.Log("Ghostline scene saved to " + ScenePath + ". See SCENE_SETUP.md for Play and material checks.");
        }

        private static GameObject CreateRectangle(string name, Transform parent, Vector2 position,
            Vector2 size, Color color, int sortingOrder)
        {
            var rectangle = new GameObject(name, typeof(SpriteRenderer), typeof(SolidSprite));
            rectangle.transform.SetParent(parent, false);
            rectangle.transform.localPosition = new Vector3(position.x, position.y, 0f);
            rectangle.GetComponent<SolidSprite>().Configure(color, size, sortingOrder);
            return rectangle;
        }

        private static void CreateWall(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject wall = CreateRectangle(name, parent, position, size,
                new Color(0.42f, 0.47f, 0.54f), 3);
            AddUnitCollider(wall, false);
        }

        private static void AddUnitCollider(GameObject gameObject, bool isTrigger)
        {
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.offset = Vector2.zero;
            collider.isTrigger = isTrigger;
        }

        private static void CreateCheckpoint(string name, Transform parent, RaceManager race,
            bool isStartFinish, int checkpointIndex, Vector2 position, Vector2 size, Vector2 forward)
        {
            GameObject checkpoint = CreateRectangle(name, parent, position, size,
                isStartFinish ? Color.white : new Color(1f, 0.65f, 0.15f, 0.7f), 2);
            AddUnitCollider(checkpoint, true);
            checkpoint.AddComponent<CheckpointTrigger>().Configure(race, isStartFinish, checkpointIndex, forward);
        }

        private static HudView CreateHud(Transform parent)
        {
            var canvasObject = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            TMP_Text current = CreateText("Current Lap", canvasObject.transform, new Vector2(24f, -24f),
                28f, "Lap  0.00 s");
            TMP_Text best = CreateText("Best Lap", canvasObject.transform, new Vector2(24f, -68f),
                28f, "Best  --");
            TMP_Text status = CreateText("Status", canvasObject.transform, new Vector2(24f, -112f),
                22f, "Cross the white line to start | WASD / arrows | R: restart");
            HudView hud = canvasObject.AddComponent<HudView>();
            hud.Configure(current, best, status);
            return hud;
        }

        private static TMP_Text CreateText(string name, Transform parent, Vector2 position,
            float fontSize, string initialText)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rectangle = textObject.GetComponent<RectTransform>();
            rectangle.anchorMin = new Vector2(0f, 1f);
            rectangle.anchorMax = new Vector2(0f, 1f);
            rectangle.pivot = new Vector2(0f, 1f);
            rectangle.anchoredPosition = position;
            rectangle.sizeDelta = new Vector2(1550f, 42f);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.text = initialText;
            return text;
        }
    }
}
