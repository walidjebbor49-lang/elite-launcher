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

namespace ELITE.Launcher
{
    public partial class MainWindow : Window
    {
        private CMLauncher? _launcher;
        private MinecraftPath? _customPath;
        private bool _isSpanish = true;

        // IP fija del servidor para el evento
        const string EventServerIp = "pristine-bridge.play.hosting";

        public MainWindow()
        {
            InitializeComponent();
            InitCustomMinecraftEngine();
        }

        private void InitCustomMinecraftEngine()
        {
            string roamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string customGameDir = Path.Combine(roamingPath, "ELITE-Event");

            Directory.CreateDirectory(customGameDir);

            _customPath = new MinecraftPath(customGameDir);
            _launcher = new CMLauncher(_customPath);
        }

        private async void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (_launcher == null || _customPath == null) return;

            BtnLaunch.IsEnabled = false;
            TxtStatus.Text = _isSpanish ? "  Verificando archivos del evento..." : "  Checking event files...";

            try
            {
                // 1. Descarga y descomprime la instancia
                TxtStatus.Text = _isSpanish ? "  Descargando instancia..." : "  Downloading instance...";
                await DownloadAndExtractInstanceAsync();

                // 2. Búsqueda inteligente de la versión instalada en la carpeta (evita el error de nombre)
                TxtStatus.Text = _isSpanish ? "  Buscando versión de Forge..." : "  Looking for Forge version...";
                
                // Buscamos dentro de la carpeta versions de ELITE-Event o subcarpetas
                string targetVersionName = "1.20.1-forge-47.2.0"; // Nombre por defecto
                string versionsDir = Path.Combine(_customPath.BasePath, "versions");
                
                if (Directory.Exists(versionsDir))
                {
                    var versionFolders = Directory.GetDirectories(versionsDir);
                    if (versionFolders.Length > 0)
                    {
                        targetVersionName = Path.GetFileName(versionFolders[0]);
                    }
                }
                else
                {
                    // Si el zip se extrajo dentro de una subcarpeta como 'forge-1-20-1', movemos los archivos a la raíz
                    string subFolder = Path.Combine(_customPath.BasePath, "forge-1-20-1");
                    if (Directory.Exists(subFolder))
                    {
                        MoveDirectoryContents(subFolder, _customPath.BasePath);
                        Directory.Delete(subFolder, true);
                        
                        versionsDir = Path.Combine(_customPath.BasePath, "versions");
                        if (Directory.Exists(versionsDir))
                        {
                            var versionFolders = Directory.GetDirectories(versionsDir);
                            if (versionFolders.Length > 0)
                            {
                                targetVersionName = Path.GetFileName(versionFolders[0]);
                            }
                        }
                    }
                }

                // 3. Iniciar Minecraft con la versión detectada y la IP del evento
                TxtStatus.Text = _isSpanish ? "  Iniciando Minecraft..." : "  Starting Minecraft...";
                var session = MSession.GetOfflineSession("ElitePlayer");

                var launchOptions = new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = 3048,
                    ServerIp = EventServerIp // Conecta automáticamente al servidor del evento
                };

                var process = await _launcher.CreateProcessAsync(targetVersionName, launchOptions);

