using System;
using System.Collections.Generic;

public static class MimeTypes
{
    private static readonly Dictionary<string, string> _m = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { ".html", "text/html; charset=utf-8" },
        { ".htm", "text/html; charset=utf-8" },
        { ".css", "text/css; charset=utf-8" },
        { ".js", "application/javascript; charset=utf-8" },
        { ".json", "application/json" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".svg", "image/svg+xml" },
        { ".txt", "text/plain; charset=utf-8" },
        { ".pdf", "application/pdf" },
        { ".zip", "application/zip" }
    };

    public static string GetMimeType(string ext)
    {
        if (string.IsNullOrEmpty(ext)) return "application/octet-stream";
        if (!_m.TryGetValue(ext, out var v)) return "application/octet-stream";
        return v;
    }
}
