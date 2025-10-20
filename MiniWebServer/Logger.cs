using System;
using System.IO;
using System.Text;

public static class Logger
{
    private static readonly object _lock = new object();

    public static void Log(string message)
    {
        try
        {
            var logDir = "logs";
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, DateTime.UtcNow.ToString("yyyy-MM-dd") + ".log");
            var line = $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}";
            lock (_lock)
            {
                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }
        catch { /* no tirar excepciones por logging */ }
    }

    public static void LogRequest(string remoteEnd, HttpRequest req)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"{remoteEnd} - {req.Method} {req.Path}");
        if (req.QueryParameters.Count > 0)
        {
            sb.Append(" ?");
            foreach (var kv in req.QueryParameters)
            {
                sb.Append($"{kv.Key}={kv.Value}&");
            }
            sb.Length--; // quitar último &
        }
        sb.Append($" - Headers: {string.Join(", ", req.Headers.Keys)}");
        Log(sb.ToString());
    }
}
