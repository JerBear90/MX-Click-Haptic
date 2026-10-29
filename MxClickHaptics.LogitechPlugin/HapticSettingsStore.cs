using MxClickHaptics.Core;

namespace Loupedeck.MxClickHapticsPlugin;

internal static class HapticSettingsStore
{
    public static void Update(Action<HapticSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var settings = HapticSettings.Load(HapticSettings.DefaultPath);
        update(settings);
        settings.Save(HapticSettings.DefaultPath);
    }

}
