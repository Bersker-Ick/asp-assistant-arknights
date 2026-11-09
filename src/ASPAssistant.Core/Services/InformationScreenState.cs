namespace ASPAssistant.Core.Services;

/// <summary>One automatic attempt per opening screen. Missing OCR never rearms it.</summary>
public sealed class InformationScreenState
{
    private readonly object _sync = new();
    private int _openingFrames;
    private int _gameplayFrames;
    private bool _consumed;

    public bool IsConsumed { get { lock (_sync) return _consumed; } }

    public bool Observe(bool opening, bool gameplay)
    {
        lock (_sync)
        {
            if (gameplay)
            {
                _openingFrames = 0;
                if (++_gameplayFrames >= 2) _consumed = false;
                return false;
            }
            _gameplayFrames = 0;
            if (!opening)
            {
                _openingFrames = 0;
                return false;
            }
            if (_consumed || ++_openingFrames < 2) return false;
            _consumed = true;
            return true;
        }
    }

    public void MarkRecorded()
    {
        lock (_sync)
        {
            _consumed = true;
            _openingFrames = _gameplayFrames = 0;
        }
    }

    public void Suspend()
    {
        lock (_sync) _openingFrames = _gameplayFrames = 0;
    }
}
