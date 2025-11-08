using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MiniWebServer.Services
{
    public class StaticFileService
    {
        private readonly string _root;

        public StaticFileService(string root)
        {
            _root = root; // PUNTO 3
        }

        // PUNTO 2 — servir index.html por defecto
        public string ResolvePath(string url)
        {
            if (url == "/")
                return Path.Combine(_root, "index.html");

            return Path.Combine(_root, url.TrimStart('/'));
        }

        // PUNTO 5 — entrega archivo 404
        public string Get404Page()
        {
            var path = Path.Combine(_root, "404.html");
            return File.Exists(path)
                ? File.ReadAllText(path)
                : "<h1>404 Not Found</h1>";
        }

        // Cargar bytes para usar con GZIP (PUNTO 8)
        public async Task<byte[]> LoadFileBytesAsync(string path)
        {
            return await File.ReadAllBytesAsync(path);
        }

        public string GetContentType(string path)
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
    }
}
