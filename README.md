# BlendHub

BlendHub is a Windows desktop application built with .NET 8 and WinUI 3 for managing Blender installations and projects. It provides an intuitive interface for downloading and managing multiple Blender versions, as well as organizing and launching Blender projects.

## Features

- **Version Manager**: Browse and download different Blender versions from web or Microsoft Store
- **Project Management**: Create, organize, and manage Blender projects
- **Multi-Version Support**: Easily switch between different Blender versions for your projects
- **Backup & Restore**: Back up and restore project files
- **Launcher**: Quick access to launch Blender projects with specific versions
- **Modern UI**: Built with WinUI 3 for a modern Windows experience

## Technology Stack

- **.NET 8**: Latest .NET runtime
- **WinUI 3**: Modern Windows UI framework
- **C#**: Primary programming language
- **XAML**: UI markup language

## Project Structure

```
BlendHub/
├── Views/              # XAML pages and their code-behind
├── Models/             # Data models and view models
├── Services/           # Business logic services
├── Helpers/            # Utility classes
├── Converters/         # XAML value converters
├── Controls/           # Custom XAML controls
├── Assets/             # Images and resources
├── Properties/         # Project publish profiles
└── Package.appxmanifest # Application manifest
```

## Key Components

### Services

- **DownloadService**: Handles Blender version downloads with progress tracking and notifications
- **BlenderSettingsService**: Manages Blender-related settings
- **LauncherSettingsService**: Manages launcher preferences

### Helpers

- **VersionHelper**: Version parsing and formatting utilities
- **FileTypeHelper**: File type detection for installers
- **WindowHelper**: Window management utilities

### Models

- **BlenderVersionGroup**: Groups related Blender versions
- **BlenderDownloadVersion**: Individual installer information
- **Project**: Project data model
- **FileLauncher**: File launch configuration

## Building

### Prerequisites

- Visual Studio 2026 or later
- .NET 8 SDK
- Windows 10 build 19041 or later

### Build Instructions

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run
```

## First Time Setup

1. Clone the repository
2. Open `BlendHub.csproj` in Visual Studio
3. Build the solution
4. Run the application

## Contributing

Contributions are welcome! Please feel free to submit pull requests or report issues.

## License

This project is provided as-is. Please check the LICENSE file for details.

## Version History

### v1.0.0 (Initial Release)
- Project creation and management
- Blender version browser
- Download management with progress tracking
- Backup and restore functionality
- Multi-project support
