# Getting Started with Musicefy

## Prerequisites

Before you start, make sure you have:

1. **Visual Studio 2019+** or **Visual Studio Community** (free)
2. **.NET Framework 4.5** (or higher) - [Download](https://dotnet.microsoft.com/download/dotnet-framework)
3. **Git** - [Download](https://git-scm.com/)

## Step 1: Clone the Repository

```bash
git clone https://github.com/marbou92/Musicefy.git
cd Musicefy
```

## Step 2: Open the Solution

1. Open Visual Studio
2. Click "Open a project or solution"
3. Navigate to the `Musicefy` folder
4. Select `Musicefy.sln`

## Step 3: Restore NuGet Packages

1. In Visual Studio, go to **Tools** → **NuGet Package Manager** → **Package Manager Console**
2. Run these commands:

```powershell
Install-Package NAudio -Version 2.2.1
Install-Package TagLibSharp -Version 2.2.0
```

Or use the NuGet Package Manager UI:
1. Right-click on the solution → **Manage NuGet Packages for Solution**
2. Search for "NAudio" and install version 2.2.1
3. Search for "TagLibSharp" and install version 2.2.0

## Step 4: Build the Solution

1. Press `Ctrl+Shift+B` or go to **Build** → **Build Solution**
2. Wait for the build to complete (should show "Build succeeded")

## Step 5: Run the Application

1. Press `F5` or click the **Start** button (green play icon)
2. The Musicefy window should open

## First Run Setup

1. Click **Add Folder** button
2. Navigate to a folder containing music files
3. The app will scan and display all music files
4. Double-click a song to start playing

## Project Structure

### Musicefy (WPF Application)
- **App.xaml** - Application configuration
- **MainWindow.xaml** - Main UI layout
- **MainWindow.xaml.cs** - UI logic

### Musicefy.Core (Class Library)
- **AudioPlayer.cs** - Handles audio playback using NAudio
- **PlaylistManager.cs** - Manages playlists and music library
- **MusicFile.cs** - Data model for music files

## Common Issues

### Issue: "NAudio not found"
**Solution:** Run `Install-Package NAudio` in Package Manager Console

### Issue: ".NET Framework 4.5 not installed"
**Solution:** Download and install from [Microsoft.com](https://dotnet.microsoft.com/download/dotnet-framework)

### Issue: Build fails with syntax errors
**Solution:** Make sure you're using C# 7.0 or later. In Visual Studio, go to **Project Properties** → **Build** → set **Language version** to latest.

## Next Steps

1. Explore the codebase
2. Read through `AudioPlayer.cs` to understand audio playback
3. Check `PlaylistManager.cs` for playlist logic
4. Modify `MainWindow.xaml` to customize the UI
5. Start adding features!

## Useful Resources

- [NAudio Documentation](https://github.com/naudio/NAudio)
- [TagLibSharp Documentation](https://github.com/mono/taglib-sharp)
- [WPF Tutorial](https://www.wpftutorial.net/)
- [C# Documentation](https://docs.microsoft.com/en-us/dotnet/csharp/)

Happy coding! 🎵
