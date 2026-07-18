using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace LinboNative
{
    public static class LinboInstaller
    {
        private const string AppName = "泠波";
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Linbo";
        private const string AppResource = "LinboAppPayload";
        private const string UninstallResource = "LinboUninstallPayload";
        private const string IconPngResource = "LinboIconPngPayload";
        private const string IconIcoResource = "LinboIconIcoPayload";
        private const string SpiritResource = "MistSpiritPayload";

        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                bool silent = false;
                bool noLaunch = false;
                bool noShortcuts = false;
                bool noRegistry = false;
                string forcedDir = null;
                foreach (string arg in args)
                {
                    if (arg.Equals("/silent", StringComparison.OrdinalIgnoreCase)) silent = true;
                    else if (arg.Equals("/nolaunch", StringComparison.OrdinalIgnoreCase)) noLaunch = true;
                    else if (arg.Equals("/noshortcuts", StringComparison.OrdinalIgnoreCase)) noShortcuts = true;
                    else if (arg.Equals("/noregistry", StringComparison.OrdinalIgnoreCase)) noRegistry = true;
                    else if (arg.StartsWith("/dir=", StringComparison.OrdinalIgnoreCase)) forcedDir = arg.Substring(5).Trim('"');
                }
                string defaultDir = Path.Combine(GetRealLocalAppData(), "Programs", "Linbo");
                string installDir = forcedDir ?? (silent ? defaultDir : ChooseInstallDir(defaultDir));
                if (String.IsNullOrEmpty(installDir)) return 0;

                Directory.CreateDirectory(installDir);
                Directory.CreateDirectory(Path.Combine(installDir, "assets"));
                KillRunningApp();
                WriteResource(AppResource, Path.Combine(installDir, "Linbo.exe"));
                WriteResource(UninstallResource, Path.Combine(installDir, "LinboUninstall.exe"));
                WriteResource(IconPngResource, Path.Combine(installDir, "assets", "linbo-icon.png"));
                WriteResource(IconIcoResource, Path.Combine(installDir, "assets", "linbo-icon.ico"));
                WriteResource(SpiritResource, Path.Combine(installDir, "assets", "mist-spirit.jpg"));

                string exePath = Path.Combine(installDir, "Linbo.exe");
                string uninstallPath = Path.Combine(installDir, "LinboUninstall.exe");
                string iconPath = Path.Combine(installDir, "assets", "linbo-icon.ico");
                if (!noShortcuts) CreateShortcuts(installDir, exePath, uninstallPath, iconPath);
                if (!noRegistry) WriteUninstallRegistry(installDir, exePath, uninstallPath, iconPath);

                if (!silent) MessageBox.Show("泠波安装完成。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (!noLaunch) Process.Start(new ProcessStartInfo(exePath) { WorkingDirectory = installDir });
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("安装失败：\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static string ChooseInstallDir(string defaultDir)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择泠波安装位置";
                dialog.SelectedPath = defaultDir;
                dialog.ShowNewFolderButton = true;
                DialogResult result = dialog.ShowDialog();
                return result == DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        private static string GetRealLocalAppData()
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            DirectoryInfo desktopDir = String.IsNullOrEmpty(desktop) ? null : new DirectoryInfo(desktop);
            DirectoryInfo userDir = desktopDir == null ? null : desktopDir.Parent;
            if (userDir != null && userDir.Name.Equals("Desktop", StringComparison.OrdinalIgnoreCase))
            {
                userDir = userDir.Parent;
            }
            if (userDir != null && Directory.Exists(userDir.FullName))
            {
                return Path.Combine(userDir.FullName, "AppData", "Local");
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        private static void KillRunningApp()
        {
            foreach (Process process in Process.GetProcessesByName(AppName))
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch { }
            }
            foreach (Process process in Process.GetProcessesByName("Linbo"))
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch { }
            }
        }

        private static void WriteResource(string resourceName, string targetPath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream input = assembly.GetManifestResourceStream(resourceName))
            {
                if (input == null) throw new InvalidOperationException("缺少安装资源：" + resourceName);
                string parent = Path.GetDirectoryName(targetPath);
                if (!String.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        if (File.Exists(targetPath)) File.Delete(targetPath);
                        input.Position = 0;
                        using (FileStream output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            input.CopyTo(output);
                        }
                        return;
                    }
                    catch
                    {
                        if (attempt == 4) throw;
                        Thread.Sleep(300);
                    }
                }
            }
        }

        private static void CreateShortcuts(string installDir, string exePath, string uninstallPath, string iconPath)
        {
            string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");
            CreateShortcut(desktopShortcut, exePath, installDir, iconPath);

            string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppName);
            Directory.CreateDirectory(startMenuDir);
            CreateShortcut(Path.Combine(startMenuDir, AppName + ".lnk"), exePath, installDir, iconPath);
            CreateShortcut(Path.Combine(startMenuDir, "卸载" + AppName + ".lnk"), uninstallPath, installDir, iconPath);
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string iconPath)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        private static void WriteUninstallRegistry(string installDir, string exePath, string uninstallPath, string iconPath)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", "3.0");
                key.SetValue("Publisher", "Linbo");
                key.SetValue("InstallLocation", installDir);
                key.SetValue("DisplayIcon", iconPath + ",0");
                key.SetValue("UninstallString", "\"" + uninstallPath + "\"");
                key.SetValue("QuietUninstallString", "\"" + uninstallPath + "\" /quiet");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
    }
}
