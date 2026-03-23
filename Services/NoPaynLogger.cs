using System.Globalization;

namespace Nop.Plugin.Payments.NoPayn.Services;

public class NoPaynLogger
{
    private readonly NoPaynSettings _settings;
    private readonly string _logDirectory;
    private readonly object _lock = new();

    public NoPaynLogger(NoPaynSettings settings)
    {
        _settings = settings;
        _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Logs");
    }

    public void LogDebug(string message)
    {
        if (!_settings.DebugLogging)
            return;

        WriteLog("DEBUG", message);
    }

    public void LogInfo(string message)
    {
        if (!_settings.DebugLogging)
            return;

        WriteLog("INFO", message);
    }

    public void LogError(string message, Exception? ex = null)
    {
        if (!_settings.DebugLogging)
            return;

        var fullMessage = ex != null ? $"{message} | Exception: {ex.Message}\n{ex.StackTrace}" : message;
        WriteLog("ERROR", fullMessage);
    }

    public void LogApiRequest(string method, string endpoint, string? body = null)
    {
        if (!_settings.DebugLogging)
            return;

        var message = $"API Request: {method} {endpoint}";
        if (!string.IsNullOrEmpty(body))
            message += $"\nBody: {body}";

        WriteLog("API", message);
    }

    public void LogApiResponse(string method, string endpoint, int statusCode, string responseBody)
    {
        if (!_settings.DebugLogging)
            return;

        WriteLog("API", $"API Response: {method} {endpoint} -> HTTP {statusCode}\nResponse: {responseBody}");
    }

    public void LogWebhook(string eventDescription, string? body = null)
    {
        if (!_settings.DebugLogging)
            return;

        var message = $"Webhook: {eventDescription}";
        if (!string.IsNullOrEmpty(body))
            message += $"\nPayload: {body}";

        WriteLog("WEBHOOK", message);
    }

    private void WriteLog(string level, string message)
    {
        try
        {
            lock (_lock)
            {
                if (!Directory.Exists(_logDirectory))
                    Directory.CreateDirectory(_logDirectory);

                var logPath = Path.Combine(_logDirectory, "NoPayn_debug.log");
                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                var logEntry = $"[{timestamp}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(logPath, logEntry);
            }
        }
        catch
        {
            // Silently ignore logging failures to avoid disrupting payment flow
        }
    }
}
