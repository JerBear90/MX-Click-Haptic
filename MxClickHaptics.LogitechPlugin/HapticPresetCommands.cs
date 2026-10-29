using MxClickHaptics.Core;

namespace Loupedeck.MxClickHapticsPlugin;

public abstract class HapticPresetCommand : PluginDynamicCommand
{
    private readonly Action<HapticSettings> _update;

    protected HapticPresetCommand(string displayName, Action<HapticSettings> update)
        : base(displayName, "Set the haptic preset used for mouse clicks.", "Haptic Settings")
    {
        _update = update;
    }

    protected override void RunCommand(string actionParameter)
    {
        HapticSettingsStore.Update(_update);
        PluginLog.Info($"Applied haptic setting: {GetType().Name}");
    }
}

public sealed class SetLeftLowCommand : HapticPresetCommand
{
    public SetLeftLowCommand() : base("Set Left Click: Low", settings => settings.LeftWaveform = "subtle_collision") { }
}

public sealed class SetLeftMediumCommand : HapticPresetCommand
{
    public SetLeftMediumCommand() : base("Set Left Click: Medium", settings => settings.LeftWaveform = "damp_collision") { }
}

public sealed class SetLeftHighCommand : HapticPresetCommand
{
    public SetLeftHighCommand() : base("Set Left Click: High", settings => settings.LeftWaveform = "sharp_state_change") { }
}

public sealed class SetRightLowCommand : HapticPresetCommand
{
    public SetRightLowCommand() : base("Set Right Click: Low", settings => settings.RightWaveform = "subtle_collision") { }
}

public sealed class SetRightMediumCommand : HapticPresetCommand
{
    public SetRightMediumCommand() : base("Set Right Click: Medium", settings => settings.RightWaveform = "damp_collision") { }
}

public sealed class SetRightHighCommand : HapticPresetCommand
{
    public SetRightHighCommand() : base("Set Right Click: High", settings => settings.RightWaveform = "sharp_state_change") { }
}

public sealed class EnableClickFeedbackCommand : HapticPresetCommand
{
    public EnableClickFeedbackCommand() : base("Enable Click Feedback", settings =>
    {
        settings.Enabled = true;
        settings.Paused = false;
    })
    {
    }
}

public sealed class PauseClickFeedbackCommand : HapticPresetCommand
{
    public PauseClickFeedbackCommand() : base("Pause Click Feedback", settings => settings.Paused = true) { }
}

public sealed class EnableLeftFeedbackCommand : HapticPresetCommand
{
    public EnableLeftFeedbackCommand() : base("Enable Left Click Feedback", settings => settings.LeftFeedbackEnabled = true) { }
}

public sealed class DisableLeftFeedbackCommand : HapticPresetCommand
{
    public DisableLeftFeedbackCommand() : base("Disable Left Click Feedback", settings => settings.LeftFeedbackEnabled = false) { }
}

public sealed class EnableRightFeedbackCommand : HapticPresetCommand
{
    public EnableRightFeedbackCommand() : base("Enable Right Click Feedback", settings => settings.RightFeedbackEnabled = true) { }
}

public sealed class DisableRightFeedbackCommand : HapticPresetCommand
{
    public DisableRightFeedbackCommand() : base("Disable Right Click Feedback", settings => settings.RightFeedbackEnabled = false) { }
}