                process.Start();
                TxtStatus.Text = _isSpanish ? "  ¡Juego iniciado!" : "  Game started!";
                this.Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al iniciar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnLaunch.IsEnabled = true;
                TxtStatus.Text = _isSpanish ? "  Error al iniciar" : "  Launch error";
            }
        }

        private async Task DownloadAndExtractInstanceAsync()
        {
            if (_customPath == null) return;

            string tempZipPath = Path.Combine(_customPath.BasePath, "modpack-temp.zip");

            string githubUser = "walidjebbor49-lang";
            string repoName = "elite-launcher";
            string releaseTag = "v1.0.1"; 
            string zipFileName = "modpack.zip"; 

            string downloadUrl = $"https://github.com/{githubUser}/{repoName}/releases/download/{releaseTag}/{zipFileName}";

            using var client = new HttpClient();

            try
            {
                byte[] zipBytes = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(tempZipPath, zipBytes);

                ZipFile.ExtractToDirectory(tempZipPath, _customPath.BasePath, overwriteFiles: true);

                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"No se pudo descargar el modpack: {ex.Message}");
            }
        }

        private void MoveDirectoryContents(string sourceDir, string targetDir)
        {
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourceDir, targetDir));
            }

            foreach (string newPath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourceDir, targetDir), true);
            }
        }

        // Navegación entre pestañas
        private void NavHome_Click(object sender, RoutedEventArgs e)
        {
            ViewHome.Visibility = Visibility.Visible;
            ViewSettings.Visibility = Visibility.Collapsed;
            HighlightNavButton(BtnNavHome);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            ViewHome.Visibility = Visibility.Collapsed;
            ViewSettings.Visibility = Visibility.Visible;
            HighlightNavButton(BtnNavSettings);
        }

        private void Optimization_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_customPath == null) return;
                string optionsFile = Path.Combine(_customPath.BasePath, "options.txt");
                string optimizedConfig = "graphicsMode:fabulous\nrenderDistance:8\nmaxFps:140\nviewDistance:8\nclouds:false\nsmoothLighting:0\n";
                File.WriteAllText(optionsFile, optimizedConfig);
                MessageBox.Show(_isSpanish ? "¡Optimización aplicada con éxito!" : "Optimization successfully applied!", "ELITE", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }

        private void Mods_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_customPath == null) return;
                string modsDir = Path.Combine(_customPath.BasePath, "mods");
                Directory.CreateDirectory(modsDir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() { FileName = modsDir, UseShellExecute = true, Verb = "open" });
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }

        private void HighlightNavButton(Button activeBtn)
        {
            BtnNavHome.Background = Brushes.Transparent;
            BtnNavHome.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B9BB4"));
            BtnNavOpt.Background = Brushes.Transparent;
            BtnNavOpt.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B9BB4"));
            BtnNavMods.Background = Brushes.Transparent;
            BtnNavMods.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B9BB4"));
            BtnNavSettings.Background = Brushes.Transparent;
            BtnNavSettings.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B9BB4"));

            activeBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222639"));
            activeBtn.Foreground = Brushes.White;
        }

        private void CbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbTheme.SelectedItem is ComboBoxItem item)
            {
                string? text = item.Content.ToString();
                if (text != null && text.Contains("Claro"))
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F6FA"));
                }
                else
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F111A"));
                }
            }
        }

        private void CbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbLanguage.SelectedItem is ComboBoxItem item)
            {
                string? text = item.Content.ToString();
                if (text != null && text.Contains("English"))
                {
                    _isSpanish = false;
                    SetLanguageEnglish();
                }
                else
                {
                    _isSpanish = true;
                    SetLanguageSpanish();
                }
            }
        }

        private void SetLanguageSpanish()
        {
            LblMenuHeader.Text = "MENÚ PRINCIPAL";
            BtnNavHome.Content = "  Inicio";
            BtnNavOpt.Content = "  Optimización";
            BtnNavMods.Content = "  Mods";
            BtnNavSettings.Content = "  Ajustes";
            LblWelcomeTag.Text = "BIENVENIDO A ELITE";
            LblWelcomeMain.Text = "Tu Minecraft. A tu manera.";
            LblWelcomeDesc.Text = "Selecciona tu versión de Forge preferida y entra al servidor.";
            LblSelectVersion.Text = "Selecciona la Versión de Forge";
            BtnLaunch.Content = "JUGAR ►";
            TxtStatus.Text = "  Sistema listo";
            LblSettingsTitle.Text = "CONFIGURACIÓN";
            LblSettingsHeader.Text = "Ajustes del Launcher";
            LblThemeLabel.Text = "Tema Visual";
            LblLangLabel.Text = "Idioma / Language";
        }

        private void SetLanguageEnglish()
        {
            LblMenuHeader.Text = "MAIN MENU";
            BtnNavHome.Content = "  Home";
            BtnNavOpt.Content = "  Optimization";
            BtnNavMods.Content = "  Mods";
            BtnNavSettings.Content = "  Settings";
            LblWelcomeTag.Text = "WELCOME TO ELITE";
            LblWelcomeMain.Text = "Your Minecraft. Your way.";
            LblWelcomeDesc.Text = "Select your preferred Forge version and join the server.";
            LblSelectVersion.Text = "Select Forge Version";
            BtnLaunch.Content = "PLAY ►";
            TxtStatus.Text = "  System ready";
            LblSettingsTitle.Text = "SETTINGS";
            LblSettingsHeader.Text = "Launcher Settings";
            LblThemeLabel.Text = "Visual Theme";
            LblLangLabel.Text = "Language / Idioma";
        }
    }
}
