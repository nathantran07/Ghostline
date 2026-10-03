using UnityEngine;

namespace Ghostline.Game
{
    /// <summary>Displays a tinted rectangle without requiring a sprite asset or custom material.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SolidSprite : MonoBehaviour
    {
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private Vector2 _size = Vector2.one;
        [SerializeField] private int _sortingOrder;
        private Sprite _sprite;
        private SpriteRenderer _renderer;

        public void Configure(Color color, Vector2 size, int sortingOrder)
        {
            _color = color;
            _size = size;
            _sortingOrder = sortingOrder;
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            _size.x = Mathf.Max(0.01f, _size.x);
            _size.y = Mathf.Max(0.01f, _size.y);
            // Creating sprites during OnValidate can run on Unity's loading thread.
            // ExecuteAlways.Update applies the edited values on the main thread.
        }

        private void Update()
        {
            if (!Application.isPlaying || _sprite == null)
                Apply();
        }

        private void Apply()
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();
            if (_sprite == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                _sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), texture.width, 0, SpriteMeshType.FullRect);
                _sprite.name = "Ghostline Solid Rectangle";
                _sprite.hideFlags = HideFlags.HideAndDontSave;
            }
            _renderer.sprite = _sprite;
            _renderer.drawMode = SpriteDrawMode.Simple;
            _renderer.color = _color;
            _renderer.sortingOrder = _sortingOrder;
            transform.localScale = new Vector3(_size.x, _size.y, 1f);
        }

        private void OnDisable()
        {
            if (_renderer != null && _renderer.sprite == _sprite)
                _renderer.sprite = null;
            if (_sprite == null)
                return;
            if (Application.isPlaying)
                Destroy(_sprite);
            else
                DestroyImmediate(_sprite);
            _sprite = null;
        }
    }
}
