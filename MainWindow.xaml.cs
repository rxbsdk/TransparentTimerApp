﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
// Use fully qualified names for MessageBox to avoid ambiguity
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using Mscc.GenerativeAI;

namespace TransparentTimerApp
{
    // Win32 API structures for cursor capture
    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;
        private TimeSpan remainingTime;
        private bool isTimerPaused = false;
        private bool isMouseOver = false;
        private DispatcherTimer hoverTimer;

        // Configuration
        private AppConfig config;

        // Win32 API constants for window styles
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        // Win32 API functions for click-through
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        // Win32 API functions for cursor capture
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr CopyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DrawIcon(IntPtr hdc, int x, int y, IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("gdi32.dll", SetLastError = true)]
        static extern bool DeleteGdiObject(IntPtr hObject);

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
            this.Focusable = false;

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

            // Apply click-through when window is loaded
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Make the window click-through but still receive mouse events
            WindowInteropHelper wndHelper = new WindowInteropHelper(this);

            int exStyle = GetWindowLong(wndHelper.Handle, GWL_EXSTYLE);
            // Add WS_EX_NOACTIVATE style to prevent activation/focus
            exStyle |= WS_EX_NOACTIVATE;

            // Apply the updated window style and check result
            int result = SetWindowLong(wndHelper.Handle, GWL_EXSTYLE, exStyle);

            // Check for errors
            if (result == 0)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 0)
                {
                    Console.WriteLine($"Error setting window style: {error}");
                }
            }
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
                Duration = TimeSpan.FromSeconds(0.2)
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

                // Check if screenshot was successful
                if (string.IsNullOrEmpty(screenshotPath))
                {
                    StatusText.Text = "Failed to take screenshot";
                    StatusText.Visibility = Visibility.Visible;
                    this.Opacity = 1;
                    return;
                }

                // Restore window visibility
                this.Opacity = 1;

                // Show status
                StatusText.Text = "Analyzing screenshot...";
                StatusText.Visibility = Visibility.Visible;

                try
                {
                    // Send to Gemini API using the SDK
                    string response = await SendImageToGeminiWithSDK(screenshotPath);

                    // Hide status
                    StatusText.Visibility = Visibility.Collapsed;

                    // Display response
                    GeminiResponseText.Text = response;
                    GeminiResponseText.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"API Error: {ex.Message}";
                    StatusText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
                this.Opacity = 1;
            }
        }

        private async Task<string> SendImageToGeminiWithSDK(string imagePath)
        {
            try
            {
                // Initialize Gemini client using the SDK
                var googleAI = new GoogleAI(apiKey: config.ApiKey);

                // Create the model with appropriate tools
                GenerativeModel model;

                if (config.UseGrounding && !string.IsNullOrEmpty(config.SearchApiKey))
                {
                    // Create the model with Google Search grounding enabled
                    model = googleAI.GenerativeModel(
                        model: Model.Gemini20Flash,
                        tools: [new Tool { GoogleSearch = new() }]
                    );

                    // Configure Google Search settings if needed
                    // model.UseGoogleSearch = true;  // This is implicitly set by adding the Tool
                }
                else
                {
                    // Use standard model without search
                    model = googleAI.GenerativeModel(model: Model.Gemini20Flash);
                }

                // Create the request with image and prompt
                var request = new GenerateContentRequest(config.Prompt);

                // Add the image to the request
                await request.AddMedia(imagePath);

                // Send the request to Gemini
                var response = await model.GenerateContent(request);

                // Check if response has text
                if (response != null && !string.IsNullOrEmpty(response.Text))
                {
                    return response.Text;
                }
                else
                {
                    return "No response text received from Gemini API.";
                }
            }
            catch (Exception ex)
            {
                return $"Error using Gemini SDK: {ex.Message}";
            }
        }

        private string TakeScreenshot()
        {
            try
            {
                // Get the combined bounds of all screens to capture everything
                System.Drawing.Rectangle bounds = System.Drawing.Rectangle.Empty;

                // Use explicit namespace to avoid ambiguity
                foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
                {
                    bounds = System.Drawing.Rectangle.Union(bounds, screen.Bounds);
                }

                // Create a bitmap of the combined screen size
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    // Create a graphics object from the bitmap
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        // Copy the screen to the bitmap
                        g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);

                        // Add the cursor to the bitmap
                        DrawCursorOnBitmap(g);

                        // Save the screenshot to a temporary file for API
                        string tempPath = Path.Combine(Path.GetTempPath(), "screenshot.jpg");
                        bitmap.Save(tempPath, ImageFormat.Jpeg);

                        // Save a copy to the configured folder if enabled
                        if (config.SaveScreenshots)
                        {
                            SaveScreenshotToFolder(bitmap);
                        }

                        return tempPath;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error taking screenshot: {ex.Message}");
                return string.Empty;
            }
        }

        private void DrawCursorOnBitmap(Graphics g)
        {
            try
            {
                // Get cursor info
                CURSORINFO cursorInfo = new CURSORINFO();
                cursorInfo.cbSize = Marshal.SizeOf(typeof(CURSORINFO));

                if (!GetCursorInfo(out cursorInfo))
                    return;

                // Check if cursor is showing (flags == 0x00000001)
                if (cursorInfo.flags != 1)
                    return;

                // Get a handle to the cursor
                IntPtr hCursor = CopyIcon(cursorInfo.hCursor);

                if (hCursor == IntPtr.Zero)
                    return;

                try
                {
                    // Get cursor position and hotspot info
                    ICONINFO iconInfo = new ICONINFO();
                    if (GetIconInfo(hCursor, out iconInfo))
                    {
                        try
                        {
                            // Calculate the correct position to draw the cursor
                            int x = cursorInfo.ptScreenPos.x - iconInfo.xHotspot;
                            int y = cursorInfo.ptScreenPos.y - iconInfo.yHotspot;

                            // Get the device context for our Graphics object
                            IntPtr hdc = g.GetHdc();

                            try
                            {
                                // Draw the cursor onto the bitmap
                                DrawIcon(hdc, x, y, hCursor);
                            }
                            finally
                            {
                                // Release the device context
                                g.ReleaseHdc(hdc);
                            }

                            // Clean up GDI resources
                            if (iconInfo.hbmMask != IntPtr.Zero)
                                DeleteGdiObject(iconInfo.hbmMask);

                            if (iconInfo.hbmColor != IntPtr.Zero)
                                DeleteGdiObject(iconInfo.hbmColor);
                        }
                        catch
                        {
                            // Ignore any errors during cursor drawing
                        }
                    }
                }
                finally
                {
                    // Clean up the cursor
                    DestroyIcon(hCursor);
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash - better to have a screenshot without the cursor
                // than no screenshot at all
                Console.WriteLine($"Error drawing cursor: {ex.Message}");
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private void SaveScreenshotToFolder(Bitmap screenshot)
        {
            try
            {
                // Create the screenshots directory if it doesn't exist
                string screenshotDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    config.ScreenshotFolder);

                if (!Directory.Exists(screenshotDir))
                {
                    Directory.CreateDirectory(screenshotDir);
                }

                // Create a filename with timestamp
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string filename = $"screenshot_{timestamp}.jpg";
                string fullPath = Path.Combine(screenshotDir, filename);

                // Save the screenshot
                screenshot.Save(fullPath, ImageFormat.Jpeg);

                Console.WriteLine($"Screenshot saved to: {fullPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving screenshot to folder: {ex.Message}");
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

                // Explicitly prevent focus 
                e.Handled = true;
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
            StatusText.Visibility = Visibility.Collapsed;
        }
    }
}