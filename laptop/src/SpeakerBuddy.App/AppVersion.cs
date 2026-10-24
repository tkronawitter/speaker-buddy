using System.Reflection;

namespace SpeakerBuddy.App;

/// <summary>Version of this program: the git describe of the build, set by the project file.</summary>
public static class AppVersion
{
    public static string Current { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}
