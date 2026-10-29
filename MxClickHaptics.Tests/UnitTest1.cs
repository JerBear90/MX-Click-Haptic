using MxClickHaptics.Core;

namespace MxClickHaptics.Tests;

public class HapticControllerTests
{
    [Fact]
    public void LeftAndRightFeedbackCanBeToggledIndependently()
    {
        var settings = new HapticSettings
        {
            Enabled = true,
            LeftFeedbackEnabled = true,
            RightFeedbackEnabled = false,
            Paused = false
        };

        Assert.True(settings.LeftFeedbackEnabled);
        Assert.False(settings.RightFeedbackEnabled);
        Assert.True(settings.Enabled);
    }

    [Fact]
    public void GlobalPauseStopsFeedback()
    {
        var settings = new HapticSettings
        {
            Enabled = true,
            LeftFeedbackEnabled = true,
            RightFeedbackEnabled = true,
            Paused = true
        };

        var controller = new HapticController(settings, new SimulatedOutputAdapter());
        var source = new SyntheticInputSource();
        source.PushButtonDown(MouseButton.Left);

        var pulses = controller.ProcessTick(source, DateTimeOffset.UtcNow);
        Assert.Empty(pulses);
    }

    [Fact]
    public void LeftButtonDownTriggersOnePulseWhenEnabled()
    {
        var settings = new HapticSettings { Enabled = true, LeftFeedbackEnabled = true, RightFeedbackEnabled = true, Paused = false };
        var controller = new HapticController(settings, new SimulatedOutputAdapter());
        var source = new SyntheticInputSource();
        source.PushButtonDown(MouseButton.Left);

        var pulses = controller.ProcessTick(source, DateTimeOffset.UtcNow);

        Assert.Single(pulses);
        Assert.Equal(MouseButton.Left, pulses[0].Button);
    }

    [Fact]
    public void LeftAndRightFeedbackAmountsAreIndependent()
    {
        var settings = new HapticSettings
        {
            Enabled = true,
            LeftFeedbackEnabled = true,
            RightFeedbackEnabled = true,
            LeftWaveform = "subtle_collision",
            RightWaveform = "damp_collision"
        };
        var output = new SimulatedOutputAdapter();
        var controller = new HapticController(settings, output);
        var left = new SyntheticInputSource();
        left.PushButtonDown(MouseButton.Left);
        var right = new SyntheticInputSource();
        right.PushButtonDown(MouseButton.Right);

        controller.ProcessTick(left, DateTimeOffset.UtcNow);
        controller.ProcessTick(right, DateTimeOffset.UtcNow);

        Assert.Equal("subtle_collision", output.Diagnostics[0].Waveform);
        Assert.Equal("damp_collision", output.Diagnostics[1].Waveform);
    }

    [Fact]
    public void ButtonUpDoesNotTriggerPulse()
    {
        var settings = new HapticSettings { Enabled = true, LeftFeedbackEnabled = true, RightFeedbackEnabled = true, Paused = false };
        var controller = new HapticController(settings, new SimulatedOutputAdapter());
        var source = new SyntheticInputSource();
        source.PushButtonUp(MouseButton.Left);

        var pulses = controller.ProcessTick(source, DateTimeOffset.UtcNow);

        Assert.Empty(pulses);
    }

    [Fact]
    public void HeldButtonDoesNotQueueRepeatedPulses()
    {
        var settings = new HapticSettings { Enabled = true, LeftFeedbackEnabled = true, RightFeedbackEnabled = true, Paused = false, QueueLimit = 10 };
        var controller = new HapticController(settings, new SimulatedOutputAdapter());
        var source = new SyntheticInputSource();
        source.PushButtonDown(MouseButton.Left);
        source.PushButtonDown(MouseButton.Left);
        source.PushButtonDown(MouseButton.Left);

        var pulses = controller.ProcessTick(source, DateTimeOffset.UtcNow);

        Assert.Single(pulses);
    }

