using System.IO;
using System.Security;
using Microsoft.Win32;

namespace WinEyes;

internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WinEyes";

    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            string? command = runKey?.GetValue(ValueName) as string;
            string? executablePath = GetExecutablePath();
            return executablePath is not null &&
                   string.Equals(command, Quote(executablePath), StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (SecurityException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool TrySetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey? runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (runKey is null)
            {
                return false;
            }

            if (enabled)
            {
                string? executablePath = GetExecutablePath();
                if (executablePath is null)
                {
                    return false;
                }

                runKey.SetValue(ValueName, Quote(executablePath), RegistryValueKind.String);
            }
            else
            {
                runKey.DeleteValue(ValueName, false);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (SecurityException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? GetExecutablePath()
    {
        string? processPath = Environment.ProcessPath;
        if (IsWinEyesExecutable(processPath))
        {
            return processPath;
        }

        string appHostPath = Path.Combine(AppContext.BaseDirectory, "WinEyes.exe");
        return File.Exists(appHostPath) ? appHostPath : null;
    }

    private static bool IsWinEyesExecutable(string? path)
    {
        return path is not null &&
               string.Equals(
                   Path.GetFileNameWithoutExtension(path),
                   "WinEyes",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string Quote(string path)
    {
        return $"\"{path}\"";
    }
}
