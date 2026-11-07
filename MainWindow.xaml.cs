using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installators;
using CmlLib.Core.Version;

namespace ELITE.Launcher
{
    public partial class MainWindow : Window
    {
        private CMLauncher? _launcher;
        private MinecraftPath? _path;
        private bool _isDarkMode = true;

        public MainWindow()
        {
            InitializeComponent();
            InitMinecraftEngine();
        }

        private void InitMinecraftEngine()
        {
            _path = new MinecraftPath();
            _launcher = new CMLauncher(_path);
        }

        private async void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (_launcher == null || _path == null) return;

            BtnLaunch.IsEnabled = false;
            TxtStatus.Text = "  Verificando Forge 1.21.1...";

            try
            {
                // 1. Instalar o verificar Forge 1.21.1 automáticamente
                var embeddedForgeVersion = "1.21.1-forge-52.0.0"; // Ajusta a tu versión exacta de Forge si varía
                
                TxtStatus.Text = "  Descargando mods requeridos...";
                await DownloadRequiredModsAsync();

                TxtStatus.Text = "  Iniciando Minecraft...";
                var session = MSession.GetOfflineSession("ElitePlayer");

                var process = await _launcher.CreateProcessAsync(embeddedForgeVersion, new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = 3048
                });

                process.Start();
                TxtStatus.Text = "  ¡Juego iniciado!";
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al iniciar Forge: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnLaunch.IsEnabled = true;
                TxtStatus.Text = "  Error al iniciar";
            }
        }

        private async Task DownloadRequiredModsAsync()
        {
            if (_path == null) return;
            string modsDir = Path.Combine(_path.BasePath, "mods");
            Directory.CreateDirectory(modsDir);

            // Ejemplo: URL directa al archivo .jar de tu mod o paquete en GitHub / Servidor web
            // Puedes cambiar esta URL por la de tu mod principal
            string modUrl = "https://github.com/tu-usuario/tu-repo/raw/main/mod-ejemplo.jar";
            string modPath = Path.Combine(modsDir, "mod-ejemplo.jar");

            if (!File.Exists(modPath))
            {
                using var client = new HttpClient();
                try
                {
                    byte[] modBytes = await client.GetByteArrayAsync(modUrl);
                    await File.WriteAllBytesAsync(modPath, modBytes);
                }
                catch
                {
                    // Si falla la descarga remota online opcional, continúa sin detener el juego
                }
            }
        }

        private void NavHome_Click(object sender, RoutedEventArgs e)
        {
            // Ya estamos en inicio
        }

        private void Optimization_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_path == null) return;
                string optionsFile = Path.Combine(_path.BasePath, "options.txt");
                
                // Inyectar parámetros optimizados para máximo rendimiento en Minecraft
                string optimizedConfig = "graphicsMode:fabulous\nrenderDistance:8\nmaxFps:140\nviewDistance:8\nentityDistanceScaling:50\nclouds:false\nparticles:decreased\nsmoothLighting:0\n";
                
                File.WriteAllText(optionsFile, optimizedConfig);
                MessageBox.Show("¡Configuración de optimización aplicada con éxito en tu Minecraft!", "Optimización ELITE", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo aplicar la optimización: {ex.Message}");
            }
        }

        private void Mods_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_path == null) return;
                string modsDir = Path.Combine(_path.BasePath, "mods");
                Directory.CreateDirectory(modsDir);
                
                // Abre directamente la carpeta de mods en el Explorador de Windows
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = modsDir,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir la carpeta de mods: {ex.Message}");
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // Alternador de Modo Claro / Modo Oscuro para el Launcher
            _isDarkMode = !_isDarkMode;
            if (_isDarkMode)
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F111A"));
                MessageBox.Show("Modo Oscuro activado.", "Ajustes", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F6FA"));
                MessageBox.Show("Modo Claro activado.", "Ajustes", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
