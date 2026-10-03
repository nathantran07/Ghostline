using System;
using System.IO;
using Ghostline.Game;
using TMPro;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.UI;

namespace Ghostline.Editor
{
    /// <summary>Builds the game scene using Lambo car art and the project's existing URP configuration.</summary>
    public static class GhostlineSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        private const string CarSpritePath = "Assets/Ghostline/Game/Art/LAMBO.png";
        private const float CarLength = 1.4f;

        // Trace of the colored centerline in docs/reference/suzuka-layout.png (1280 x 720).
        // x = pixelX / 1280; y = 1 - pixelY / 720, so Unity Y points up.
        // Driving order starts at the checkered flag; both crossover passes stay on their road.
        // Hand-tune only this normalized array; corner groups follow the reference labels.
        private static readonly Vector2[] SuzukaNormalizedKnots =
        {
            // Start/finish and straight into corner 1.
            new Vector2(0.71406f, 0.61667f),
            new Vector2(0.87969f, 0.25417f),
            // Corners 1-2.
            new Vector2(0.89375f, 0.21528f),
            new Vector2(0.89609f, 0.18333f),
            new Vector2(0.88516f, 0.13611f),
            new Vector2(0.87109f, 0.12083f),
            new Vector2(0.85469f, 0.12500f),
            new Vector2(0.84297f, 0.14722f),
            new Vector2(0.82812f, 0.18889f),
            // Corners 3-4.
            new Vector2(0.81172f, 0.23611f),
            new Vector2(0.79922f, 0.25556f),
            new Vector2(0.76406f, 0.26389f),
            new Vector2(0.75078f, 0.27639f),
            new Vector2(0.74375f, 0.29861f),
            // Corners 5-7: S curves.
            new Vector2(0.73828f, 0.33472f),
            new Vector2(0.73125f, 0.36806f),
            new Vector2(0.71641f, 0.39028f),
            new Vector2(0.66563f, 0.40833f),
            new Vector2(0.65469f, 0.43194f),
            new Vector2(0.65391f, 0.46111f),
            new Vector2(0.66094f, 0.50000f),
            new Vector2(0.66641f, 0.53611f),
            new Vector2(0.66250f, 0.56806f),
            new Vector2(0.64844f, 0.59028f),
            new Vector2(0.62578f, 0.61111f),
            new Vector2(0.60234f, 0.62222f),
            new Vector2(0.57969f, 0.62083f),
            new Vector2(0.55937f, 0.60694f),
            new Vector2(0.53906f, 0.58056f),
            new Vector2(0.51562f, 0.54028f),
            // Corners 8-9.
            new Vector2(0.49844f, 0.50139f),
            new Vector2(0.48672f, 0.47917f),
            new Vector2(0.46094f, 0.47361f),
            new Vector2(0.43281f, 0.46944f),
            new Vector2(0.41875f, 0.47778f),
            new Vector2(0.41563f, 0.50278f),
            // Corners 10-11: widened hairpin.
            new Vector2(0.39844f, 0.66806f),
            new Vector2(0.39609f, 0.70000f),
            new Vector2(0.40156f, 0.73472f),
            new Vector2(0.41172f, 0.77500f),
            new Vector2(0.41328f, 0.79167f),
            new Vector2(0.40781f, 0.80556f),
            new Vector2(0.39766f, 0.80278f),
            new Vector2(0.38984f, 0.78472f),
            new Vector2(0.37344f, 0.74028f),
            // Corner 12.
            new Vector2(0.35625f, 0.69444f),
            new Vector2(0.34219f, 0.67361f),
            new Vector2(0.32188f, 0.65972f),
            new Vector2(0.29844f, 0.65278f),
            new Vector2(0.27344f, 0.65833f),
            new Vector2(0.20547f, 0.70694f),
            new Vector2(0.18906f, 0.73611f),
            // Corners 13-14: upper-left loop.
            new Vector2(0.16172f, 0.84167f),
            new Vector2(0.15234f, 0.87778f),
            new Vector2(0.13984f, 0.90000f),
            new Vector2(0.11953f, 0.90417f),
            new Vector2(0.10000f, 0.89444f),
            new Vector2(0.08672f, 0.87361f),
            new Vector2(0.08125f, 0.84722f),
            new Vector2(0.08594f, 0.82222f),
            new Vector2(0.09922f, 0.79722f),
            // Back straight through crossover to corner 15.
            new Vector2(0.13750f, 0.74444f),
            new Vector2(0.25156f, 0.63194f),
            new Vector2(0.32812f, 0.58194f),
            new Vector2(0.40859f, 0.53750f),
            new Vector2(0.43047f, 0.52778f),
            new Vector2(0.45391f, 0.53889f),
            new Vector2(0.47578f, 0.55694f),
            // Corners 16-18: chicane and return to start.
            new Vector2(0.51953f, 0.62500f),
            new Vector2(0.55781f, 0.68056f),
            new Vector2(0.57266f, 0.69861f),
            new Vector2(0.58203f, 0.69167f),
            new Vector2(0.59297f, 0.67639f),
            new Vector2(0.60469f, 0.68056f),
            new Vector2(0.61953f, 0.70139f),
            new Vector2(0.63516f, 0.70833f),
            new Vector2(0.65391f, 0.70278f),
            new Vector2(0.67734f, 0.68194f),
            new Vector2(0.69844f, 0.65278f),
        };

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

