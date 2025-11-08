using MiniWebServer.Models;
using MiniWebServer.Services;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiniWebServer
{
    public class WebServer
    {
        private readonly int _port;
        private readonly string _root;

        private readonly HttpRequestParser _parser;
        private readonly StaticFileService _staticFiles;
        private readonly RequestLogger _logger;
        private readonly GzipHelper _gzip;

        public WebServer()
        {
            // PUNTO 3 y PUNTO 4 — cargar config externa
            var configText = File.ReadAllText("Config/config.json");
            var config = JsonSerializer.Deserialize<ServerConfig>(configText);

            _port = config.port;     // PUNTO 4 — puerto configurable
            _root = config.root;     // PUNTO 3 — carpeta raíz configurable

            _parser = new HttpRequestParser();
            _staticFiles = new StaticFileService(_root);
            _logger = new RequestLogger();
            _gzip = new GzipHelper();
        }

        public async Task StartAsync()
        {
            // PUNTO 10 — trabajar directamente sobre sockets
            var listener = new TcpListener(IPAddress.Any, _port);
            listener.Start();

            Console.WriteLine($"Servidor iniciado en puerto {_port}");

            // PUNTO 1 — atender solicitudes indefinidas y concurrentes
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();

                // PUNTO 1 — manejar cada cliente de forma asíncrona
                _ = HandleClientAsync(client);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // PUNTO 10 — parseo manual de HTTP
            var request = await _parser.ParseAsync(reader);
            if (request == null) return;

            // Obtener IP del cliente (PUNTO 9)
            var remote = client.Client.RemoteEndPoint as IPEndPoint;
            string clientIp = remote?.Address.ToString() ?? "unknown";

            if (request.Method != "POST")
            {
                Console.WriteLine($"Solicitud: {request.Method} {request.Url} desde {clientIp}");
            }
            
            // Si el cliente solicita gzip, mostrar y loggear esa intención
            if (request.AcceptGzip)
            {
                Console.WriteLine("[Accept-Encoding: gzip]");
            }

            // PUNTO 6 — POST
            if (request.Method == "POST")
            {
                if (!string.IsNullOrEmpty(request.Body))
                {
                    Console.WriteLine("Body POST -> " + request.Body);
                }

                // Preparar texto extra para el logger: cuerpo + flag gzip si aplica
                string postExtra = request.Body;
                if (request.AcceptGzip)
                {
                    postExtra = string.IsNullOrEmpty(postExtra)
                        ? "[Accept-Encoding: gzip]"
                        : postExtra + " | [Accept-Encoding: gzip]";
                }

                // PUNTO 9 — log de POST
                await _logger.LogAsync(clientIp, "POST", request.Url, request.Body);

                // Respuesta simple
                await SendResponseAsync(writer, "200 OK", "text/plain", "POST recibido correctamente");
                return;
            }

            // PUNTO 6 — GET
            if (request.Method == "GET")
            {
                // PUNTO 2 — servir index.html si no especifican archivo
                string resolvedPath = _staticFiles.ResolvePath(request.Url);

                // PUNTO 9 — Log básico de la petición GET, incluir flag gzip si aplica
                string logExtra = null;
                if (request.AcceptGzip)
                {
                    logExtra = "[Accept-Encoding: gzip]";
                }

                // PUNTO 7 — manejo de parámetros en la URL
                string urlWithoutQuery = request.Url.Split('?')[0];
                string query = request.Url.Contains("?")
                    ? request.Url.Split('?')[1]
                    : null;

                if (query != null)
                {
                    logExtra = logExtra == null
                    ? $"query: {query}"
                    : $"{logExtra} | query: {query}";
                }

                // PUNTO 9 — log final (UN SOLO log)
                await _logger.LogAsync(clientIp, "GET", request.Url, logExtra);

                if (File.Exists(resolvedPath))
                {
                    // Cargar archivo
                    var contentType = _staticFiles.GetContentType(resolvedPath);
                    var bytes = await _staticFiles.LoadFileBytesAsync(resolvedPath);

                    // PUNTO 8 — compresión gzip
                    if (request.AcceptGzip)
                    {
                        var compressed = await _gzip.CompressAsync(bytes);

                        await writer.WriteLineAsync("HTTP/1.1 200 OK");
                        await writer.WriteLineAsync($"Content-Type: {contentType}");
                        await writer.WriteLineAsync("Content-Encoding: gzip");
                        await writer.WriteLineAsync($"Content-Length: {compressed.Length}");
                        await writer.WriteLineAsync("Connection: close");
                        await writer.WriteLineAsync();
                        await stream.WriteAsync(compressed, 0, compressed.Length);
                    }
                    else
                    {
                        // Respuesta normal
                        string content = Encoding.UTF8.GetString(bytes);
                        await SendResponseAsync(writer, "200 OK", contentType, content);
                    }
                }
                else
                {
                    // PUNTO 5 — devolver archivo 404 personalizado
                    string notFound = _staticFiles.Get404Page();

                    string header =
                        "HTTP/1.1 404 Not Found\r\n" +
                        "Content-Type: text/html; charset=UTF-8\r\n" +
                        $"Content-Length: {Encoding.UTF8.GetByteCount(notFound)}\r\n" +
                        "\r\n";

                    await stream.WriteAsync(Encoding.UTF8.GetBytes(header + notFound));
                }
            }
            else
            {
                // PUNTO extra — métodos no permitidos
                await SendResponseAsync(writer, "405 Method Not Allowed", "text/plain", "Método no soportado");
            }
        }

        private async Task SendResponseAsync(StreamWriter writer, string status, string contentType, string content)
        {
            // Respuesta HTTP básica, usada en GET y POST
            await writer.WriteLineAsync($"HTTP/1.1 {status}");
            await writer.WriteLineAsync($"Content-Type: {contentType}");
            await writer.WriteLineAsync($"Content-Length: {Encoding.UTF8.GetByteCount(content)}");
            await writer.WriteLineAsync("Connection: close");
            await writer.WriteLineAsync();
            await writer.WriteAsync(content);
        }
    }
}
