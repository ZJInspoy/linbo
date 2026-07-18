using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LinboNative
{
    public class ExportState
    {
        public List<ExportCard> cards { get; set; }
    }

    public class ExportCard
    {
        public string title { get; set; }
        public string content { get; set; }
        public bool archived { get; set; }
        public double createdAt { get; set; }
    }

    public static class LinboUninstaller
    {
        private const string AppName = "泠波";
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Linbo";

        [STAThread]
        public static int Main(string[] args)
        {
            bool quiet = false;
            foreach (string arg in args) if (arg.Equals("/quiet", StringComparison.OrdinalIgnoreCase)) quiet = true;

            string installDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!quiet)
            {
                DialogResult result = MessageBox.Show(
                    "确定卸载泠波？\n\n本地卡片数据也会被清除。",
                    AppName,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (result != DialogResult.Yes) return 0;

                if (!OfferExportCards()) return 0;
            }

            TryKillRunningApp();
            RemoveShortcuts();
            RemoveRegistry();
            RemoveLocalData();
            ScheduleDirectoryRemoval(installDir);

            if (!quiet)
            {
                MessageBox.Show("泠波将被卸载。\n\n本地卡片数据已清除。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return 0;
        }

        private static bool OfferExportCards()
        {
            DialogResult export = MessageBox.Show(
                "是否一键导出卡片内容？\n\n选择“是”后，请选择导出文件夹。每张卡片会导出为一个 Word 文件，文件名使用卡片标题，正文使用卡片的粘贴内容。",
                AppName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (export != DialogResult.Yes) return true;

            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择卡片 Word 导出位置";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return ConfirmContinueWithoutExport("未选择导出位置。");
                }

                try
                {
                    int count = ExportCardsToWord(dialog.SelectedPath);
                    MessageBox.Show(
                        count > 0 ? "已导出 " + count + " 个 Word 文件。" : "没有可导出的卡片内容。",
                        AppName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return true;
                }
                catch (Exception ex)
                {
                    return ConfirmContinueWithoutExport("导出失败：\n" + ex.Message);
                }
            }
        }

        private static bool ConfirmContinueWithoutExport(string reason)
        {
            DialogResult result = MessageBox.Show(
                reason + "\n\n是否继续卸载并清除本地卡片数据？",
                AppName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            return result == DialogResult.Yes;
        }

        private static int ExportCardsToWord(string outputDir)
        {
            string statePath = Path.Combine(GetDataDir(), "linbo-native-state.json");
            if (!File.Exists(statePath)) return 0;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            ExportState state = serializer.Deserialize<ExportState>(File.ReadAllText(statePath, Encoding.UTF8));
            if (state == null || state.cards == null || state.cards.Count == 0) return 0;

            Directory.CreateDirectory(outputDir);
            Dictionary<string, int> usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int exported = 0;
            foreach (ExportCard card in state.cards.OrderBy(c => c.createdAt))
            {
                string content = card == null ? "" : (card.content ?? "");
                if (String.IsNullOrWhiteSpace(content)) continue;

                string baseName = SanitizeFileName(card.title);
                if (String.IsNullOrWhiteSpace(baseName)) baseName = "未命名卡片";
                string fileName = UniqueFileName(baseName, usedNames) + ".docx";
                string path = Path.Combine(outputDir, fileName);
                CreateDocx(path, content);
                exported++;
            }
            return exported;
        }

        private static string SanitizeFileName(string title)
        {
            string name = String.IsNullOrWhiteSpace(title) ? "" : Regex.Replace(title.Trim(), @"\s+", " ");
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            if (name.Length > 80) name = name.Substring(0, 80).Trim();
            return name;
        }

        private static string UniqueFileName(string baseName, Dictionary<string, int> usedNames)
        {
            int count;
            if (!usedNames.TryGetValue(baseName, out count))
            {
                usedNames[baseName] = 1;
                return baseName;
            }
            count++;
            usedNames[baseName] = count;
            return baseName + " " + count;
        }

        private static void CreateDocx(string path, string content)
        {
            if (File.Exists(path)) File.Delete(path);
            using (FileStream file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (ZipArchive archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                    "</Types>");
                WriteEntry(archive, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
                    "</Relationships>");
                WriteEntry(archive, "word/document.xml", BuildDocumentXml(content));
            }
        }

        private static string BuildDocumentXml(string content)
        {
            StringBuilder body = new StringBuilder();
            string normalized = (content ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
            string[] lines = normalized.Split('\n');
            foreach (string line in lines)
            {
                body.Append("<w:p>");
                if (!String.IsNullOrEmpty(line))
                {
                    body.Append("<w:r><w:t xml:space=\"preserve\">");
                    body.Append(EscapeXml(line));
                    body.Append("</w:t></w:r>");
                }
                body.Append("</w:p>");
            }

            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
                "<w:body>" +
                body +
                "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/><w:pgMar w:top=\"1440\" w:right=\"1440\" w:bottom=\"1440\" w:left=\"1440\"/></w:sectPr>" +
                "</w:body></w:document>";
        }

        private static string EscapeXml(string value)
        {
            return (value ?? "")
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using (StreamWriter writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
            {
                writer.Write(content);
            }
        }

        private static void TryKillRunningApp()
        {
            foreach (Process process in Process.GetProcessesByName("泠波"))
            {
                try { process.Kill(); }
                catch { }
            }
            foreach (Process process in Process.GetProcessesByName("Linbo"))
            {
                try { process.Kill(); }
                catch { }
            }
        }

        private static void RemoveShortcuts()
        {
            TryDeleteFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"));
            string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppName);
            try
            {
                if (Directory.Exists(startMenuDir)) Directory.Delete(startMenuDir, true);
            }
            catch { }
        }

        private static void RemoveRegistry()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(RegistryPath); }
            catch { }
        }

        private static void RemoveLocalData()
        {
            string dataDir = GetDataDir();
            try
            {
                if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
            }
            catch { }
        }

        private static string GetDataDir()
        {
            string dataOverride = Environment.GetEnvironmentVariable("LINBO_DATA_DIR");
            return String.IsNullOrWhiteSpace(dataOverride)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Linbo")
                : dataOverride;
        }

        private static void ScheduleDirectoryRemoval(string installDir)
        {
            string script = Path.Combine(Path.GetTempPath(), "linbo-uninstall-" + Guid.NewGuid().ToString("N") + ".cmd");
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("@echo off");
            builder.AppendLine("setlocal");
            builder.AppendLine("set TARGET=" + installDir);
            builder.AppendLine("set COUNT=0");
            builder.AppendLine(":retry");
            builder.AppendLine("rmdir /s /q \"%TARGET%\" >nul 2>nul");
            builder.AppendLine("if exist \"%TARGET%\" (");
            builder.AppendLine("  set /a COUNT+=1");
            builder.AppendLine("  if %COUNT% GEQ 20 goto done");
            builder.AppendLine("  timeout /t 1 /nobreak >nul");
            builder.AppendLine("  goto retry");
            builder.AppendLine(")");
            builder.AppendLine(":done");
            builder.AppendLine("del \"%~f0\" >nul 2>nul");
            File.WriteAllText(script, builder.ToString(), Encoding.ASCII);
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"");
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            Process.Start(info);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}
