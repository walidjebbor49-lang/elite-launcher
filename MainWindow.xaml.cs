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
            // Creamos una carpeta totalmente independiente llamada "ELITE-Event" 
            // al lado de la carpeta .minecraft tradicional (en AppData\Roaming)
            string roamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string customGameDir = Path.Combine(roamingPath, "ELITE-Event");

            Directory.CreateDirectory(customGameDir);

            // Configuramos CmlLib para que trabaje exclusivamente en esta carpeta aislada
            _customPath = new MinecraftPath(customGameDir);
            _launcher = new CMLauncher(_customPath);
        }

        private async void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (_launcher == null || _customPath == null) return;

            string targetVersionName = "1.20.1-forge-47.2.0";

            BtnLaunch.IsEnabled = false;
            TxtStatus.Text = _isSpanish ? "  Verificando archivos del evento..." : "  Checking event files...";

            try
            {
                // 1. Descarga y descomprime la instancia en la carpeta aislada ELITE-Event
                TxtStatus.Text = _isSpanish ? "  Descargando instancia limpia..." : "  Downloading clean instance...";
                await DownloadAndExtractInstanceAsync();

                // 2. Iniciar Minecraft con la versión aislada e IP del evento inyectada
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
            string releaseTag = "v1.0.1"; // Versión del modpack en GitHub Releases
            string zipFileName = "modpack.zip"; // Tu archivo zip con la instancia

            string downloadUrl = $"https://github.com/{githubUser}/{repoName}/releases/download/{releaseTag}/{zipFileName}";

            using var client = new HttpClient();

            try
            {
                // Descarga el modpack completo
                byte[] zipBytes = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(tempZipPath, zipBytes);

                // Descomprime exclusivamente dentro de la carpeta ELITE-Event
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
