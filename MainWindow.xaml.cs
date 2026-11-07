using System;
using System.IO;
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

            // Identificar qué versión eligió el usuario en el ComboBox
            string selectedVersionName = "1.21.1-forge-52.0.0"; // Por defecto
            string githubFolderMods = "mods-1.21";

            if (CbVersions.SelectedItem is ComboBoxItem selectedItem)
            {
                string? content = selectedItem.Content.ToString();
                if (content != null && content.Contains("1.20.1"))
                {
                    selectedVersionName = "1.20.1-forge-47.2.0"; // Ajusta el ID de Forge 1.20.1 si lo necesitas
                    githubFolderMods = "mods-1.20";
                }
                else
                {
                    selectedVersionName = "1.21.1-forge-52.0.0";
                    githubFolderMods = "mods-1.21";
                }
            }

            BtnLaunch.IsEnabled = false;
            TxtStatus.Text = $"  Verificando {selectedVersionName}...";

            try
            {
                // Descargar los mods específicos de esa versión desde GitHub
                TxtStatus.Text = "  Descargando mods y recursos...";
                await DownloadRequiredModsAsync(githubFolderMods);

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

        private async Task DownloadRequiredModsAsync(string modFolderRepo)
        {
            if (_path == null) return;

            string modsDir = Path.Combine(_path.BasePath, "mods");
            string resourcepacksDir = Path.Combine(_path.BasePath, "resourcepacks");

            Directory.CreateDirectory(modsDir);
            Directory.CreateDirectory(resourcepacksDir);

            string githubUser = "walidjebbor49-lang";
            string repoName = "elite-launcher";
            string branch = "main";

            // Puedes organizar tus carpetas en GitHub como 'mods-1.20', 'mods-1.21' y 'pancho' para los texture packs
            var filesToDownload = new Dictionary<string, string>
            {
                { Path.Combine(modsDir, "mod-servidor.jar"), $"https://raw.githubusercontent.com/{githubUser}/{repoName}/{branch}/{modFolderRepo}/mod-servidor.jar" },
                { Path.Combine(resourcepacksDir, "pancho-pack.zip"), $"https://raw.githubusercontent.com/{githubUser}/{repoName}/{branch}/pancho/pancho-pack.zip" }
            };

            using var client = new HttpClient();

            foreach (var file in filesToDownload)
            {
                if (!File.Exists(file.Key))
                {
                    try
                    {
                        byte[] bytes = await client.GetByteArrayAsync(file.Value);
                        await File.WriteAllBytesAsync(file.Key, bytes);
                    }
                    catch
                    {
                        // Si algún archivo aún no está subido, el juego sigue arrancando sin bloquearse
                    }
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
