using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class WebServer
{
    private readonly int _port;
    private readonly string _rootDir;
    private readonly TcpListener _listener;

    public WebServer(int port, string rootDir)
    {
        _port = port;
        _rootDir = rootDir;
        _listener = new TcpListener(IPAddress.Any, _port);
    }

    public async Task StartAsync()
    {
        _listener.Start();
        Console.WriteLine($"Servidor iniciado. Puerto: {_port}  Root: {_rootDir}");
        while (true)
        {
            // Aceptar conexiones concurrentemente
            TcpClient client = await _listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client); // no await -> concurrente
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        var remoteEnd = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                stream.ReadTimeout = 5000;
                stream.WriteTimeout = 5000;

                // Leer petición completa (headers + posible body)
                var request = await HttpRequest.ParseAsync(stream);

                // Log: IP de origen, método, path y query
                Logger.LogRequest(remoteEnd, request);

                // Manejo de parámetros (solo logging según consigna)
                if (request.QueryParameters.Count > 0)
                {
                    Logger.Log($"Query params: {string.Join(", ", request.QueryParameters)}");
                }

                if (request.Method == "POST")
                {
                    // Loguear body (según consigna)
                    Logger.Log($"POST body: {request.BodyAsString}");
                }

                // Resolver archivo
                string relativePath = request.Path.TrimStart('/');
                if (string.IsNullOrEmpty(relativePath) || relativePath.EndsWith("/"))
                {
                    relativePath = Path.Combine(relativePath, "index.html");
                }

                string filePath = Path.Combine(_rootDir, Uri.UnescapeDataString(relativePath.Replace('/', Path.DirectorySeparatorChar)));

                if (Directory.Exists(filePath))
                {
                    // si es directorio, servir index.html dentro
                    filePath = Path.Combine(filePath, "index.html");
                }

                if (!File.Exists(filePath))
                {
                    // 404
                    string notFoundFile = Path.Combine(_rootDir, "404.html");
                    if (File.Exists(notFoundFile))
                    {
                        await SendFileResponse(stream, notFoundFile, 404, request);
                    }
                    else
                    {
                        await SendTextResponse(stream, "<h1>404 Not Found</h1>", "text/html; charset=utf-8", 404, request);
                    }
                }
                else
                {
                    await SendFileResponse(stream, filePath, 200, request);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error manejando cliente {remoteEnd}: {ex}");
        }
    }

    private async Task SendTextResponse(NetworkStream stream, string content, string contentType, int statusCode, HttpRequest request)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        bool compress = ClientAcceptsGzip(request);
        if (compress)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionMode.Compress, true))
            {
                await gz.WriteAsync(bytes, 0, bytes.Length);
            }
            var comp = ms.ToArray();
            var header = BuildHeader(statusCode, contentType, comp.Length, "gzip");
            await stream.WriteAsync(header, 0, header.Length);
            await stream.WriteAsync(comp, 0, comp.Length);
        }
        else
        {
            var header = BuildHeader(statusCode, contentType, bytes.Length, null);
            await stream.WriteAsync(header, 0, header.Length);
            await stream.WriteAsync(bytes, 0, bytes.Length);
        }
    }

    private async Task SendFileResponse(NetworkStream stream, string filePath, int statusCode, HttpRequest request)
    {
        string contentType = MimeTypes.GetMimeType(Path.GetExtension(filePath));
        byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
        bool compress = ClientAcceptsGzip(request) && ShouldCompress(contentType);

        if (compress)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionMode.Compress, true))
            {
                await gz.WriteAsync(fileBytes, 0, fileBytes.Length);
            }
            var comp = ms.ToArray();
            var header = BuildHeader(statusCode, contentType, comp.Length, "gzip");
            await stream.WriteAsync(header, 0, header.Length);
            await stream.WriteAsync(comp, 0, comp.Length);
        }
        else
        {
            var header = BuildHeader(statusCode, contentType, fileBytes.Length, null);
            await stream.WriteAsync(header, 0, header.Length);
            await stream.WriteAsync(fileBytes, 0, fileBytes.Length);
        }
    }

    private static byte[] BuildHeader(int statusCode, string contentType, long contentLength, string contentEncoding)
    {
        string reason = statusCode == 200 ? "OK" : statusCode == 404 ? "Not Found" : "OK";
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {statusCode} {reason}\r\n");
        sb.Append($"Date: {DateTime.UtcNow:R}\r\n");
        sb.Append($"Server: MiniWebServer/1.0\r\n");
        sb.Append($"Content-Type: {contentType}\r\n");
        sb.Append($"Content-Length: {contentLength}\r\n");
        if (!string.IsNullOrEmpty(contentEncoding))
            sb.Append($"Content-Encoding: {contentEncoding}\r\n");
        sb.Append($"Connection: close\r\n");
        sb.Append($"\r\n");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static bool ClientAcceptsGzip(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Accept-Encoding", out var val))
        {
            return val.Contains("gzip");
        }
        return false;
    }

    private static bool ShouldCompress(string mime)
    {
        // No tiene sentido comprimir imágenes o ya comprimidos
        var notCompress = new[] { ".png", ".jpg", ".jpeg", ".gif", ".zip", ".gz", ".rar", ".mp4" };
        foreach (var ext in notCompress)
            if (mime.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return false;

        // comprimimos html, css, js, txt, json, xml, etc.
        return true;
    }
}
