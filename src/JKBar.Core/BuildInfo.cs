// Reports the running build's version so the UI and diagnostics quote one value.
using System.Reflection;

namespace JKBar.Core;

public static class BuildInfo
{
    /// <summary>
    /// Reads the informational version rather than the assembly version because that is the string a user sees
    /// and, later, the string an updater would compare.
    /// </summary>
    public static string Version =>
        typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(BuildInfo).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";
}
