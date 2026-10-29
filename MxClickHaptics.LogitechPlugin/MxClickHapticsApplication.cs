namespace Loupedeck.MxClickHapticsPlugin;

public sealed class MxClickHapticsApplication : ClientApplication
{
    protected override string GetProcessName() => string.Empty;

    protected override string GetBundleName() => string.Empty;

    public override ClientApplicationStatus GetApplicationStatus() => ClientApplicationStatus.Unknown;
}
