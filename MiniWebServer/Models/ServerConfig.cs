using System;

namespace MiniWebServer.Models
{
    // PUNTO 3 y PUNTO 4
    // La configuración externa define:
    //  - root: carpeta desde donde servir archivos (PUNTO 3)
    //  - port: puerto de escucha configurable (PUNTO 4)
    public class ServerConfig
    {
        public int port { get; set; }   // PUNTO 4
        public string root { get; set; }   // PUNTO 3
    }
}
