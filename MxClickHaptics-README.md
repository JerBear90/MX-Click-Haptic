# MX Click Haptics

MX Click Haptics is a Windows desktop control panel and Logitech Actions SDK plugin for sending haptic events when the left or right mouse button is clicked.

## Requirements

- Windows
- .NET 10 SDK
- Logitech Options+ and the Logi Plugin Service
- `PluginApi.dll` installed at `C:\Program Files\Logi\LogiPluginService\PluginApi.dll`
- A Logitech device that supports the selected haptic waveform

## Build and load the plugin

From this directory, build the solution:

```powershell
dotnet build .\MxClickHaptics.slnx -nologo --verbosity minimal
```

The plugin project creates a development link under `%LOCALAPPDATA%\Logi\LogiPluginService\Plugins`. Restart the Logi Plugin Service after the first build so it loads the plugin.

## Run the desktop app

```powershell
dotnet run --project .\MxClickHaptics.App\MxClickHaptics.App.csproj
```

Select **Real Logitech output** and set the left and right click amounts independently:

- **Low** uses `subtle_collision`.
- **Medium** uses `damp_collision`.
- **High** uses `sharp_state_change`.

These are Logitech's named waveform presets, not a continuous intensity control. Use **Test left** and **Test right** to try each setting. With the plugin loaded, mouse clicks send haptic events through the local named-pipe bridge. The plugin maps those events in `MxClickHaptics.LogitechPlugin\package\events\extra\eventMapping.yaml`.

The app also supports **Simulated** output for testing without the Logitech plugin.

## Tests

```powershell
dotnet test .\MxClickHaptics.slnx -nologo --verbosity minimal
```

## Limitations

The Logitech SDK lets the plugin raise haptic events but does not confirm that the physical device delivered a pulse. Actual output depends on the connected device and Logitech runtime configuration. The plugin requires the locally installed `PluginApi.dll` and is built for Windows.

## Logitech Marketplace

The development link created by `dotnet build` is for local testing, not Marketplace distribution. Logitech requires a `.lplug4` package, an icon in the plugin metadata folder, an end-user license agreement, and submission through its [contribution form](https://marketplace.logitech.com/contribute) for review. See Logitech's [Marketplace approval guidelines](https://logitech.github.io/actions-sdk-docs/marketplace-approval-guidelines/) and [C# distribution instructions](https://logitech.github.io/actions-sdk-docs/csharp/plugin-development/distributing-the-plugin/). The installed Plugin Tool/package command still needs to be verified on this machine.
