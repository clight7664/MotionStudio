using System.Diagnostics;

namespace MotionStudio.HostLauncher;

public static class Program
{
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string appRoot = ResolveAppRoot();
        string electronRealExe = Path.Combine(appRoot, "node_modules", "electron", "dist", "electron-real.exe");
        if (!File.Exists(electronRealExe))
        {
            Console.Error.WriteLine($"[MotionStudio HostLauncher] Missing Electron runtime at: {electronRealExe}");
            return 1;
        }

        ProcessStartInfo psi = new()
        {
            FileName = electronRealExe,
            WorkingDirectory = appRoot,
            UseShellExecute = false,
        };

        bool hasAppPathArg = args.Length > 0 &&
            !args[0].StartsWith('-') &&
            (Directory.Exists(args[0]) || File.Exists(args[0]));

        if (!hasAppPathArg)
        {
            psi.ArgumentList.Add(appRoot);
        }

        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using Process? proc = Process.Start(psi);
        if (proc is null)
        {
            Console.Error.WriteLine("[MotionStudio HostLauncher] Failed to start Electron runtime process.");
            return 1;
        }

        proc.WaitForExit();
        return proc.ExitCode;
    }

    private static string ResolveAppRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "electron-main.cjs");
            if (File.Exists(candidate))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        string cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "electron-main.cjs")))
        {
            return cwd;
        }

        return AppContext.BaseDirectory;
    }
}
