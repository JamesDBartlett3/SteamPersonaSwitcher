using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace SteamPersonaSwitcher;

public partial class App : Application
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Allocate a console window for debug output
        if (GetConsoleWindow() == IntPtr.Zero)
        {
            AllocConsole();
            Console.WriteLine("===========================================");
            Console.WriteLine("Steam Persona Switcher - Debug Console");
            Console.WriteLine("===========================================");
            Console.WriteLine($"Started at: {DateTime.Now}");
            Console.WriteLine();
        }
    }
}
