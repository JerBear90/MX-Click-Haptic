using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace MxClickHaptics.Core;

public enum MouseButton
{
    Left,
    Right
}

public enum OutputMode
{
    Simulated,
    Real
}

public readonly record struct MouseButtonEvent(MouseButton Button, bool IsDown);

public interface IInputSource
{
    bool TryReadNextEvent(out MouseButton button, out bool isDown);
}

public interface IHapticOutput : IDisposable
{
    bool IsAvailable { get; }
    string Status { get; }
    void EnqueuePulse(HapticPulse pulse);
}

public sealed record HapticPulse(MouseButton Button, DateTimeOffset Timestamp, string Waveform);

public sealed class SyntheticInputSource : IInputSource
{
    private readonly Queue<MouseButtonEvent> _events = new();

    public void PushButtonDown(MouseButton button) => _events.Enqueue(new MouseButtonEvent(button, true));

    public void PushButtonUp(MouseButton button) => _events.Enqueue(new MouseButtonEvent(button, false));

    public bool TryReadNextEvent(out MouseButton button, out bool isDown)
    {
        if (_events.Count == 0)
        {
            button = default;
            isDown = false;
            return false;
        }

        var current = _events.Dequeue();
        button = current.Button;
        isDown = current.IsDown;
        return true;
    }
}

public sealed class HapticSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MX-Click-Haptics",
        "settings.json");

    public bool Enabled { get; set; } = true;
    public bool Paused { get; set; }
    public bool LeftFeedbackEnabled { get; set; } = true;
    public bool RightFeedbackEnabled { get; set; } = true;
    public OutputMode OutputMode { get; set; } = OutputMode.Simulated;
    public int QueueLimit { get; set; } = 8;
    public string LeftWaveform { get; set; } = "sharp_state_change";
    public string RightWaveform { get; set; } = "sharp_state_change";

    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static HapticSettings Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new HapticSettings();
        }

        var content = File.ReadAllText(path);
        return JsonSerializer.Deserialize<HapticSettings>(content) ?? new HapticSettings();
    }
}

public sealed class HapticController
{
    private readonly HapticSettings _settings;
    private readonly IHapticOutput _output;
    private readonly HashSet<MouseButton> _pressedButtons = new();
    private readonly Queue<HapticPulse> _pendingPulses = new();

    public HapticController(HapticSettings settings, IHapticOutput output)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    public List<HapticPulse> ProcessTick(IInputSource inputSource, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(inputSource);

        while (inputSource.TryReadNextEvent(out var button, out var isDown))
        {
            if (!isDown)
            {
                _pressedButtons.Remove(button);
                continue;
            }

            if (!_settings.Enabled || _settings.Paused)
            {
                continue;
            }

            if (button == MouseButton.Left && !_settings.LeftFeedbackEnabled)
            {
                continue;
            }

            if (button == MouseButton.Right && !_settings.RightFeedbackEnabled)
            {
                continue;
            }

            if (!_pressedButtons.Add(button))
            {
                continue;
            }

            var waveform = button switch
            {
                MouseButton.Left => _settings.LeftWaveform,
                MouseButton.Right => _settings.RightWaveform,
                _ => "sharp_state_change"
            };

            var pulse = new HapticPulse(button, timestamp, waveform);
            _pendingPulses.Enqueue(pulse);
            while (_pendingPulses.Count > Math.Max(1, _settings.QueueLimit))
            {
                _pendingPulses.Dequeue();
            }

            _output.EnqueuePulse(pulse);
        }

        var snapshot = _pendingPulses.ToList();
        return snapshot;
    }

    public void ClearQueue()
    {
        _pendingPulses.Clear();
        _pressedButtons.Clear();
    }
}

public sealed class SimulatedOutputAdapter : IHapticOutput
{
    private readonly Queue<HapticPulse> _pending = new();
    private readonly List<HapticPulse> _diagnostics = new();
    private const int DefaultMaxDiagnostics = 64;

    public bool IsAvailable => true;
    public string Status => IsDisposed ? "Simulated output disposed." : "Simulated output active.";
    public bool IsDisposed { get; private set; }
    public IReadOnlyList<HapticPulse> Diagnostics => _diagnostics.AsReadOnly();

