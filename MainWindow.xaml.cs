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
        private MinecraftPath? _path;
        private bool _isSpanish = true;

        // IP fija del servidor para el evento
        private const string EventServerIp = "pristine-bridge.play.hosting";

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

            string modsZipName = "mods-1.20.zip";

            BtnLaunch.IsEnabled = false;
            TxtStatus.Text = _isSpanish ? "  Verificando entorno de juego..." : "  Checking game environment...";

            try
            {
                // 1. Descargar mods y texturas primero
                TxtStatus.Text = _isSpanish ? "  Descargando mods y texture pack..." : "  Downloading mods and texture pack...";
                await DownloadAndExtractModsAsync(modsZipName);

                // 2. Comprobación inteligente de versiones locales
                TxtStatus.Text = _isSpanish ? "  Verificando versión 1.20.1..." : "  Verifying 1.20.1 version...";
                var installedVersions = _path.GetVersions();
                string targetVersion = "";

                foreach (var v in installedVersions)
                {
                    if (v.Name.Contains("1.20.1"))
                    {
                        targetVersion = v.Name;
                        break;
                    }
                }

                // Si no está instalada, la descargamos automáticamente de forma limpia
                if (string.IsNullOrEmpty(targetVersion))
                {
                    TxtStatus.Text = _isSpanish ? "  Descargando Minecraft 1.20.1 base..." : "  Downloading base Minecraft 1.20.1...";
                    var vanillaVersion = await _launcher.GetVersionAsync("1.20.1");
                    await _launcher.CheckAndDownloadAsync(vanillaVersion);
                    targetVersion = "1.20.1";
                }

                // 3. Iniciar el juego
                TxtStatus.Text = _isSpanish ? "  Iniciando Minecraft..." : "  Starting Minecraft...";
                var session = MSession.GetOfflineSession("ElitePlayer");

                var launchOptions = new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = 3048,
                    ServerIp = EventServerIp // Conecta automáticamente al servidor del evento
                };

                var process = await _launcher.CreateProcessAsync(targetVersion, launchOptions);

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

                if (File.Exists(tempModsZip)) File.Delete(tempModsZip);
            }
            catch { }

            if (!File.Exists(panchoTexturePackPath))
            {
                try
                {
                    byte[] panchoBytes = await client.GetByteArrayAsync(panchoUrl);
                    await File.WriteAllBytesAsync(panchoTexturePackPath, panchoBytes);
                }
                catch { }
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
                if (_path == null) return;
                string optionsFile = Path.Combine(_path.BasePath, "options.txt");
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
                if (_path == null) return;
                string modsDir = Path.Combine(_path.BasePath, "mods");
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

        // Cambio de Tema Oscuro / Claro
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

        // Cambio de Idioma (Español / Inglés)
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
