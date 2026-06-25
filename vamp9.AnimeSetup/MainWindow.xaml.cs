using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace vamp9.AnimeSetup
{
    public partial class MainWindow : Window
    {
        private TaskCompletionSource<string> _passwordTcs;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Storyboard fadeIn = (Storyboard)FindResource("FadeIn");
            fadeIn.Begin(this);

            try
            {
                await RunSetupFlowAsync();
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
                StatusText.Text = "Instalación fallida o cancelada.";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                CloseButton.Visibility = Visibility.Visible;
            }
        }

        private void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogText.Text += $"{DateTime.Now:HH:mm:ss} - {message}\n";
                LogScroll.ScrollToEnd();
            });
        }

        private async Task RunSetupFlowAsync()
        {
            StatusText.Text = "Analizando sistema...";
            Progress.Value = 5;

            Log("Analizando dependencias instaladas...");
            
            // Check WSL installation globally first
            bool wslInstalled = true;
            try
            {
                await RunProcessAsync("wsl", "--status");
            }
            catch
            {
                wslInstalled = false;
            }

            if (!wslInstalled)
            {
                Log("WSL no está instalado en el sistema. Solicitando instalación automática (requiere permisos de Administrador)...");
                StatusText.Text = "Instalando WSL...";
                try
                {
                    var procInfo = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = "-Command \"wsl --install -d Ubuntu\"",
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    var proc = Process.Start(procInfo);
                    proc.WaitForExit();
                    Log("Instalación de WSL ejecutada.");
                    MessageBox.Show("Se ha ejecutado la instalación de WSL. Es altamente probable que necesites REINICIAR TU PC para que los cambios surtan efecto. Si después de aceptar este mensaje el proceso falla, por favor reinicia tu computadora y vuelve a abrir el Setup.", "vamp9 Anime Suite", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    Log($"Error instalando WSL: {ex.Message}");
                    throw new Exception("No se pudo instalar WSL automáticamente. Por favor, abre PowerShell como Administrador y ejecuta: wsl --install -d Ubuntu");
                }
            }

            // Check WSL clients
            Log("Verificando clientes en WSL...");
            int checkAniEs = -1;
            int checkAniCli = -1;
            try { checkAniEs = await RunProcessAsync("wsl", "-- command -v ani-es"); } catch { }
            try { checkAniCli = await RunProcessAsync("wsl", "-- command -v ani-cli"); } catch { }
            bool clientsInstalled = (checkAniEs == 0 && checkAniCli == 0);

            // Check MPV
            string mpvExtractPath = @"C:\mpv";
            string mpvLocalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mpv");
            bool mpvInstalled = (Directory.Exists(mpvExtractPath) && File.Exists(Path.Combine(mpvExtractPath, "mpv.exe"))) ||
                                (Directory.Exists(mpvLocalPath) && File.Exists(Path.Combine(mpvLocalPath, "mpv.exe")));

            Log($"Estado de dependencias: WSL Clientes = {(clientsInstalled ? "INSTALADO" : "FALTANTE")}, Windows MPV = {(mpvInstalled ? "INSTALADO" : "FALTANTE")}");

            if (clientsInstalled && mpvInstalled)
            {
                Progress.Value = 100;
                StatusText.Text = "Todo instalado.";
                Log("¡Ya tienes todo instalado y configurado correctamente! No es necesario realizar ninguna acción.");
                return;
            }

            // Install WSL dependencies if missing
            if (!clientsInstalled)
            {
                StatusText.Text = "Instalando dependencias en WSL...";
                Log("Iniciando configuración de WSL...");

                Log("Verificando si sudo requiere contraseña en WSL...");
                int checkSudo = await RunProcessAsync("wsl", "-- sudo -n true");
                string sudoPassword = "";

                if (checkSudo != 0)
                {
                    Log("Sudo requiere contraseña. Solicitando credenciales...");
                    sudoPassword = await PromptForPasswordAsync();
                    if (string.IsNullOrEmpty(sudoPassword))
                    {
                        throw new Exception("Se canceló la introducción de la contraseña de sudo.");
                    }
                }

                string escapedPassword = sudoPassword.Replace("'", "'\\''");
                
                Log("Actualizando repositorios e instalando dependencias (python3, fzf, wget, curl, jq, git)...");
                if (string.IsNullOrEmpty(sudoPassword))
                {
                    await RunProcessAsync("wsl", "-- sudo apt update");
                    await RunProcessAsync("wsl", "-- sudo DEBIAN_FRONTEND=noninteractive apt install python3 fzf wget curl jq grep sed git yt-dlp -y");
                }
                else
                {
                    await RunProcessAsync("wsl", $"-- bash -c \"echo '{escapedPassword}' | sudo -S apt update\"");
                    await RunProcessAsync("wsl", $"-- bash -c \"echo '{escapedPassword}' | sudo -S DEBIAN_FRONTEND=noninteractive apt install python3 fzf wget curl jq grep sed git yt-dlp -y\"");
                }

                Progress.Value = 40;
                Log("Clonando repositorios ani-es y ani-cli...");
                await RunProcessAsync("wsl", "-- rm -rf ~/ani-es ~/ani-cli");
                await RunProcessAsync("wsl", "-- git clone https://github.com/Zhuchii/ani-es.git ~/ani-es");
                await RunProcessAsync("wsl", "-- git clone https://github.com/pystardust/ani-cli.git ~/ani-cli");

                Progress.Value = 50;
                Log("Instalando clientes (Español e Inglés)...");
                if (string.IsNullOrEmpty(sudoPassword))
                {
                    await RunProcessAsync("wsl", "-- bash -c \"cd ~/ani-es && chmod +x install.sh && ./install.sh\"");
                    await RunProcessAsync("wsl", "-- sudo cp ~/ani-cli/ani-cli /usr/local/bin/ani-cli && sudo chmod +x /usr/local/bin/ani-cli");
                }
                else
                {
                    await RunProcessAsync("wsl", $"-- bash -c \"echo '{escapedPassword}' | sudo -S bash -c 'cd ~/ani-es && chmod +x install.sh && ./install.sh'\"");
                    await RunProcessAsync("wsl", $"-- bash -c \"echo '{escapedPassword}' | sudo -S bash -c 'cp ~/ani-cli/ani-cli /usr/local/bin/ani-cli && chmod +x /usr/local/bin/ani-cli'\"");
                }
                Log("ani-es y ani-cli instalados correctamente en WSL.");
            }
            else
            {
                Log("ani-es ya está instalado en WSL. Omitiendo instalación en Linux.");
            }

            Progress.Value = 60;

            // Install MPV if missing
            if (!mpvInstalled)
            {
                StatusText.Text = "Configurando MPV en Windows...";
                Log("Iniciando descarga de MPV...");
                
                string mpvUrl = "https://github.com/mpv-player/mpv/releases/download/v0.41.0/mpv-v0.41.0-x86_64-pc-windows-msvc.zip";
                string mpvZipPath = Path.Combine(Path.GetTempPath(), "mpv.zip");
                string targetMpvPath = @"C:\mpv";

                try
                {
                    using (var client = new HttpClient())
                    {
                        var response = await client.GetByteArrayAsync(mpvUrl);
                        File.WriteAllBytes(mpvZipPath, response);
                    }
                    
                    Progress.Value = 80;
                    Log("Extrayendo MPV...");
                    Directory.CreateDirectory(targetMpvPath);
                    ZipFile.ExtractToDirectory(mpvZipPath, targetMpvPath, true);
                }
                catch (Exception ex)
                {
                    Log($"Fallo al escribir en C:\\mpv: {ex.Message}. Intentando en LocalAppData...");
                    targetMpvPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mpv");
                    Directory.CreateDirectory(targetMpvPath);
                    ZipFile.ExtractToDirectory(mpvZipPath, targetMpvPath, true);
                }

                Progress.Value = 90;
                Log($"MPV extraído en: {targetMpvPath}");
                Log("Configurando variables de entorno...");
                string pathEnv = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
                if (pathEnv != null && !pathEnv.Contains(targetMpvPath))
                {
                    Environment.SetEnvironmentVariable("PATH", pathEnv + ";" + targetMpvPath, EnvironmentVariableTarget.User);
                }
                mpvExtractPath = targetMpvPath;
            }
            else
            {
                Log("MPV ya está configurado. Omitiendo instalación en Windows.");
                if (!Directory.Exists(mpvExtractPath))
                {
                    mpvExtractPath = mpvLocalPath;
                }
            }

            // Write premium mpv.conf
            Log("Asegurando configuración premium en mpv.conf...");
            string appDataMpv = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mpv");
            Directory.CreateDirectory(appDataMpv);
            
            string mpvConfContent = @"profile=high-quality
scale=spline36
cscale=spline36
dscale=mitchell
hwdec=auto-safe
deband=yes
sub-font='Trebuchet MS'
sub-border-size=3.5
sub-color='#FFFFFF'
sub-shadow-color='#000000'
sub-shadow-offset=2";

            File.WriteAllText(Path.Combine(appDataMpv, "mpv.conf"), mpvConfContent);

            Progress.Value = 100;
            StatusText.Text = "¡Instalación completada!";
            Log("Todo listo. Ya puedes abrir el Dashboard.");
        }

        private async Task<string> PromptForPasswordAsync()
        {
            Dispatcher.Invoke(() =>
            {
                InstallPanel.Visibility = Visibility.Collapsed;
                PasswordPanel.Visibility = Visibility.Visible;
                PasswordInput.Focus();
            });

            _passwordTcs = new TaskCompletionSource<string>();
            return await _passwordTcs.Task;
        }

        private void ConfirmPassword_Click(object sender, RoutedEventArgs e)
        {
            string password = PasswordInput.Password;
            PasswordPanel.Visibility = Visibility.Collapsed;
            InstallPanel.Visibility = Visibility.Visible;
            _passwordTcs?.SetResult(password);
        }

        private async Task<int> RunProcessAsync(string fileName, string arguments)
        {
            var tcs = new TaskCompletionSource<int>();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (s, e) => { if (e.Data != null) Log($"[WSL] {e.Data}"); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) Log($"[WSL ERROR] {e.Data}"); };

            process.Exited += (s, e) =>
            {
                tcs.SetResult(process.ExitCode);
                process.Dispose();
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return await tcs.Task;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}