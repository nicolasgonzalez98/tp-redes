using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Cargar configuración
        var cfgPath = "config.json";
        if (args.Length > 0) cfgPath = args[0];
        if (!File.Exists(cfgPath))
        {
            Console.WriteLine($"No existe {cfgPath}. Crea el archivo de configuración antes de ejecutar.");
            return;
        }

        var cfgJson = await File.ReadAllTextAsync(cfgPath);
        var cfg = System.Text.Json.JsonSerializer.Deserialize<Config>(cfgJson);

        if (cfg == null)
        {
            Console.WriteLine("Error al leer la configuración.");
            return;
        }

        Directory.CreateDirectory(cfg.RootDirectory); // asegura existencia

        var server = new WebServer(cfg.Port, cfg.RootDirectory);
        Console.CancelKeyPress += (s, e) =>
        {
            Console.WriteLine("Cancel requested - exiting...");
            Environment.Exit(0);
        };

        await server.StartAsync();
    }

    public class Config
    {
        public int Port { get; set; } = 8080;
        public string RootDirectory { get; set; } = "wwwroot";
    }
}

