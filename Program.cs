using System;
using Plastic;
using static Plastic.EngineFeatures;
using Environment = System.Environment;

class Program
{
    static void Main(string[] args)
    {
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
        else
        {
            Console.WriteLine($"Unknown command: {command}");
        }
    }
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
    public static string GetPluginPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string pluginPath = Path.Combine(appData, "Plastic", "Plugins");
        Directory.CreateDirectory(pluginPath);
        return pluginPath;
    }
}