    public void EnqueuePulse(HapticPulse pulse)
    {
        if (IsDisposed)
        {
            return;
        }

        _pending.Enqueue(pulse);
        while (_pending.Count > 32)
        {
            _pending.Dequeue();
        }

        _diagnostics.Add(pulse);
        while (_diagnostics.Count > DefaultMaxDiagnostics)
        {
            _diagnostics.RemoveAt(0);
        }
    }

    public void Dispose()
    {
        IsDisposed = true;
        _pending.Clear();
        _diagnostics.Clear();
    }
}

public sealed class RealLogitechOutputAdapter : IHapticOutput
{
    public bool IsAvailable => TryPing(out _);

    public string Status => TryPing(out var error)
        ? "Logitech haptics plugin connected. Device delivery is not confirmed by the SDK."
        : $"Logitech haptics plugin is not connected: {error}";

    public void EnqueuePulse(HapticPulse pulse)
    {
        ArgumentNullException.ThrowIfNull(pulse);

        if (!LogitechHapticsBridgeProtocol.TryCreateRequest(pulse, out var request))
        {
            throw new InvalidOperationException(
                $"Unsupported Logitech haptic event: {pulse.Button} / {pulse.Waveform}.");
        }

        using var pipe = Connect();
        var response = Exchange(pipe, request);
        if (response != "DISPATCHED")
        {
            throw new InvalidOperationException(
                $"The Logitech haptics plugin rejected the event: {response ?? "no response"}.");
        }
    }

    public void Dispose()
    {
    }

    private static bool TryPing(out string error)
    {
        try
        {
            using var pipe = Connect();
            if (Exchange(pipe, "PING") == "READY")
            {
                error = string.Empty;
                return true;
            }

            error = "the plugin did not return a ready response";
            return false;
        }
        catch (IOException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (TimeoutException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static NamedPipeClientStream Connect()
    {
        var pipe = new NamedPipeClientStream(
            ".",
            LogitechHapticsBridgeProtocol.PipeName,
            PipeDirection.InOut,
            PipeOptions.None,
            TokenImpersonationLevel.Impersonation);

        try
        {
            pipe.Connect(300);
            return pipe;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            pipe.Dispose();
            throw;
        }
    }

    private static string? Exchange(NamedPipeClientStream pipe, string message)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        try
        {
            return ExchangeAsync(pipe, message, timeout.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException ex)
        {
            throw new TimeoutException("Timed out waiting for the Logitech haptics plugin.", ex);
        }
    }

    private static async Task<string?> ExchangeAsync(
        NamedPipeClientStream pipe,
        string message,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true)
        {
            AutoFlush = true
        };
        await writer.WriteLineAsync(message.AsMemory(), cancellationToken).ConfigureAwait(false);
        return await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
    }
}

public static class LogitechHapticsBridgeProtocol
{
    public const string PipeName = "MxClickHaptics.Logitech";

    public static IReadOnlyList<string> SupportedWaveforms { get; } =
    [
        "sharp_state_change",
        "subtle_collision",
        "damp_collision",
        "completed",
        "happy_alert"
    ];

    public static bool TryCreateRequest(HapticPulse pulse, out string request)
    {
        if (TryGetEventName(pulse.Button, pulse.Waveform, out _))
        {
            request = $"{pulse.Button.ToString().ToUpperInvariant()}|{pulse.Waveform}";
            return true;
        }

        request = string.Empty;
        return false;
    }

    public static bool TryGetEventNameFromRequest(string? request, out string eventName)
    {
        var parts = request?.Split('|');
        if (parts is { Length: 2 })
        {
            var button = parts[0] switch
            {
                "LEFT" => MouseButton.Left,
                "RIGHT" => MouseButton.Right,
                _ => (MouseButton?)null
            };

            if (button.HasValue && TryGetEventName(button.Value, parts[1], out eventName))
            {
                return true;
            }
        }

        eventName = string.Empty;
        return false;
    }

    private static bool TryGetEventName(MouseButton button, string waveform, out string eventName)
    {
        var buttonName = button switch
        {
            MouseButton.Left => "left",
            MouseButton.Right => "right",
            _ => null
        };
        var waveformName = waveform switch
        {
            "sharp_state_change" => "SharpStateChange",
            "subtle_collision" => "SubtleCollision",
            "damp_collision" => "DampCollision",
            "completed" => "Completed",
            "happy_alert" => "HappyAlert",
            _ => null
        };

        if (buttonName is not null && waveformName is not null)
        {
            eventName = buttonName + waveformName;
            return true;
        }

        eventName = string.Empty;
        return false;
    }
}
