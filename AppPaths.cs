using System;
using System.IO;
using System.Reflection;

namespace MusicPlayerSyncInterface;

/// <summary>
/// Resolves where an app keeps its per-user persisted data: the folder that directly contains its config
/// file and its SQLite database file.
///
/// On Linux this follows the XDG Base Directory specification, so the data lands below $XDG_DATA_HOME
/// (defaulting to ~/.local/share) instead of next to the executable - which for a development install is
/// the build output folder and therefore thrown away by every clean/rebuild. Everywhere else (Windows,
/// macOS, ...) the "Persistence" folder next to the executable is kept, which is the portable layout
/// those installs use (their build output directory already separates Debug from Release).
///
/// The whole resolution can be overridden with the MUSIC_PLAYER_DATA_DIR environment variable. That is
/// mainly needed for the EF Core tools ("dotnet ef ..."): the entry assembly of those tools is the tool
/// itself, not the app, so the executable-adjacent default would otherwise point into the tool's own
/// directory (which is why the port's launcher used to pass a path around explicitly).
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// Overrides the data directory (which is then used verbatim, without any configuration suffix).
    /// </summary>
    public const string DataDirectoryEnvironmentVariable = "MUSIC_PLAYER_DATA_DIR";

    /// <summary>Name of the data folder that is used next to the executable off Linux.</summary>
    public const string ExecutableAdjacentFolderName = "Persistence";

    /// <summary>
    /// The app's data directory (not necessarily existing yet, see <see cref="EnsureDataDirectory"/>).
    /// </summary>
    /// <param name="appName">Folder name of the app, e.g. "MusicPlayerAvaloniaPort".</param>
    /// <param name="configurationName">
    /// Optional suffix for the folder name, e.g. "Debug"/"Release". On Linux it separates the data of the
    /// build configurations, which share one data root there; off Linux it is ignored, because the
    /// executable (and with it the "Persistence" folder) already lives in a configuration-specific build
    /// output directory.
    /// </param>
    public static string GetDataDirectory(string appName, string? configurationName = null)
    {
        string? overriddenDirectory = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overriddenDirectory))
            return Path.GetFullPath(overriddenDirectory);

        if (OperatingSystem.IsLinux())
        {
            string? dataHome = GetXdgDataHome();
            if (dataHome != null)
                return Path.Combine(dataHome, GetFolderName(appName, configurationName));
        }

        return GetExecutableAdjacentDataDirectory();
    }

    /// <summary>
    /// Like <see cref="GetDataDirectory"/> but also creates the folder when it does not exist yet.
    /// </summary>
    public static string EnsureDataDirectory(string appName, string? configurationName = null)
    {
        string dataDirectory = GetDataDirectory(appName, configurationName);
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    /// <summary>
    /// The folder name <see cref="GetDataDirectory"/> uses on Linux, e.g. "MusicPlayerAvaloniaPort-Release".
    /// </summary>
    public static string GetFolderName(string appName, string? configurationName) =>
        string.IsNullOrWhiteSpace(configurationName) ? appName : $"{appName}-{configurationName}";

    /// <summary>
    /// The folder every platform persisted into before <see cref="GetDataDirectory"/> was introduced: one
    /// "Persistence" folder next to the executable. Used as the source of the one-time data migration of an
    /// existing installation (and as the data directory itself off Linux).
    /// </summary>
    public static string GetExecutableAdjacentDataDirectory() =>
        Path.Combine(GetExecutableDirectory(), ExecutableAdjacentFolderName);

    /// <summary>
    /// $XDG_DATA_HOME, or ~/.local/share when it is not set. A relative XDG_DATA_HOME is ignored (the
    /// specification requires an absolute path); null is returned when there is no home directory either,
    /// so the caller falls back to the executable-adjacent folder.
    /// </summary>
    static string? GetXdgDataHome()
    {
        string? dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(dataHome) && Path.IsPathRooted(dataHome))
            return dataHome;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(home) ? null : Path.Combine(home, ".local", "share");
    }

    static string GetExecutableDirectory() =>
        // A single-file publish reports an empty Location, in which case the base directory (the folder the
        // executable was started from) is the right answer.
        Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? "") is { Length: > 0 } executableDirectory
            ? executableDirectory
            : AppContext.BaseDirectory;
}
