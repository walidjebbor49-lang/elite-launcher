using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CmlLib.Core;
using CmlLib.Core.Auth;
using Microsoft.Win32;

namespace ELITE.Launcher
{
    public partial class MainWindow : Window
    {
        private CMLauncher? _launcher;
        private MinecraftPath? _customPath;
        private bool _isSpanish = true;

        // ───────── CONFIGURACIÓN DEL EVENTO ─────────
        private const string EventServerIp = "pristine-bridge.play.hosting";

        private const string GitHubUser = "walidjebbor49-lang";
        private const string RepoName = "elite-launcher";

        // Siempre baja el modpack.zip de la ÚLTIMA release (no depende del nombre del tag).
        private const string ModpackUrl =
            "https://github.com/" + GitHubUser + "/" + RepoName + "/releases/latest/download/modpack.zip";

        // Lista de invitados online: whitelist.json en la raíz del repo (rama main).
        private const string WhitelistUrl =
            "https://raw.githubusercontent.com/" + GitHubUser + "/" + RepoName + "/main/whitelist.json";

        // SHA-256 de la contraseña de administrador (no se guarda en texto plano).
        private const string AdminPasswordHash =
            "72f4b0d6823bade11901349612a600893be27f9f30b8f5a72c1860dcd9ddc789";
        // ────────────────────────────────────────────

        private static readonly Regex NameRegex = new(@"^[A-Za-z0-9_]{3,16}$");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private sealed class WhitelistFile { public List<string> Players { get; set; } = new(); }
        private sealed class UserFile { public string Name { get; set; } = ""; }

        private string _dataDir = "";
        private string LocalListPath => Path.Combine(_dataDir, "whitelist.local.json"); // lo que edita el admin
        private string CacheListPath => Path.Combine(_dataDir, "whitelist.cache.json");  // última copia online
        private string UserPath => Path.Combine(_dataDir, "user.json");                  // jugador registrado

        private readonly ObservableCollection<string> _participants = new();
        private string _playerName = "";

        public MainWindow()
        {
            InitializeComponent();
            InitCustomMinecraftEngine();
            LoadLocalData();

            LstParticipants.ItemsSource = _participants;
            UpdateAdminCount();
            UpdateRegistrationUi();
        }

        private string T(string es, string en) => _isSpanish ? es : en;

        private void InitCustomMinecraftEngine()
        {
            string roamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string customGameDir = Path.Combine(roamingPath, "ELITE-Event");
            Directory.CreateDirectory(customGameDir);

            // Datos del launcher fuera de la carpeta del juego para que el modpack no los pise.
            _dataDir = Path.Combine(roamingPath, "ELITE-Launcher-Data");
            Directory.CreateDirectory(_dataDir);

            _customPath = new MinecraftPath(customGameDir);
            _launcher = new CMLauncher(_customPath);
        }

        // ═════════════════ LISTA DE INVITADOS / REGISTRO ═════════════════

        private void LoadLocalData()
        {
            try
            {
                if (File.Exists(LocalListPath))
                    foreach (var n in ParseWhitelist(File.ReadAllText(LocalListPath)))
                        _participants.Add(n);
            }
            catch { /* archivo corrupto: se ignora */ }

            try
            {
                if (File.Exists(UserPath))
                    _playerName = JsonSerializer.Deserialize<UserFile>(File.ReadAllText(UserPath), JsonOpts)?.Name ?? "";
            }
            catch { }
        }

        private static List<string> ParseWhitelist(string json)
        {
            var file = JsonSerializer.Deserialize<WhitelistFile>(json, JsonOpts);
            return (file?.Players ?? new List<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void SaveLocalList()
        {
            var json = JsonSerializer.Serialize(new WhitelistFile { Players = _participants.ToList() }, JsonOpts);
            File.WriteAllText(LocalListPath, json);
        }

        private async Task<List<string>?> FetchRemoteWhitelistAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                string json = await http.GetStringAsync(WhitelistUrl + "?t=" + DateTime.UtcNow.Ticks);
                var list = ParseWhitelist(json);
                File.WriteAllText(CacheListPath, json);
                return list;
            }
            catch
            {
                return null;
            }
        }

