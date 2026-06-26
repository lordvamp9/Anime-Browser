using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace vamp9.AnimeDashboard
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _mpvWatcherTimer;
        private Process _trackedMpvProcess;
        private string _lastPlayedAnime;

        public MainWindow()
        {
            InitializeComponent();

            // Inyectar rutas de MPV en el PATH del proceso actual para garantizar que WSL lo encuentre
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            string mpvPath1 = @"C:\mpv";
            string mpvPath2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mpv");
            if (!pathEnv.Contains(mpvPath1)) pathEnv += ";" + mpvPath1;
            if (!pathEnv.Contains(mpvPath2)) pathEnv += ";" + mpvPath2;
            Environment.SetEnvironmentVariable("PATH", pathEnv, EnvironmentVariableTarget.Process);

            LoadData();

            _mpvWatcherTimer = new DispatcherTimer();
            _mpvWatcherTimer.Interval = TimeSpan.FromSeconds(2);
            _mpvWatcherTimer.Tick += MpvWatcherTimer_Tick;
            _mpvWatcherTimer.Start();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseApp_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void LoadData()
        {
            var db = DatabaseManager.Load();
            WatchLaterList.ItemsSource = db.Where(x => x.IsWatchLater).ToList();
            ActiveListView.ItemsSource = db.Where(x => !x.IsWatchLater && x.LastWatched != "-").OrderByDescending(x => x.LastWatched).ToList();
            HistoryGrid.ItemsSource = db.Where(x => x.LastWatched != "-").OrderByDescending(x => x.LastWatched).ToList();
        }

        private void TriggerPageAnimation()
        {
            Storyboard pageEnter = (Storyboard)FindResource("PageEnter");
            pageEnter.Begin(ViewBrowser);
            pageEnter.Begin(ViewWatchLater);
            pageEnter.Begin(ViewActive);
            pageEnter.Begin(ViewHistory);
            pageEnter.Begin(ViewSettings);
        }

        private void HideAllViews()
        {
            ViewBrowser.Visibility = Visibility.Collapsed;
            ViewWatchLater.Visibility = Visibility.Collapsed;
            ViewActive.Visibility = Visibility.Collapsed;
            ViewHistory.Visibility = Visibility.Collapsed;
            ViewSettings.Visibility = Visibility.Collapsed;
        }

        private void Nav_Browser_Click(object sender, RoutedEventArgs e)
        {
            HideAllViews();
            ViewBrowser.Visibility = Visibility.Visible;
            TriggerPageAnimation();
        }

        private void Nav_WatchLater_Click(object sender, RoutedEventArgs e)
        {
            HideAllViews();
            LoadData();
            ViewWatchLater.Visibility = Visibility.Visible;
            TriggerPageAnimation();
        }

        private void Nav_Active_Click(object sender, RoutedEventArgs e)
        {
            HideAllViews();
            LoadData();
            ViewActive.Visibility = Visibility.Visible;
            TriggerPageAnimation();
        }

        private void Nav_History_Click(object sender, RoutedEventArgs e)
        {
            HideAllViews();
            LoadData();
            ViewHistory.Visibility = Visibility.Visible;
            TriggerPageAnimation();
        }

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                await PerformSearch(SearchBox.Text);
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                await PerformSearch(SearchBox.Text);
            }
        }

        private async Task PerformSearch(string query)
        {
            // Sanitizar query para evitar inyección de comandos (solo permitir letras, números, espacios, guiones y guiones bajos)
            string sanitizedQuery = new string(query.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_').ToArray());

            SearchLoadingText.Visibility = Visibility.Visible;
            SearchResultsGrid.ItemsSource = null;

            try
            {
                string tempScriptPath = Path.Combine(Path.GetTempPath(), "ani-es-search.sh");
                
                string scriptContent = "#!/bin/bash\n" +
                                       "query=$(echo \"$1\" | tr ' ' '_')\n" +
                                       "wget -q --user-agent=\"Mozilla/5.0 (Windows NT 10.0; Win64; x64)\" -O - \"https://jkanime.net/buscar/$query/\" | grep -oP '<h5><a\\s+href=\"[^\"]*\">\\K.*?(?=</a></h5>)' | sed 's/&quot;//g'\n";
                
                File.WriteAllText(tempScriptPath, scriptContent.Replace("\r\n", "\n"));

                // Convertir la ruta de Windows a formato WSL /mnt/d/path...
                string wslScriptPath = tempScriptPath.Replace("\\", "/");
                if (wslScriptPath.Length > 2 && wslScriptPath[1] == ':')
                {
                    char drive = char.ToLower(wslScriptPath[0]);
                    wslScriptPath = $"/mnt/{drive}{wslScriptPath.Substring(2)}";
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "wsl",
                        Arguments = $"-- bash \"{wslScriptPath}\" \"{sanitizedQuery}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                process.WaitForExit();

                var results = new List<AnimeEntry>();
                if (!string.IsNullOrEmpty(output))
                {
                    var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && trimmed != "Salir" && trimmed != "Siguiente" && trimmed != "Anterior")
                        {
                            results.Add(new AnimeEntry { Name = trimmed });
                        }
                    }
                }

                // If scraping fails or returns nothing, just add the literal search query so the user can still launch it via ani-es
                if (results.Count == 0)
                {
                    results.Add(new AnimeEntry { Name = query });
                }

                SearchResultsGrid.ItemsSource = results;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error en la búsqueda real en WSL: " + ex.Message);
            }
            finally
            {
                SearchLoadingText.Visibility = Visibility.Collapsed;
            }
        }

        private void PlayAnime_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string animeName)
            {
                _lastPlayedAnime = animeName;
                bool isContinue = ViewActive.Visibility == Visibility.Visible || ViewHistory.Visibility == Visibility.Visible;
                LaunchAnime(animeName, isContinue);
            }
        }

        private void AddWatchLater_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string animeName)
            {
                DatabaseManager.AddWatchLater(animeName);
                MessageBox.Show($"{animeName} añadido a Ver Más Tarde.", "Anime Dashboard", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LaunchAnime(string animeName, bool isContinue = false)
        {
            try
            {
                // Escapar comillas simples para evitar inyección de comandos en bash
                string escapedName = animeName.Replace("'", "'\\''");
                string client = "ani-es";
                string args = isContinue ? $"/c start wsl -e bash -ic \"{client} -c '{escapedName}'\"" : $"/c start wsl -e bash -ic \"{client} '{escapedName}'\"";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = args,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo iniciar ani-es: " + ex.Message);
            }
        }

        private void MpvWatcherTimer_Tick(object sender, EventArgs e)
        {
            if (_trackedMpvProcess == null || _trackedMpvProcess.HasExited)
            {
                var processes = Process.GetProcessesByName("mpv");
                if (processes.Length > 0)
                {
                    _trackedMpvProcess = processes[0];
                    _trackedMpvProcess.EnableRaisingEvents = true;
                    _trackedMpvProcess.Exited += TrackedMpvProcess_Exited;
                }
            }
        }

        private void TrackedMpvProcess_Exited(object sender, EventArgs e)
        {
            _trackedMpvProcess = null;
            
            Dispatcher.Invoke(() =>
            {
                ModalAnimeName.Text = _lastPlayedAnime ?? "Anime Desconocido";
                ModalOverlay.Visibility = Visibility.Visible;
            });
        }

        private void ModalYes_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastPlayedAnime))
            {
                DatabaseManager.UpdateEpisode(_lastPlayedAnime);
                LoadData();
            }
            ModalOverlay.Visibility = Visibility.Collapsed;
        }

        private void ModalNo_Click(object sender, RoutedEventArgs e)
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
        }
        private void Nav_Settings_Click(object sender, RoutedEventArgs e)
        {
            HideAllViews();
            ViewSettings.Visibility = Visibility.Visible;
            TriggerPageAnimation();
            SettingsStatusText.Visibility = Visibility.Collapsed;
            ConfigEditorOverlay.Visibility = Visibility.Collapsed;
            SettingsMainPanel.Visibility = Visibility.Visible;
        }

        private void BtnCustome_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string mpvConfContent = @"profile=high-quality
