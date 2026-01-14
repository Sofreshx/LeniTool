using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace LeniTool.Desktop.Services;

internal static class CrashLogger
{
    private static readonly object Gate = new();
    private static string? _logFilePath;

    public static void Initialize()
    {
        // Idempotent.
        if (_logFilePath is not null)
            return;

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LeniTool",
            "logs");

        Directory.CreateDirectory(logDirectory);

        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        _logFilePath = Path.Combine(logDirectory, $"desktop-{stamp}.log");

        Trace.AutoFlush = true;
        Trace.Listeners.Add(new TextWriterTraceListener(_logFilePath));

        WriteLine("CrashLogger initialized");
        WriteLine($"Log file: {_logFilePath}");
    }

    public static void WriteLine(string message)
    {
        lock (Gate)
        {
            try
            {
                Trace.WriteLine($"[{DateTimeOffset.Now:O}] {message}");
            }
            catch
            {
                // Never throw from logging.
            }
        }
    }

    public static void WriteException(Exception ex, string context)
    {
        lock (Gate)
        {
            try
            {
                Trace.WriteLine($"[{DateTimeOffset.Now:O}] EXCEPTION: {context}");
                Trace.WriteLine(ex.ToString());
            }
            catch
            {
                // Never throw from logging.
            }
        }
    }
}
