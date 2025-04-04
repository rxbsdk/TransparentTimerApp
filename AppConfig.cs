using System;
using System.IO;
using System.Text.Json;

namespace TransparentTimerApp
{
    public class AppConfig
    {
        // Default values
        public string ApiKey { get; set; } = string.Empty;
        public string Prompt { get; set; } = "Describe what you see in this image";
        public int TimerSeconds { get; set; } = 120;

        public static AppConfig LoadConfig()
        {
            try
            {
                string configPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "config.json");

                if (File.Exists(configPath))
                {
                    string jsonContent = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(jsonContent);

                    if (config != null)
                    {
                        if (string.IsNullOrWhiteSpace(config.ApiKey))
                        {
                            Console.WriteLine("Warning: API Key is empty in config.json");
                        }

                        if (config.TimerSeconds < 5)
                        {
                            config.TimerSeconds = 5;
                        }

                        return config;
                    }
                }

                var defaultConfig = new AppConfig();
                CreateDefaultConfigFile(configPath, defaultConfig);

                return defaultConfig;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
                return new AppConfig();
            }
        }

        private static void CreateDefaultConfigFile(string path, AppConfig defaultConfig)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string jsonString = JsonSerializer.Serialize(defaultConfig, options);
                File.WriteAllText(path, jsonString);

                Console.WriteLine($"Created default config file at: {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not create default config file: {ex.Message}");
            }
        }
    }
}