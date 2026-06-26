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
            try { checkAniEs = await RunProcessAsync("wsl", "-- command -v ani-es"); } catch { }
            bool clientsInstalled = (checkAniEs == 0);

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

                Log("Obteniendo nombre de usuario de WSL...");
                string wslUser = await GetWslUsernameAsync();
                Log($"Usuario WSL detectado: {wslUser}");
                string userHome = $"/home/{wslUser}";

                // Liberar bloqueos previos de apt/dpkg y git
                Log("Cerrando procesos bloqueados previos en WSL...");
                try
                {
                    await RunProcessAsync("wsl", "-- pkill -f git");
                    await RunProcessAsync("wsl", "-- pkill -f apt");
                    await RunProcessAsync("wsl", "-- pkill -f dpkg");
                }
                catch { }

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
                    await RunProcessAsync("wsl", "-- sudo DEBIAN_FRONTEND=noninteractive apt install python3 fzf wget curl jq grep sed git -y");
                }
                else
                {
                    await RunProcessAsync("wsl", $"-- bash -c \"echo '{escapedPassword}' | sudo -S apt update\"");
                    await RunProcessAsync("wsl", $"-- bash -c \"echo '{escapedPassword}' | sudo -S DEBIAN_FRONTEND=noninteractive apt install python3 fzf wget curl jq grep sed git -y\"");
                }

                Progress.Value = 40;
                Log("Preparando carpetas locales e instalando ani-es...");
                
                // Limpiar directorios previos
                await RunProcessAsync("wsl", $"-- rm -rf {userHome}/ani-es");
                await RunProcessAsync("wsl", $"-- mkdir -p {userHome}/ani-es");

                // Usar versión parcheada con soporte de descargas y smart player
                Log("Instalando versión parcheada de ani-es (Smart Player & Descargas)...");
                string localPatched = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ani-es-patched.sh");
                int curlEsCode = 1;
                
                if (File.Exists(localPatched))
                {
                    string wslPatched = localPatched.Replace("\\", "/");
                    if (wslPatched.Length > 2 && wslPatched[1] == ':')
                    {
                        wslPatched = $"/mnt/{char.ToLower(wslPatched[0])}{wslPatched.Substring(2)}";
                    }
                    curlEsCode = await RunProcessAsync("wsl", $"-- cp '{wslPatched}' {userHome}/ani-es/ani-es");
                }
                
                if (curlEsCode != 0)
                {
                    Log("Archivo parcheado no encontrado, usando versión original de github...");
                    curlEsCode = await RunProcessAsync("wsl", $"-- curl -sSL https://raw.githubusercontent.com/Zhuchii/ani-es/main/ani-es -o {userHome}/ani-es/ani-es");
                }
                
                int curlJsonCode = await RunProcessAsync("wsl", $"-- curl -sSL https://raw.githubusercontent.com/Zhuchii/ani-es/main/excepciones.json -o {userHome}/ani-es/excepciones.json");

                bool curlSuccess = (curlEsCode == 0 && curlJsonCode == 0);

                if (!curlSuccess)
                {
                    Log("Curl falló o no descargó completo. Intentando fallback mediante git clone (shallow)...");
                    // Limpiar directorios antes de clonar
                    await RunProcessAsync("wsl", $"-- rm -rf {userHome}/ani-es");
                    
                    int cloneEs = await RunProcessAsync("wsl", $"-- git clone --depth 1 --no-tags --single-branch https://github.com/Zhuchii/ani-es.git {userHome}/ani-es");

                    if (cloneEs != 0)
                    {
                        throw new Exception("No se pudieron descargar los scripts de ani-es por ningún método (curl/git clone). Verifica tu conexión a internet.");
                    }
                }

                Progress.Value = 50;
                Log("Instalando clientes en /usr/local/bin...");
                
                // Comandos de instalación manual (evita que install.sh ejecute apt-get otra vez)
                if (string.IsNullOrEmpty(sudoPassword))
                {
                    // Crear carpeta de datos
                    await RunProcessAsync("wsl", $"-- mkdir -p {userHome}/ani-es");
                    // Copiar ejecutables a /usr/local/bin
                    await RunProcessAsync("wsl", $"-- sudo cp {userHome}/ani-es/ani-es /usr/local/bin/ani-es");
                    await RunProcessAsync("wsl", $"-- sudo cp {userHome}/ani-es/excepciones.json /usr/local/bin/excepciones.json");
                    
                    // Permisos de ejecución
                    await RunProcessAsync("wsl", "-- sudo chmod +x /usr/local/bin/ani-es");
                    
                    // Crear base de datos de historial por defecto si no existe
                    await RunProcessAsync("wsl", $"-- touch {userHome}/ani-es/history.db");
                    await RunProcessAsync("wsl", $"-- chmod 777 {userHome}/ani-es/history.db");
                }
                else
                {
                    // Crear carpeta de datos
                    await RunProcessAsync("wsl", $"-- mkdir -p {userHome}/ani-es");
                    
                    // Copiar ejecutables usando sudo -S
                    await RunProcessAsync("wsl", $"-- bash -c \"echo '{escapedPassword}' | sudo -S cp {userHome}/ani-es/ani-es /usr/local/bin/ani-es\"");
                    await RunProcessAsync("wsl", $"-- bash -c \"echo '{escapedPassword}' | sudo -S cp {userHome}/ani-es/excepciones.json /usr/local/bin/excepciones.json\"");
                    
                    // Permisos de ejecución
                    await RunProcessAsync("wsl", $"-- bash -c \"echo '{escapedPassword}' | sudo -S chmod +x /usr/local/bin/ani-es\"");
                    
                    // Crear base de datos de historial por defecto si no existe
                    await RunProcessAsync("wsl", $"-- touch {userHome}/ani-es/history.db");
                    await RunProcessAsync("wsl", $"-- chmod 777 {userHome}/ani-es/history.db");
                }
                
                Log("ani-es instalado correctamente en WSL.");
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

        private async Task<string> GetWslUsernameAsync()
        {
            try
            {
                string result = await RunProcessWithOutputAsync("wsl", "-- whoami");
                return string.IsNullOrEmpty(result) ? "root" : result;
            }
            catch
            {
                return "root";
            }
        }

        private async Task<string> RunProcessWithOutputAsync(string fileName, string arguments)
        {
            var tcs = new TaskCompletionSource<string>();
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

            string output = "";
            process.OutputDataReceived += (s, e) => { if (e.Data != null) output += e.Data + "\n"; };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) Log($"[WSL ERROR] {e.Data}"); };

            process.Exited += (s, e) =>
            {
                tcs.SetResult(output.Trim());
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