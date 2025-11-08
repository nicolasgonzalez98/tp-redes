using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MiniWebServer.Services
{
    public class HttpRequestData
    {
        public string Method { get; set; }
        public string Url { get; set; }
        public bool AcceptGzip { get; set; }  // PUNTO 8
        public int ContentLength { get; set; } // PUNTO 6
        public string Body { get; set; } // POST body (PUNTO 6)
    }

    public class HttpRequestParser
    {
        // PUNTO 10
        // El servidor DEBE parsear manualmente la solicitud HTTP
        public async Task<HttpRequestData> ParseAsync(StreamReader reader)
        {
            var requestData = new HttpRequestData();

            // PUNTO 10 — lectura de request line
            var requestLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(requestLine)) return null;

            var parts = requestLine.Split(' ');
            requestData.Method = parts[0];  // GET / POST
            requestData.Url = parts[1];

            string line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
            {
                // PUNTO 6 — Content-Length
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line.Split(':')[1].Trim(), out var len))
                        requestData.ContentLength = len;
                }

                // PUNTO 8 — detectar gzip
                if (line.StartsWith("Accept-Encoding:", StringComparison.OrdinalIgnoreCase))
                {
                    if (line.Contains("gzip", StringComparison.OrdinalIgnoreCase))
                        requestData.AcceptGzip = true;
                }
            }

            // PUNTO 6 — leer cuerpo POST
            if (requestData.Method == "POST" && requestData.ContentLength > 0)
            {
                var buffer = new char[requestData.ContentLength];
                await reader.ReadBlockAsync(buffer, 0, requestData.ContentLength);
                requestData.Body = new string(buffer);
            }

            return requestData;
        }
    }
}
