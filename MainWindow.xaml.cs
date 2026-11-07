using System;
using System.IO;
using System.IO.Compression; // Para descomprimir el archivo .zip
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CmlLib.Core;
using CmlLib.Core.Auth;

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

            string selectedVersionName = "1.21.1-forge-52.0.0";
            string zipName = "mods-1.21.zip";

            if (CbVersions.SelectedItem is ComboBoxItem selectedItem)
            {
                string? content = selectedItem.Content.ToString();
                if (content != null && content.Contains("1.20.1"))
                {
                    selectedVersionName = "1.20.1-forge-47.2.0";
                    zipName = "mods-1.20.zip";
                }
                else
                {
                    selectedVersionName = "1.21.1-forge-52.0.0";
                    zipName = "mods-1.21.zip";
                }
            }

            BtnLaunch.IsEnabled = false;
            TxtStatus.Text = $"  Verificando {selectedVersionName}...";

            try
            {
                TxtStatus.Text = "  Descargando paquete de mods (.zip)...";
                await DownloadAndExtractModsAsync(zipName);

                TxtStatus.Text = "  Iniciando Minecraft...";
                var session = MSession.GetOfflineSession("ElitePlayer");

                var process = await _launcher.CreateProcessAsync(selectedVersionName, new MLaunchOption
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

        private async Task DownloadAndExtractModsAsync(string zipName)
        {
            if (_path == null) return;

            string modsDir = Path.Combine(_path.BasePath, "mods");
            string tempZipPath = Path.Combine(_path.BasePath, zipName);

            Directory.CreateDirectory(modsDir);

            // Enlace directo a tu Release en GitHub donde subas los .zip pesados
            string githubUser = "walidjebbor49-lang";
            string repoName = "elite-launcher";
            string releaseTag = "v1.0.0"; // Ajusta a la etiqueta de tu Release
            
            string zipUrl = $"https://github.com/{githubUser}/{repoName}/releases/download/{releaseTag}/{zipName}";

            using var client = new HttpClient();

            try
            {
                // 1. Descargar el archivo .zip de mods
                byte[] zipBytes = await client.GetByteArrayAsync(zipUrl);
                await File.WriteAllBytesAsync(tempZipPath, zipBytes);

                // 2. Descomprimir el contenido en la carpeta %appdata%/.minecraft/mods
                ZipFile.ExtractToDirectory(tempZipPath, modsDir, overwriteFiles: true);

                // 3. Borrar el archivo .zip temporal para mantener limpia la carpeta
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }
            }
            catch
            {
                // Si ya existen o no hay conexión, continúa el arranque
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
