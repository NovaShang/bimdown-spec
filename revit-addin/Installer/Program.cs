namespace BimDown.Installer;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0)
            return CliMode(args[0]);

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
        return 0;
    }

    static int CliMode(string command)
    {
        switch (command.ToLowerInvariant())
        {
            case "--install":
                return BundleInstaller.Install();
            case "--uninstall":
                return BundleInstaller.Uninstall();
            case "--check":
                return BundleInstaller.Check();
            default:
                Console.WriteLine("Usage: BimDownInstaller.exe [--install | --uninstall | --check]");
                Console.WriteLine("  No args: launch GUI");
                Console.WriteLine("  --install: install plugin silently");
                Console.WriteLine("  --uninstall: uninstall plugin silently");
                Console.WriteLine("  --check: verify embedded bundle and show install status");
                return 1;
        }
    }
}
