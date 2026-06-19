using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace %%TARGET_NAMESPACE%%.Support;

internal static class SupportLogger
{
    private static ILogger _logger = NullLogger.Instance;
    public static void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    [Conditional("DEBUG")]
    public static void Log(LogLevel level, string fmt, params object[] args)
    {
        _logger.Log(level, "[NativeImpl C#    ]: " + fmt, args);
    }

    [Conditional("DEBUG")]
    public static void Log(LogLevel level, string message)
    {
        _logger.Log(level, "[NativeImpl C#    ]: {message}", message);
    }

    [Conditional("DEBUG")]
    public static void Info(string message)
    {
        _logger.Log(LogLevel.Information, "[NativeImpl C#    ]: {message}", message);
    }

    [Conditional("DEBUG")]
    public static void Trace(string message)
    {
        _logger.Log(LogLevel.Trace, "[NativeImpl C#    ]: {message}", message);
    }
}
