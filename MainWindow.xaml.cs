using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge; // Necesario para instalar Forge automáticamente

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

            string versionId = "1.20.1-forge-47.2.0";
            string modsZipName = "mods-1.20.zip";

            BtnLaunch.IsEnabled = false;
            TxtStatus.Text = "  Verificando archivos de Minecraft...";

            try
            {
                // 1. Comprobar e instalar Forge automáticamente si falta
                var osVersions = await _launcher.GetAllVersionsAsync();
                var targetVersion = osVersions.GetVersion(versionId);

                if (targetVersion == null)
                {
                    TxtStatus.Text = "  Instalando Forge 1.20.1 (puede tardar un poco)...";
                    
                    var forge = new MForge(_launcher);
                    // Instala la versión de Forge de forma nativa
                    targetVersion = await forge.InstallAsync("1.20.1", "47.2.0");
                }

                // 2. Descargar mods y texture pack desde el Release de GitHub
                TxtStatus.Text = "  Descargando mods y texture pack...";
                await DownloadAndExtractModsAsync(modsZipName);

                // 3. Iniciar el juego
                TxtStatus.Text = "  Iniciando Minecraft...";
                var session = MSession.GetOfflineSession("ElitePlayer");

                var process = await _launcher.CreateProcessAsync(targetVersion, new MLaunchOption
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
                MessageBox.Show($"Error al iniciar el juego: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnLaunch.IsEnabled = true;
                TxtStatus.Text = "  Error al iniciar";
            }
        }

        private async Task DownloadAndExtractModsAsync(string modsZipName)
        {
            if (_path == null) return;

            string modsDir = Path.Combine(_path.BasePath, "mods");
            string resourcepacksDir = Path.Combine(_path.BasePath, "resourcepacks");

            Directory.CreateDirectory(modsDir);
            Directory.CreateDirectory(resourcepacksDir);

            string tempModsZip = Path.Combine(_path.BasePath, modsZipName);
            string panchoTexturePackPath = Path.Combine(resourcepacksDir, "pancho.zip");

            string githubUser = "walidjebbor49-lang";
            string repoName = "elite-launcher";
            string releaseTag = "v1.0.0";

            string modsUrl = $"https://github.com/{githubUser}/{repoName}/releases/download/{releaseTag}/{modsZipName}";
            string panchoUrl = $"https://github.com/{githubUser}/{repoName}/releases/download/{releaseTag}/pancho.zip";

            using var client = new HttpClient();

            try
            {
                byte[] zipBytes = await client.GetByteArrayAsync(modsUrl);
                await File.WriteAllBytesAsync(tempModsZip, zipBytes);
                ZipFile.ExtractToDirectory(tempModsZip, modsDir, overwriteFiles: true);

                if (File.Exists(tempModsZip))
                {
                    File.Delete(tempModsZip);
                }
            }
            catch
            {
                // Si falla, continúa el arranque
            }

            if (!File.Exists(panchoTexturePackPath))
            {
                try
                {
                    byte[] panchoBytes = await client.GetByteArrayAsync(panchoUrl);
                    await File.WriteAllBytesAsync(panchoTexturePackPath, panchoBytes);
                }
                catch
                {
                    // Continúa si hay algún fallo temporal
                }
            }
        }

        private void NavHome_Click(object sender, RoutedEventArgs e) { }

        private void Optimization_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_path == null) return;
                string optionsFile = Path.Combine(_path.BasePath, "options.txt");
                string optimizedConfig = "graphicsMode:fabulous\nrenderDistance:8\nmaxFps:140\nviewDistance:8\nclouds:false\nsmoothLighting:0\n";
                File.WriteAllText(optionsFile, optimizedConfig);
                MessageBox.Show("¡Optimización aplicada con éxito!", "ELITE", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }

        private void Mods_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_path == null) return;
                string modsDir = Path.Combine(_path.BasePath, "mods");
                Directory.CreateDirectory(modsDir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() { FileName = modsDir, UseShellExecute = true, Verb = "open" });
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
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
