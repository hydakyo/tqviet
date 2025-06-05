# Photobooth

This repository contains a multi-project C# solution structured for a commercial Photobooth application using the MVVM pattern.

## Solution Structure

- `Photobooth.App` - WPF application entry point.
- `Photobooth.ViewModels` - view models for UI screens.
- `Photobooth.Models` - domain models.
- `Photobooth.Services` - business logic services.
- `Photobooth.Adapters` - hardware adapters (camera, printer, etc.).
- `Photobooth.Core` - base classes such as `ViewModelBase` and `RelayCommand`.
- `Photobooth.Integration` - composition and bootstrapping.
- `Photobooth.Resources` - resource files (strings, images).

The solution file is `Photobooth/Photobooth.sln` which can be opened with Visual Studio.
