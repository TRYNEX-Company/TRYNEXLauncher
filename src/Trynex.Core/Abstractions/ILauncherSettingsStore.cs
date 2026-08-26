using Trynex.Core.Settings;

namespace Trynex.Core.Abstractions;

public interface ILauncherSettingsStore
{
    Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default);
}
