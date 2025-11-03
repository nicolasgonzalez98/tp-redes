using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO.Compression; 

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
            //Punto 10
            var listener = new TcpListener(IPAddress.Any, _port);
            listener.Start();

            Console.WriteLine($"Servidor iniciado en puerto {_port}. Root: {_root}");

            // 3️⃣ Bucle infinito para aceptar clientes concurrentemente
            while (true)
            {
                //Punto 10
                var client = await listener.AcceptTcpClientAsync();

                // 4️⃣ Cada cliente se maneja en un hilo asíncrono aparte
                _ = HandleClientAsync(client);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            //Punto 10
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // 5️⃣ Leer la primera línea de la solicitud HTTP 
            //Punto 10
            var requestLine = await reader.ReadLineAsync(); //Ej: "GET /index.html HTTP/1.1"
            if (string.IsNullOrEmpty(requestLine)) return; 

            Console.WriteLine($"Solicitud recibida: {requestLine}");

            // 6️⃣ Parsear método y recurso
            var parts = requestLine.Split(' ');
            var method = parts[0];
            var url = parts[1];

            //Punto 9
            var clientIp = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
            await LogRequestAsync(clientIp, method, url);
            //*******//

            bool acceptGzip = false;
            string line;
            int contentLength = 0;

            // 🧩 Leemos headers comunes para GET y POST
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
            {
                Console.WriteLine($"Header recibido: {line}");

                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line.Split(':')[1].Trim(), out int len))
                        contentLength = len;
                }
                else if (line.StartsWith("Accept-Encoding:", StringComparison.OrdinalIgnoreCase))
                {
                    if (line.Contains("gzip", StringComparison.OrdinalIgnoreCase))
                        acceptGzip = true;
                }
                
            }
            Console.WriteLine("👉 Fin de headers detectado");
            

            // ---------- POST ----------
            if (method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                if (contentLength > 0)
                {
                    var buffer = new char[contentLength];
                    await reader.ReadBlockAsync(buffer, 0, contentLength);
                    var postData = new string(buffer);

                    Console.WriteLine($"Datos POST recibidos: {postData}");
                    //Punto 9
                    await LogRequestAsync(clientIp, method, url, postData);
                    //********//
                    await SendResponseAsync(writer, "200 OK", "text/plain", "POST recibido correctamente");
                    return;
                }
                else
                {
                    Console.WriteLine("[POST] Sin cuerpo recibido");
                }
            }

            if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                if (url == "/") url = "/index.html";

                var urlParts = url.Split('?', 2);
                var path = urlParts[0];
                var query = urlParts.Length > 1 ? urlParts[1] : string.Empty;

                if (!string.IsNullOrEmpty(query))
                {
                    var parameters = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
                    Console.WriteLine("📘 Parámetros recibidos en la URL:");
                    foreach (var param in parameters)
                    {
                        var kv = param.Split('=', 2);
                        var key = WebUtility.UrlDecode(kv[0]);
                        var value = kv.Length > 1 ? WebUtility.UrlDecode(kv[1]) : "";
                        Console.WriteLine($"  → {key}: {value}");
                    }
                    //Punto 9
                    await LogRequestAsync(clientIp, method, url, query);
                }

                var filePath = Path.Combine(_root, path.TrimStart('/'));

                if (File.Exists(filePath))
                {
                    // 🧩 Si el cliente acepta gzip, comprimimos
                    byte[] fileBytes = await File.ReadAllBytesAsync(filePath);

                    if (acceptGzip)
                    {
                        using var compressedStream = new MemoryStream();
                        using (var gzip = new GZipStream(compressedStream, CompressionMode.Compress, true))
                        {
                            await gzip.WriteAsync(fileBytes, 0, fileBytes.Length);
                        }

                        byte[] compressedData = compressedStream.ToArray();

                        await writer.WriteLineAsync("HTTP/1.1 200 OK");
                        await writer.WriteLineAsync($"Content-Type: {GetContentType(filePath)}");
                        await writer.WriteLineAsync("Content-Encoding: gzip"); // 🧩 importante
                        await writer.WriteLineAsync($"Content-Length: {compressedData.Length}");
                        await writer.WriteLineAsync("Connection: close");
                        await writer.WriteLineAsync();
                        await stream.WriteAsync(compressedData, 0, compressedData.Length);
                    }
                    else
                    {
                        // Si no acepta gzip, envío normal
                        var content = Encoding.UTF8.GetString(fileBytes);
                        await SendResponseAsync(writer, "200 OK", GetContentType(filePath), content);
                    }
                }
                else
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
            else
            {
                await SendResponseAsync(writer, "405 Method Not Allowed", "text/plain", "Método no soportado");
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

        //Punto 9
        private async Task LogRequestAsync(string clientIp, string method, string url, string? extraData = null)
        {
            string logsDir = "logs";
            Directory.CreateDirectory(logsDir); // Crea la carpeta si no existe

            string filePath = Path.Combine(logsDir, $"{DateTime.Now:yyyy-MM-dd}.log");
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {clientIp} {method} {url}";

            if (!string.IsNullOrEmpty(extraData))
                logEntry += $" -> {extraData}";

            logEntry += Environment.NewLine;

            await File.AppendAllTextAsync(filePath, logEntry);
        }

        private class ServerConfig
        {
            public int port { get; set; }
            public string root { get; set; }
        }
    }
}
