using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

public class HttpRequest
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public string HttpVersion { get; set; } = "HTTP/1.1";
    public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> QueryParameters { get; set; } = new Dictionary<string, string>();
    public byte[] Body { get; set; } = Array.Empty<byte>();

    public string BodyAsString => Body == null ? "" : Encoding.UTF8.GetString(Body);

    public static async Task<HttpRequest> ParseAsync(Stream stream)
    {
        var req = new HttpRequest();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        // Leer request line
        string requestLine = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(requestLine)) return req;

        var parts = requestLine.Split(' ', 3);
        req.Method = parts[0];
        var url = parts[1];
        req.HttpVersion = parts.Length > 2 ? parts[2] : "HTTP/1.1";

        // Separar path y query
        var qidx = url.IndexOf('?');
        if (qidx >= 0)
        {
            req.Path = url.Substring(0, qidx);
            var qstring = url.Substring(qidx + 1);
            var qpairs = qstring.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in qpairs)
            {
                var kv = p.Split('=', 2);
                var k = Uri.UnescapeDataString(kv[0]);
                var v = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
                req.QueryParameters[k] = v;
            }
        }
        else
        {
            req.Path = url;
        }

        // Leer headers
        string line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                var name = line.Substring(0, colon).Trim();
                var value = line.Substring(colon + 1).Trim();
                req.Headers[name] = value;
            }
        }

        // Si hay body (Content-Length), leerlo
        if (req.Headers.TryGetValue("Content-Length", out var lenStr) && int.TryParse(lenStr, out var len) && len > 0)
        {
            req.Body = new byte[len];
            int read = 0;
            while (read < len)
            {
                int r = await stream.ReadAsync(req.Body, read, len - read);
                if (r <= 0) break;
                read += r;
            }
        }
        else
        {
            req.Body = Array.Empty<byte>();
        }

        return req;
    }
}
