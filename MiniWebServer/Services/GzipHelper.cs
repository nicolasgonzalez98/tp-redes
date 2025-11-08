using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace MiniWebServer.Services
{
    public class GzipHelper
    {
        // PUNTO 8 — compresión gzip
        public async Task<byte[]> CompressAsync(byte[] data)
        {
            using var ms = new MemoryStream();
            using (var gzip = new GZipStream(ms, CompressionMode.Compress, true))
            {
                await gzip.WriteAsync(data, 0, data.Length);
            }
            return ms.ToArray();
        }
    }
}
