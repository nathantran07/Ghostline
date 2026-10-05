using System;

namespace Ghostline.Core
{
    /// <summary>The phases of holding Delete and confirming a best-lap clear.</summary>
    public enum ClearBestLapState
    {
        /// <summary>Waits for a fresh Delete hold.</summary>
        Idle,
        /// <summary>Accumulates an uninterrupted Delete hold.</summary>
        Holding,
        /// <summary>Waits for explicit confirmation or cancellation until the deadline.</summary>
        AwaitingConfirm
    }

    /// <summary>Emits a clear request using supplied time and only the dedicated clear controls.</summary>
    public sealed class ClearBestLapFlow
    {
        private readonly double _holdDuration;
        private readonly double _confirmationTimeout;
        private double _elapsed;
        private bool _deleteArmed = true;

        /// <summary>Creates a flow with positive, finite hold and confirmation durations.</summary>
        public ClearBestLapFlow(float holdDuration = 1f, float confirmationTimeout = 5f)
        {
            NumericGuard.Nonnegative(holdDuration, nameof(holdDuration));
            NumericGuard.Nonnegative(confirmationTimeout, nameof(confirmationTimeout));
            if (holdDuration == 0f)
                throw new ArgumentOutOfRangeException(nameof(holdDuration));
            if (confirmationTimeout == 0f)
                throw new ArgumentOutOfRangeException(nameof(confirmationTimeout));
            _holdDuration = holdDuration;
            _confirmationTimeout = confirmationTimeout;
        }

        /// <summary>Gets the current confirmation phase.</summary>
        public ClearBestLapState State { get; private set; }

        /// <summary>Gets normalized hold progress; one while awaiting confirmation.</summary>
        public float HoldProgress => State == ClearBestLapState.Idle ? 0f
            : State == ClearBestLapState.AwaitingConfirm ? 1f : (float)(_elapsed / _holdDuration);

        /// <summary>Gets the remaining confirmation fraction; zero outside the prompt.</summary>
        public float TimeoutFraction => State == ClearBestLapState.AwaitingConfirm
            ? (float)Math.Max(0d, 1d - _elapsed / _confirmationTimeout) : 0f;

        /// <summary>Advances by nonnegative finite seconds; returns true once for a confirmed clear.</summary>
        public bool Tick(float deltaTime, bool deleteHeld, bool confirmPressed, bool cancelPressed)
        {
            NumericGuard.Nonnegative(deltaTime, nameof(deltaTime));
            if (State == ClearBestLapState.AwaitingConfirm)
            {
                _elapsed += deltaTime;
                // At the deadline or with conflicting keys, preserve the saved lap.
                bool confirmed = _elapsed < _confirmationTimeout && confirmPressed && !cancelPressed;
                if (_elapsed >= _confirmationTimeout || cancelPressed || confirmPressed)
                    Close(deleteHeld);
                return confirmed;
            }

            if (!deleteHeld)
            {
                Close(false);
                return false;
            }
            if (State == ClearBestLapState.Idle)
            {
                if (!_deleteArmed)
                    return false;
                _deleteArmed = false;
                State = ClearBestLapState.Holding;
            }
            _elapsed += deltaTime;
            if (_elapsed >= _holdDuration)
            {
                State = ClearBestLapState.AwaitingConfirm;
                _elapsed = 0d;
            }
            // Keys on the frame opening the prompt cannot confirm or cancel it.
            return false;
        }

        private void Close(bool deleteHeld)
        {
            State = ClearBestLapState.Idle;
            _elapsed = 0d;
            _deleteArmed = !deleteHeld;
        }
    }
}
