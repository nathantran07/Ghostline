using Ghostline.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ghostline.Game
{
    /// <summary>Noninteractive confirmation prompts and clear-best feedback on the existing HUD canvas.</summary>
    public sealed class ClearBestLapView : MonoBehaviour
    {
        private static readonly Color Amber = new Color(1f, 0.67f, 0.2f, 1f);
        private RectTransform _hold;
        private RectTransform _confirmation;
        private RectTransform _result;
        private RectTransform _quit;
        private Image _holdFill;
        private Image _timeoutFill;
        private TMP_Text _resultText;
        private Texture2D _whiteTexture;
        private Sprite _whiteSprite;

        public void Initialize(Canvas parentCanvas, TMP_FontAsset font)
        {
            RectTransform rectangle = GetComponent<RectTransform>();
            rectangle.anchorMin = Vector2.zero;
            rectangle.anchorMax = Vector2.one;
            rectangle.offsetMin = rectangle.offsetMax = Vector2.zero;
            Canvas overlay = gameObject.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingLayerID = parentCanvas.sortingLayerID;
            overlay.sortingOrder = Mathf.Min(short.MaxValue, parentCanvas.sortingOrder + 10);
            CanvasGroup group = gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // Filled Images need a sprite; share one runtime pixel using the default UI material.
            _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Clear best UI pixel", hideFlags = HideFlags.DontSave
            };
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
            _whiteSprite = Sprite.Create(_whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
            _whiteSprite.hideFlags = HideFlags.DontSave;

            _hold = CreatePanel("Hold", new Vector2(480f, 92f));
            CreateText(_hold, "Title", "Hold Delete to clear best lap", font, 24f,
                new Vector2(0f, 15f), new Vector2(440f, 40f));
            _holdFill = CreateBar(_hold, "Progress", new Vector2(432f, 12f), -23f);

            _confirmation = CreatePanel("Confirmation", new Vector2(640f, 180f));
            CreateText(_confirmation, "Title", "Clear best lap and ghost?", font, 34f,
                new Vector2(0f, 40f), new Vector2(600f, 50f));
            CreateText(_confirmation, "Keys", "[Y] Clear    [N / Esc] Cancel", font, 26f,
                new Vector2(0f, -17f), new Vector2(600f, 40f));
            _timeoutFill = CreateBar(_confirmation, "Timeout", new Vector2(592f, 6f), -65f);

            _quit = CreatePanel("Quit", new Vector2(640f, 180f));
            _quit.anchorMin = _quit.anchorMax = Vector2.one * 0.5f;
            CreateText(_quit, "Title", "Quit Ghostline?", font, 34f,
                new Vector2(0f, 40f), new Vector2(600f, 50f));
            CreateText(_quit, "Keys", "[Y] Quit    [N / Esc] Resume", font, 26f,
                new Vector2(0f, -17f), new Vector2(600f, 40f));

            _result = CreatePanel("Result", new Vector2(640f, 100f));
            _resultText = CreateText(_result, "Message", string.Empty, font, 28f,
                Vector2.zero, new Vector2(600f, 70f));
            Render(null, null);
        }

        public void Render(ClearBestLapFlow flow, string resultMessage, bool quitOpen = false)
        {
            ClearBestLapState state = flow == null ? ClearBestLapState.Idle : flow.State;
            _quit.gameObject.SetActive(quitOpen);
            _hold.gameObject.SetActive(!quitOpen && state == ClearBestLapState.Holding);
            _confirmation.gameObject.SetActive(!quitOpen && state == ClearBestLapState.AwaitingConfirm);
            _result.gameObject.SetActive(!quitOpen && state == ClearBestLapState.Idle && resultMessage != null);
            _holdFill.fillAmount = flow == null ? 0f : flow.HoldProgress;
            _timeoutFill.fillAmount = flow == null ? 0f : flow.TimeoutFraction;
            _resultText.text = resultMessage ?? string.Empty;
            RefreshLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_result != null)
                RefreshLayout();
        }

        private void RefreshLayout()
        {
            Rect viewport = ((RectTransform)transform).rect;
            // Keep the entire panel below the top-right minimap, including on short canvases.
            float scale = Mathf.Min(1f, viewport.width * 0.9f / 640f, viewport.height * 0.2f / 180f);
            _hold.localScale = _confirmation.localScale = _result.localScale = _quit.localScale = Vector3.one * scale;
        }

        private RectTransform CreatePanel(string name, Vector2 size)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.hideFlags = HideFlags.DontSave;
            child.transform.SetParent(transform, false);
            RectTransform panel = child.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.4f);
            panel.sizeDelta = size;
            Image backing = CreateImage(panel, "Backing", new Color(0.035f, 0.045f, 0.065f, 0.93f));
            backing.rectTransform.anchorMin = Vector2.zero;
            backing.rectTransform.anchorMax = Vector2.one;
            backing.rectTransform.offsetMin = Vector2.one * 2f;
            backing.rectTransform.offsetMax = Vector2.one * -2f;
            CreateBorder(panel, "Top", new Vector2(size.x, 2f), new Vector2(0f, (size.y - 2f) * 0.5f));
            CreateBorder(panel, "Bottom", new Vector2(size.x, 2f), new Vector2(0f, (2f - size.y) * 0.5f));
            CreateBorder(panel, "Left", new Vector2(2f, size.y), new Vector2((2f - size.x) * 0.5f, 0f));
            CreateBorder(panel, "Right", new Vector2(2f, size.y), new Vector2((size.x - 2f) * 0.5f, 0f));
            return panel;
        }

        private void CreateBorder(RectTransform parent, string name, Vector2 size, Vector2 position)
        {
            Image border = CreateImage(parent, "Border " + name, Amber);
            border.rectTransform.sizeDelta = size;
            border.rectTransform.anchoredPosition = position;
        }

        private Image CreateBar(RectTransform parent, string name, Vector2 size, float y)
        {
            Image track = CreateImage(parent, name, new Color(0.22f, 0.24f, 0.28f, 1f));
            track.rectTransform.sizeDelta = size;
            track.rectTransform.anchoredPosition = new Vector2(0f, y);
            Image fill = CreateImage(track.transform, "Fill", Amber);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            return fill;
        }

        private Image CreateImage(Transform parent, string name, Color color)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Image));
            child.hideFlags = HideFlags.DontSave;
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.sprite = _whiteSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(RectTransform parent, string name, string content,
            TMP_FontAsset font, float size, Vector2 position, Vector2 dimensions)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            child.hideFlags = HideFlags.DontSave;
            child.transform.SetParent(parent, false);
            TMP_Text text = child.GetComponent<TMP_Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = content;
            text.rectTransform.sizeDelta = dimensions;
            text.rectTransform.anchoredPosition = position;
            return text;
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                Destroy(_whiteSprite);
                Destroy(_whiteTexture);
            }
            else
            {
                DestroyImmediate(_whiteSprite);
                DestroyImmediate(_whiteTexture);
            }
        }
    }
}