vo=gpu
hwdec=auto-safe
fs=yes
save-position-on-quit=yes
keep-open=yes
autofit=75%
cache=yes
demuxer-max-bytes=150MiB
demuxer-max-back-bytes=50MiB
sub-font='Trebuchet MS'
sub-font-size=50
sub-color='#FFFFFF'
sub-border-color='#000000'
sub-border-size=3.5
sub-shadow-offset=0
sub-spacing=0.5
sub-margin-y=45
sub-ass-override=force
sub-style-to-old=yes
embeddedfonts=no
osc=yes
osd-font='Arial'
osd-blur=0.2
osd-color='#FFFFFF'
saturation=12
contrast=2
sharpen=0.5
scale=spline36
cscale=spline36
dscale=mitchell
deband=yes
deband-iterations=4
deband-threshold=48
deband-range=16
deband-grain=5
script-opts=osc-layout=bottombar
script-opts-append=osc-seekbarstyle=bar
script-opts-append=osc-hidetimeout=2000
script-opts-append=osc-scalewindowout=1.2
script-opts-append=osc-timetotal=yes";

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                
                // 1. MPV real path (para que funcione con mpv.exe)
                string mpvPath = Path.Combine(appData, "mpv");
                Directory.CreateDirectory(mpvPath);
                File.WriteAllText(Path.Combine(mpvPath, "mpv.conf"), mpvConfContent);

                // 2. MVP path (pedido literal)
                string mvpPath = Path.Combine(appData, "mvp");
                Directory.CreateDirectory(mvpPath);
                File.WriteAllText(Path.Combine(mvpPath, "mvp.conf"), mpvConfContent);

                SettingsStatusText.Text = "¡Configuración premium guardada con éxito en AppData/mpv y AppData/mvp!";
                SettingsStatusText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar la configuración: " + ex.Message);
            }
        }

        private void BtnOpenMvpConf_Click(object sender, RoutedEventArgs e)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string mpvConfPath = Path.Combine(appData, "mpv", "mpv.conf");

            // Si el archivo no existe, generarlo primero con el default
            if (!File.Exists(mpvConfPath))
            {
                BtnCustome_Click(null, null);
            }

            if (File.Exists(mpvConfPath))
            {
                ConfigTextBox.Text = File.ReadAllText(mpvConfPath);
            }
            else
            {
                ConfigTextBox.Text = "";
            }

            SettingsMainPanel.Visibility = Visibility.Collapsed;
            ConfigEditorOverlay.Visibility = Visibility.Visible;
            ConfigTextBox.Focus();
        }

        private void ConfigTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                try
                {
                    string updatedContent = ConfigTextBox.Text;
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                    // Guardar en la ruta real oficial de MPV
                    string mpvPath = Path.Combine(appData, "mpv");
                    Directory.CreateDirectory(mpvPath);
                    File.WriteAllText(Path.Combine(mpvPath, "mpv.conf"), updatedContent);

                    // Guardar en la ruta literal 'mvp' y 'mvp.conf'
                    string mvpPath = Path.Combine(appData, "mvp");
                    Directory.CreateDirectory(mvpPath);
                    File.WriteAllText(Path.Combine(mvpPath, "mvp.conf"), updatedContent);

                    SettingsStatusText.Text = "¡Configuración manual guardada con éxito!";
                    SettingsStatusText.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al guardar la configuración: " + ex.Message);
                }
                finally
                {
                    ConfigEditorOverlay.Visibility = Visibility.Collapsed;
                    SettingsMainPanel.Visibility = Visibility.Visible;
                }
            }
        }
    }
}