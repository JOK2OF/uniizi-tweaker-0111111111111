namespace UniiziTweaker01.Infrastructure;

/// <summary>Minimal file logger (%LocalAppData%\UNIIZI\TWEAKER01\logs). Never throws.</summary>
public static class AppLog
{
    private static readonly object Gate = new();

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UNIIZI", "TWEAKER01", "logs");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex == null ? message : message + " | " + ex);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                string file = Path.Combine(LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, $"{DateTime.Now:O} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never crash the application.
        }
    }
}
