// ============================================================
// ayugram-telegram-alias (C# / .NET Framework, no dependencies)
// ------------------------------------------------------------
// Launches portable AyuGram under the name Telegram.exe so that
// any process-based router treats it as stock Telegram.
// Do NOT disable auto-update: the link is recreated on every
// launch + a watchdog restores the name after an Updater restart.
//
// Build WITHOUT installing anything: uses the C# compiler built
// into Windows (csc.exe from .NET Framework).
// Just run Build.bat — ayugram-telegram-alias.exe will appear.
// ============================================================

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Launcher
{
    private const string AppName = "ayugram-telegram-alias";

    private static string launcherDir;
    private static string iniPath;
    private static string srcExe;
    private static string appDir;
    private static string dstExe;
    private static NotifyIcon tray;
    private static bool updaterBalloonShown;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint dwVolumeSerialNumber;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint nNumberOfLinks;
        public uint nFileIndexHigh;
        public uint nFileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(IntPtr hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_ALL = 7;
    private const uint OPEN_EXISTING = 3;

    // Is it the same physical file (hard link)? Compared by volume FileId.
    private static bool SameFileId(string a, string b)
    {
        IntPtr ha = IntPtr.Zero;
        IntPtr hb = IntPtr.Zero;
        try
        {
            ha = CreateFile(a, GENERIC_READ, FILE_SHARE_ALL, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            hb = CreateFile(b, GENERIC_READ, FILE_SHARE_ALL, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (ha == IntPtr.Zero || ha == new IntPtr(-1))
                return false;
            if (hb == IntPtr.Zero || hb == new IntPtr(-1))
                return false;
            BY_HANDLE_FILE_INFORMATION ia;
            BY_HANDLE_FILE_INFORMATION ib;
            if (!GetFileInformationByHandle(ha, out ia))
                return false;
            if (!GetFileInformationByHandle(hb, out ib))
                return false;
            return ia.dwVolumeSerialNumber == ib.dwVolumeSerialNumber
                && ia.nFileIndexHigh == ib.nFileIndexHigh
                && ia.nFileIndexLow == ib.nFileIndexLow;
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                if (ha != IntPtr.Zero && ha != new IntPtr(-1))
                    CloseHandle(ha);
                if (hb != IntPtr.Zero && hb != new IntPtr(-1))
                    CloseHandle(hb);
            }
            catch
            {
            }
        }
    }

    [STAThread]
    private static void Main()
    {
        bool createdNew;
        using (Mutex mutex = new Mutex(true, "ayugram-telegram-alias", out createdNew))
        {
            if (!createdNew)
            {
                MessageBox.Show("The launcher is already running — check the tray icon.", AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            launcherDir = AppDomain.CurrentDomain.BaseDirectory;
            iniPath = Path.Combine(launcherDir, "ayugram-telegram-alias.ini");

            string found = ResolveAyuGramPath();
            if (found == null)
                return; // user cancelled the selection

            srcExe = found;
            appDir = Path.GetDirectoryName(srcExe);
            dstExe = Path.Combine(appDir, "Telegram.exe");

            SetupTray();

            WaitForUpdater();
            KillScoped();
            Thread.Sleep(1500);

            if (!EnsureTelegramLink())
            {
                MessageBox.Show("Could not create Telegram.exe in folder:\n" + appDir +
                    "\n\nCheck write permissions and free disk space.",
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            StartTelegram();
            ShowBalloon("AyuGram started as Telegram.exe");

            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 6000;
            timer.Tick += new EventHandler(OnWatchTick);
            timer.Start();

            Application.Run();
            tray.Visible = false;
        }
    }

    // ---------- locating AyuGram.exe ----------

    private static string ResolveAyuGramPath()
    {
        string saved = ReadIniPath();
        if (saved != null && File.Exists(saved))
            return saved;

        string own = Path.Combine(launcherDir, "AyuGram.exe");
        if (File.Exists(own))
        {
            SaveIniPath(own);
            return own;
        }

        string home = Environment.GetEnvironmentVariable("USERPROFILE");
        if (home != null)
        {
            string[] rel = new string[] {
                "Downloads\\AyuGram\\AyuGram.exe",
                "Desktop\\AyuGram\\AyuGram.exe",
                "Documents\\AyuGram\\AyuGram.exe"
            };
            foreach (string r in rel)
            {
                string cand = Path.Combine(home, r);
                if (File.Exists(cand))
                {
                    SaveIniPath(cand);
                    return cand;
                }
            }
        }

        MessageBox.Show("Could not find AyuGram.exe automatically.\n\n" +
            "A file dialog will open — please select AyuGram.exe once. The path will be remembered.",
            AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);

        while (true)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Select AyuGram.exe (once, the path will be remembered)";
                dlg.Filter = "AyuGram.exe|AyuGram.exe|Programs (*.exe)|*.exe";
                dlg.FilterIndex = 1;
                dlg.FileName = "AyuGram.exe";
                dlg.CheckFileExists = true;
                if (dlg.ShowDialog() != DialogResult.OK)
                    return null;

                string sel = dlg.FileName;
                if (!Path.GetFileName(sel).Equals("AyuGram.exe", StringComparison.OrdinalIgnoreCase))
                {
                    DialogResult ans = MessageBox.Show("You selected:\n" + sel +
                        "\n\nThis is not AyuGram.exe. Pick another file?",
                        AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (ans == DialogResult.Yes)
                        continue;
                    return null;
                }

                SaveIniPath(sel);
                return sel;
            }
        }
    }

    private static string ReadIniPath()
    {
        try
        {
            if (!File.Exists(iniPath))
                return null;
            foreach (string raw in File.ReadAllLines(iniPath))
            {
                string line = raw.Trim();
                if (line.StartsWith("AyuGramPath=", StringComparison.OrdinalIgnoreCase))
                    return line.Substring("AyuGramPath=".Length).Trim();
            }
        }
        catch
        {
        }
        return null;
    }

    private static void SaveIniPath(string path)
    {
        try
        {
            // Don't touch the file if the path is already stored — a redundant
            // write nudges Explorer (especially if the launcher sits on the Desktop).
            if (string.Equals(ReadIniPath(), path, StringComparison.OrdinalIgnoreCase))
                return;
            File.WriteAllText(iniPath, "[Settings]\r\nAyuGramPath=" + path + "\r\n");
        }
        catch
        {
        }
    }

    // ---------- processes (AyuGram folder only) ----------

    private static bool AnyScoped(string shortName)
    {
        string prefix = appDir + Path.DirectorySeparatorChar;
        Process[] list;
        try
        {
            list = Process.GetProcessesByName(shortName);
        }
        catch
        {
            return false;
        }
        foreach (Process p in list)
        {
            try
            {
                string f = null;
                try
                {
                    f = p.MainModule.FileName;
                }
                catch
                {
                }
                if (f != null && f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            finally
            {
                try
                {
                    p.Dispose();
                }
                catch
                {
                }
            }
        }
        return false;
    }

    private static void KillScoped()
    {
        string prefix = appDir + Path.DirectorySeparatorChar;
        string[] names = new string[] { "AyuGram", "Telegram" };
        foreach (string name in names)
        {
            Process[] list;
            try
            {
                list = Process.GetProcessesByName(name);
            }
            catch
            {
                continue;
            }
            foreach (Process p in list)
            {
                try
                {
                    string f = null;
                    try
                    {
                        f = p.MainModule.FileName;
                    }
                    catch
                    {
                    }
                    if (f != null && f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            p.Kill();
                        }
                        catch
                        {
                        }
                    }
                }
                finally
                {
                    try
                    {
                        p.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
        }
    }

    private static void WaitForUpdater()
    {
        int waited = 0;
        while (AnyScoped("Updater") && waited < 120)
        {
            if (!updaterBalloonShown)
            {
                ShowBalloon("Waiting for the AyuGram update to finish…");
                updaterBalloonShown = true;
            }
            Thread.Sleep(3000);
            waited += 3;
        }
    }

    // ---------- Telegram.exe -> AyuGram.exe link ----------

    // Does the existing link already point at the current AyuGram.exe?
    // If so, don't touch anything: needlessly deleting/recreating a 239 MB
    // file pokes Explorer and forces a desktop icon repaint.
    private static bool IsLinkFresh()
    {
        try
        {
            if (!File.Exists(srcExe) || !File.Exists(dstExe))
                return false;

            FileInfo si = new FileInfo(srcExe);
            FileInfo di = new FileInfo(dstExe);

            // Size or timestamp differ — definitely stale.
            if (si.Length != di.Length)
                return false;
            if (si.LastWriteTimeUtc != di.LastWriteTimeUtc)
                return false;

            // They match — verify it's a hard link to the same file.
            // One-liner: the FileIds of both paths must match.
            if (SameFileId(srcExe, dstExe))
                return true;

            // Same size/time but different files (an old copy).
            // Rare case — recreate it, a one-off operation.
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool EnsureTelegramLink()
    {
        // The link is already fresh — don't touch the file at all.
        if (IsLinkFresh())
            return true;

        try
        {
            if (File.Exists(dstExe))
                File.Delete(dstExe);
        }
        catch
        {
        }
        if (File.Exists(dstExe))
        {
            Thread.Sleep(2000);
            try
            {
                File.Delete(dstExe);
            }
            catch
            {
            }
        }
        if (File.Exists(dstExe))
            return false;

        try
        {
            if (CreateHardLink(dstExe, srcExe, IntPtr.Zero) && File.Exists(dstExe))
                return true;
        }
        catch
        {
        }

        // Fallback — a full copy
        try
        {
            File.Copy(srcExe, dstExe, true);
        }
        catch
        {
            return false;
        }
        return File.Exists(dstExe);
    }

    private static void StartTelegram()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = dstExe;
            psi.WorkingDirectory = appDir;
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not start Telegram.exe:\n" + ex.Message,
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }
    }

    // ---------- watchdog ----------

    private static void OnWatchTick(object sender, EventArgs e)
    {
        bool ayu = AnyScoped("AyuGram");
        bool tg = AnyScoped("Telegram");
        if (ayu && !tg)
        {
            ShowBalloon("Update finished — switching back to Telegram.exe");
            KillScoped();
            Thread.Sleep(1500);
            if (EnsureTelegramLink())
                StartTelegram();
        }
        else if (!ayu && !tg)
        {
            Application.Exit();
        }
    }

    // ---------- tray ----------

    private static void SetupTray()
    {
        tray = new NotifyIcon();
        try
        {
            Icon ic = Icon.ExtractAssociatedIcon(srcExe);
            tray.Icon = (ic != null) ? ic : SystemIcons.Application;
        }
        catch
        {
            tray.Icon = SystemIcons.Application;
        }
        tray.Text = AppName;
        tray.Visible = true;

        ContextMenuStrip menu = new ContextMenuStrip();
        ToolStripMenuItem open = new ToolStripMenuItem("Open AyuGram folder");
        open.Click += new EventHandler(OnOpenFolder);
        menu.Items.Add(open);
        ToolStripMenuItem quit = new ToolStripMenuItem("Quit launcher (AyuGram keeps running)");
        quit.Click += new EventHandler(OnQuitLauncher);
        menu.Items.Add(quit);
        tray.ContextMenuStrip = menu;
    }

    private static void ShowBalloon(string text)
    {
        try
        {
            tray.BalloonTipTitle = AppName;
            tray.BalloonTipText = text;
            tray.ShowBalloonTip(3000);
        }
        catch
        {
        }
    }

    private static void OnOpenFolder(object sender, EventArgs e)
    {
        try
        {
            Process.Start(appDir);
        }
        catch
        {
        }
    }

    private static void OnQuitLauncher(object sender, EventArgs e)
    {
        Application.Exit();
    }
}
