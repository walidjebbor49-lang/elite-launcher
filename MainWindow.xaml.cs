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
        // La IP ya no aparece en la interfaz y no va en texto plano dentro del exe (Base64).
        // OJO: esto solo evita que se vea de un vistazo; la protección real es la whitelist del servidor.
        private static readonly string EventServerIp =
            Encoding.UTF8.GetString(Convert.FromBase64String("cHJpc3RpbmUtYnJpZGdlLnBsYXkuaG9zdGluZw=="));

        // true = cada vez que se abre el launcher hay que pasar primero por el registro.
        // false = se recuerda al jugador y entra directo a Inicio.
        private const bool AskRegisterEveryTime = true;

        // Versión de Forge que se instala si el modpack no la trae (o la que diga su manifest.json).
        private const string McVersion = "1.20.1";
        private const string DefaultForgeVersion = "47.2.0";

        private const string GitHubUser = "walidjebbor49-lang";
        private const string RepoName = "elite-launcher";

        // Siempre baja el modpack.zip de la ÚLTIMA release (no depende del nombre del tag).
        private const string ModpackUrl =
            "https://github.com/" + GitHubUser + "/" + RepoName + "/releases/latest/download/modpack.zip";

        // Lista de invitados online: whitelist.json en la raíz del repo (rama main).
        private const string WhitelistUrl =
            "https://raw.githubusercontent.com/" + GitHubUser + "/" + RepoName + "/main/whitelist.json";

        // Anuncios del organizador: announcement.txt en la raíz del repo (texto plano; si no existe, no se muestra nada).
        private const string AnnouncementUrl =
            "https://raw.githubusercontent.com/" + GitHubUser + "/" + RepoName + "/main/announcement.txt";

        // SHA-256 de la contraseña de administrador (no se guarda en texto plano).
        private const string AdminPasswordHash =
            "72f4b0d6823bade11901349612a600893be27f9f30b8f5a72c1860dcd9ddc789";

        // Invitados que existen por defecto en la lista.
        private static readonly string[] DefaultGuests = { "shuasbsjn", "mkdt" };
        // ────────────────────────────────────────────

        private static readonly Regex NameRegex = new(@"^[A-Za-z0-9_]{3,16}$");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private sealed class WhitelistFile
        {
            public List<string> Players { get; set; } = new();
            public bool DefaultsSeeded { get; set; }
        }

        private sealed class UserFile { public string Name { get; set; } = ""; }

        private sealed class SettingsFile
        {
            public bool Dark { get; set; } = true;
            public bool Spanish { get; set; } = true;
            public int RamMb { get; set; } = 3072;
            public int Accent { get; set; } = 0;
            public bool HideOnPlay { get; set; } = true;
            public bool CheckUpdates { get; set; } = true;
        }

        private static readonly int[] RamOptions = { 2048, 3072, 4096, 6144, 8192 };

        private static readonly (string Main, string Dark)[] Accents =
        {
            ("#7C5CFF", "#5B3FE0"), // violeta
            ("#3B82F6", "#1D4ED8"), // azul
            ("#10B981", "#047857"), // verde
            ("#EF4444", "#B91C1C"), // rojo
            ("#F59E0B", "#B45309")  // naranja
        };

        private string _dataDir = "";
        private string LocalListPath => Path.Combine(_dataDir, "whitelist.local.json"); // lo que edita el admin
        private string CacheListPath => Path.Combine(_dataDir, "whitelist.cache.json");  // última copia online
        private string UserPath => Path.Combine(_dataDir, "user.json");                  // jugador registrado
        private string SettingsPath => Path.Combine(_dataDir, "settings.json");          // ajustes del launcher

        private readonly ObservableCollection<string> _participants = new();
        private string _playerName = "";
        private SettingsFile _settings = new();
        private bool _loading;
        private bool _sessionVerified;
        private bool _refreshingHome;
        private System.Windows.Threading.DispatcherTimer? _homeTimer;

        private bool IsRegistered => !string.IsNullOrEmpty(_playerName);
        private bool IsUnlocked => IsRegistered && _sessionVerified;

        public MainWindow()
        {
            InitializeComponent();
            InitCustomMinecraftEngine();
            LoadSettings();
            LoadLocalData();

            // Con AskRegisterEveryTime el jugador siempre empieza en Registro (con su nombre ya escrito).
            _sessionVerified = !AskRegisterEveryTime && IsRegistered;
            TxtRegName.Text = _playerName;

            LstParticipants.ItemsSource = _participants;
            ApplySettingsToUi();
            ApplyRegistrationLock();

            // Refresca estado del servidor y anuncios cada minuto mientras se ve Inicio.
            _homeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _homeTimer.Tick += async (_, _) =>
            {
                if (ViewHome.Visibility == Visibility.Visible) await RefreshHomeInfoAsync();
            };
            _homeTimer.Start();

            Loaded += Window_Loaded;
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

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!IsUnlocked)
            {
                TxtRegName.Focus();
                TxtRegName.CaretIndex = TxtRegName.Text.Length;
                return;
            }

            // Modo "recordar jugador": si ya te quitaron de la lista, se te pide registrarte de nuevo.
            var allowed = await GetAllowedListAsync();
            if (IsRegistered && FindInList(allowed, _playerName) == null)
            {
                ClearRegistration();
                SetRegStatus(T("Tu nombre ya no está en la lista de invitados. Vuelve a registrarte.",
                               "Your name is no longer on the guest list. Please register again."), false);
                return;
            }

            await RefreshHomeInfoAsync();
        }

        // ═════════════════ AJUSTES ═════════════════

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    _settings = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(SettingsPath), JsonOpts) ?? new SettingsFile();
            }
            catch { _settings = new SettingsFile(); }

            if (_settings.Accent < 0 || _settings.Accent >= Accents.Length) _settings.Accent = 0;
            if (!RamOptions.Contains(_settings.RamMb)) _settings.RamMb = 3072;
        }

        private void SaveSettings()
        {
            try { File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_settings, JsonOpts)); }
            catch { /* no es crítico */ }
        }

        private void ApplySettingsToUi()
        {
            _loading = true;
            CbTheme.SelectedIndex = _settings.Dark ? 0 : 1;
            CbAccent.SelectedIndex = _settings.Accent;
            CbLanguage.SelectedIndex = _settings.Spanish ? 0 : 1;
            CbRam.SelectedIndex = Array.IndexOf(RamOptions, _settings.RamMb);
            ChkHideOnPlay.IsChecked = _settings.HideOnPlay;
            ChkCheckUpdates.IsChecked = _settings.CheckUpdates;
            _loading = false;

            _isSpanish = _settings.Spanish;
            ApplyTheme(_settings.Dark);
            ApplyAccent(_settings.Accent);
            ApplyLanguage();
        }

        private void SetColor(string key, string hex) =>
            Resources[key] = (Color)ColorConverter.ConvertFromString(hex);

        private void ApplyTheme(bool dark)
        {
            if (dark)
            {
                SetColor("BgColor", "#0B0D14");
                SetColor("SidebarColor", "#11141D");
                SetColor("CardColor", "#171B28");
                SetColor("InputColor", "#1F2435");
                SetColor("LineColor", "#2A3045");
                SetColor("TextColor", "#FFFFFF");
                SetColor("MutedColor", "#8B9BB4");
                SetColor("NavActiveColor", "#1F2435");
            }
            else
            {
                SetColor("BgColor", "#F3F4F9");
                SetColor("SidebarColor", "#FFFFFF");
                SetColor("CardColor", "#FFFFFF");
                SetColor("InputColor", "#EEF0F6");
                SetColor("LineColor", "#D9DDE8");
                SetColor("TextColor", "#1B1F2E");
                SetColor("MutedColor", "#6B7488");
                SetColor("NavActiveColor", "#ECEEF8");
            }
        }

        private void ApplyAccent(int index)
        {
            var (main, dark) = Accents[index];
            SetColor("AccentColor", main);
            SetColor("AccentColor2", dark);
        }

        private void CbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized || _loading || CbTheme.SelectedIndex < 0) return;
            _settings.Dark = CbTheme.SelectedIndex == 0;
            ApplyTheme(_settings.Dark);
            SaveSettings();
        }

        private void CbAccent_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized || _loading || CbAccent.SelectedIndex < 0) return;
            _settings.Accent = CbAccent.SelectedIndex;
            ApplyAccent(_settings.Accent);
            SaveSettings();
        }

        private void CbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Se dispara durante InitializeComponent, cuando otros controles aún no existen.
            if (!IsInitialized || _loading || CbLanguage.SelectedIndex < 0) return;
            _settings.Spanish = CbLanguage.SelectedIndex == 0;
            _isSpanish = _settings.Spanish;
            ApplyLanguage();
            SaveSettings();
        }

        private void CbRam_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized || _loading || CbRam.SelectedIndex < 0) return;
            _settings.RamMb = RamOptions[CbRam.SelectedIndex];
            SaveSettings();
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized || _loading) return;
            _settings.HideOnPlay = ChkHideOnPlay.IsChecked == true;
            _settings.CheckUpdates = ChkCheckUpdates.IsChecked == true;
            SaveSettings();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_customPath == null) return;
                Directory.CreateDirectory(_customPath.BasePath);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _customPath.BasePath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }

        private void Reinstall_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_customPath == null) return;
                var answer = MessageBox.Show(
                    T("Se volverá a descargar y a instalar el modpack la próxima vez que pulses JUGAR. ¿Continuar?",
                      "The modpack will be downloaded and installed again the next time you press PLAY. Continue?"),
                    "ELITE", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes) return;

                string marker = GetMarkerPath(_customPath.BasePath);
                if (File.Exists(marker)) File.Delete(marker);
                MessageBox.Show(T("Listo. Pulsa JUGAR para reinstalar.", "Done. Press PLAY to reinstall."), "ELITE");
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }

        private void ChangeUser_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(
                T("Se cerrará tu registro y tendrás que registrarte de nuevo. ¿Continuar?",
                  "Your registration will be removed and you will have to register again. Continue?"),
                "ELITE", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            ClearRegistration();
        }

        // ═════════════════ LISTA DE INVITADOS / REGISTRO ═════════════════

        private void LoadLocalData()
        {
            bool seeded = false;
            try
            {
                if (File.Exists(LocalListPath))
                {
                    string json = File.ReadAllText(LocalListPath);
                    seeded = JsonSerializer.Deserialize<WhitelistFile>(json, JsonOpts)?.DefaultsSeeded ?? false;
                    foreach (var n in ParseWhitelist(json)) _participants.Add(n);
                }
            }
            catch { /* archivo corrupto: se ignora */ }

            // Primera vez: se añaden los invitados por defecto (una sola vez, para poder quitarlos después).
            if (!seeded)
            {
                foreach (var g in DefaultGuests)
                    if (!_participants.Any(n => string.Equals(n, g, StringComparison.OrdinalIgnoreCase)))
                        _participants.Add(g);
                try { SaveLocalList(); } catch { }
            }

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
            var json = JsonSerializer.Serialize(
                new WhitelistFile { Players = _participants.ToList(), DefaultsSeeded = true }, JsonOpts);
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

        // Orden: lista online → última copia online guardada → lista local (con los invitados por defecto).
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
            TxtRegCurrent.Text = IsRegistered
                ? T("Registrado como: ", "Registered as: ") + _playerName
                : T("Sin registrar", "Not registered");

            TxtSidebarUser.Text = IsRegistered ? _playerName : T("Sin registrar", "Not registered");

            TxtHomePlayer.Text = IsRegistered ? T("Jugador: ", "Player: ") + _playerName : "";

            BtnRegister.Content = IsRegistered ? T("ENTRAR", "ENTER") : T("REGISTRARME", "REGISTER");
        }

        private void SetRegStatus(string text, bool? ok)
        {
            TxtRegStatus.Text = text;
            if (ok == null)
                TxtRegStatus.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            else
                TxtRegStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ok == true ? "#00B894" : "#FF7675"));
        }

        // Registro obligatorio: sin pasar por el registro solo se puede usar Registro y Admin.
        private void ApplyRegistrationLock()
        {
            bool reg = IsUnlocked;
            BtnNavHome.IsEnabled = reg;
            BtnNavOpt.IsEnabled = reg;
            BtnNavMods.IsEnabled = reg;
            BtnNavSettings.IsEnabled = reg;
            BtnLaunch.IsEnabled = reg;

            if (!reg) ShowView(ViewRegister, BtnNavRegister);
        }

        private void ClearRegistration()
        {
            _playerName = "";
            _sessionVerified = false;
            try { if (File.Exists(UserPath)) File.Delete(UserPath); } catch { }
            TxtRegName.Clear();
            UpdateRegistrationUi();
            ApplyRegistrationLock();
        }

        private void TxtRegName_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) BtnRegister_Click(sender, e);
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
                _sessionVerified = true;
                File.WriteAllText(UserPath, JsonSerializer.Serialize(new UserFile { Name = match }, JsonOpts));
                UpdateRegistrationUi();
                SetRegStatus(T("¡Registro correcto! Entrando...", "Registration successful! Entering..."), true);

                await Task.Delay(900);
                ApplyRegistrationLock();
                ShowView(ViewHome, BtnNavHome);
                TxtRegStatus.Text = "";
                _ = RefreshHomeInfoAsync();
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
                FontSize = 14
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

            var pb = new PasswordBox { Height = 32, FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 10, 0, 16) };

            var ok = new Button { Content = T("Entrar", "Enter"), Width = 96, Height = 34, IsDefault = true, Margin = new Thickness(0, 0, 10, 0), Style = (Style)FindResource("PrimaryButton") };
            var cancel = new Button { Content = T("Cancelar", "Cancel"), Width = 96, Height = 34, IsCancel = true, Style = (Style)FindResource("SecondaryButton") };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var panel = new StackPanel { Margin = new Thickness(22) };
            panel.Children.Add(label);
            panel.Children.Add(pb);
            panel.Children.Add(buttons);

            var win = new Window
            {
                Title = "Admin",
                Width = 360,
                Height = 200,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Content = panel
            };
            win.SetResourceReference(Window.BackgroundProperty, "CardBrush");

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
                var json = JsonSerializer.Serialize(new { players = _participants.ToList() }, JsonOpts);
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

            if (!IsUnlocked)
            {
                ShowView(ViewRegister, BtnNavRegister);
                SetRegStatus(T("Primero tienes que registrarte con tu nombre de invitado.",
                               "You need to register with your guest name first."), false);
                return;
            }

            BtnLaunch.IsEnabled = false;
            PbLaunch.Visibility = Visibility.Visible;
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
                    ClearRegistration();
                    return;
                }

                // 2. Descargar (si hace falta), extraer y localizar la versión de Forge
                TxtStatus.Text = T("  Descargando / verificando instancia...", "  Downloading / checking instance...");
                string versionName = await EnsureInstanceInstalledAsync(_customPath.BasePath, s => TxtStatus.Text = s);

                // 3. Iniciar Minecraft con el nombre del jugador registrado
                TxtStatus.Text = T("  Iniciando Minecraft...", "  Starting Minecraft...");
                var session = MSession.GetOfflineSession(_playerName);

                var launchOptions = new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = _settings.RamMb,
                    ServerIp = EventServerIp
                };

                var process = await _launcher.CreateProcessAsync(versionName, launchOptions);
                process.EnableRaisingEvents = true;
                process.Exited += (_, _) => Dispatcher.Invoke(() =>
                {
                    Show();
                    BtnLaunch.IsEnabled = IsUnlocked;
                    TxtStatus.Text = T("  Sistema listo", "  System ready");
                    _ = RefreshHomeInfoAsync();
                });

                process.Start();
                started = true;
                TxtStatus.Text = T("  ¡Juego iniciado!", "  Game started!");
                if (_settings.HideOnPlay) Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al iniciar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = T("  Error al iniciar", "  Launch error");
            }
            finally
            {
                PbLaunch.Visibility = Visibility.Collapsed;
                if (!started) BtnLaunch.IsEnabled = IsUnlocked;
            }
        }

        // ═════════════════ INSTALACIÓN DEL MODPACK + FIX DE FORGE ═════════════════

        private static string GetMarkerPath(string root) => Path.Combine(root, ".elite-modpack");

        private async Task<string> EnsureInstanceInstalledAsync(string root, Action<string> report)
        {
            string markerPath = GetMarkerPath(root);
            string? installed = FindForgeVersion(root);

            // Ya instalado y sin buscar actualizaciones: no se toca la red.
            if (installed != null && File.Exists(markerPath) && !_settings.CheckUpdates)
                return installed;

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
                bool zipUpToDate = length != null && stored == length.ToString();

                // Mismo modpack que la última vez y Forge ya presente: no se vuelve a bajar nada.
                if (installed != null && zipUpToDate)
                    return installed;

                // Si el modpack ya estaba copiado pero faltaba Forge, solo se reintenta Forge.
                if (!zipUpToDate)
                {
                    report(T("  Descargando modpack...", "  Downloading modpack..."));
                    string tempZip = Path.Combine(root, "modpack-temp.zip");
                    string extractDir = Path.Combine(root, "_extract");

                    await using (var fs = File.Create(tempZip))
                    {
                        await resp.Content.CopyToAsync(fs);
                    }

                    report(T("  Extrayendo archivos...", "  Extracting files..."));
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

                    // El modpack ya está copiado: se anota para no volver a bajarlo si Forge falla.
                    File.WriteAllText(markerPath, length?.ToString() ?? "");
                }

                string? version = FindForgeVersion(root);
                if (version == null)
                {
                    // El zip no traía Forge: se instala con el instalador oficial.
                    await InstallForgeAsync(root, report);
                    version = FindForgeVersion(root);
                }

                if (version == null)
                {
                    string found = string.Join(", ", Directory.GetDirectories(root).Select(Path.GetFileName));
                    throw new Exception(
                        "Forge se instaló pero no aparece en la carpeta versions. " +
                        $"Carpetas encontradas en {root}: {found}");
                }

                return version;
            }
        }

        // Lee la versión de Forge que pide el modpack (manifest.json de CurseForge), si existe.
        private static string? DetectForgeVersion(string root)
        {
            try
            {
                string manifest = Path.Combine(root, "manifest.json");
                if (!File.Exists(manifest)) return null;

                using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                if (!doc.RootElement.TryGetProperty("minecraft", out var mc)) return null;
                if (!mc.TryGetProperty("modLoaders", out var loaders)) return null;

                foreach (var loader in loaders.EnumerateArray())
                {
                    string? id = loader.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    if (id != null && id.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
                        return id.Substring("forge-".Length);
                }
            }
            catch { /* manifest raro: se usa la versión por defecto */ }
            return null;
        }

        // Instala Forge con su instalador oficial dentro de la carpeta del juego.
        private async Task InstallForgeAsync(string root, Action<string> report)
        {
            if (_launcher == null) throw new Exception("Launcher no inicializado.");

            string forgeVersion = DetectForgeVersion(root) ?? DefaultForgeVersion;
            string installerUrl =
                $"https://maven.minecraftforge.net/net/minecraftforge/forge/{McVersion}-{forgeVersion}/forge-{McVersion}-{forgeVersion}-installer.jar";

            // 1. Minecraft vanilla (lo necesita el instalador de Forge). Esto también baja el Java de Mojang.
            report(T("  Instalando Minecraft " + McVersion + " (solo la primera vez)...",
                     "  Installing Minecraft " + McVersion + " (first time only)..."));

            string javaExe;
            var offline = MSession.GetOfflineSession("ElitePlayer");
            using (var vanilla = await _launcher.CreateProcessAsync(McVersion, new MLaunchOption { Session = offline, MaximumRamMb = 1024 }))
            {
                javaExe = vanilla.StartInfo.FileName;
            }

            // Se usa java.exe (con consola) en vez de javaw.exe para poder leer la salida del instalador.
            string javaDir = Path.GetDirectoryName(javaExe) ?? "";
            string javaConsole = Path.Combine(javaDir, "java.exe");
            if (javaDir.Length > 0 && File.Exists(javaConsole)) javaExe = javaConsole;

            // 2. El instalador exige que exista launcher_profiles.json.
            string profiles = Path.Combine(root, "launcher_profiles.json");
            if (!File.Exists(profiles))
                File.WriteAllText(profiles, "{\"profiles\":{},\"version\":3}");

            // 3. Descargar el instalador de Forge.
            report(T("  Descargando Forge " + forgeVersion + "...", "  Downloading Forge " + forgeVersion + "..."));
            string installer = Path.Combine(root, "forge-installer.jar");
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                byte[] bytes = await http.GetByteArrayAsync(installerUrl);
                await File.WriteAllBytesAsync(installer, bytes);
            }

            // 4. Ejecutarlo (descarga librerías y parchea el cliente: puede tardar varios minutos).
            report(T("  Instalando Forge (puede tardar unos minutos)...", "  Installing Forge (this may take a few minutes)..."));

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = javaExe,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-jar");
            psi.ArgumentList.Add(installer);
            psi.ArgumentList.Add("--installClient");
            psi.ArgumentList.Add(root);

            var log = new StringBuilder();
            using var proc = new System.Diagnostics.Process { StartInfo = psi };
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) lock (log) log.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (log) log.AppendLine(e.Data); };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            var exited = proc.WaitForExitAsync();
            if (await Task.WhenAny(exited, Task.Delay(TimeSpan.FromMinutes(20))) != exited)
            {
                try { proc.Kill(true); } catch { }
                throw new Exception(T("La instalación de Forge tardó demasiado y se canceló.",
                                      "The Forge installation took too long and was cancelled."));
            }

            string logPath = Path.Combine(_dataDir, "forge-install.log");
            string logText;
            lock (log) logText = log.ToString();
            try { File.WriteAllText(logPath, logText); } catch { }
            try { File.Delete(installer); } catch { }

            if (proc.ExitCode != 0)
            {
                string tail = string.Join("\n", logText.Split('\n').Where(l => l.Trim().Length > 0).TakeLast(6));
                throw new Exception(
                    T($"La instalación de Forge falló (código {proc.ExitCode}).", $"The Forge installation failed (code {proc.ExitCode}).") +
                    $"\n\n{tail}\n\nLog: {logPath}");
            }
        }

        // ═════════════════ INICIO: ESTADO DEL SERVIDOR Y ANUNCIOS ═════════════════

        // Consulta el estado con una API pública para no mostrar ni exponer la IP en la interfaz.
        private static async Task<(bool Online, int Players, int Max)?> GetServerStatusAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("ELITE-Launcher/1.0");
                string json = await http.GetStringAsync("https://api.mcsrvstat.us/3/" + EventServerIp);

                using var doc = JsonDocument.Parse(json);
                var rootEl = doc.RootElement;
                bool online = rootEl.TryGetProperty("online", out var o) && o.ValueKind == JsonValueKind.True;

                int players = 0, max = 0;
                if (online && rootEl.TryGetProperty("players", out var pl))
                {
                    if (pl.TryGetProperty("online", out var po) && po.ValueKind == JsonValueKind.Number) players = po.GetInt32();
                    if (pl.TryGetProperty("max", out var pm) && pm.ValueKind == JsonValueKind.Number) max = pm.GetInt32();
                }
                return (online, players, max);
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> FetchAnnouncementAsync()
        {
            string cache = Path.Combine(_dataDir, "announcement.cache.txt");
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                using var resp = await http.GetAsync(AnnouncementUrl + "?t=" + DateTime.UtcNow.Ticks);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    try { if (File.Exists(cache)) File.Delete(cache); } catch { }
                    return "";
                }

                resp.EnsureSuccessStatusCode();
                string text = (await resp.Content.ReadAsStringAsync()).Trim();
                if (text.Length > 600) text = text.Substring(0, 600) + "…";
                File.WriteAllText(cache, text);
                return text;
            }
            catch
            {
                try { if (File.Exists(cache)) return File.ReadAllText(cache); } catch { }
                return "";
            }
        }

        private async Task RefreshHomeInfoAsync()
        {
            if (_refreshingHome || !IsUnlocked) return;
            _refreshingHome = true;
            try
            {
                var statusTask = GetServerStatusAsync();
                var newsTask = FetchAnnouncementAsync();
                var status = await statusTask;
                string news = await newsTask;

                string dot;
                if (status == null)
                {
                    dot = "#8B9BB4";
                    TxtServerStatus.Text = T("Estado del servidor: sin datos", "Server status: unavailable");
                }
                else if (status.Value.Online)
                {
                    dot = "#00B894";
                    TxtServerStatus.Text = T($"Servidor en línea · {status.Value.Players}/{status.Value.Max} jugadores",
                                             $"Server online · {status.Value.Players}/{status.Value.Max} players");
                }
                else
                {
                    dot = "#FF7675";
                    TxtServerStatus.Text = T("Servidor apagado", "Server offline");
                }
                ElServerDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dot));

                TxtNews.Text = news;
                BdNews.Visibility = string.IsNullOrWhiteSpace(news) ? Visibility.Collapsed : Visibility.Visible;
            }
            finally
            {
                _refreshingHome = false;
            }
        }

        private void OpenLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_customPath == null) return;
                string logsDir = Path.Combine(_customPath.BasePath, "logs");
                string latest = Path.Combine(logsDir, "latest.log");
                string target = File.Exists(latest) ? latest : logsDir;

                if (!File.Exists(latest))
                {
                    Directory.CreateDirectory(logsDir);
                    MessageBox.Show(T("Todavía no hay ningún log: se crea la primera vez que juegas.",
                                      "There is no log yet: it is created the first time you play."), "ELITE");
                    return;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = target, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
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

        private void NavHome_Click(object sender, RoutedEventArgs e)
        {
            ShowView(ViewHome, BtnNavHome);
            _ = RefreshHomeInfoAsync();
        }

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

        // El estilo del botón muestra la barra de acento cuando Tag == "Active".
        private void HighlightNavButton(Button activeBtn)
        {
            foreach (var b in new[] { BtnNavHome, BtnNavRegister, BtnNavOpt, BtnNavMods, BtnNavSettings, BtnNavAdmin })
                b.Tag = ReferenceEquals(b, activeBtn) ? "Active" : null;
        }

        // ═════════════════ IDIOMA ═════════════════

        private void ApplyLanguage()
        {
            LblMenuHeader.Text = T("MENÚ PRINCIPAL", "MAIN MENU");
            BtnNavHome.Content = T("Inicio", "Home");
            BtnNavRegister.Content = T("Registro", "Register");
            BtnNavOpt.Content = T("Optimización", "Optimization");
            BtnNavMods.Content = "Mods";
            BtnNavSettings.Content = T("Ajustes", "Settings");
            BtnNavAdmin.Content = "Admin";
            LblSidebarUserTag.Text = T("JUGADOR", "PLAYER");

            LblWelcomeTag.Text = T("BIENVENIDO A ELITE", "WELCOME TO ELITE");
            LblWelcomeMain.Text = T("Tu Minecraft. A tu manera.", "Your Minecraft. Your way.");
            LblWelcomeDesc.Text = T("Selecciona tu versión de Forge preferida y entra al servidor.", "Select your preferred Forge version and join the server.");
            LblSelectVersion.Text = T("Selecciona la Versión de Forge", "Select Forge Version");
            BtnLaunch.Content = T("JUGAR ►", "PLAY ►");
            TxtStatus.Text = T("  Sistema listo", "  System ready");

            LblRegTag.Text = T("REGISTRO OBLIGATORIO", "REGISTRATION REQUIRED");
            LblRegTitle.Text = T("Regístrate", "Register");
            LblRegDesc.Text = T("Escribe el nombre de jugador con el que te invitaron al evento.", "Enter the player name you were invited with.");
            LblRegName.Text = T("Nombre de jugador", "Player name");
            TxtRegStatus.Text = "";
            LblNewsTitle.Text = T("ANUNCIOS DEL EVENTO", "EVENT ANNOUNCEMENTS");

            LblAdminTag.Text = T("PANEL DE ADMINISTRADOR", "ADMIN PANEL");
            LblAdminTitle.Text = T("Participantes invitados", "Invited participants");
            LblAdminDesc.Text = T("Solo los nombres de esta lista podrán registrarse y jugar.", "Only names on this list can register and play.");
            BtnAdminAdd.Content = T("Añadir", "Add");
            BtnAdminEdit.Content = T("Editar", "Edit");
            BtnAdminRemove.Content = T("Quitar", "Remove");
            BtnAdminExport.Content = T("Exportar whitelist.json", "Export whitelist.json");
            BtnAdminLoad.Content = T("Cargar lista online", "Load online list");

            LblSettingsTitle.Text = T("CONFIGURACIÓN", "SETTINGS");
            LblSettingsHeader.Text = T("Ajustes del Launcher", "Launcher Settings");
            LblCardAppearance.Text = T("Apariencia", "Appearance");
            LblThemeLabel.Text = T("Tema Visual", "Visual Theme");
            LblAccentLabel.Text = T("Color de acento", "Accent color");
            LblLangLabel.Text = T("Idioma / Language", "Language / Idioma");
            LblCardGame.Text = T("Juego", "Game");
            LblRamLabel.Text = T("Memoria RAM para Minecraft", "RAM for Minecraft");
            ChkHideOnPlay.Content = T("Ocultar el launcher mientras juegas", "Hide the launcher while playing");
            ChkCheckUpdates.Content = T("Buscar actualizaciones del modpack al jugar", "Check for modpack updates when playing");
            LblCardMaint.Text = T("Mantenimiento", "Maintenance");
            BtnOpenFolder.Content = T("Abrir carpeta del juego", "Open game folder");
            BtnReinstall.Content = T("Reinstalar modpack", "Reinstall modpack");
            BtnOpenLog.Content = T("Abrir último log", "Open latest log");
            BtnChangeUser.Content = T("Cambiar de usuario", "Change user");

            UpdateRegistrationUi();
            UpdateAdminCount();
        }
    }
}
