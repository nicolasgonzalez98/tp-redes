using System;
using System.IO;
using System.Threading.Tasks;

namespace MiniWebServer.Services
{
    public class RequestLogger
    {
        private readonly string logsDir = "logs";

        public RequestLogger()
        {
            Directory.CreateDirectory(logsDir);
        }

        // PUNTO 9 — loguear solicitudes por día e incluir la IP
        public async Task LogAsync(string ip, string method, string url, string extra = null)
        {
            string filePath = Path.Combine(logsDir, $"{DateTime.Now:yyyy-MM-dd}.log");
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ip} {method} {url}";

            if (!string.IsNullOrEmpty(extra))
                entry += $" -> {extra}";

            entry += Environment.NewLine;

            await File.AppendAllTextAsync(filePath, entry);
        }
    }
}
