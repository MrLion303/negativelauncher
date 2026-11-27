using System;
using System.Windows;
using Negative_Client.Services;
using Velopack;

namespace Negative_Client
{
    public partial class App : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Proceso auxiliar: registra el cierre de Minecraft aunque el launcher ya esté cerrado.
            if (args.Length > 0 &&
                string.Equals(args[0], "--playtime-monitor", StringComparison.Ordinal))
            {
                PlaytimeService.RunMonitorAsync(args).GetAwaiter().GetResult();
                return;
            }

            // Velopack procesa aquí los comandos especiales de instalación y actualización.
            VelopackApp.Build().Run();

            App app = new();
            app.InitializeComponent();
            app.Run();
        }
    }
}