            Sprite carSprite = LoadCarSprite();
            Material playerMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            Material ghostMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Ghostline/Game/Art/GhostSprite.mat");
            if (playerMaterial == null || ghostMaterial == null)
                throw new InvalidOperationException("Ghostline needs the URP unlit and GhostSprite materials.");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Ghostline");
            var track = new GameObject("Track", typeof(SplineContainer), typeof(TrackGenerator), typeof(TrackVisuals));
            track.transform.SetParent(root.transform, false);
            var positions = new float3[SuzukaNormalizedKnots.Length];
            // CarController caps speed at 12 units/s; assume 60-75% average (7.2-9).
            // A ~730-unit centerline gives ~81-101 s, or ~87 s at 70% of top speed.
            // Keep image aspect ratio (1280:720). Final width/radius validation sets scale,
            // rather than Suzuka's real dimensions; widen hairpin knots before narrowing road.
            const float layoutWidth = 300f;
            for (int i = 0; i < positions.Length; i++)
            {
                Vector2 knot = SuzukaNormalizedKnots[i] - Vector2.one * 0.5f;
                positions[i] = new float3(knot.x * layoutWidth, knot.y * layoutWidth * 720f / 1280f, 0f);
            }
            track.GetComponent<SplineContainer>().Spline = new Spline(positions, TangentMode.AutoSmooth, true);
            TrackGenerator generator = track.GetComponent<TrackGenerator>();
            generator.Configure(playerMaterial);
            TrackVisuals visuals = track.GetComponent<TrackVisuals>();
            // Corner 8 begins at knot 30; knot 65 is the corner 15 apex after the crossover.
            visuals.Configure(18f, CarLength, 0.55f, 30, 65);
            TrackSample spawn = visuals.GetSpawnSample(generator);
            float spawnRotation = Mathf.Atan2(spawn.Tangent.y, spawn.Tangent.x) * Mathf.Rad2Deg - 90f;

            GameObject carObject = CreateCar("Car", root.transform, carSprite, Color.white, 5, playerMaterial,
                spawn.Position, spawnRotation);
            BoxCollider2D carCollider = carObject.AddComponent<BoxCollider2D>();
            carCollider.size = new Vector2(0.55f, 1.18f);
            carCollider.offset = new Vector2(0f, 0.08f);
            Rigidbody2D body = carObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearDamping = 1.2f;
            body.angularDamping = 8f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CarController car = carObject.AddComponent<CarController>();

            GameObject ghostObject = CreateCar("Ghost", root.transform, carSprite,
                new Color(0.8f, 0.95f, 1f, 0.4f), 4, ghostMaterial, spawn.Position, spawnRotation);
            GhostCarView ghost = ghostObject.AddComponent<GhostCarView>();

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(spawn.Position.x, spawn.Position.y, -10f);
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
            race.Configure(car, ghost, hud, cameraFollow, generator.CheckpointCount, spawn.Position, spawnRotation);
            generator.Generate(race);
            cameraFollow.SnapToTarget();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Ghostline could not save " + ScenePath);
            Selection.activeGameObject = raceObject;
            Debug.Log("Ghostline scene saved to " + ScenePath + ". See SCENE_SETUP.md for Play and material checks.");
        }

        private static Sprite LoadCarSprite()
        {
            var importer = AssetImporter.GetAtPath(CarSpritePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Ghostline needs the car sprite at " + CarSpritePath);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("Ghostline could not read the car sprite dimensions.");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spritePixelsPerUnit = height / CarLength;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.filterMode = FilterMode.Bilinear;
            settings.wrapMode = TextureWrapMode.Clamp;
            settings.mipmapEnabled = false;
            settings.alphaIsTransparency = true;
            importer.SetTextureSettings(settings);
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CarSpritePath);
            if (sprite == null)
                throw new InvalidOperationException("Ghostline could not import " + CarSpritePath + " as a Sprite.");
            return sprite;
        }

        private static GameObject CreateCar(string name, Transform parent, Sprite sprite, Color color,
            int sortingOrder, Material material, Vector2 position, float rotation)
        {
            var car = new GameObject(name);
            car.transform.SetParent(parent, false);
            car.transform.localPosition = position;
            car.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var visual = new GameObject("Visual", typeof(SpriteRenderer));
            visual.transform.SetParent(car.transform, false);
            // FixedUpdate drives along local up; the supplied sprite's nose points down.
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = material;
            return car;
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
            TMP_Text countdown = CreateText("Countdown", canvasObject.transform, Vector2.zero, 96f, "3");
            RectTransform countdownRectangle = countdown.rectTransform;
            countdownRectangle.anchorMin = new Vector2(0.5f, 0.5f);
            countdownRectangle.anchorMax = new Vector2(0.5f, 0.5f);
            countdownRectangle.pivot = new Vector2(0.5f, 0.5f);
            countdownRectangle.sizeDelta = new Vector2(400f, 160f);
            countdown.alignment = TextAlignmentOptions.Center;
            TMP_Text delta = CreateText("Ghost Delta", canvasObject.transform, new Vector2(280f, -24f),
                28f, string.Empty);
            delta.rectTransform.sizeDelta = new Vector2(200f, 42f);
            delta.enabled = false;
            HudView hud = canvasObject.AddComponent<HudView>();
            hud.Configure(current, best, status, countdown, delta);
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
