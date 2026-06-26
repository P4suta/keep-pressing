using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeepPressing.Launcher;

// ルート直下のこの exe は、本体一式を隔離した app\ サブフォルダの実体 exe を起動するだけの薄いシム。
// 「解凍したらどれを起動すればいいか」を一目で分かるようにするためのもので、ロジックは持たない。
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
                // .NET apphost は .deps.json / .runtimeconfig.json を自身のディレクトリから解決するため、
                // 作業ディレクトリを app\ に合わせるのは必須。
                WorkingDirectory = appDir,
                UseShellExecute = false,
            };

            // ランチャーへ渡された引数を本体へそのまま転送する（先頭の実行ファイル名は除く）。
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                psi.ArgumentList.Add(args[i]);
            }

            // 子の終了は待たない（spawn-and-exit）。GUI 本体が立ち上がればランチャーの役目は終わり。
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

    // コンソールを持たない GUI サブシステムでの唯一の報告手段。
    private static void Fail(string message)
        => _ = MessageBoxW(IntPtr.Zero, message, "KeepPressing", MB_ICONERROR);
}
