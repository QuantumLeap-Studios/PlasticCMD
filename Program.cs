using Plastic;
using static Plastic.EngineFeatures;
using Environment = System.Environment;
using System.Security.Principal;
using System.Diagnostics;
using Spectre.Console;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp;

class Program
{
    public static Version Version { get; } = new Version(4, 1, 0);

    static void Main(string[] args)
    {
        if(!IsRunningAsAdmin())
        {
            Console.WriteLine("[!] PlasticCMD may crash if run without admin privileges on systems with Core Isolation (Memory Integrity) enabled.\r\n    → Please run from an Administrator Command Prompt.\r\n");
            return;
        }

        if (args.Length == 0)
        {
            Console.WriteLine("Usage: PlasticCMD run <code-or-path>");
            return;
        }

        string command = args[0];

        if (command == "run")
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Please provide a code snippet or file path.");
                return;
            }

            string input = args[1];

            bool respondWithReturn = false;
            if (args.Length > 2)
            {
                bool.TryParse(args[2], out respondWithReturn);
            }

            if (System.IO.File.Exists(input))
            {
                string code = System.IO.File.ReadAllText(input);
                Interpret(code, respondWithReturn);
            }
            else
            {
                Interpret(input, respondWithReturn);
            }
        }
        else if (command == "pluginpath")
        {
            string pluginPath = GetPluginPath();
            Console.WriteLine(pluginPath);
        }
        else if (command == "install")
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Please provide a plugin name (e.g., myplugin.pl).");
                return;
            }

            string pluginName = args[1];
            string pluginPath = GetPluginPath();
            string pluginFilePath = Path.Combine(pluginPath, pluginName);

            if (System.IO.File.Exists(pluginFilePath))
            {
                Console.WriteLine($"Plugin {pluginName} is already installed.");
                return;
            }

            try
            {
                using (var client = new System.Net.WebClient())
                {
                    string downloadUrl = $"https://quantumleapstudios.org/plastic/plugins/download.php?name={pluginName}";
                    client.DownloadFile(downloadUrl, pluginFilePath);
                    Console.WriteLine($"Plugin {pluginName} installed successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to install plugin: {ex.Message}");
            }
        }
        else if (command == "upload")
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Please provide the path to the plugin file.");
                return;
            }

            string localPath = args[1];

            if (!System.IO.File.Exists(localPath))
            {
                Console.WriteLine("File does not exist.");
                return;
            }

            string extension = Path.GetExtension(localPath).ToLower();
            if (extension != ".pl" && extension != ".plastic")
            {
                Console.WriteLine("Only .pl and .plastic files are allowed.");
                return;
            }

            try
            {
                UploadAsset(args[1]);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to upload plugin: {ex.Message}");
            }
        }
        else if (command == "version")
        {
            Console.WriteLine($"PlasticCMD Version: {Version}");
        }
        else if (command == "euid")
        {
            int euid = GetEuid();
            Console.WriteLine($"Effective UID: {euid}");
        }
        else if (command == "help")
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("  run <code-or-path> - Run a code snippet or file.");
            Console.WriteLine("  pluginpath - Get the path to the plugin directory.");
            Console.WriteLine("  install <plugin-name> - Install a plugin.");
            Console.WriteLine("  upload <file-path> - Upload a plugin file.");
            Console.WriteLine("  version - Show the version of PlasticCMD.");
            Console.WriteLine("  euid - Show the effective UID.");
            Console.WriteLine("  help - Show this help message.");
            Console.WriteLine("  compile <code-or-path> - Compile a code snippet or file to an executable.");
        }
        else if(command == "compile")
        {
            string input = args.Length > 1 ? args[1] : null;
            string outputPath = args.Length > 2 ? args[2] : "output.exe";
            string imagePath = args.Length > 3 ? args[3] : null;

            bool fullyPackaged = false;

            if (args.Length > 3 && args[3] == "fully-packaged")
            {
                fullyPackaged = true;
            }

            if (string.IsNullOrEmpty(outputPath) || !outputPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Output path must end with .exe");
                return;
            }

            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("Please provide a code snippet or file to compile.");
                return;
            }
            if (System.IO.File.Exists(input))
            {
                string code = System.IO.File.ReadAllText(input);
                Compile(code, outputPath, true, fullyPackaged, false, imagePath);
            }
            else
            {
                Compile(input, outputPath, true, fullyPackaged, false, imagePath);
            }
        }
        else
        {
            Console.WriteLine($"Unknown command: {command}");
        }
    }
    static bool IsRunningAsAdmin()
    {
        if (OperatingSystem.IsWindows())
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return getuid() == 0;
        }

        throw new PlatformNotSupportedException("Unsupported OS.");
    }

    static int GetEuid()
    {
        if (OperatingSystem.IsWindows())
            return -1;
        try
        {
            return (int)typeof(System.Environment)
                .Assembly
                .GetType("System.Environment+Unix")
                ?.GetMethod("GetEuid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.Invoke(null, null);
        }
        catch
        {
            try
            {
                return getuid();
            }
            catch
            {
                return -1;
            }
        }
    }

    [System.Runtime.InteropServices.DllImport("libc")]
    static extern int getuid();

    public static void RunWithUpdateLoop(string code, bool debugReturn = false)
    {
        bool shouldExit = false;

        InterpretResult result = Interpret(code, debugReturn);

        while (!shouldExit)
        {
            System.Threading.Thread.Sleep(16);
            shouldExit = EngineFeatures.ShouldLeave(result.PlasticEngine);
        }
    }
    public static void UploadAsset(string filePath)
    {
        using (var client = new System.Net.Http.HttpClient())
        using (var form = new System.Net.Http.MultipartFormDataContent())
        {
            var fileContent = new System.Net.Http.StreamContent(System.IO.File.OpenRead(filePath));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            form.Add(fileContent, "plugin", Path.GetFileName(filePath));

            var response = client.PostAsync("https://quantumleapstudios.org/plastic/plugins/upload.php", form).Result;

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Upload successful");
            }
            else
            {
                Console.WriteLine("Upload failed: " + response.ReasonPhrase);
            }
            Console.WriteLine("Response: " + response.Content.ReadAsStringAsync().Result);
        }
    }
    public static InterpretResult Interpret(string code, bool debugReturn = false)
    {
        return EngineFeatures.Interpret(code, debugReturn);
    }
    private static string GenerateCSharpCode(string plasticSource, bool createConsole)
    {
        var escaped = plasticSource.Replace("\"", "\"\"");

        var template = $@"
using Plastic;

namespace PlasticCompiled
{{
    public static class Program
    {{
        public static int Main(string[] args)
        {{
                var engine = new Plastic.PlasticEngine();
                string source = @""{escaped}"";
                var result = engine.Evaluate(source);
                if (result is int intResult)
                    return intResult;
                return 0;
        }}
    }}
}}
";
        return template;
    }

    public static void Compile(string code, string outputPath, bool createConsole = true, bool fullyPackaged = false, bool log = false, string iconPath = "")
    {
        string tempProjectDir = Path.Combine(Path.GetTempPath(), "PlasticBuild_" + Guid.NewGuid().ToString("N"));
        string projectName = Path.GetFileNameWithoutExtension(outputPath);
        string programFilePath = Path.Combine(tempProjectDir, "Program.cs");
        string projectFilePath = Path.Combine(tempProjectDir, projectName + ".csproj");
        string publishDir = Path.Combine(tempProjectDir, "publish");

        void LogStatus(string message, bool success = true, bool codeError = false)
        {
            string statusLabel;
            string color;

            if (codeError)
            {
                statusLabel = "ERROR";
                color = "yellow";
            }
            else if (success)
            {
                statusLabel = " OK ";
                color = "green";
            }
            else
            {
                statusLabel = "FAIL";
                color = "red";
            }

            AnsiConsole.Markup($"[[[bold {color}]{statusLabel}[/]]] {message}\n");
        }

        var engine = new Plastic.PlasticEngine();
        var errors = engine.GetErrors(code);

        if (errors.Count > 0)
        {
            foreach (var error in errors)
                LogStatus(error.ToString(), false, true);
        }
        else
        {
            LogStatus("No errors found.");
        }

        if (errors.Count > 0)
        {
            LogStatus("Compilation aborted due to errors.", false);
            AnsiConsole.MarkupLine("[red]Please fix the errors and try again.[/]");
            return;
        }

        var steps = new[]
        {
            "Creating temporary project directory...",
            "Writing Program...",
            "Generating project file...",
            "Preparing publish options...",
            "Building project, this may take a while...",
            "Build succeeded.",
            "Copying published files...",
            "Removing debug and support files...",
            "Cleaning up temporary files...",
            "Compilation process completed."
        };

        bool hasIcon = false;
        string iconIcoPath = "";

        if (!string.IsNullOrWhiteSpace(iconPath))
        {
            if (!File.Exists(iconPath))
            {
                LogStatus($"Icon file '{iconPath}' does not exist.", false);
                return;
            }

            string ext = Path.GetExtension(iconPath).ToLowerInvariant();
            if (ext != ".ico")
            {
                iconIcoPath = Path.Combine(tempProjectDir, "icon.ico");
                try
                {
                    Directory.CreateDirectory(tempProjectDir);
                    using var inputStream = File.OpenRead(iconPath);
                    using var outputStream = File.Create(iconIcoPath);
                    ConvertToIco(inputStream, outputStream, 256, 256);
                }
                catch (Exception ex)
                {
                    LogStatus($"Failed to convert icon: {ex.Message}", false);
                    return;
                }
            }
            else
            {
                iconIcoPath = iconPath;
            }
            steps[3] = "Preparing publish options with icon...";
            hasIcon = true;
        }

        try
        {
            AnsiConsole.Progress()
                .AutoClear(true)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn(),
                })
                .Start(ctx =>
                {
                    var task = ctx.AddTask("[green]Compiling...[/]", maxValue: steps.Length);

                    LogStatus(steps[0]);
                    Directory.CreateDirectory(tempProjectDir);
                    Thread.Sleep(100);
                    task.Increment(1);

                    LogStatus(steps[1]);
                    File.WriteAllText(programFilePath, GenerateCSharpCode(code, createConsole));
                    Thread.Sleep(100);
                    task.Increment(1);

                    LogStatus(steps[2]);
                    string referencePath = typeof(Plastic.PlasticEngine).Assembly.Location;
                    string iconProperty = hasIcon ? $"\n    <ApplicationIcon>{iconIcoPath}</ApplicationIcon>" : "";
                    File.WriteAllText(projectFilePath, $@"
    <Project Sdk=""Microsoft.NET.Sdk"">
      <PropertyGroup>
        <OutputType>{(createConsole ? "Exe" : "Library")}</OutputType>
        <TargetFramework>net8.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <LangVersion>latest</LangVersion>{iconProperty}
      </PropertyGroup>
      <ItemGroup>
        <Reference Include=""Plastic"">
          <HintPath>{referencePath}</HintPath>
        </Reference>
      </ItemGroup>
    </Project>
    ");
                    Thread.Sleep(100);
                    task.Increment(1);

                    LogStatus(steps[3]);
                    var args = fullyPackaged
                        ? $"publish \"{projectFilePath}\" -c Release -o \"{publishDir}\""
                        : $"publish \"{projectFilePath}\" -c Release -o \"{publishDir}\" -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true";
                    task.Increment(1);

                    LogStatus(steps[4]);
                    var psi = new ProcessStartInfo("dotnet", args)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    proc.WaitForExit();

                    string stdOut = proc.StandardOutput.ReadToEnd();
                    string stdErr = proc.StandardError.ReadToEnd();

                    if (proc.ExitCode != 0)
                    {
                        LogStatus("dotnet publish failed", false);
                        LogStatus(stdErr, false);
                        return;
                    }
                    task.Increment(1);

                    LogStatus(steps[5]);

                    if (log)
                        LogStatus(stdOut);

                    task.Increment(1);

                    LogStatus(steps[6]);
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    foreach (var file in Directory.GetFiles(publishDir))
                    {
                        string dest = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileName(file));
                        File.Copy(file, dest, overwrite: true);
                        Thread.Sleep(50);
                    }
                    if (log)
                        LogStatus($"Published to: {Path.GetDirectoryName(outputPath)}");
                    task.Increment(1);

                    if (!fullyPackaged)
                    {
                        LogStatus(steps[7]);
                        var outputDir = Path.GetDirectoryName(outputPath);
                        if (Directory.Exists(outputDir))
                        {
                            foreach (var file in Directory.GetFiles(outputDir))
                            {
                                if (file.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
                                    file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                                {
                                    File.Delete(file);
                                    Thread.Sleep(50);
                                }
                            }
                        }
                        else
                        {
                            LogStatus($"Output directory {outputDir} is invalid or missing.", false);
                        }
                    }
                    task.Increment(1);

                    LogStatus(steps[8]);
                    try
                    {
                        Directory.Delete(tempProjectDir, recursive: true);
                        if (log)
                            LogStatus("Temporary files removed.");
                    }
                    catch (Exception ex)
                    {
                        LogStatus($"Could not delete temp dir {tempProjectDir}: {ex.Message}", false);
                    }
                    task.Increment(1);

                    LogStatus(steps[9]);
                    task.Increment(1);
                });
        }
        catch (Exception ex)
        {
            LogStatus("Fatal error during compilation.", false);
            LogStatus(ex.ToString(), false);
        }
    }

    private static void ConvertToIco(Stream input, Stream output, int width, int height)
    {
        using var image = SixLabors.ImageSharp.Image.Load(input);
        image.Mutate(x => x.Resize(width, height));

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        ms.Position = 0;

        using var bw = new BinaryWriter(output);
        bw.Write((short)0);    
        bw.Write((short)1);      
        bw.Write((short)1);      

        bw.Write((byte)width);     
        bw.Write((byte)height);    
        bw.Write((byte)0);          
        bw.Write((byte)0);         
        bw.Write((short)1);         
        bw.Write((short)32);         
        bw.Write((int)ms.Length);    
        bw.Write(22);                  

        bw.Write(ms.ToArray());
    }

    public static string GetPluginPath()
    {
        string basePath;

        if (OperatingSystem.IsWindows())
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else
        {
            basePath = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        string pluginPath = Path.Combine(basePath, "Plastic", "Plugins");
        Directory.CreateDirectory(pluginPath);
        return pluginPath;
    }
}
