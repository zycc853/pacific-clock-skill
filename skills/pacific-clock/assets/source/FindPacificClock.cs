using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class FindPacificClock
{
    [STAThread]
    private static void Main()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PacificClock.exe"), "--find") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("无法打开时钟：\n" + ex.Message, "太平洋时钟");
        }
    }
}
