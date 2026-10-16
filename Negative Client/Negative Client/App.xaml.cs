using System.Windows;
using Velopack;

namespace Negative_Client
{
    public partial class App : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Velopack procesa aquí los comandos especiales de instalación y actualización.
            VelopackApp.Build().Run();

            App app = new();
            app.InitializeComponent();
            app.Run();
        }
    }
}
