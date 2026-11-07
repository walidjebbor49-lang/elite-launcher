using System;
using System.Windows;
using CmlLib.Core;
using CmlLib.Core.Auth;

namespace ELITE.Launcher
{
    public partial class MainWindow : Window
    {
        private CMLauncher _launcher;

        public MainWindow()
        {
            InitializeComponent();
            InitMinecraftEngine();
        }

        private void InitMinecraftEngine()
        {
            // Inicializa la ruta por defecto del juego (%appdata%/.minecraft)
            var path = new MinecraftPath();
            _launcher = new CMLauncher(path);
        }

        private async void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            BtnLaunch.IsEnabled = false;
            TxtStatus.Text = "  Iniciando...";

            try
            {
                // Obtiene la lista de versiones y selecciona la primera disponible (o una versión estable)
                var versions = await _launcher.GetAllVersionsAsync();
                string targetVersion = null;

                foreach (var item in versions)
                {
                    if (item.MType == CmlLib.Core.Version.MVersionType.Release)
                    {
                        targetVersion = item.Name;
                        break;
                    }
                }

                if (targetVersion == null && versions.Count > 0)
                    targetVersion = versions[0].Name;

                if (targetVersion == null)
                {
                    MessageBox.Show("No se encontró ninguna versión de Minecraft disponible.");
                    BtnLaunch.IsEnabled = true;
                    TxtStatus.Text = "  Error";
                    return;
                }

                TxtStatus.Text = $"  Cargando {targetVersion}...";

                // Sesión en modo sin conexión / offline
                var session = MSession.GetOfflineSession("ElitePlayer");

                // Configurar el proceso de inicio del juego
                var process = await _launcher.CreateProcessAsync(targetVersion, new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = 2048 // Memoria RAM asignada al juego
                });

                // Lanzar Minecraft
                process.Start();

                TxtStatus.Text = "  ¡Juego iniciado!";
                
                // Ocultar el launcher para que pase a segundo plano mientras juegas
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al iniciar el juego: {ex.Message}", "Error de Lanzamiento", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnLaunch.IsEnabled = true;
                TxtStatus.Text = "  Error al iniciar";
            }
        }

        private void ShowStage(string section) => MessageBox.Show($"La sección {section} se activará en la siguiente etapa de desarrollo.", "ELITE Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
        
        private void Versions_Click(object sender, RoutedEventArgs e) => ShowStage("Versiones");
        private void Optimization_Click(object sender, RoutedEventArgs e) => ShowStage("Optimización");
        private void Mods_Click(object sender, RoutedEventArgs e) => ShowStage("Mods");
        private void Settings_Click(object sender, RoutedEventArgs e) => ShowStage("Ajustes");
    }
}