        // Orden: lista online → última copia online guardada → lista local del admin.
        private async Task<List<string>> GetAllowedListAsync()
        {
            var remote = await FetchRemoteWhitelistAsync();
            if (remote != null) return remote;

            try
            {
                if (File.Exists(CacheListPath))
                    return ParseWhitelist(File.ReadAllText(CacheListPath));
            }
            catch { }

            return _participants.ToList();
        }

        private static string? FindInList(List<string> list, string name) =>
            list.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

        private void UpdateRegistrationUi()
        {
            TxtRegCurrent.Text = string.IsNullOrEmpty(_playerName)
                ? T("Sin registrar", "Not registered")
                : T("Registrado como: ", "Registered as: ") + _playerName;
        }

        private void SetRegStatus(string text, bool? ok)
        {
            TxtRegStatus.Text = text;
            string color = ok == true ? "#00B894" : ok == false ? "#FF7675" : "#8B9BB4";
            TxtRegStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }

        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtRegName.Text.Trim();
            if (!NameRegex.IsMatch(name))
            {
                SetRegStatus(T("Nombre no válido: 3-16 caracteres, solo letras, números o _.",
                               "Invalid name: 3-16 characters, letters, numbers or _ only."), false);
                return;
            }

            BtnRegister.IsEnabled = false;
            SetRegStatus(T("Comprobando invitación...", "Checking invitation..."), null);
            try
            {
                var list = await GetAllowedListAsync();
                string? match = FindInList(list, name);
                if (match == null)
                {
                    SetRegStatus(T("No estás en la lista de invitados. Habla con el organizador.",
                                   "You are not on the guest list. Contact the organizer."), false);
                    return;
                }

                _playerName = match;
                File.WriteAllText(UserPath, JsonSerializer.Serialize(new UserFile { Name = match }, JsonOpts));
                SetRegStatus(T("¡Registro correcto! Ya puedes jugar.", "Registration successful! You can play now."), true);
                UpdateRegistrationUi();
            }
            catch (Exception ex)
            {
                SetRegStatus($"Error: {ex.Message}", false);
            }
            finally
            {
                BtnRegister.IsEnabled = true;
            }
        }

        // ═════════════════ PANEL DE ADMINISTRADOR ═════════════════

        private static string HashPassword(string text) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

        private bool AskAdminPassword()
        {
            var label = new TextBlock
            {
                Text = T("Contraseña de administrador:", "Administrator password:"),
                Foreground = Brushes.White,
                FontSize = 14
            };
            var pb = new PasswordBox { Height = 30, FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 10, 0, 15) };
            var ok = new Button { Content = T("Entrar", "Enter"), Width = 90, Height = 30, IsDefault = true, Margin = new Thickness(0, 0, 10, 0) };
            var cancel = new Button { Content = T("Cancelar", "Cancel"), Width = 90, Height = 30, IsCancel = true };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(label);
            panel.Children.Add(pb);
            panel.Children.Add(buttons);

            var win = new Window
            {
                Title = "Admin",
                Width = 340,
                Height = 190,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#161925")),
                Content = panel
            };

