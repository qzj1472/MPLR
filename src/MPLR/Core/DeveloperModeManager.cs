using Fischless.Configuration;

namespace MPLR.Core;

internal static class DeveloperModeManager
{
    public static event EventHandler<bool>? Changed;

    public static bool IsEnabled => Configurations.IsDeveloperModeEnabled.Get();

    public static void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
        {
            return;
        }

        Configurations.IsDeveloperModeEnabled.Set(enabled);
        ConfigurationManager.Save();
        Changed?.Invoke(null, enabled);
    }
}
