namespace GodotGenerator.Blazor.Client.Services;

/// <summary>
/// In-memory log lines for the console / log panel.
/// </summary>
public sealed class LogBufferService
{
    private readonly List<string> _lines = [];
    private readonly object _gate = new();

    public event Action? Changed;

    public IReadOnlyList<string> GetSnapshot()
    {
        lock (_gate)
        {
            return _lines.ToArray();
        }
    }

    public void Append(string line)
    {
        var text = $"{DateTimeOffset.Now:O} {line}";
        lock (_gate)
        {
            _lines.Add(text);
            if (_lines.Count > 500)
            {
                _lines.RemoveRange(0, _lines.Count - 500);
            }
        }

        Changed?.Invoke();
    }
}
