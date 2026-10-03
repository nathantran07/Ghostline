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

        public void Configure(TMP_Text currentTimeText, TMP_Text bestTimeText, TMP_Text statusText)
        {
            _currentTimeText = currentTimeText;
            _bestTimeText = bestTimeText;
            _statusText = statusText;
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
                _statusText.text = "Cross the white line to start | WASD / arrows | R: restart";
            else if (session.Timer.State == LapTimerState.Finished)
                _statusText.text = saveFailed ? "Lap complete; save failed (see Console) | R: restart"
                    : "Lap complete | R: restart";
            else
                _statusText.text = session.Checkpoints.NextCheckpointIndex == 4
                    ? "All checkpoints passed. Cross the white finish line."
                    : $"Next checkpoint: {session.Checkpoints.NextCheckpointIndex + 1} / 4";
        }
    }
}
