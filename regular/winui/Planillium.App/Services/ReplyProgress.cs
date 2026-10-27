namespace Planillium.App.Services;

/// <summary>
/// Counts how far a streaming Claude reply has got (G4, 2026-10-27), so the Ask dialog can show
/// "N tasks so far" instead of a silent wait. Fed the reply's pieces as they arrive (from a background
/// thread); counts each <c>"task":</c> key, the one key every task in both reply shapes (Replace remaining,
/// Add plan) carries once. A key split across two pieces is still counted once. Plain C#, display only —
/// the reply itself is untouched and still goes through the dialog's normal validation.
/// </summary>
public sealed class ReplyProgress
{
    private const string Key = "\"task\"";
    private readonly object _gate = new();
    private string _pending = "";
    private int _chars, _tasks;

    public int Chars { get { lock (_gate) return _chars; } }
    public int Tasks { get { lock (_gate) return _tasks; } }

    public void Feed(string piece)
    {
        lock (_gate)
        {
            _chars += piece.Length;
            var s = _pending + piece;
            var i = 0;
            int keepFrom;
            while (true)
            {
                var q = s.IndexOf(Key, i, StringComparison.Ordinal);
                if (q < 0) { keepFrom = Math.Max(i, s.Length - (Key.Length - 1)); break; }
                var j = q + Key.Length;
                while (j < s.Length && char.IsWhiteSpace(s[j])) j++;
                if (j == s.Length) { keepFrom = q; break; }   // the ':' may still be on its way
                if (s[j] == ':') _tasks++;
                i = j;
            }
            _pending = s[keepFrom..];
        }
    }
}
