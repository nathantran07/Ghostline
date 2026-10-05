using System.Globalization;
using Ghostline.Core;
using TMPro;
using UnityEngine;

namespace Ghostline.Game
{
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _currentTimeText;
        [SerializeField] private TMP_Text _bestTimeText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _countdownText;
        [SerializeField] private TMP_Text _deltaText;
        private const float DeltaDisplaySeconds = 3f;
        private const float DeltaFadeSeconds = 1f;
        private float _deltaSecondsRemaining;
        private const float ClearBestMessageSeconds = 2f;
        private ClearBestLapFlow _clearBestFlow;
        private ClearBestLapView _clearBestView;
        private QuitFlow _quitFlow;
        private string _clearBestMessage;
        private float _clearBestMessageSecondsRemaining;
        private StartGantryView _startGantry;

        public void Configure(TMP_Text currentTimeText, TMP_Text bestTimeText, TMP_Text statusText,
            TMP_Text countdownText = null, TMP_Text deltaText = null)
        {
            _currentTimeText = currentTimeText;
            _bestTimeText = bestTimeText;
            _statusText = statusText;
            _countdownText = countdownText;
            _deltaText = deltaText;
        }

        public void RenderCountdown(StartSequence sequence)
        {
            if (_countdownText == null)
                return;
            if (_startGantry == null)
            {
                var gantryObject = new GameObject("Start Gantry", typeof(RectTransform), typeof(StartGantryView));
                gantryObject.hideFlags = HideFlags.DontSave;
                gantryObject.transform.SetParent(_countdownText.transform, false);
                var rectangle = gantryObject.GetComponent<RectTransform>();
                rectangle.anchorMin = rectangle.anchorMax = rectangle.pivot = new Vector2(0.5f, 0.5f);
                rectangle.sizeDelta = new Vector2(360f, 96f);
                _startGantry = gantryObject.GetComponent<StartGantryView>();
                _startGantry.raycastTarget = false;
            }
            _countdownText.text = string.Empty;
            _countdownText.enabled = false;
            _startGantry.Render(sequence.State, sequence.LitLampCount);
        }

        public void ShowDelta(float? deltaSeconds)
        {
            _deltaSecondsRemaining = deltaSeconds.HasValue ? DeltaDisplaySeconds : 0f;
            if (_deltaText == null)
                return;
            _deltaText.enabled = deltaSeconds.HasValue;
            _deltaText.text = deltaSeconds.HasValue
                ? (deltaSeconds.Value < 0f ? "-" : "+")
                    + Mathf.Abs(deltaSeconds.Value).ToString("0.000", CultureInfo.InvariantCulture) : string.Empty;
            if (deltaSeconds.HasValue)
                _deltaText.color = deltaSeconds.Value < 0f ? Color.green
                    : deltaSeconds.Value > 0f ? Color.red : Color.white;
        }

        public void Tick(float deltaTime)
        {
            if (_deltaText == null || _deltaSecondsRemaining <= 0f)
                return;
            _deltaSecondsRemaining = Mathf.Max(0f, _deltaSecondsRemaining - deltaTime);
            Color color = _deltaText.color;
            color.a = Mathf.Clamp01(_deltaSecondsRemaining / DeltaFadeSeconds);
            _deltaText.color = color;
            if (_deltaSecondsRemaining == 0f)
                ShowDelta(null);
        }

        public void RenderClearBest(ClearBestLapFlow flow)
        {
            _clearBestFlow = flow;
            RefreshClearBestView();
        }

        public void RenderQuit(QuitFlow flow)
        {
            _quitFlow = flow;
            RefreshClearBestView();
        }

        public void ShowClearBestResult(bool cleared)
        {
            _clearBestMessage = cleared ? "Best lap cleared" : "Could not clear best lap (see Console)";
            _clearBestMessageSecondsRemaining = ClearBestMessageSeconds;
            RefreshClearBestView();
        }

        public void TickClearBestMessage(float deltaTime)
        {
            _clearBestMessageSecondsRemaining = Mathf.Max(0f, _clearBestMessageSecondsRemaining - deltaTime);
            RefreshClearBestView();
        }

        private void RefreshClearBestView()
        {
            bool quitOpen = _quitFlow != null && _quitFlow.State == QuitState.AwaitingConfirm;
            string message = _clearBestMessageSecondsRemaining > 0f ? _clearBestMessage : null;
            if (_clearBestView == null)
            {
                if ((_clearBestFlow == null || _clearBestFlow.State == ClearBestLapState.Idle) && message == null && !quitOpen)
                    return;
                Canvas canvas = _statusText != null ? _statusText.canvas : GetComponentInParent<Canvas>();
                if (canvas == null)
                    return;
                var overlay = new GameObject("Clear Best Lap Overlay", typeof(RectTransform), typeof(ClearBestLapView));
                overlay.hideFlags = HideFlags.DontSave;
                overlay.transform.SetParent(canvas.transform, false);
                _clearBestView = overlay.GetComponent<ClearBestLapView>();
                _clearBestView.Initialize(canvas, _statusText != null ? _statusText.font : TMP_Settings.defaultFontAsset);
            }
            _clearBestView.Render(_clearBestFlow, message, quitOpen);
        }

        private void OnDestroy()
        {
            if (_clearBestView == null)
                return;
            if (Application.isPlaying)
                Destroy(_clearBestView.gameObject);
            else
                DestroyImmediate(_clearBestView.gameObject);
        }

        public void Render(RaceSession session, BestLapData bestLap, bool saveFailed)
        {
            if (_currentTimeText != null)
                _currentTimeText.text = $"Lap  {session.Timer.ElapsedTime:0.00} s";
            if (_bestTimeText != null)
                _bestTimeText.text = bestLap == null ? "Best  --" : $"Best  {bestLap.LapTime:0.00} s";
            if (_statusText == null)
                return;
            if (session.Timer.State == LapTimerState.NotStarted)
                _statusText.text = "Cross the white line to start | WASD / arrows | R: restart | Esc: quit";
            else if (session.Timer.State == LapTimerState.Finished)
                _statusText.text = saveFailed ? "Lap complete; save failed (see Console) | R: restart"
                    : "Lap complete | R: restart";
            else
                _statusText.text = session.Checkpoints.NextCheckpointIndex == session.Checkpoints.CheckpointCount
                    ? "All checkpoints passed. Cross the white finish line."
                    : $"Next checkpoint: {session.Checkpoints.NextCheckpointIndex + 1} / {session.Checkpoints.CheckpointCount}";
        }
    }
}
