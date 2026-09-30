namespace ChroniclesLauncher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new LauncherForm(args.Length > 0 ? args[0] : null));
    }
}
