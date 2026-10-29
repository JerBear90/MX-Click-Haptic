namespace Loupedeck.MxClickHapticsPlugin;

internal static class PluginLog
{
    private static PluginLogFile? _log;

    public static void Initialize(PluginLogFile log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public static void Info(string message) => _log?.Info(message);

    public static void Error(Exception exception, string message) => _log?.Error(exception, message);
}
