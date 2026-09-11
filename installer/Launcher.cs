// Install.exe: opens the installer window without any console.
// Built by tools/pack.ps1 with the C# compiler that ships in every Windows
// (.NET Framework 4), so nothing extra is needed to produce the release.
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

static class Launcher
{
    [STAThread]
    static void Main()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string script = Path.Combine(dir, Path.Combine("installer", "Installer.ps1"));
        if (!File.Exists(script))
        {
            MessageBox.Show("The file installer/Installer.ps1 was not found next to Install.exe." + Environment.NewLine + "Extract the whole zip first.",
                "Drag'n Wash Localization", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File \"" + script + "\"",
            WorkingDirectory = dir,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        Process.Start(psi);
    }
}
