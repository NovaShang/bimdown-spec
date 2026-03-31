using System.IO.Compression;
using System.Reflection;

namespace BimDown.Installer;

static class BundleInstaller
{
    public static readonly string AppPluginsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Autodesk", "ApplicationPlugins");

    public static readonly string BundleName = "BimDown.bundle";

    public static string TargetDir => Path.Combine(AppPluginsDir, BundleName);

    public static bool IsInstalled => Directory.Exists(TargetDir);

    public static int Install()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("bundle.zip");

            if (stream is null)
            {
                Console.Error.WriteLine("ERROR: Bundle data not found in installer.");
                Console.Error.WriteLine("Available resources:");
                foreach (var name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                    Console.Error.WriteLine($"  {name}");
                return 1;
            }

            if (Directory.Exists(TargetDir))
                Directory.Delete(TargetDir, recursive: true);

            Directory.CreateDirectory(AppPluginsDir);

            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var prefix = BundleName + "/";
            var prefixBackslash = BundleName + "\\";
            foreach (var entry in zip.Entries)
            {
                if (!entry.FullName.StartsWith(prefix) && !entry.FullName.StartsWith(prefixBackslash))
                    continue;

                var destPath = Path.Combine(AppPluginsDir,
                    entry.FullName.Replace('/', Path.DirectorySeparatorChar));

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destPath);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    entry.ExtractToFile(destPath, overwrite: true);
                }
            }

            Console.WriteLine($"Installed to: {TargetDir}");
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("ERROR: Access denied. Run as Administrator.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    public static int Uninstall()
    {
        if (!IsInstalled)
        {
            Console.WriteLine("Not installed.");
            return 0;
        }

        try
        {
            Directory.Delete(TargetDir, recursive: true);
            Console.WriteLine("Uninstalled.");
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("ERROR: Access denied. Run as Administrator.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    public static int Check()
    {
        Console.WriteLine($"Install location: {TargetDir}");
        Console.WriteLine($"Installed: {IsInstalled}");

        var names = Assembly.GetExecutingAssembly().GetManifestResourceNames();
        Console.WriteLine($"Embedded resources: {names.Length}");
        foreach (var name in names)
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            Console.WriteLine($"  {name} ({s?.Length ?? 0} bytes)");
        }

        if (names.Length == 0 || !names.Contains("bundle.zip"))
        {
            Console.Error.WriteLine("ERROR: bundle.zip not embedded!");
            return 1;
        }

        // Verify zip contents
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("bundle.zip")!;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        Console.WriteLine($"Bundle entries: {zip.Entries.Count}");
        foreach (var entry in zip.Entries)
            Console.WriteLine($"  {entry.FullName} ({entry.Length} bytes)");

        return 0;
    }
}