            bool granted = false;
            ok.Click += (_, _) =>
            {
                if (HashPassword(pb.Password) == AdminPasswordHash)
                {
                    granted = true;
                    win.DialogResult = true;
                }
                else
                {
                    MessageBox.Show(T("Contraseña incorrecta.", "Wrong password."), "ELITE", MessageBoxButton.OK, MessageBoxImage.Warning);
                    pb.Clear();
                    pb.Focus();
                }
            };
            win.Loaded += (_, _) => pb.Focus();
            win.ShowDialog();
            return granted;
        }

        private void NavAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (!AskAdminPassword()) return;
            ShowView(ViewAdmin, BtnNavAdmin);
        }

        private void UpdateAdminCount()
        {
            TxtAdminCount.Text = $"{_participants.Count} " + T("invitados", "guests");
        }

        private bool TryGetAdminName(out string name)
        {
            name = TxtAdminName.Text.Trim();
            if (NameRegex.IsMatch(name)) return true;

            MessageBox.Show(T("El nombre debe tener 3-16 caracteres: letras, números o _ (sin espacios).",
                              "The name must be 3-16 characters: letters, numbers or _ (no spaces)."),
                            "ELITE", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private bool NameExists(string name, int ignoreIndex = -1) =>
            _participants.Where((n, i) => i != ignoreIndex)
                         .Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

        private void AdminAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetAdminName(out var name)) return;
            if (NameExists(name))
            {
                MessageBox.Show(T("Ese nombre ya está en la lista.", "That name is already on the list."), "ELITE");
                return;
            }
            _participants.Add(name);
            SaveLocalList();
            TxtAdminName.Clear();
            UpdateAdminCount();
        }

        private void AdminEdit_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstParticipants.SelectedIndex;
            if (idx < 0)
            {
                MessageBox.Show(T("Selecciona un participante de la lista.", "Select a participant from the list."), "ELITE");
                return;
            }
            if (!TryGetAdminName(out var name)) return;
            if (NameExists(name, idx))
            {
                MessageBox.Show(T("Ese nombre ya está en la lista.", "That name is already on the list."), "ELITE");
                return;
            }
            _participants[idx] = name;
            SaveLocalList();
            LstParticipants.SelectedIndex = idx;
        }

        private void AdminRemove_Click(object sender, RoutedEventArgs e)
        {
            int idx = LstParticipants.SelectedIndex;
            if (idx < 0)
            {
                MessageBox.Show(T("Selecciona un participante de la lista.", "Select a participant from the list."), "ELITE");
                return;
            }
            _participants.RemoveAt(idx);
            SaveLocalList();
            TxtAdminName.Clear();
            UpdateAdminCount();
        }

        private void LstParticipants_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstParticipants.SelectedItem is string s) TxtAdminName.Text = s;
        }

        private void AdminExport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { FileName = "whitelist.json", Filter = "JSON (*.json)|*.json" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var json = JsonSerializer.Serialize(new WhitelistFile { Players = _participants.ToList() }, JsonOpts);
                File.WriteAllText(dlg.FileName, json);
                MessageBox.Show(
                    T("Archivo guardado.\n\nSúbelo a la raíz del repositorio de GitHub (rama main) con el nombre whitelist.json para que todos los jugadores vean la lista actualizada.",
                      "File saved.\n\nUpload it to the root of the GitHub repository (main branch) as whitelist.json so every player sees the updated list."),
                    "ELITE", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }

        private async void AdminLoadRemote_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(
                T("Esto reemplazará la lista local por la que hay online. ¿Continuar?",
                  "This will replace the local list with the online one. Continue?"),
                "ELITE", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            var remote = await FetchRemoteWhitelistAsync();
            if (remote == null)
            {
                MessageBox.Show(T("No se pudo cargar la lista online (¿todavía no has subido whitelist.json?).",
                                  "Could not load the online list (have you uploaded whitelist.json yet?)."), "ELITE");
                return;
            }

            _participants.Clear();
            foreach (var n in remote) _participants.Add(n);
            SaveLocalList();
            UpdateAdminCount();
        }

        // ═════════════════ JUGAR ═════════════════

        private async void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (_launcher == null || _customPath == null) return;

            if (string.IsNullOrEmpty(_playerName))
            {
                ShowView(ViewRegister, BtnNavRegister);
                SetRegStatus(T("Primero tienes que registrarte con tu nombre de invitado.",
                               "You need to register with your guest name first."), false);
                return;
            }

            BtnLaunch.IsEnabled = false;
            bool started = false;

            try
            {
                // 1. Comprobar que sigue estando invitado
                TxtStatus.Text = T("  Comprobando invitación...", "  Checking invitation...");
                var allowed = await GetAllowedListAsync();
                if (FindInList(allowed, _playerName) == null)
                {
                    TxtStatus.Text = T("  No estás en la lista de invitados", "  You are not on the guest list");
                    MessageBox.Show(T("Tu nombre ya no está en la lista de invitados.", "Your name is no longer on the guest list."),
                                    "ELITE", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 2. Descargar (si hace falta), extraer y localizar la versión de Forge
                TxtStatus.Text = T("  Descargando / verificando instancia...", "  Downloading / checking instance...");
                string versionName = await EnsureInstanceInstalledAsync(_customPath.BasePath);

                // 3. Iniciar Minecraft con el nombre del jugador registrado
                TxtStatus.Text = T("  Iniciando Minecraft...", "  Starting Minecraft...");
                var session = MSession.GetOfflineSession(_playerName);

                var launchOptions = new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = 3048,
                    ServerIp = EventServerIp
                };

                var process = await _launcher.CreateProcessAsync(versionName, launchOptions);
                process.EnableRaisingEvents = true;
                process.Exited += (_, _) => Dispatcher.Invoke(() =>
                {
                    Show();
                    BtnLaunch.IsEnabled = true;
                    TxtStatus.Text = T("  Sistema listo", "  System ready");
                });

                process.Start();
                started = true;
                TxtStatus.Text = T("  ¡Juego iniciado!", "  Game started!");
                Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al iniciar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = T("  Error al iniciar", "  Launch error");
            }
            finally
            {
                if (!started) BtnLaunch.IsEnabled = true;
            }
        }

        // ═════════════════ INSTALACIÓN DEL MODPACK + FIX DE FORGE ═════════════════

        private async Task<string> EnsureInstanceInstalledAsync(string root)
        {
            string markerPath = Path.Combine(root, ".elite-modpack");
            string? installed = FindForgeVersion(root);

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            HttpResponseMessage resp;
            try
            {
                resp = await client.GetAsync(ModpackUrl, HttpCompletionOption.ResponseHeadersRead);
                resp.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                // Sin conexión pero ya instalado: se juega con lo que hay.
                if (installed != null) return installed;
                throw new Exception($"No se pudo descargar el modpack: {ex.Message}");
            }

            using (resp)
            {
                long? length = resp.Content.Headers.ContentLength;
                string? stored = File.Exists(markerPath) ? File.ReadAllText(markerPath).Trim() : null;

                // Mismo tamaño que la última vez y Forge ya presente: no se vuelve a bajar.
                if (installed != null && length != null && stored == length.ToString())
                    return installed;

                string tempZip = Path.Combine(root, "modpack-temp.zip");
                string extractDir = Path.Combine(root, "_extract");

                await using (var fs = File.Create(tempZip))
                {
                    await resp.Content.CopyToAsync(fs);
                }

                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(tempZip, extractDir, overwriteFiles: true);
                File.Delete(tempZip);

                // Localiza la carpeta real de la instancia, esté donde esté dentro del zip
                // (raíz, forge-1.20.1/, modpack/forge-1-20-1/, etc.).
                string content = FindDirContaining(extractDir, "versions")
                                 ?? FindDirContaining(extractDir, "mods")
                                 ?? extractDir;

                // Si el modpack trae mods, se reemplazan los antiguos para no mezclar versiones.
                string newMods = Path.Combine(content, "mods");
                string oldMods = Path.Combine(root, "mods");
                if (Directory.Exists(newMods) && Directory.Exists(oldMods))
                    Directory.Delete(oldMods, true);

                MergeDirectory(content, root);
                Directory.Delete(extractDir, true);

                string? version = FindForgeVersion(root);
                if (version == null)
                {
                    string found = string.Join(", ", Directory.GetDirectories(root).Select(Path.GetFileName));
                    throw new Exception(
                        "El modpack se descargó pero no contiene ninguna versión de Forge " +
                        "(carpeta versions/<nombre-con-forge>/<nombre>.json). " +
                        $"Carpetas encontradas en {root}: {found}");
                }

                File.WriteAllText(markerPath, length?.ToString() ?? "");
                return version;
            }
        }

        // Devuelve el nombre de la versión de Forge dentro de <root>/versions, o null si no hay.
        private static string? FindForgeVersion(string root)
        {
            string versionsDir = Path.Combine(root, "versions");
            if (!Directory.Exists(versionsDir)) return null;

            foreach (var dir in Directory.GetDirectories(versionsDir))
            {
                string name = Path.GetFileName(dir);
                if (name.IndexOf("forge", StringComparison.OrdinalIgnoreCase) < 0) continue;

                // CmlLib exige versions/<nombre>/<nombre>.json. Si el json tiene otro nombre, se copia.
                string json = Path.Combine(dir, name + ".json");
                if (!File.Exists(json))
                {
                    string? any = Directory.GetFiles(dir, "*.json").FirstOrDefault();
                    if (any == null) continue;
                    File.Copy(any, json, true);
                }
                return name;
            }
            return null;
        }

        private static readonly HashSet<string> SkipDirs =
            new(StringComparer.OrdinalIgnoreCase) { "assets", "libraries", "runtime", "saves", "logs" };

        // Busca (anchura primero, máx. 3 niveles) una carpeta que contenga una subcarpeta llamada childName.
        private static string? FindDirContaining(string start, string childName)
        {
            var queue = new Queue<(string Dir, int Depth)>();
            queue.Enqueue((start, 0));

            while (queue.Count > 0)
            {
                var (dir, depth) = queue.Dequeue();
                if (Directory.Exists(Path.Combine(dir, childName))) return dir;
                if (depth >= 3) continue;

                foreach (var sub in Directory.GetDirectories(dir))
                {
                    if (SkipDirs.Contains(Path.GetFileName(sub))) continue;
                    queue.Enqueue((sub, depth + 1));
                }
            }
            return null;
        }

        // Copia src dentro de dst sobrescribiendo, sin tocar el options.txt que el jugador ya tenga.
        private static void MergeDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);

            foreach (var file in Directory.GetFiles(src))
            {
                string name = Path.GetFileName(file);
                string target = Path.Combine(dst, name);
                if (name.Equals("options.txt", StringComparison.OrdinalIgnoreCase) && File.Exists(target)) continue;
                File.Copy(file, target, true);
            }

            foreach (var sub in Directory.GetDirectories(src))
                MergeDirectory(sub, Path.Combine(dst, Path.GetFileName(sub)));
        }

        // ═════════════════ NAVEGACIÓN ═════════════════

        private void ShowView(UIElement view, Button nav)
        {
            ViewHome.Visibility = Visibility.Collapsed;
            ViewRegister.Visibility = Visibility.Collapsed;
            ViewAdmin.Visibility = Visibility.Collapsed;
            ViewSettings.Visibility = Visibility.Collapsed;
            view.Visibility = Visibility.Visible;
            HighlightNavButton(nav);
        }

        private void NavHome_Click(object sender, RoutedEventArgs e) => ShowView(ViewHome, BtnNavHome);

        private void NavRegister_Click(object sender, RoutedEventArgs e)
        {
            UpdateRegistrationUi();
            ShowView(ViewRegister, BtnNavRegister);
        }

        private void Settings_Click(object sender, RoutedEventArgs e) => ShowView(ViewSettings, BtnNavSettings);

        private void Optimization_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_customPath == null) return;
                string optionsFile = Path.Combine(_customPath.BasePath, "options.txt");
                string optimizedConfig = "graphicsMode:fabulous\nrenderDistance:8\nmaxFps:140\nviewDistance:8\nclouds:false\nsmoothLighting:0\n";
                File.WriteAllText(optionsFile, optimizedConfig);
                MessageBox.Show(T("¡Optimización aplicada con éxito!", "Optimization successfully applied!"), "ELITE", MessageBoxButton.OK, MessageBoxImage.Information);
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
            var muted = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B9BB4"));
            foreach (var b in new[] { BtnNavHome, BtnNavRegister, BtnNavOpt, BtnNavMods, BtnNavSettings, BtnNavAdmin })
            {
                b.Background = Brushes.Transparent;
                b.Foreground = muted;
            }

            activeBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222639"));
            activeBtn.Foreground = Brushes.White;
        }

        // ═════════════════ TEMA E IDIOMA ═════════════════

        private void CbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (CbTheme.SelectedItem is ComboBoxItem item)
            {
                string? text = item.Content.ToString();
                string color = text != null && text.Contains("Claro") ? "#F5F6FA" : "#0F111A";
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            }
        }

        private void CbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Se dispara durante InitializeComponent, cuando otros controles aún no existen.
            if (!IsInitialized) return;

            if (CbLanguage.SelectedItem is ComboBoxItem item)
            {
                string? text = item.Content.ToString();
                _isSpanish = !(text != null && text.Contains("English"));
                ApplyLanguage();
            }
        }

        private void ApplyLanguage()
        {
            LblMenuHeader.Text = T("MENÚ PRINCIPAL", "MAIN MENU");
            BtnNavHome.Content = T("  Inicio", "  Home");
            BtnNavRegister.Content = T("  Registro", "  Register");
            BtnNavOpt.Content = T("  Optimización", "  Optimization");
            BtnNavMods.Content = "  Mods";
            BtnNavSettings.Content = T("  Ajustes", "  Settings");
            BtnNavAdmin.Content = "  Admin";

            LblWelcomeTag.Text = T("BIENVENIDO A ELITE", "WELCOME TO ELITE");
            LblWelcomeMain.Text = T("Tu Minecraft. A tu manera.", "Your Minecraft. Your way.");
            LblWelcomeDesc.Text = T("Selecciona tu versión de Forge preferida y entra al servidor.", "Select your preferred Forge version and join the server.");
            LblSelectVersion.Text = T("Selecciona la Versión de Forge", "Select Forge Version");
            BtnLaunch.Content = T("JUGAR ►", "PLAY ►");
            TxtStatus.Text = T("  Sistema listo", "  System ready");

            LblRegTag.Text = T("ACCESO SOLO INVITADOS", "GUESTS ONLY");
            LblRegTitle.Text = T("Regístrate", "Register");
            LblRegDesc.Text = T("Escribe el nombre de jugador con el que te invitaron al evento.", "Enter the player name you were invited with.");
            LblRegName.Text = T("Nombre de jugador", "Player name");
            BtnRegister.Content = T("REGISTRARME", "REGISTER");
            TxtRegStatus.Text = "";
            UpdateRegistrationUi();

            LblAdminTag.Text = T("PANEL DE ADMINISTRADOR", "ADMIN PANEL");
            LblAdminTitle.Text = T("Participantes invitados", "Invited participants");
            LblAdminDesc.Text = T("Solo los nombres de esta lista podrán registrarse y jugar.", "Only names on this list can register and play.");
            BtnAdminAdd.Content = T("Añadir", "Add");
            BtnAdminEdit.Content = T("Editar", "Edit");
            BtnAdminRemove.Content = T("Quitar", "Remove");
            BtnAdminExport.Content = T("Exportar whitelist.json", "Export whitelist.json");
            BtnAdminLoad.Content = T("Cargar lista online", "Load online list");
            UpdateAdminCount();

            LblSettingsTitle.Text = T("CONFIGURACIÓN", "SETTINGS");
            LblSettingsHeader.Text = T("Ajustes del Launcher", "Launcher Settings");
            LblThemeLabel.Text = T("Tema Visual", "Visual Theme");
            LblLangLabel.Text = T("Idioma / Language", "Language / Idioma");
        }
    }
}
