using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace AndroidToolkit
{
    public partial class ToolkitForm : Form
    {
        private string adbPath;
        private string tempFolderPath;
        private Process adbShellProcess;
        private bool isShellActive = false;
        private System.Windows.Forms.Timer deviceCheckTimer;
        private string lastConnectedDevice;

        public ToolkitForm()
        {
            InitializeComponent();

            tempFolderPath = Path.Combine(Path.GetTempPath(), "AndroidToolkit");
            Directory.CreateDirectory(tempFolderPath);
            adbPath = Path.Combine(tempFolderPath, "adb.exe");

            textBox1.KeyDown += textBox1_KeyDown;
            richTextBox1.LinkClicked += richTextBox1_LinkClicked;

            richTextBox1.DetectUrls = true;
        }

        private void richTextBox1_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            try
            { 
                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = e.LinkText,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log($"Exception opening link: {ex.Message}", LogLevel.Error);
            }
        }

        private void ExtractResourceToFile(string resourceName, string outputPath)
        {
            try
            {
                using (var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (resourceStream == null)
                    {
                        throw new FileNotFoundException($"Resource '{resourceName}' not found.");
                    }
                    using (var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Exception extracting resource '{resourceName}': {ex.Message}", LogLevel.Error);
            }
        }


        private void ExtractEmbeddedResources()
        {
       
            var resources = new Dictionary<string, string>
            {
                { "AndroidToolkit.adb.exe", "adb.exe" },
                { "AndroidToolkit.fastboot.exe", "fastboot.exe" },
                { "AndroidToolkit.AdbWinApi.dll", "AdbWinApi.dll" },
                { "AndroidToolkit.AdbWinUsbApi.dll", "AdbWinUsbApi.dll" }
            };

          
            foreach (var resource in resources)
            {
                string resourceName = resource.Key;
                string outputPath = Path.Combine(tempFolderPath, resource.Value);

                if (!File.Exists(outputPath))
                {
                    ExtractResourceToFile(resourceName, outputPath);
                }
            }
        }

        private void DeleteExtractedFiles()
        {
       
            string[] filePaths = {
                Path.Combine(tempFolderPath, "adb.exe"),
                Path.Combine(tempFolderPath, "fastboot.exe"),
                Path.Combine(tempFolderPath, "AdbWinApi.dll"),
                Path.Combine(tempFolderPath, "AdbWinUsbApi.dll")
            };

            foreach (string filePath in filePaths)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        Log($"Deleted file: {filePath}", LogLevel.Info);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Failed to delete file: {filePath}. Exception: {ex.Message}", LogLevel.Error);
                }
            }

            
            try
            {
                if (Directory.Exists(tempFolderPath))
                {
                    Directory.Delete(tempFolderPath, true);
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to delete temp folder: {tempFolderPath}. Exception: {ex.Message}", LogLevel.Error);
            }
        }

        private async void ToolkitForm_Load(object sender, EventArgs e)
        {
            ExtractEmbeddedResources();

            if (!File.Exists(adbPath))
            {
                Log("ADB executable not found.", LogLevel.Error);
                return;
            }

            InitializeDeviceCheck();

            await StartAdbServerAsync();
        }
        private void InitializeDeviceCheck()
        {
            deviceCheckTimer = new System.Windows.Forms.Timer();
            deviceCheckTimer.Interval = 5000;
            deviceCheckTimer.Tick += DeviceCheckTimer_Tick;
            deviceCheckTimer.Start();
        }

        private async void DeviceCheckTimer_Tick(object sender, EventArgs e)
        {
            await CheckConnectedDevicesAsync();
        }

        private async Task CheckConnectedDevicesAsync()
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = "devices",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(processStartInfo))
                {
                    await process.WaitForExitAsync();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string errors = await process.StandardError.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(output))
                    {
                        Log($"Device Check Output:\n{output}", LogLevel.Info);

                        string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                        if (lines.Length > 1)
                        {
                            string deviceId = lines[1].Split('\t')[0];
                            if (!deviceId.Equals(lastConnectedDevice, StringComparison.OrdinalIgnoreCase))
                            {
                                lastConnectedDevice = deviceId;
                                await LoadDeviceInfoAsync(deviceId);

                                
                                deviceCheckTimer.Stop();
                            }
                        }
                        else
                        {
                            if (lastConnectedDevice != null)
                            {
                                Log("No devices connected.", LogLevel.Warning);
                                lastConnectedDevice = null;

                                
                                deviceCheckTimer.Start();
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(errors))
                    {
                        Log($"Device Check Errors:\n{errors}", LogLevel.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Exception checking connected devices: {ex.Message}", LogLevel.Error);
            }
        }


        private async Task LoadDeviceInfoAsync(string deviceId)
        {
            string[] commands = {
        "shell getprop ro.product.model",
        "shell getprop ro.build.version.release",
        "shell getprop ro.build.version.sdk",
        "shell getprop ro.product.manufacturer",
        "shell getprop ro.serialno"
    };

            string[] labels = {
        "Model",
        "Android Version",
        "SDK Version",
        "Manufacturer",
        "Serial Number"
    };

            Log($"Loading device information for {deviceId}...", LogLevel.Info);

            for (int i = 0; i < commands.Length; i++)
            {
                string output = await RunAdbCommandWithOutputAsync(commands[i], deviceId);
                if (!string.IsNullOrEmpty(output))
                {
                    Log($"{labels[i]}: {output.Trim()}", LogLevel.Info);
                }
            }

            
            await CheckAdvancedDeviceInfoAsync(deviceId);

            
            Log("**Note:** All commands already have the 'adb' prefix. You only need to type the rest of the command (e.g., `devices`, `shell`, etc.).", LogLevel.Info);
            Log("For more information about this tool, type the `info` command.", LogLevel.Info);

        }

        private async Task CheckAdvancedDeviceInfoAsync(string deviceId)
        {
            Log("Performing advanced device checks...", LogLevel.Info);

            
            string rootCheck = await RunAdbCommandWithOutputAsync("shell which su", deviceId);
            if (!string.IsNullOrEmpty(rootCheck))
            {
                Log("Device is Rooted.", LogLevel.Info);
            }
            else
            {
                Log("Device is not Rooted.", LogLevel.Warning);
            }

            
            await CheckBootloaderStatusAsync(deviceId);

           
            string oemUnlockStatus = await RunAdbCommandWithOutputAsync("shell getprop sys.oem_unlock_allowed", deviceId);
            if (oemUnlockStatus.Trim() == "1")
            {
                Log("OEM Unlock is Enabled.", LogLevel.Info);
            }
            else
            {
                Log("OEM Unlock is Disabled.", LogLevel.Warning);
            }

          
            string suCheck = await RunAdbCommandWithOutputAsync("shell su -c \"id\"", deviceId);
            if (suCheck.Contains("uid=0(root)"))
            {
                Log("Device has SU rights.", LogLevel.Info);
            }
            else
            {
                Log("Device does not have SU rights.", LogLevel.Warning);
            }
        }

        private async Task CheckBootloaderStatusAsync(string deviceId)
        {
            Log("Checking bootloader status...", LogLevel.Info);

            
            string bootloaderStatus = await RunAdbCommandWithOutputAsync("shell getprop ro.bootloader", deviceId);
            if (!string.IsNullOrEmpty(bootloaderStatus))
            {
                Log($"Bootloader Status (ro.bootloader): {bootloaderStatus.Trim()}", LogLevel.Info);
            }
            else
            {
                Log("Bootloader Status (ro.bootloader): Unknown", LogLevel.Warning);
            }

          
            string secureBootLockState = await RunAdbCommandWithOutputAsync("shell getprop ro.secureboot.lockstate", deviceId);
            if (!string.IsNullOrEmpty(secureBootLockState))
            {
                Log($"Bootloader Lock State (ro.secureboot.lockstate): {secureBootLockState.Trim()}", LogLevel.Info);
            }
            else
            {
                Log("Bootloader Lock State (ro.secureboot.lockstate): Unknown", LogLevel.Warning);
            }

         
            string flashLockState = await RunAdbCommandWithOutputAsync("shell getprop ro.boot.flash.locked", deviceId);
            if (!string.IsNullOrEmpty(flashLockState))
            {
                Log($"Flash Lock State (ro.boot.flash.locked): {(flashLockState.Trim() == "0" ? "Unlocked" : "Locked")}", LogLevel.Info);
            }
            else
            {
                Log("Flash Lock State (ro.boot.flash.locked): Unknown", LogLevel.Warning);
            }

          
            string verifiedBootState = await RunAdbCommandWithOutputAsync("shell getprop ro.boot.verifiedbootstate", deviceId);
            if (!string.IsNullOrEmpty(verifiedBootState))
            {
                Log($"Verified Boot State (ro.boot.verifiedbootstate): {verifiedBootState.Trim()}", LogLevel.Info);
            }
            else
            {
                Log("Verified Boot State (ro.boot.verifiedbootstate): Unknown", LogLevel.Warning);
            }

        
            string oemUnlockSupported = await RunAdbCommandWithOutputAsync("shell getprop ro.oem_unlock_supported", deviceId);
            if (!string.IsNullOrEmpty(oemUnlockSupported))
            {
                Log($"OEM Unlock Supported (ro.oem_unlock_supported): {oemUnlockSupported.Trim()}", LogLevel.Info);
            }
            else
            {
                Log("OEM Unlock Supported (ro.oem_unlock_supported): Unknown", LogLevel.Warning);
            }
        }


        private async Task<string> RunAdbCommandWithOutputAsync(string command, string deviceId)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = $"-s {deviceId} {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(processStartInfo))
                {
                    await process.WaitForExitAsync();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string errors = await process.StandardError.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(errors))
                    {
                        Log($"Command '{command}' Errors:\n{errors}", LogLevel.Error);
                    }

                    return output;
                }
            }
            catch (Exception ex)
            {
                Log($"Exception running command '{command}': {ex.Message}", LogLevel.Error);
                return null;
            }
        }



        private async Task StartAdbServerAsync()
        {
            try
            {
                Log("Starting ADB server...", LogLevel.Info);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = "start-server",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(processStartInfo))
                {
                  
                    await process.WaitForExitAsync();

         
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string errors = await process.StandardError.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(output))
                    {
                        Log($"ADB Server Start Output:\n{output}", LogLevel.Info);
                    }

                    if (!string.IsNullOrEmpty(errors))
                    {
                        Log($"ADB Server Start Errors:\n{errors}", LogLevel.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Exception starting ADB server: {ex.Message}", LogLevel.Error);
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            string command = textBox1.Text.Trim();
            if (!string.IsNullOrEmpty(command))
            {
                if (command.Equals("info", StringComparison.OrdinalIgnoreCase))
                {
                    Log("**Android Toolkit v1.0**", LogLevel.Info);
                    Log("This toolkit allows you to manage and interact with your Android device using ADB commands.", LogLevel.Info);
                    Log("Features include:", LogLevel.Info);
                    Log("- Start and stop ADB server", LogLevel.Info);
                    Log("- Check connected devices", LogLevel.Info);
                    Log("- Execute shell commands", LogLevel.Info);
                    Log("- Load device information", LogLevel.Info);
                    Log("- Check bootloader status", LogLevel.Info);
                    Log("- Perform advanced device checks such as root and OEM unlock status", LogLevel.Info);
                    Log("Thank you for using Android Toolkit!", LogLevel.Info);
                    Log("Temporary files used by this toolkit are stored in the following directory and are deleted after toolkit is terminated:", LogLevel.Info);
                    Log($"    {tempFolderPath}", LogLevel.Info);
                    Log("To report a bug or discuss any issues, please visit my GitHub profile: https://github.com/adelheid2357894357846", LogLevel.Info);
                }
                else if (isShellActive)
                {
                    ExecuteShellCommand(command);
                }
                else
                {
                    if (command.Equals("shell", StringComparison.OrdinalIgnoreCase))
                    {
                        await StartAdbShellAsync();
                    }
                    else
                    {
                        await RunAdbCommandAsync(command);
                    }
                }
                textBox1.Text = string.Empty;
            }
            else
            {
                Log("Please enter an ADB command.", LogLevel.Warning);
            }
        }



        private async Task RunAdbCommandAsync(string command)
        {
            try
            {
                Log($"Executing command: {command}", LogLevel.Info);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(processStartInfo))
                {
                    await process.WaitForExitAsync();
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string errors = await process.StandardError.ReadToEndAsync();
                    if (!string.IsNullOrEmpty(output))
                    {
                        Log($"Output:\n{output}", LogLevel.Info);
                    }

                    if (!string.IsNullOrEmpty(errors))
                    {
                        Log($"Errors:\n{errors}", LogLevel.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Exception: {ex.Message}", LogLevel.Error);
            }
        }

        private async Task StartAdbShellAsync()
        {
            try
            {
                Log("Starting ADB shell...", LogLevel.Info);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = "shell",
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                adbShellProcess = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true };
                adbShellProcess.OutputDataReceived += (sender, e) => Log(e.Data, LogLevel.Info);
                adbShellProcess.ErrorDataReceived += (sender, e) => Log(e.Data, LogLevel.Error);

                adbShellProcess.Start();
                adbShellProcess.BeginOutputReadLine();
                adbShellProcess.BeginErrorReadLine();

                isShellActive = true;

                Log("ADB shell started. You can now enter shell commands.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Log($"Exception starting ADB shell: {ex.Message}", LogLevel.Error);
            }
        }

        private void ExecuteShellCommand(string command)
        {
            try
            {
                Log($"Executing shell command: {command}", LogLevel.Info);

                if (command.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                  
                    Log("Exiting ADB shell and returning to normal mode.", LogLevel.Info);
                    CloseAdbShell();
                    return;
                }
          
                adbShellProcess.StandardInput.WriteLine(command);
                adbShellProcess.StandardInput.Flush();
            }
            catch (Exception ex)
            {
                Log($"Exception executing shell command: {ex.Message}", LogLevel.Error);
            }
        }

        private void CloseAdbShell()
        {
            try
            {
                if (adbShellProcess != null && !adbShellProcess.HasExited)
                {
                    adbShellProcess.Kill();
                    adbShellProcess.WaitForExit();
                    Log("ADB shell process terminated.", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to kill ADB shell process: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                isShellActive = false;
            }
        }

        private void KillAllAdbProcesses()
        {
            try
            {
                foreach (var process in Process.GetProcessesByName("adb"))
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit();
                        Log($"Terminated orphaned adb.exe process (PID: {process.Id}).", LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to kill adb.exe processes: {ex.Message}", LogLevel.Error);
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            CloseAdbShell();
            KillAllAdbProcesses();
            DeleteExtractedFiles();
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                button1.PerformClick();
            }
        }
        private void Log(string message, LogLevel level)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logMessage = $"[{timestamp}] [{level}] {message}\n";

            richTextBox1.Invoke((Action)(() =>
            {
                richTextBox1.AppendText(logMessage);

                richTextBox1.ScrollToCaret();
            }));
        }


        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }
}
