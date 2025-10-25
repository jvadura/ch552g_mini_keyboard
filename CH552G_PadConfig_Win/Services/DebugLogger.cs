using System.Windows.Controls;
using System.Windows.Threading;

namespace CH552G_PadConfig_Win.Services;

/// <summary>
/// Thread-safe logger for status updates
/// Appends messages to a TextBox with timestamps
/// </summary>
public class DebugLogger
{
    private readonly TextBox _textBox;
    private readonly Dispatcher _dispatcher;

    public DebugLogger(TextBox textBox)
    {
        _textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
        _dispatcher = _textBox.Dispatcher;
    }

    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var formatted = $"[{timestamp}] {message}";

        if (_dispatcher.CheckAccess())
        {
            AppendText(formatted);
        }
        else
        {
            _dispatcher.Invoke(() => AppendText(formatted));
        }
    }

    public void LogSuccess(string message)
    {
        Log($"✓ {message}");
    }

    public void LogError(string message)
    {
        Log($"✗ ERROR: {message}");
    }

    public void LogWarning(string message)
    {
        Log($"⚠ WARNING: {message}");
    }

    public void Clear()
    {
        if (_dispatcher.CheckAccess())
        {
            _textBox.Clear();
        }
        else
        {
            _dispatcher.Invoke(() => _textBox.Clear());
        }
    }

    private void AppendText(string text)
    {
        _textBox.AppendText(text + Environment.NewLine);
        _textBox.ScrollToEnd();
    }
}
