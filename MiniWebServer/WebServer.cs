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

        public WebServer()
        {
            // 1️⃣ Leer archivo de configuración JSON
            var configText = File.ReadAllText("config.json");

            var config = JsonSerializer.Deserialize<ServerConfig>(configText);

            _port = config.port;
            _root = config.root;
        }

        public async Task StartAsync()
        {
            // 2️⃣ Creamos el socket TCP para escuchar conexiones
            var listener = new TcpListener(IPAddress.Any, _port);
            listener.Start();

            Console.WriteLine($"Servidor iniciado en puerto {_port}. Root: {_root}");

            // 3️⃣ Bucle infinito para aceptar clientes concurrentemente
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();

                // 4️⃣ Cada cliente se maneja en un hilo asíncrono aparte
                _ = HandleClientAsync(client);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // 5️⃣ Leer la primera línea de la solicitud HTTP (ej: GET /index.html HTTP/1.1)
            var requestLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(requestLine)) return;

            Console.WriteLine($"Solicitud recibida: {requestLine}");

            // 6️⃣ Parsear método y recurso
            var parts = requestLine.Split(' ');
            var method = parts[0];
            var url = parts[1];

            // 7️⃣ Si no especifica archivo, servir index.html
            if (url == "/") url = "/index.html";

            var filePath = Path.Combine(_root, url.TrimStart('/'));

            // 8️⃣ Verificamos si existe el archivo solicitado
            if (File.Exists(filePath))
            {
                var content = await File.ReadAllTextAsync(filePath);
                await SendResponseAsync(writer, "200 OK", GetContentType(filePath), content);
            }
            else
            //Punto 5, creacion de archivo 404.html
            {
                string notFoundPath = Path.Combine(_root, "404.html");
                string notFoundContent = File.Exists(notFoundPath)
                    ? File.ReadAllText(notFoundPath)
                    : "<h1>404 Not Found</h1>";

                string header = "HTTP/1.1 404 Not Found\r\n" +
                                "Content-Type: text/html; charset=UTF-8\r\n" +
                                $"Content-Length: {Encoding.UTF8.GetByteCount(notFoundContent)}\r\n" +
                                "\r\n";

                await stream.WriteAsync(Encoding.UTF8.GetBytes(header + notFoundContent));
                return;
            }
        }

        private async Task SendResponseAsync(StreamWriter writer, string status, string contentType, string content)
        {
            await writer.WriteLineAsync($"HTTP/1.1 {status}");
            await writer.WriteLineAsync($"Content-Type: {contentType}");
            await writer.WriteLineAsync($"Content-Length: {Encoding.UTF8.GetByteCount(content)}");
            await writer.WriteLineAsync("Connection: close");
            await writer.WriteLineAsync();
            await writer.WriteAsync(content);
        }

        private string GetContentType(string path)
        {
            return Path.GetExtension(path) switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".png" => "image/png",
                _ => "text/plain"
            };
        }

        private class ServerConfig
        {
            public int port { get; set; }
            public string root { get; set; }
        }
    }
}