    [Fact]
    public void QueueLimitDropsOldestPendingEvents()
    {
        var settings = new HapticSettings { Enabled = true, LeftFeedbackEnabled = true, RightFeedbackEnabled = true, Paused = false, QueueLimit = 2 };
        var controller = new HapticController(settings, new SimulatedOutputAdapter());
        var source = new SyntheticInputSource();

        source.PushButtonDown(MouseButton.Left);
        source.PushButtonDown(MouseButton.Right);
        source.PushButtonDown(MouseButton.Left);
        source.PushButtonDown(MouseButton.Right);
        source.PushButtonDown(MouseButton.Left);

        var pulses = controller.ProcessTick(source, DateTimeOffset.UtcNow);

        Assert.Equal(2, pulses.Count);
        Assert.Equal(MouseButton.Left, pulses[0].Button);
        Assert.Equal(MouseButton.Right, pulses[1].Button);
    }

    [Fact]
    public void SettingsPersistAndReload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mx-click-haptics-{Guid.NewGuid():N}.json");
        var settings = new HapticSettings
        {
            Enabled = true,
            LeftFeedbackEnabled = true,
            RightFeedbackEnabled = false,
            Paused = false,
            OutputMode = OutputMode.Simulated,
            QueueLimit = 4,
            LeftWaveform = "subtle_collision",
            RightWaveform = "damp_collision"
        };

        settings.Save(path);
        var restored = HapticSettings.Load(path);

        Assert.True(restored.Enabled);
        Assert.True(restored.LeftFeedbackEnabled);
        Assert.False(restored.RightFeedbackEnabled);
        Assert.Equal(OutputMode.Simulated, restored.OutputMode);
        Assert.Equal(4, restored.QueueLimit);
        Assert.Equal("subtle_collision", restored.LeftWaveform);
        Assert.Equal("damp_collision", restored.RightWaveform);

        File.Delete(path);
    }

    [Fact]
    public void SimulatedAdapterProvidesDiagnosticsAndShutdownCleansUp()
    {
        var adapter = new SimulatedOutputAdapter();
        adapter.EnqueuePulse(new HapticPulse(MouseButton.Left, DateTimeOffset.UtcNow, "sharp_state_change"));
        adapter.EnqueuePulse(new HapticPulse(MouseButton.Right, DateTimeOffset.UtcNow, "sharp_state_change"));

        adapter.Dispose();

        Assert.True(adapter.IsDisposed);
    }

    [Theory]
    [InlineData(MouseButton.Left, "sharp_state_change", "LEFT|sharp_state_change", "leftSharpStateChange")]
    [InlineData(MouseButton.Right, "subtle_collision", "RIGHT|subtle_collision", "rightSubtleCollision")]
    [InlineData(MouseButton.Left, "damp_collision", "LEFT|damp_collision", "leftDampCollision")]
    [InlineData(MouseButton.Right, "damp_collision", "RIGHT|damp_collision", "rightDampCollision")]
    [InlineData(MouseButton.Left, "completed", "LEFT|completed", "leftCompleted")]
    [InlineData(MouseButton.Right, "happy_alert", "RIGHT|happy_alert", "rightHappyAlert")]
    public void LogitechBridgeMapsSupportedPulsesToPluginEvents(
        MouseButton button,
        string waveform,
        string request,
        string eventName)
    {
        var pulse = new HapticPulse(button, DateTimeOffset.UtcNow, waveform);

        Assert.True(LogitechHapticsBridgeProtocol.TryCreateRequest(pulse, out var actualRequest));
        Assert.Equal(request, actualRequest);
        Assert.True(LogitechHapticsBridgeProtocol.TryGetEventNameFromRequest(actualRequest, out var actualEventName));
        Assert.Equal(eventName, actualEventName);
    }

    [Theory]
    [InlineData("LEFT|alert")]
    [InlineData("MIDDLE|sharp_state_change")]
    [InlineData("RIGHT|sharp_state_change|extra")]
    public void LogitechBridgeRejectsUnsupportedRequests(string request)
    {
        Assert.False(LogitechHapticsBridgeProtocol.TryGetEventNameFromRequest(request, out var eventName));
        Assert.Empty(eventName);
    }
}
