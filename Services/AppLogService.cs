using System.Text;

namespace GlassBilling.Services;

public sealed class AppLogService
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _logDirectory = Path.Combine(FileSystem.AppDataDirectory, "Logs");

    public string LogDirectory => _logDirectory;

    public Task InfoAsync(string message) => WriteAsync("INFO", message);

    public Task ErrorAsync(string message, Exception exception) =>
        WriteAsync("ERROR", $"{message}{Environment.NewLine}{exception}");

    private async Task WriteAsync(string level, string message)
    {
        try
        {
            await _writeLock.WaitAsync();
            Directory.CreateDirectory(_logDirectory);
            DeleteOldLogs();

            var filePath = Path.Combine(_logDirectory, $"glassbilling-{DateTime.Now:yyyyMMdd}.log");
            var entry = new StringBuilder()
                .Append('[').Append(DateTimeOffset.Now.ToString("O")).Append("] ")
                .Append(level).Append(" | ")
                .Append("Version=").Append(GetVersion())
                .Append(" | Platform=").Append(DeviceInfo.Platform)
                .Append(" | ").AppendLine(message)
                .ToString();

            await File.AppendAllTextAsync(filePath, entry);
        }
        catch
        {
        }
        finally
        {
            if (_writeLock.CurrentCount == 0)
                _writeLock.Release();
        }
    }

    private void DeleteOldLogs()
    {
        foreach (var file in Directory.EnumerateFiles(_logDirectory, "glassbilling-*.log"))
        {
            if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-14))
                File.Delete(file);
        }
    }

    private static string GetVersion()
    {
        try
        {
            return AppInfo.Current.VersionString;
        }
        catch
        {
            return "unknown";
        }
    }
}
