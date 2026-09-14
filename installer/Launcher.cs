// Install.exe: opens the installer window without any console.
// Built by tools/pack.ps1 through Launcher.csproj, deterministically, so the
// file stays byte-identical across releases while this source is unchanged.
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

// What Windows shows under Properties → Details. The version is the launcher's own
// and changes only with this file, never with the mod's releases, so the build
// stays byte-identical from release to release (see Launcher.csproj).
[assembly: System.Reflection.AssemblyTitle("Drag'n Wash Localization Installer")]
[assembly: System.Reflection.AssemblyDescription("Opens the installer for Drag'n Wash Localization, an unofficial fan-made translation mod. Source: https://github.com/TomXV/dragnwash-localization")]
[assembly: System.Reflection.AssemblyCompany("TomXV")]
[assembly: System.Reflection.AssemblyProduct("Drag'n Wash Localization")]
[assembly: System.Reflection.AssemblyCopyright("Copyright (c) 2026 TomXV. MIT License.")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.0.0")]

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
