using System.Windows;
using System.Windows.Controls;
using System.IO;
using MxClickHaptics.Core;

namespace MxClickHaptics.App;

public partial class MainWindow : Window
{
    private readonly HapticSettings _settings = new();
    private readonly SimulatedOutputAdapter _simulatedOutput = new();
    private readonly RealLogitechOutputAdapter _realOutput = new();
    private const string SettingsPath = "mx-click-haptics-settings.json";
    private MouseHookController? _mouseHook;
    private IHapticOutput _activeOutput;
    private HapticController _controller;

    public MainWindow()
    {
        InitializeComponent();

        _settings = HapticSettings.Load(SettingsPath);
        _activeOutput = _settings.OutputMode == OutputMode.Real ? _realOutput : _simulatedOutput;
        _controller = new HapticController(_settings, _activeOutput);

        ApplyUiFromSettings();
        UpdateStatus();
        _mouseHook = new MouseHookController(OnMouseButtonChanged);
    }

    private void OnMouseButtonChanged(MouseButton button, bool isDown)
    {
        if (Dispatcher.CheckAccess() == false)
        {
            Dispatcher.Invoke(() => OnMouseButtonChanged(button, isDown));
            return;
        }

        ProcessInput(button, isDown);
    }

    private void ProcessInput(MouseButton button, bool isDown)
    {
        var source = new SyntheticInputSource();
        if (isDown)
        {
            source.PushButtonDown(button);
        }
        else
        {
            source.PushButtonUp(button);
        }

        try
        {
            var pulses = _controller.ProcessTick(source, DateTimeOffset.UtcNow);
            UpdateDiagnostics(pulses);
            UpdateStatus();
        }
        catch (IOException ex)
        {
            StatusText.Text = $"Logitech haptics plugin connection failed: {ex.Message}";
        }
        catch (TimeoutException ex)
        {
            StatusText.Text = $"Logitech haptics plugin timed out: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            StatusText.Text = $"Logitech haptics event failed: {ex.Message}";
        }
    }

    private void TestLeftFeedbackButton_Click(object sender, RoutedEventArgs e)
    {
        ProcessInput(MouseButton.Left, true);
    }

    private void TestRightFeedbackButton_Click(object sender, RoutedEventArgs e)
    {
        ProcessInput(MouseButton.Right, true);
    }

    private void ApplyUiFromSettings()
    {
        EnabledCheckBox.IsChecked = _settings.Enabled;
        PausedCheckBox.IsChecked = _settings.Paused;
        LeftFeedbackCheckBox.IsChecked = _settings.LeftFeedbackEnabled;
        RightFeedbackCheckBox.IsChecked = _settings.RightFeedbackEnabled;
        OutputModeCombo.SelectedIndex = _settings.OutputMode == OutputMode.Real ? 1 : 0;

        SelectWaveform(LeftAmountCombo, _settings.LeftWaveform);
        SelectWaveform(RightAmountCombo, _settings.RightWaveform);

        EnabledCheckBox.Checked += (_, _) => ApplySettings();
        EnabledCheckBox.Unchecked += (_, _) => ApplySettings();
        PausedCheckBox.Checked += (_, _) => ApplySettings();
        PausedCheckBox.Unchecked += (_, _) => ApplySettings();
        LeftFeedbackCheckBox.Checked += (_, _) => ApplySettings();
        LeftFeedbackCheckBox.Unchecked += (_, _) => ApplySettings();
        RightFeedbackCheckBox.Checked += (_, _) => ApplySettings();
        RightFeedbackCheckBox.Unchecked += (_, _) => ApplySettings();
        OutputModeCombo.SelectionChanged += (_, _) => ApplySettings();
        LeftAmountCombo.SelectionChanged += (_, _) => ApplySettings();
        RightAmountCombo.SelectionChanged += (_, _) => ApplySettings();
    }

    private void ApplySettings()
    {
        _settings.Enabled = EnabledCheckBox.IsChecked == true;
        _settings.Paused = PausedCheckBox.IsChecked == true;
        _settings.LeftFeedbackEnabled = LeftFeedbackCheckBox.IsChecked == true;
        _settings.RightFeedbackEnabled = RightFeedbackCheckBox.IsChecked == true;
        _settings.OutputMode = OutputModeCombo.SelectedIndex == 1 ? OutputMode.Real : OutputMode.Simulated;

        _settings.LeftWaveform = GetSelectedWaveform(LeftAmountCombo);
        _settings.RightWaveform = GetSelectedWaveform(RightAmountCombo);

        _activeOutput = _settings.OutputMode == OutputMode.Real ? _realOutput : _simulatedOutput;
        _controller = new HapticController(_settings, _activeOutput);
        _settings.Save(SettingsPath);
        UpdateStatus();
    }

    private static void SelectWaveform(ComboBox comboBox, string waveform)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == waveform)
            ?? comboBox.Items.OfType<ComboBoxItem>()
                .First(item => item.Tag?.ToString() == "sharp_state_change");
    }

    private static string GetSelectedWaveform(ComboBox comboBox)
    {
        if (comboBox.SelectedItem is ComboBoxItem { Tag: string waveform }
            && LogitechHapticsBridgeProtocol.SupportedWaveforms.Contains(waveform))
        {
            return waveform;
        }

        throw new InvalidOperationException($"No supported feedback amount is selected for {comboBox.Name}.");
    }

    private void UpdateStatus()
    {
        var statusText = _settings.Paused
            ? "Paused. No haptic feedback is being sent."
            : _settings.Enabled
                ? _activeOutput.Status
                : "Disabled. All haptic output is off.";

        StatusText.Text = statusText;
    }

    private void UpdateDiagnostics(IReadOnlyCollection<HapticPulse>? pulses)
    {
        if (pulses is null)
        {
            return;
        }

        if (_activeOutput is SimulatedOutputAdapter simulated)
        {
            var lines = simulated.Diagnostics.Select(p => $"{p.Timestamp:HH:mm:ss.fff} {p.Button} {p.Waveform}");
            DiagnosticsList.ItemsSource = lines.TakeLast(12).ToList();
        }
        else
        {
            var lines = pulses.Select(p => $"{p.Timestamp:HH:mm:ss.fff} {p.Button} {p.Waveform}");
            DiagnosticsList.ItemsSource = lines.TakeLast(12).ToList();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _mouseHook?.Dispose();
        _simulatedOutput.Dispose();
        _realOutput.Dispose();
        _settings.Save(SettingsPath);
    }
}