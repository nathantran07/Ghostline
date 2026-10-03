using System;
using Ghostline.Core;
using UnityEngine;

namespace Ghostline.Game
{
    public sealed class GhostCarView : MonoBehaviour
    {
        private GhostRecording _recording;
        private SpriteRenderer _renderer;

        private void Awake()
        {
            Hide();
        }

        public void SetLap(BestLapData data)
        {
            _recording = data == null ? null : new GhostRecording(data.Samples);
            Hide();
        }

        public void ShowAt(float lapTime)
        {
            if (_recording == null)
            {
                Hide();
                return;
            }
            GhostSample sample = _recording.Evaluate(lapTime);
            transform.SetPositionAndRotation(new Vector3(sample.X, sample.Y, 0f),
                Quaternion.Euler(0f, 0f, sample.Rotation));
            _renderer.enabled = true;
        }

        public void Hide()
        {
            if (_renderer == null)
                _renderer = GetComponentInChildren<SpriteRenderer>(true);
            if (_renderer == null)
                throw new InvalidOperationException("GhostCarView needs a child SpriteRenderer.");
            _renderer.enabled = false;
        }
    }
}
