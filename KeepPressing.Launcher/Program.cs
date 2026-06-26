using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeepPressing.Launcher;

// This root-level exe is a thin shim that just launches the real exe in the app\ subfolder. It exists so
// users can tell at a glance which file to run after extracting, and holds no logic.
internal static partial class Program
{
    private const uint MB_ICONERROR = 0x00000010;

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private static int Main()
    {
        string? launcherPath = Environment.ProcessPath;
        if (launcherPath is null)
        {
            Fail("ランチャー自身のパスを解決できませんでした。");
            return 1;
        }

        string root = Path.GetDirectoryName(launcherPath)!;
        string appDir = Path.Combine(root, "app");
        string appExe = Path.Combine(appDir, "KeepPressing.exe");

        if (!File.Exists(appExe))
        {
            Fail($"本体が見つかりません:\n{appExe}\n\n" +
                 "zip を展開したフォルダーごと実行してください（app フォルダーは移動・分離しないでください）。");
            return 1;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = appExe,
                // The .NET apphost resolves .deps.json / .runtimeconfig.json from its own directory, so the
                // working directory must be app\.
                WorkingDirectory = appDir,
                UseShellExecute = false,
            };

            // Forward the launcher's arguments to the app (skipping arg 0, the executable name).
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                psi.ArgumentList.Add(args[i]);
            }

            // Spawn and exit; don't wait for the child. Once the GUI app starts, the launcher is done.
            using (Process.Start(psi))
            {
            }

            return 0;
        }
        catch (Exception ex)
        {
            Fail($"KeepPressing の起動に失敗しました:\n{ex.Message}");
            return 1;
        }
    }

    // The only way to report errors in a console-less GUI subsystem.
    private static void Fail(string message)
        => _ = MessageBoxW(IntPtr.Zero, message, "KeepPressing", MB_ICONERROR);
}
