using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace UndertaleModTool_WinUI;

internal static class WinUiFileAssociations
{
    private const string SoftwareClassName = "UndertaleModTool.WinUI";
    private const long ShcneAssocChanged = 0x08000000;
    private static readonly string[] Extensions = [".win", ".unx", ".ios", ".droid"];

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(long eventId, uint flags, IntPtr item1, IntPtr item2);

    internal static void Apply(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            if (enabled)
                CreateAssociations();
            else
                RemoveAssociations();
        }
        catch
        {
            // File-association failure must never prevent the editor from starting
            // or saving its settings. The next settings change will retry it.
        }
    }

    private static void CreateAssociations()
    {
        string? executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
            return;

        using RegistryKey classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes", writable: true);
        using RegistryKey app = classes.CreateSubKey(SoftwareClassName, writable: true);
        bool changed = false;

        changed |= SetDefaultValue(app, string.Empty, "UndertaleModTool WinUI Preview");
        changed |= SetDefaultValue(app, @"shell\open\command", $"\"{executable}\" \"%1\"");

        foreach (string extension in Extensions)
            changed |= SetDefaultValue(classes, extension, SoftwareClassName);

        if (changed)
            SHChangeNotify(ShcneAssocChanged, 0, IntPtr.Zero, IntPtr.Zero);
    }

    private static void RemoveAssociations()
    {
        using RegistryKey classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes", writable: true);
        bool changed = false;

        foreach (string extension in Extensions)
        {
            using RegistryKey? extensionKey = classes.OpenSubKey(extension, writable: false);
            if (!string.Equals(extensionKey?.GetValue(string.Empty) as string, SoftwareClassName, StringComparison.Ordinal))
                continue;

            extensionKey?.Dispose();
            classes.DeleteSubKeyTree(extension, throwOnMissingSubKey: false);
            changed = true;
        }

        using RegistryKey? appKey = classes.OpenSubKey(SoftwareClassName, writable: false);
        if (appKey is not null)
        {
            appKey.Dispose();
            classes.DeleteSubKeyTree(SoftwareClassName, throwOnMissingSubKey: false);
            changed = true;
        }

        if (changed)
            SHChangeNotify(ShcneAssocChanged, 0, IntPtr.Zero, IntPtr.Zero);
    }

    private static bool SetDefaultValue(RegistryKey parent, string subkeyName, string value)
    {
        RegistryKey key = string.IsNullOrEmpty(subkeyName)
            ? parent
            : parent.CreateSubKey(subkeyName, writable: true);
        try
        {
            string? existing = key.GetValue(string.Empty) as string;
            if (string.Equals(existing, value, StringComparison.Ordinal))
                return false;

            key.SetValue(string.Empty, value, RegistryValueKind.String);
            return true;
        }
        finally
        {
            if (!ReferenceEquals(key, parent))
                key.Dispose();
        }
    }
}
