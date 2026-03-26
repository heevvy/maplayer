using System.Diagnostics;

namespace PipPlayer;

internal static class Startup
{
    private const string AppName = "Maplayer";
    private static readonly string InstallDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
    private static readonly string InstalledExe = Path.Combine(InstallDir, "PipPlayer.exe");

    private static readonly string StartupFolder =
        Environment.GetFolderPath(Environment.SpecialFolder.Startup);
    private static readonly string ShortcutPath =
        Path.Combine(StartupFolder, $"{AppName}.lnk");

    public static bool EnsureInstalled()
    {
        string currentExe = Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(currentExe)) return true;

        string currentNorm = Path.GetFullPath(currentExe).ToLowerInvariant();
        string installedNorm = Path.GetFullPath(InstalledExe).ToLowerInvariant();

        if (currentNorm == installedNorm)
        {
            CreateStartupShortcut();
            return true;
        }

        try
        {
            KillExistingInstances();
            Thread.Sleep(500);

            Directory.CreateDirectory(InstallDir);
            File.Copy(currentExe, InstalledExe, overwrite: true);
            CreateStartupShortcut();

            Process.Start(new ProcessStartInfo
            {
                FileName = InstalledExe,
                UseShellExecute = true,
            });

            System.Windows.Forms.MessageBox.Show(
                "Maplayer 설치가 완료되었습니다!\n\n" +
                "• PC 시작 시 자동으로 백그라운드 실행됩니다\n" +
                "• Chrome 확장에서 PIP을 시작하세요\n" +
                "• 이 설치 파일은 삭제해도 됩니다",
                "Maplayer",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information);

            return false;
        }
        catch
        {
            CreateStartupShortcut(currentExe);
            return true;
        }
    }

    private static void KillExistingInstances()
    {
        try
        {
            int myPid = Environment.ProcessId;
            string exeName = Path.GetFileNameWithoutExtension(InstalledExe);
            foreach (var name in new[] { AppName, exeName })
            {
                foreach (var proc in Process.GetProcessesByName(name))
                {
                    if (proc.Id != myPid)
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                    }
                    proc.Dispose();
                }
            }
        }
        catch { }
    }

    private static void CreateStartupShortcut(string? targetExe = null)
    {
        try
        {
            string target = targetExe ?? InstalledExe;

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(ShortcutPath);
            shortcut.TargetPath = target;
            shortcut.WorkingDirectory = Path.GetDirectoryName(target);
            shortcut.Description = "PIP Player";
            shortcut.Save();
        }
        catch { }
    }
}
