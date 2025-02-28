using Microsoft.Extensions.Logging;

namespace %%TARGET_NAMESPACE%%.Support;

internal static class SupportLogger
{
    private static ILogger _logger;
    public static void SetLogger(ILogger logger)
    {
        _logger = logger;
    }
    public static void Log(LogLevel level, string fmt, params object[] args)
    {
        _logger.Log(level, fmt, args);
    }
    public static void Log(LogLevel level, string message)
    {
        _logger.Log(level, "{x}", message);
    }
    public static void Info(string message)
    {
        _logger.Log(LogLevel.Information, "{x}", message);
    }
    public static void Trace(string message)
    {
        _logger.Log(LogLevel.Trace, "{x}", message);
    }
}
