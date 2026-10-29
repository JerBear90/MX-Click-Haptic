# MX Click Haptics

MX Click Haptics is a Logitech Actions SDK plugin for sending haptic events on left and right mouse clicks. It detects clicks inside the Logi Plugin Service; the optional Windows desktop app provides settings and test buttons.

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

The app's **Real Logitech output** mode sends test events to the plugin. Set left and right click amounts independently in the plugin's **Haptic Settings** actions:

- **Set Left Click: Low/Medium/High** uses `subtle_collision`, `damp_collision`, or `sharp_state_change` (mapped to `sharp_collision` on MX Master 4).
- **Set Right Click: Low/Medium/High** sets the right side independently.
- Enable/disable actions control each side, and **Pause Click Feedback** / **Enable Click Feedback** pause or resume output.

These are Logitech's named waveform presets, not a continuous intensity control. Use the app's **Test left** and **Test right** buttons to try each setting. Plugin actions and the optional app share settings in `%LOCALAPPDATA%\MX-Click-Haptics\settings.json`. Mouse clicks are detected by the Logitech plugin itself and mapped to the waveforms in `MxClickHaptics.LogitechPlugin\package\events\extra\eventMapping.yaml`.

The app also supports **Simulated** output for testing without the Logitech plugin.

## Tests

```powershell
dotnet test .\MxClickHaptics.slnx -nologo --verbosity minimal
```

## Limitations

The Logitech SDK lets the plugin raise haptic events but does not confirm that the physical device delivered a pulse. Actual output depends on the connected device and Logitech runtime configuration. The plugin requires the locally installed `PluginApi.dll` and is built for Windows.

## Logitech Marketplace

### Prepared in this repository

- Plugin metadata includes the Marketplace name, version, author, MIT license, homepage, and GitHub Issues support link.
- A 256x256 plugin icon is included at `MxClickHaptics.LogitechPlugin\package\metadata\Icon256x256.png`. Regenerate it on Windows with `.\tools\GenerateMarketplaceIcon.ps1`.
- The MIT license/EULA is in `LICENSE` and included as `EULA.txt` in the package.
- A Release package target runs Logitech's pack and verify commands:

```powershell
dotnet msbuild .\MxClickHaptics.LogitechPlugin\MxClickHaptics.LogitechPlugin.csproj -target:PackMarketplace -property:Configuration=Release
```

This requires `logiplugintool` on `PATH`. The official Logitech tool is installed as a global .NET tool, and version 1.3 has been built and verified at `MxClickHaptics.LogitechPlugin\artifacts\Marketplace\MxClickHaptics_1_3.lplug4`.

### Remaining blockers before submission

1. **Accept the Logitech Marketplace Developer Agreement.** The MIT `LICENSE` is included as the user terms/EULA, as approved by the publisher.
2. **Confirm physical click detection** with the installed package on supported hardware. Version 1.3 runs the Windows low-level mouse hook on a dedicated message-loop thread and queues click processing off that hook thread.
3. **Submit** the verified package and listing details through Logitech's [contribution form](https://marketplace.logitech.com/contribute) for approval. Logitech's review is required before Marketplace publication.

The version 1.2 package's left and right test pulses were confirmed on an MX Master 4. Version 1.3 contains the physical click-capture fix and is installed, but normal physical click testing is still needed to confirm it.

The app and plugin currently process mouse button events locally, send them only over a local named pipe, and store settings in a local JSON file; they do not send telemetry to a server. The repository includes a [privacy notice](PRIVACY.md); review it and publish/link it if Logitech requests one. If data collection changes, update the notice before submission.

See Logitech's [Marketplace approval guidelines](https://logitech.github.io/actions-sdk-docs/marketplace-approval-guidelines/), [C# distribution instructions](https://logitech.github.io/actions-sdk-docs/csharp/plugin-development/distributing-the-plugin/), and [plugin structure requirements](https://logitech.github.io/actions-sdk-docs/csharp/tutorial/plugin-structure/).
