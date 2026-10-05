namespace Ghostline.Core
{
    /// <summary>The phases of explicitly confirming an application quit.</summary>
    public enum QuitState
    {
        Idle,
        AwaitingConfirm
    }

    /// <summary>Uses supplied key presses and availability; emits one quit request without a timeout.</summary>
    public sealed class QuitFlow
    {
        public QuitState State { get; private set; }

        public bool Tick(bool escapePressed, bool confirmPressed, bool cancelPressed, bool enabled)
        {
            if (!enabled)
            {
                State = QuitState.Idle;
                return false;
            }
            if (State == QuitState.Idle)
            {
                if (escapePressed)
                    State = QuitState.AwaitingConfirm;
                // Opening-frame presses cannot also confirm or dismiss the prompt.
                return false;
            }
            if (escapePressed || cancelPressed)
            {
                State = QuitState.Idle;
                return false;
            }
            if (!confirmPressed)
                return false;
            State = QuitState.Idle;
            return true;
        }
    }
}
