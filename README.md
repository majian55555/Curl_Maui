# Curl_Maui

A cross-platform HTTP client application built with .NET MAUI that provides a graphical interface for making HTTP GET requests, similar to the command-line tool `curl`. The application allows you to send requests with custom headers and view responses in various formats (images, videos, or text).

## Features

- **HTTP GET Requests**: Send HTTP GET requests to any URL
- **Custom Headers**: Add up to 3 custom header key-value pairs to your requests
- **Response Display**: Automatically detects and displays:
  - **Images**: View image responses directly in the application
  - **Videos**: Play video responses (MP4 format) with built-in media player
  - **Text**: Display text/JSON/HTML responses in an editable editor
- **Response Information**: View detailed response information including:
  - HTTP status code
  - Content headers
  - Response headers
- **Request Timer**: Track how long each request takes to complete
- **Save Responses**: Save downloaded content (images, videos, or text) to your Downloads folder
- **Config Persistence**: Automatically saves and restores your URL and header configurations
- **Request Cancellation**: Cancel in-progress requests if needed
- **Cross-Platform**: Supports Windows, Android, iOS, and Mac Catalyst

## Requirements

- .NET 9.0 SDK or later
- Visual Studio 2022 (17.8 or later) with .NET MAUI workload, or
- Visual Studio Code with C# extension and .NET MAUI extension
- Platform-specific requirements:
  - **Windows**: Windows 10 version 17763.0 or later
  - **Mac Catalyst**: macOS 12.0 or later

## Installation

### Install Dotnet and .NET MAUI

We need Dotnet version 9

[Install .NET on macOS](https://learn.microsoft.com/en-us/dotnet/core/install/macos)

[Install MAUI on macOS](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation?view=net-maui-10.0&tabs=visual-studio-code)

### Clone the Repository

```bash
git clone <repository-url>
cd Curl_Maui
```

### Restore Dependencies

```bash
dotnet restore
```

## Building

### Build for Windows

```bash
dotnet build -f net9.0-windows10.0.19041.0 -c Release -p:RuntimeIdentifierOverride=win10-x64
```

### Publish for Windows

```bash
dotnet publish -f net9.0-windows10.0.19041.0 -c Release -p:RuntimeIdentifierOverride=win10-x64
```

### Publish for Mac Catalyst

```bash
dotnet publish -f net9.0-maccatalyst -c Release -p:CreatePackage=false
```

## Usage

1. **Enter URL**: Type or paste the URL you want to request in the "URL" field
2. **Add Headers (Optional)**: Fill in up to 3 custom header key-value pairs if needed
3. **Send Request**: Click the "Send URL request" button
4. **View Response**: 
   - Images will be displayed automatically
   - Videos will play in the built-in media player
   - Text/JSON/HTML will be shown in an editor
5. **View Headers**: Check the right panel for response code, content headers, and response headers
6. **Save Content**: Click "Save File" to save the response to your Downloads folder
7. **Cancel Request**: Use "Cancel URL request" to stop an in-progress request

### Configuration Persistence

The application automatically saves your URL and header configurations when you close the app. These settings will be restored the next time you launch the application.

## Project Structure

```
Curl_Maui/
├── Models/
│   └── Extensions.cs          # Extension methods and utility classes
├── Services/
│   ├── DialogService.cs       # Dialog and alert services
│   └── NavigationService.cs   # Navigation service
├── ViewModels/
│   └── MainPageVm.cs          # Main page view model with request logic
├── Views/
│   ├── App.xaml               # Application definition
│   ├── App.xaml.cs            # Application code-behind
│   ├── MainPage.xaml          # Main page UI
│   └── MainPage.xaml.cs       # Main page code-behind
├── Platforms/                 # Platform-specific implementations
│   ├── Android/
│   ├── iOS/
│   ├── MacCatalyst/
│   ├── Tizen/
│   └── Windows/
├── Resources/                 # App resources (images, fonts, styles)
└── MauiProgram.cs            # Application entry point and DI configuration
```

## Technologies Used

- **.NET MAUI 9.0**: Cross-platform UI framework
- **CommunityToolkit.Maui**: Community toolkit for MAUI with MediaElement support
- **NLog**: Logging framework
- **OneOf**: Discriminated union library for error handling
- **HttpClient**: For making HTTP requests

## Key Components

- **MainPageVm**: Handles HTTP requests, response processing, and content type detection
- **DialogService**: Provides platform-agnostic dialog and alert functionality
- **StaticValues**: Manages file paths for configuration and saved content
- **Extensions**: Utility extension methods for tasks, exceptions, and collections

## Notes

- Requests have a default timeout of 60 seconds
- Video responses are temporarily saved to app data directory and cleaned up on app close
- Saved files are stored in your Downloads folder with sanitized filenames based on the URL
- The application uses `ConfigureAwait(false)` throughout for better async performance

## License

MIT License

Copyright (c) 2025 Jian Ma

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
