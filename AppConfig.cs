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
        public string ScreenshotFolder { get; set; } = "Screenshots";
        public bool SaveScreenshots { get; set; } = true;

        // Google Search grounding settings
        public bool UseGrounding { get; set; } = false;
        public string SearchApiKey { get; set; } = string.Empty; // Can use the same API key as Gemini

        // Static method to load config
        public static AppConfig LoadConfig()
        {
            try
            {
                // Look for config.json in the same directory as the executable
                string configPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "config.json");

                // Check if file exists
                if (File.Exists(configPath))
                {
                    string jsonContent = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(jsonContent, _serializerOptions);

                    // Make sure we don't return null
                    if (config != null)
                    {
                        // Validate settings
                        if (string.IsNullOrWhiteSpace(config.ApiKey))
                        {
                            Console.WriteLine("Warning: API Key is empty in config.json");
                        }

                        // Make sure timer is at least 5 seconds
                        if (config.TimerSeconds < 5)
                        {
                            config.TimerSeconds = 5;
                        }

                        return config;
                    }
                }

                // If we reach here, we either didn't find the file or couldn't deserialize it properly
                // Return default config and create a template file for user to edit
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

        // Create a default config file for the user to edit
        // Create a static JsonSerializerOptions instance to be reused
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            WriteIndented = true
        };

        private static void CreateDefaultConfigFile(string path, AppConfig defaultConfig)
        {
            try
            {
                // Use the cached options instance
                string jsonString = JsonSerializer.Serialize(defaultConfig, _serializerOptions);
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