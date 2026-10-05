using System;
using Ghostline.Core;
using UnityEngine;

namespace Ghostline.Game
{
    /// <summary>Routes dedicated prompt controls with clear-best priority and an injectable quit boundary.</summary>
    public sealed class ConfirmationPrompts
    {
        private readonly Action _requestQuit;
        private readonly Func<bool> _isEditor;

        public ClearBestLapFlow ClearBest { get; }
        public QuitFlow Quit { get; } = new QuitFlow();

        public ConfirmationPrompts(ClearBestLapFlow clearBest, Action requestQuit = null, Func<bool> isEditor = null)
        {
            ClearBest = clearBest ?? throw new ArgumentNullException(nameof(clearBest));
            _requestQuit = requestQuit ?? QuitApplication;
            _isEditor = isEditor ?? (() => Application.isEditor);
        }

        /// <summary>Returns a clear request; quit requests go only through the injected callback.</summary>
        public bool Tick(float deltaTime, bool deleteHeld, bool escapePressed, bool confirmPressed, bool cancelPressed)
        {
            bool quitEnabled = !_isEditor();
            if (Quit.State == QuitState.AwaitingConfirm)
            {
                if (Quit.Tick(escapePressed, confirmPressed, cancelPressed, quitEnabled))
                    _requestQuit();
                // Consume the closing frame too; Delete cannot open another prompt on that frame.
                return false;
            }

            bool clearWasActive = ClearBest.State != ClearBestLapState.Idle;
            bool clear = ClearBest.Tick(deltaTime, deleteHeld, confirmPressed, cancelPressed || escapePressed);
            // Check both sides of the tick so cancellation, timeout, and a new hold all consume Escape.
            Quit.Tick(escapePressed && !clearWasActive && ClearBest.State == ClearBestLapState.Idle,
                false, false, quitEnabled);
            return clear;
        }

        private static void QuitApplication()
        {
#if !UNITY_EDITOR
            Application.Quit();
#endif
        }
    }
}
