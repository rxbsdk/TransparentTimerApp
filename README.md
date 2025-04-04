# Transparent Timer with Gemini API Integration

A discreet, transparent timer application for Windows that takes a screenshot when the timer reaches zero and sends it to Google's Gemini API for analysis.

## Features

- Semi-transparent timer that appears at the bottom middle of your screen
- Timer pauses and hides when you hover your mouse over it
- Automatically takes a screenshot when the timer reaches zero
- Sends the screenshot to Google's Gemini API for analysis
- Displays the API response above the timer
- Click anywhere on the timer to reset it
- Fully configurable via a JSON file

## Setup Instructions

### Prerequisites

- Windows 10/11
- Visual Studio 2022 (Community edition is fine)
- .NET 6.0 SDK or newer
- A Google Gemini API key

### Configuration

The application uses a `config.json` file located in the same directory as the executable. If the file doesn't exist when the application starts, a default one will be created automatically.

Edit the `config.json` file to customize your settings:

```json
{
  "ApiKey": "YOUR_GEMINI_API_KEY_HERE",
  "Prompt": "Describe what you see in this image",
  "TimerSeconds": 120,
  "SaveScreenshots": false
}
```

- **ApiKey**: Your Google Gemini API key
- **Prompt**: The text prompt to send to Gemini along with the screenshot
- **TimerSeconds**: The timer duration in seconds (minimum 5 seconds)

Helpful prompts:
  1. Answer the question only at the top of the web page. Ignore all others. It is likely multiple choice, so say the right answer, and be absolutley sure. No more than 3 short and concise sentences.
  2. Answer the question highlighted in the blue box, ignore all others.
  3. (default prompt)

### Building and Running

1. Open the solution in Visual Studio
2. Build the solution (Build > Build Solution or press F6)
3. Run the application (Debug > Start Debugging or press F5)
4. Edit the `config.json` file created in the application's directory

## Usage

- The timer will appear at the bottom middle of your screen
- Move your mouse over the timer to hide it temporarily
- Move your mouse away from the timer to make it visible again
- Click on the timer to reset it to the configured duration
- When the timer reaches zero, a screenshot is taken and sent to Gemini
- The API response will be displayed above the timer
- You can create a shortcut to pin to start to launch the app silently
- You can create another shortcut to close all processes with the path of `C:\Windows\System32\cmd.exe /C "C:\PATH\net6.0-windows\exit.bat"`
exit.bat:
`@echo off
taskkill /F /IM TransparentTimerApp.exe 2>nul`

## Troubleshooting

- If the application doesn't start, check the `config.json` file for errors
- Make sure your Gemini API key is valid and has access to the vision models
- If you get a "SecurityException" when taking screenshots, try running the application as administrator
