using System.IO.Pipes;
using System.Text;
using MxClickHaptics.Core;

namespace Loupedeck.MxClickHapticsPlugin;

public sealed class MxClickHapticsPlugin : Plugin
{
    private CancellationTokenSource? _serverCancellation;
    private Task? _serverTask;

    public override bool UsesApplicationApiOnly => true;
    public override bool HasNoApplication => true;

    public MxClickHapticsPlugin()
    {
        PluginLog.Initialize(Log);
    }

    public override void Load()
    {
        _serverCancellation = new CancellationTokenSource();
        _serverTask = RunPipeServerAsync(_serverCancellation.Token);
        PluginLog.Info("MX Click Haptics plugin loaded.");
    }

    public override void Unload()
    {
        _serverCancellation?.Cancel();
        _serverTask?.GetAwaiter().GetResult();
        _serverCancellation?.Dispose();
        _serverCancellation = null;
        _serverTask = null;
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
