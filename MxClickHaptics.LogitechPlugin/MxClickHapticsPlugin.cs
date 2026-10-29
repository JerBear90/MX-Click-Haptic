using System.IO.Pipes;
using System.Text;
using MxClickHaptics.Core;
using System.Text.Json;
using CoreMouseButton = MxClickHaptics.Core.MouseButton;

namespace Loupedeck.MxClickHapticsPlugin;

public sealed class MxClickHapticsPlugin : Plugin
{
    private static readonly (string Name, string DisplayName)[] HapticEvents =
    [
        ("leftSharpStateChange", "Left click - sharp state change"),
        ("leftSubtleCollision", "Left click - low"),
        ("leftDampCollision", "Left click - medium"),
        ("leftCompleted", "Left click - completed"),
        ("leftHappyAlert", "Left click - happy alert"),
        ("rightSharpStateChange", "Right click - sharp state change"),
        ("rightSubtleCollision", "Right click - low"),
        ("rightDampCollision", "Right click - medium"),
        ("rightCompleted", "Right click - completed"),
        ("rightHappyAlert", "Right click - happy alert")
    ];

    private CancellationTokenSource? _serverCancellation;
    private Task? _serverTask;
    private MouseHookController? _mouseHook;

    public override bool UsesApplicationApiOnly => true;
    public override bool HasNoApplication => true;

    public MxClickHapticsPlugin()
    {
        PluginLog.Initialize(Log);
    }

    public override void Load()
    {
        foreach (var hapticEvent in HapticEvents)
        {
            PluginEvents.AddEvent(
                hapticEvent.Name,
                hapticEvent.DisplayName,
                "Fired when the corresponding click haptic is sent.");
        }

        _mouseHook = new MouseHookController(OnMouseButtonChanged);
        _serverCancellation = new CancellationTokenSource();
        _serverTask = RunPipeServerAsync(_serverCancellation.Token);
        PluginLog.Info("MX Click Haptics plugin loaded.");
    }

    public override void Unload()
    {
        _mouseHook?.Dispose();
        _mouseHook = null;
        _serverCancellation?.Cancel();
        _serverTask?.GetAwaiter().GetResult();
        _serverCancellation?.Dispose();
        _serverCancellation = null;
        _serverTask = null;
    }

    private void OnMouseButtonChanged(CoreMouseButton button, bool isDown)
    {
        if (!isDown)
        {
            return;
        }

        try
        {
            var settings = HapticSettings.Load(HapticSettings.DefaultPath);
            if (!settings.Enabled || settings.Paused)
            {
                return;
            }

            if ((button == CoreMouseButton.Left && !settings.LeftFeedbackEnabled)
                || (button == CoreMouseButton.Right && !settings.RightFeedbackEnabled))
            {
                return;
            }

            var pulse = new HapticPulse(
                button,
                DateTimeOffset.UtcNow,
                button == CoreMouseButton.Left ? settings.LeftWaveform : settings.RightWaveform);
            if (LogitechHapticsBridgeProtocol.TryCreateRequest(pulse, out var request)
                && LogitechHapticsBridgeProtocol.TryGetEventNameFromRequest(request, out var eventName))
            {
                PluginEvents.RaiseEvent(eventName);
            }
        }
        catch (IOException ex)
        {
            PluginLog.Error(ex, "Unable to read MX Click Haptics settings.");
        }
        catch (UnauthorizedAccessException ex)
        {
            PluginLog.Error(ex, "Unable to access MX Click Haptics settings.");
        }
        catch (JsonException ex)
        {
            PluginLog.Error(ex, "MX Click Haptics settings contain invalid JSON.");
        }
        catch (InvalidOperationException ex)
        {
            PluginLog.Error(ex, "Logi Plugin Service rejected a haptic event.");
        }
    }

    private async Task RunPipeServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    LogitechHapticsBridgeProtocol.PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true)
                {
                    AutoFlush = true
                };

                var request = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                string response;
                if (request == "PING")
                {
                    response = "READY";
                }
                else if (LogitechHapticsBridgeProtocol.TryGetEventNameFromRequest(request, out var eventName))
                {
                    PluginEvents.RaiseEvent(eventName);
                    response = "DISPATCHED";
                }
                else
                {
                    response = "ERROR|Unsupported event request";
                }

                await writer.WriteLineAsync(response.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "MX Click Haptics pipe listener failed.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }
}
