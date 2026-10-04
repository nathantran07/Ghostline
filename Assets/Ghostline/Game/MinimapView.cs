using Ghostline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ghostline.Game
{
    [DisallowMultipleComponent]
    public sealed class MinimapView : MonoBehaviour
    {
        [SerializeField] private TrackGenerator _track;
        [SerializeField] private Transform _player;
        [SerializeField] private GhostCarView _ghost;
        [SerializeField] private RaceManager _race;
        [SerializeField] private Image _background;
        [SerializeField] private RawImage _map;
        [SerializeField] private Image _playerDot;
        [SerializeField] private Image _ghostDot;
        [SerializeField, Range(0.05f, 0.4f)] private float _heightFraction = 0.3f;
        [SerializeField] private Vector2 _panelOffset = new Vector2(24f, 24f);
        [SerializeField, Range(64, 1024)] private int _textureResolution = 256;
        [SerializeField, Min(0f)] private float _padding = 16f;
        [SerializeField, Min(0.1f)] private float _lineWidth = 3f;
        [SerializeField, Min(0.1f)] private float _tickWidth = 3f;
        [SerializeField, Min(0.1f)] private float _tickLength = 12f;
        [SerializeField, Min(1f)] private float _playerDotSize = 8f;
        [SerializeField, Min(1f)] private float _ghostDotSize = 7f;
        [SerializeField, Min(0f)] private float _outlineWidth = 1.5f;
        [SerializeField] private Color _backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.65f);
        [SerializeField] private Color _trackColor = new Color(0.7f, 0.75f, 0.8f, 1f);
        [SerializeField] private Color _tickColor = Color.white;
        [SerializeField] private Color _playerColor = new Color(1f, 0.9f, 0.2f, 1f);
        [SerializeField] private Color _outlineColor = new Color(0.02f, 0.02f, 0.03f, 1f);
        [SerializeField] private Color _ghostColor = new Color(0.2f, 0.8f, 1f, 0.5f);
        private MinimapProjection _projection;
        private Texture2D _texture;
        private RectTransform _canvasRect;
        private RectTransform _panel;

        public void Configure(TrackGenerator track, Transform player, GhostCarView ghost, RaceManager race,
            Image background, RawImage map, Image playerDot, Image ghostDot)
        {
            _track = track;
            _player = player;
            _ghost = ghost;
            _race = race;
            _background = background;
            _map = map;
            _playerDot = playerDot;
            _ghostDot = ghostDot;
            Canvas.ForceUpdateCanvases();
            ApplyLayout();
            if (_ghostDot != null)
                _ghostDot.enabled = false;
        }

        private void Start()
        {
            if (_track == null || _player == null || _race == null || _background == null || _map == null
                || _playerDot == null || _ghostDot == null || _track.Samples.Count == 0)
            {
                Debug.LogError("Ghostline MinimapView needs a sampled track, player, race, and all UI references.", this);
                enabled = false;
                return;
            }
            ApplyLayout();
            Vector2 minimum = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 maximum = new Vector2(float.MinValue, float.MinValue);
            var points = new TrackPoint[_track.Samples.Count];
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 position = _track.transform.TransformPoint(_track.Samples[i].Position);
                minimum = Vector2.Min(minimum, position);
                maximum = Vector2.Max(maximum, position);
                points[i] = new TrackPoint(position.x, position.y);
            }
            _projection = new MinimapProjection(new TrackPoint(minimum.x, minimum.y),
                new TrackPoint(maximum.x, maximum.y), _textureResolution, _textureResolution, _padding);
            var pixels = new Color[_textureResolution * _textureResolution];
            for (int i = 0; i < points.Length; i++)
                DrawSegment(pixels, ToVector(_projection.Project(points[i])),
                    ToVector(_projection.Project(points[(i + 1) % points.Length])), _lineWidth, _trackColor);

            Vector2 start = ToVector(_projection.Project(points[0]));
            Vector2 next = ToVector(_projection.Project(points[1 % points.Length]));
            Vector2 direction = (next - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (_tickLength * 0.5f);
            DrawSegment(pixels, start - normal, start + normal, _tickWidth, _tickColor);
            _texture = new Texture2D(_textureResolution, _textureResolution, TextureFormat.RGBA32, false)
            {
                name = "Ghostline Minimap (runtime)",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            _texture.SetPixels(pixels);
            _texture.Apply(false, false);
            _map.texture = _texture;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (_projection == null)
                return;
            ApplyLayout();
            _playerDot.enabled = _player != null && _player.gameObject.activeInHierarchy;
            if (_playerDot.enabled)
                PlaceDot(_playerDot, _player.position);
            _ghostDot.enabled = _ghost != null && _race != null && _race.ShowGhostOnMinimap;
            if (_ghostDot.enabled)
                PlaceDot(_ghostDot, _race.GhostMinimapPosition);
        }

        private void ApplyLayout()
        {
            if (_background == null || _map == null || _playerDot == null || _ghostDot == null)
                return;
            if (_panel == null)
                _panel = (RectTransform)transform;
            if (_canvasRect == null && _map.canvas != null)
                _canvasRect = (RectTransform)_map.canvas.rootCanvas.transform;
            if (_canvasRect == null)
                return;
            _panel.anchorMin = Vector2.one;
            _panel.anchorMax = Vector2.one;
            _panel.pivot = Vector2.one;
            _panel.anchoredPosition = -_panelOffset;
            float size = _canvasRect.rect.height * _heightFraction;
            _panel.sizeDelta = new Vector2(size, size);
            RectTransform mapRect = _map.rectTransform;
            mapRect.anchorMin = Vector2.zero;
            mapRect.anchorMax = Vector2.one;
            mapRect.offsetMin = Vector2.zero;
            mapRect.offsetMax = Vector2.zero;
            _background.color = _backgroundColor;
            _background.raycastTarget = false;
            _map.raycastTarget = false;
            ConfigureDot(_playerDot, _playerDotSize, _playerColor);
            ConfigureDot(_ghostDot, _ghostDotSize, _ghostColor);
            Outline outline = _playerDot.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = _outlineColor;
                outline.effectDistance = new Vector2(_outlineWidth, -_outlineWidth);
            }
        }

        private static void ConfigureDot(Image dot, float size, Color color)
        {
            dot.rectTransform.anchorMin = new Vector2(0f, 1f);
            dot.rectTransform.anchorMax = new Vector2(0f, 1f);
            dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            dot.color = color;
            dot.raycastTarget = false;
        }

        private void PlaceDot(Image dot, Vector2 position)
        {
            TrackPoint point = _projection.ProjectClamped(new TrackPoint(position.x, position.y));
            Rect rect = _map.rectTransform.rect;
            dot.rectTransform.anchoredPosition = new Vector2(point.X / _textureResolution * rect.width,
                -point.Y / _textureResolution * rect.height);
        }

        private void DrawSegment(Color[] pixels, Vector2 start, Vector2 end, float width, Color color)
        {
            float radius = width * 0.5f;
            int minimumX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.x, end.x) - radius - 1f));
            int maximumX = Mathf.Min(_textureResolution - 1, Mathf.CeilToInt(Mathf.Max(start.x, end.x) + radius + 1f));
            int minimumY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.y, end.y) - radius - 1f));
            int maximumY = Mathf.Min(_textureResolution - 1, Mathf.CeilToInt(Mathf.Max(start.y, end.y) + radius + 1f));
            Vector2 segment = end - start;
            for (int y = minimumY; y <= maximumY; y++)
                for (int x = minimumX; x <= maximumX; x++)
                {
                    Vector2 position = new Vector2(x + 0.5f, y + 0.5f);
                    float t = segment.sqrMagnitude == 0f ? 0f : Mathf.Clamp01(Vector2.Dot(position - start, segment) / segment.sqrMagnitude);
                    float coverage = Mathf.Clamp01(radius + 0.5f - Vector2.Distance(position, start + segment * t));
                    int index = (_textureResolution - 1 - y) * _textureResolution + x;
                    float alpha = coverage * color.a;
                    // Maximum coverage avoids dark seams where sampled line segments overlap.
                    if (alpha > 0f && alpha >= pixels[index].a)
                        pixels[index] = new Color(color.r, color.g, color.b, alpha);
                }
        }

        private static Vector2 ToVector(TrackPoint point)
        {
            return new Vector2(point.X, point.Y);
        }

        private void OnDestroy()
        {
            if (_map != null && _map.texture == _texture)
                _map.texture = null;
            if (_texture != null)
            {
                if (Application.isPlaying)
                    Destroy(_texture);
                else
                    DestroyImmediate(_texture);
            }
        }
    }
}
