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
        private const string EventServerIp = "pristine-bridge.play.hosting";

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
            TxtStatus.Text = _isSpanish ? "  Verificando archivos..." : "  Checking files...";

            try
            {
                // 1. Descargar y extraer el modpack
                TxtStatus.Text = _isSpanish ? "  Descargando instancia..." : "  Downloading instance...";
                await DownloadAndExtractInstanceAsync();

                // 2. Mover automáticamente todo lo que haya dentro de carpetas intermedias (forge-1-20-1 o forge-1.20.1) a la raíz de ELITE-Event
                TxtStatus.Text = _isSpanish ? "  Organizando carpetas del juego..." : "  Organizing game files...";
                OrganizeExtractedInstance(_customPath.BasePath);

                // 3. Buscar el nombre exacto de la versión dentro de ELITE-Event/versions
                string targetVersionName = "1.20.1-forge-47.2.0";
                string versionsDir = Path.Combine(_customPath.BasePath, "versions");

                if (Directory.Exists(versionsDir))
                {
                    var versionDirs = Directory.GetDirectories(versionsDir);
                    if (versionDirs.Length > 0)
                    {
                        targetVersionName = Path.GetFileName(versionDirs[0]);
                    }
                }

                // 4. Iniciar Minecraft con la versión y la IP fija
                TxtStatus.Text = _isSpanish ? "  Iniciando Minecraft..." : "  Starting Minecraft...";
                var session = MSession.GetOfflineSession("ElitePlayer");

                var launchOptions = new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = 3048,
                    ServerIp = EventServerIp
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

        private void OrganizeExtractedInstance(string rootPath)
        {
            // Busca si existe alguna subcarpeta que empiece por "forge" dentro de ELITE-Event
            var subDirs = Directory.GetDirectories(rootPath);
            foreach (var dir in subDirs)
            {
                string dirName = Path.GetFileName(dir).ToLower();
                if (dirName.StartsWith("forge"))
                {
                    // Mueve todas las subcarpetas (mods, config, versions, etc.) a la raíz de ELITE-Event
                    foreach (var subDir in Directory.GetDirectories(dir))
                    {
                        string destDir = Path.Combine(rootPath, Path.GetFileName(subDir));
                        if (Directory.Exists(destDir))
                        {
                            Directory.Delete(destDir, true);
                        }
                        Directory.Move(subDir, destDir);
                    }

                    // Mueve todos los archivos (options.txt, etc.) a la raíz de ELITE-Event
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        string destFile = Path.Combine(rootPath, Path.GetFileName(file));
                        if (File.Exists(destFile))
                        {
                            File.Delete(destFile);
                        }
                        File.Move(file, destFile);
                    }

                    // Borra la carpeta intermedia vacía
                    Directory.Delete(dir, true);
                    break;
                }
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
