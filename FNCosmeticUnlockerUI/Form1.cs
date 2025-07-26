using System;
using System.Diagnostics;
using System.Net;
using System.Windows.Forms;
using Fiddler;
using Newtonsoft.Json.Linq;

namespace FNCosmeticUnlockerUI
{
    public partial class Form1 : Form
    {
        private Button launchBackendButton;
        private Button stopBackendButton;
        private Button startPieButton;
        private static TextBox logTextBox;
        
        private Process backendProcess;

        public Form1()
        {
            InitializeComponent();
            InitializeUI();
            LoadRemoteIconAsync();
            this.FormClosing += StopBackend_Click;
            this.Shown += DownloadContent;
        }

        private async void LoadRemoteIconAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var data = await client.GetByteArrayAsync("https://pie-data.pages.dev/new_icon.ico");

                    using (var ms = new MemoryStream(data))
                    {
                        this.Icon = new Icon(ms);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load icon: " + ex.Message);
            }
        }
        
        
        private static void TryLaunchPlayInFrontEnd()
        {
            string manifestsDir = null;

            foreach (char drive in "CDEFGHIJKLMNOPQRSTUVWXYZ")
            {
                string EpicManifests = $@"{drive}:\\ProgramData\\Epic\\EpicGamesLauncher\\Data\\Manifests";
                if (Directory.Exists(EpicManifests))
                {
                    manifestsDir = EpicManifests;
                    break;
                }
            }

            if (manifestsDir == null)
            {
                AppendLog("There is no Manifests folder");
                return;
            }

            string[] itemFiles = Directory.GetFiles(manifestsDir, "*.item");
            string targetLaunchExecutable = "FortniteGame/Binaries/Win64/UnrealEditorFortnite-Win64-Shipping.exe";

            foreach (var itemFile in itemFiles)
            {
                try
                {
                    string json = File.ReadAllText(itemFile);
                    JObject root = JObject.Parse(json);

                    var launchExeToken = root["LaunchExecutable"];
                    if (launchExeToken != null)
                    {
                        string launchExe = launchExeToken.ToString();

                        if (!string.IsNullOrEmpty(launchExe) && launchExe.Contains(targetLaunchExecutable))
                        {
                            var installLocToken = root["InstallLocation"];
                            if (installLocToken != null)
                            {
                                string installLocation = installLocToken.ToString();
                                string exeFullPath = Path.Combine(installLocation, launchExe.Replace('/', Path.DirectorySeparatorChar));
                                string exeDir = Path.GetDirectoryName(exeFullPath);

                                if (Directory.Exists(exeDir))
                                {
                                    string playInFrontEndExe = Path.Combine(exeDir, "UnrealEditorFortnite-Win64-Shipping-PlayInFrontEnd.exe");
                                    
                                    if (File.Exists(playInFrontEndExe))
                                    {
                                        var startInfo = new ProcessStartInfo
                                        {
                                            FileName = playInFrontEndExe,
                                            Arguments = "-disableplugins=\"AtomVK,ValkyrieFortnite\"",
                                            UseShellExecute = false,
                                        };

                                        Process.Start(startInfo);
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"Parsing error: {ex.Message}");
                }
            }
        }


        
        private void InitializeUI()
        {
            // Form properties
            this.Text = "Fortnite Cosmetic Unlocker";
            this.Width = 400;
            this.Height = 350;
            // Launch Backend Button
            launchBackendButton = new Button();
            launchBackendButton.Text = "Launch Backend";
            launchBackendButton.Width = 150;
            launchBackendButton.Top = 20;
            launchBackendButton.Left = 30;
            launchBackendButton.Click += LaunchBackend_Click;
            this.Controls.Add(launchBackendButton);

            // Stop Backend Button
            stopBackendButton = new Button();
            stopBackendButton.Text = "Stop Backend";
            stopBackendButton.Width = 150;
            stopBackendButton.Top = 20;
            stopBackendButton.Left = 200;
            stopBackendButton.Click += StopBackend_Click;
            this.Controls.Add(stopBackendButton);

            // Start PIE Button
            startPieButton = new Button();
            startPieButton.Text = "Start PIE";
            startPieButton.Width = 320;
            startPieButton.Top = 60;
            startPieButton.Left = 30;
            startPieButton.Click += StartPie_Click;
            this.Controls.Add(startPieButton);

            // Log Output Field
            logTextBox = new TextBox();
            logTextBox.Multiline = true;
            logTextBox.ScrollBars = ScrollBars.Vertical;
            logTextBox.Width = 320;
            logTextBox.Height = 180;
            logTextBox.Top = 110;
            logTextBox.Left = 30;
            logTextBox.ReadOnly = true;
            this.Controls.Add(logTextBox);
        }
        
        private static void OnFiddlerLog(object sender, LogEventArgs e)
        { 
            AppendLog($"[FiddlerCore Log] {e.LogString}");
        }        
        private void LaunchBackend_Click(object sender, EventArgs e)
        {
            FiddlerApplication.Log.OnLogString += OnFiddlerLog;

            
            var flags = FiddlerCoreStartupFlags.DecryptSSL
                        | FiddlerCoreStartupFlags.RegisterAsSystemProxy;
            FiddlerApplication.Startup(9999, flags);
            


            FiddlerApplication.BeforeRequest += OnBeforeRequest;
            FiddlerApplication.BeforeResponse += OnBeforeResponse;
        }

        private static void OnBeforeRequest(Session oS)
        {
            try
            {
                if (oS.oRequest.headers["User-Agent"]?.Split('/')?[0] == "Fortnite")
                {
                    if (oS.PathAndQuery.StartsWith("/lightswitch/api/service/") ||
                        oS.PathAndQuery.StartsWith("/fortnite/api/game/v2/profile/") ||
                        oS.PathAndQuery.StartsWith("/api/locker/v4/"))
                    {
                        oS.fullUrl = "http://localhost:1911" + oS.PathAndQuery;
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"OnBeforeRequest error: {ex.Message}");
            }
        }

        private static void OnBeforeResponse(Session session)
        {
            // :)
        }
        
        private void StopBackend_Click(object sender, EventArgs e)
        {
            AppendLog("Stopping Backend");
            FiddlerApplication.Shutdown();
            KillFortniteProcess();
        }

        private void StartPie_Click(object sender, EventArgs e)
        {
            AppendLog("Starting PIE...");
            TryLaunchPlayInFrontEnd();
            // Your PIE start logic here
        }
        
        private static void KillFortniteProcess()
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName("UnrealEditorFortnite-Win64-Shipping-PlayInFrontEnd"))
                {
                    proc.Kill();
                    proc.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Process termination error: {ex.Message}");
            }
        }

        private static void AppendLog(string message)
        {
            if (message != null)
            {
                logTextBox.Invoke((MethodInvoker)(() =>
                {
                    logTextBox.AppendText(message + Environment.NewLine);
                }));
            }
        }

        private static void DownloadContent(object sender, EventArgs e)
        {
            string profilesPath = Path.Combine(Directory.GetCurrentDirectory(), "profiles");
            if (!Directory.Exists(profilesPath))
            {
                Directory.CreateDirectory(profilesPath);
            }

            using (WebClient webClient = new WebClient())
            {
                (string url, string fileName)[] files = new[]
                {
                    ("https://pie-data.pages.dev/profile_athena.json", "athena.json"),
                    ("https://github.com/Landmark1218/Trash/raw/refs/heads/main/campaign.json", "campaign.json"),
                    ("https://github.com/Landmark1218/Trash/raw/refs/heads/main/collection_book_people0.json", "collection_book_people0.json"),
                    ("https://github.com/Landmark1218/Trash/raw/refs/heads/main/collection_book_schematics0.json", "collection_book_schematics0.json"),
                    ("https://github.com/Landmark1218/Trash/raw/refs/heads/main/collections.json", "collections.json"),
                    ("https://github.com/Landmark1218/Trash/raw/refs/heads/main/common_core.json", "common_core.json"),
                    ("https://github.com/Landmark1218/Trash/raw/refs/heads/main/common_public.json", "common_public.json"),
                    ("https://github.com/Landmark1218/Trash/raw/refs/heads/main/creative.json", "creative.json"),
                    ("https://github.com/Landmark1218/Trash/raw/refs/heads/main/metadata.json", "metadata.json"),
                    ("https://github.com/Landmark1218/Trash/raw/refs/heads/main/outpost0.json", "outpost0.json"),
                    ("https://github.com/Landmark1218/Trash/raw/refs/heads/main/recycle_bin.json", "recycle_bin.json"),
                    ("https://github.com/Landmark1218/Trash/raw/refs/heads/main/theater0.json", "theater0.json"),
                    ("https://github.com/Landmark1218/Trash/raw/refs/heads/main/theater1.json", "theater1.json"),
                    ("https://github.com/Landmark1218/Trash/raw/refs/heads/main/theater2.json", "theater2.json"),
                };

                foreach (var (url, fileName) in files)
                {
                    string savePath = Path.Combine(profilesPath, fileName);

                    if (File.Exists(savePath))
                    {
                        AppendLog($"Skipped (already exists): {fileName}");
                        continue;
                    }

                    try
                    {
                        AppendLog($"Downloading...{url}");
                        webClient.DownloadFile(url, savePath);
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Failed to download {url}: {ex.Message}");
                    }
                }
            }
        }
    }
}
