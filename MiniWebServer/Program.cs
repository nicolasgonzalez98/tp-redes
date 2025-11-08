using System;
using System.Threading.Tasks;

namespace MiniWebServer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Iniciando MiniWebServer...");

            var server = new WebServer();
            await server.StartAsync();
        }
    }
}
