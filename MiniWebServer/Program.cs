using System.Threading.Tasks;

namespace MiniWebServer
{
    class Program
    {
        static async Task Main()
        {
            var server = new WebServer();
            await server.StartAsync();
        }
    }
}

