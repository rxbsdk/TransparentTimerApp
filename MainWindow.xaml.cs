using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TransparentTimerApp
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;
        private TimeSpan remainingTime;
        private bool isTimerPaused = false;
        private bool isMouseOver = false;
        private DispatcherTimer hoverTimer;
        private const string GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

        // Configuration
        private AppConfig config;

        public MainWindow()
        {
            InitializeComponent();

            // Load configuration
            config = AppConfig.LoadConfig();

            // Configure the window
            this.AllowsTransparency = true;
            this.WindowStyle = WindowStyle.None;
            this.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
            this.Topmost = true;
            this.ShowInTaskbar = false;

            // Position the window at the bottom middle of the screen
            this.Width = 200;
            this.Height = 200;
            this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
            this.Top = SystemParameters.PrimaryScreenHeight - this.Height - 50;

            // Initialize the timer with duration from config
            remainingTime = TimeSpan.FromSeconds(config.TimerSeconds);
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;

            // Update the UI with initial time
            UpdateTimerDisplay();

            // Start the timer
            timer.Start();

            // Create a timer to check mouse position
            hoverTimer = new DispatcherTimer();
            hoverTimer.Interval = TimeSpan.FromMilliseconds(100);
            hoverTimer.Tick += HoverTimer_Tick;
            hoverTimer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!isTimerPaused)
            {
                remainingTime = remainingTime.Subtract(TimeSpan.FromSeconds(1));
                UpdateTimerDisplay();

                // Check if timer reached zero
                if (remainingTime.TotalSeconds <= 0)
                {
                    timer.Stop();
                    TakeScreenshotAndSendToGemini();
                }
            }
        }

        private void HoverTimer_Tick(object? sender, EventArgs e)
        {
            // Get current mouse position
            System.Windows.Point mousePosition = Mouse.GetPosition(this);

            // Check if mouse is within the window boundaries
            bool isInBounds = mousePosition.X >= 0 && mousePosition.X <= this.ActualWidth &&
                             mousePosition.Y >= 0 && mousePosition.Y <= this.ActualHeight;

            // Only take action when the state changes
            if (isInBounds != isMouseOver)
            {
                isMouseOver = isInBounds;

                if (isMouseOver)
                {
                    // Mouse entered
                    isTimerPaused = true;
                    AnimateFade(1.0, 0.0);
                }
                else
                {
                    // Mouse left
                    isTimerPaused = false;
                    AnimateFade(0.0, 1.0);
                }
            }
        }

        private void AnimateFade(double from, double to)
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(0.3)
            };

            this.BeginAnimation(OpacityProperty, animation);
        }

        private void UpdateTimerDisplay()
        {
            TimerText.Text = string.Format("{0}:{1:00}",
                (int)remainingTime.TotalMinutes,
                remainingTime.Seconds);
        }

        private async void TakeScreenshotAndSendToGemini()
        {
            try
            {
                // Make the window temporarily invisible for the screenshot
                this.Opacity = 0;

                // Give UI time to update
                await Task.Delay(100);

                // Take the screenshot
                string screenshotPath = TakeScreenshot();

                // Restore window visibility
                this.Opacity = 1;

                // Send to Gemini API
                string response = await SendImageToGeminiAPI(screenshotPath);

                // Display response
                GeminiResponseText.Text = response;
                GeminiResponseText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private string TakeScreenshot()
        {
            // Capture the entire screen
            int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            // Create a bitmap of the screen size
            using (Bitmap bitmap = new Bitmap(screenWidth, screenHeight))
            {
                // Create a graphics object from the bitmap
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    // Copy the screen to the bitmap
                    g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));

                    // Save the screenshot to a temporary file
                    string tempPath = Path.Combine(Path.GetTempPath(), "screenshot.jpg");
                    bitmap.Save(tempPath, ImageFormat.Jpeg);

                    return tempPath;
                }
            }
        }

        private async Task<string> SendImageToGeminiAPI(string imagePath)
        {
            try
            {
                // Read the image and convert to base64
                byte[] imageBytes = File.ReadAllBytes(imagePath);
                string base64Image = Convert.ToBase64String(imageBytes);

                // Create the request payload
                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = config.Prompt },
                                new
                                {
                                    inline_data = new
                                    {
                                        mime_type = "image/jpeg",
                                        data = base64Image
                                    }
                                }
                            }
                        }
                    }
                };

                // Serialize the payload to JSON
                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);

                // Create the HTTP client
                using (HttpClient client = new HttpClient())
                {
                    // Create the request
                    var requestUrl = $"{GEMINI_API_URL}?key={config.ApiKey}";
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    // Send the request
                    var response = await client.PostAsync(requestUrl, content);

                    // Process the response
                    if (response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();

                        // Parse the JSON response to extract the text portion
                        using (System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(responseBody))
                        {
                            try
                            {
                                var root = doc.RootElement;
                                // Navigate to the text property in the response
                                if (root.TryGetProperty("candidates", out var candidates) &&
                                    candidates.GetArrayLength() > 0 &&
                                    candidates[0].TryGetProperty("content", out var content1) &&
                                    content1.TryGetProperty("parts", out var parts) &&
                                    parts.GetArrayLength() > 0 &&
                                    parts[0].TryGetProperty("text", out var text))
                                {
                                    return text.GetString();
                                }
                            }
                            catch
                            {
                                return "Failed to parse API response";
                            }
                        }
                    }

                    return $"API Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}";
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        // Allow dragging the window and reset timer on click
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                // First, handle window dragging
                this.DragMove();

                // Then reset the timer
                ResetTimer();
            }
        }

        private void ResetTimer()
        {
            // Reset to configured time
            remainingTime = TimeSpan.FromSeconds(config.TimerSeconds);

            // Update display
            UpdateTimerDisplay();

            // Make sure timer is running
            if (!timer.IsEnabled)
            {
                timer.Start();
            }

            // Hide any previous Gemini response
            GeminiResponseText.Visibility = Visibility.Collapsed;
        }
    }
}