# Getting Started with Musicefy

This guide will help you set up your development environment and start contributing to Musicefy.

## 📋 Prerequisites

Before you begin, ensure you have the following installed:

### Required Software
1. **Visual Studio 2019** or later
   - [Visual Studio Community](https://visualstudio.microsoft.com/vs/community/) (free)
   - Make sure to select the **".NET desktop development"** workload during installation

2. **.NET Framework 4.7.2 SDK** or higher
   - Usually included with Visual Studio 2019+
   - [Download manually](https://dotnet.microsoft.com/download/dotnet-framework/net472) if needed

3. **Git** for version control
   - [Download Git](https://git-scm.com/)
   - Verify installation: `git --version`

### Recommended Tools
- **Visual Studio Code** (optional, for lightweight editing)
- **DB Browser for SQLite** (for database inspection and query optimization)
- **Postman** (for testing Subsonic API integration)
- **JetBrains dotPeek** (for decompiling and understanding dependencies)

## 🚀 Quick Start

### Step 1: Clone the Repository

Open a terminal or command prompt and run:

```bash
git clone https://github.com/marbou92/Musicefy.git
cd Musicefy
```

### Step 2: Open the Solution

**Using Visual Studio:**
1. Launch Visual Studio
2. Click **"Open a project or solution"** (or File → Open → Project/Solution)
3. Navigate to the `Musicefy` folder
4. Select `Musicefy.sln` and click Open

**Using Command Line:**
```bash
# From the repository root
dotnet build Musicefy.sln
```

### Step 3: Restore Dependencies

NuGet packages will automatically restore when you build, but you can also restore manually:

**In Visual Studio:**
- Right-click the solution → **Restore NuGet Packages**

**Command Line:**
```bash
dotnet restore Musicefy.sln
```

The project uses these main packages:
- **NAudio** (audio playback and processing)
- **TagLibSharp** (metadata extraction and embedded artwork)
- **Microsoft.Data.Sqlite** (local database with full indexing)
- **Dapper** (high-performance ORM for data access)
- **Newtonsoft.Json** (JSON serialization for APIs)
- **Microsoft.Extensions.DependencyInjection** (dependency injection container)

### Step 4: Build the Solution

**In Visual Studio:**
1. Set the configuration to **Debug** or **Release** (toolbar dropdown)
2. Press `Ctrl+Shift+B` or go to **Build** → **Build Solution**
3. Wait for "Build succeeded" message in the Output window

**Command Line:**
```bash
# Debug build
dotnet build Musicefy.sln --configuration Debug

# Release build
dotnet build Musicefy.sln --configuration Release
```

### Step 5: Run the Application

**In Visual Studio:**
1. Set `Musicefy` as the startup project (right-click → Set as Startup Project)
2. Press `F5` to run with debugging
3. Or press `Ctrl+F5` to run without debugging

**From File Explorer:**
- Navigate to `Musicefy/bin/Debug/net472/` or `Musicefy/bin/Release/net472/`
- Double-click `Musicefy.exe`

**Command Line:**
```bash
dotnet run --project Musicefy/Musicefy.csproj
```

## 🎯 First Run Configuration

### Setting Up Your Music Library

1. **Launch Musicefy** - The application opens to the Home view

2. **Open Settings:**
   - Click the gear icon ⚙️ in the bottom-left corner
   - Or press `Ctrl+,` (Control + Comma)

3. **Add Music Folders:**
   - Navigate to **Library** settings
   - Click **"Add Folder"**
   - Browse to a directory containing music files
   - Click **"Scan Now"** to start background indexing

4. **Monitor Scan Progress:**
   - A toast notification shows real-time progress:
     - Files processed and total count
     - Tracks added, updated, or removed
     - Current file being scanned
   - The scan runs in the background without blocking the UI
   - You can continue using the app during scanning

5. **Understand What Happens During Scan:**
   - **File Discovery**: Recursively finds all supported audio formats
   - **Change Detection**: Compares file timestamps to skip unchanged files
   - **Metadata Extraction**: Reads ID3 tags (title, artist, album, genre, year, lyrics)
   - **Artwork Caching**: Extracts embedded art or folder-level artwork (cover.jpg, folder.jpg, etc.)
   - **Database Indexing**: Stores metadata in SQLite with indexes on Artist, Album, Title, Favourites, LastPlayed
   - **User Data Preservation**: Never overwrites IsFavourite, PlayCount, or LastPlayed fields

6. **Explore Your Library:**
   - Return to the main view
   - Click **"Library"** in the navigation panel
   - Browse by folders or use Home/Search views for indexed content

### Configuring Streaming Sources (Optional)

#### YouTube Music
1. Go to **Settings** → **Sources**
2. Enable **YouTube Music** provider
3. Note: Some features may require authentication

#### Subsonic Server
1. Go to **Settings** → **Sources**
2. Click **"Add Subsonic Server"**
3. Enter your server details:
   - Server URL (e.g., `https://your-server:port`)
   - Username
   - Password
   - API version (usually auto-detected)
4. Click **"Test Connection"** to verify
5. Click **"Save"**

### Customizing Appearance

1. Go to **Settings** → **Appearance**
2. Choose from available themes:
   - Dark (default)
   - Light
   - Other custom themes
3. Adjust accent colors if desired
4. Changes apply immediately

## 📁 Understanding the Project Structure

### Solution Overview

```
Musicefy.sln
├── Musicefy (WPF Application)      - Main UI layer
├── Musicefy.Core (Class Library)   - Business logic & data access
└── Musicefy.Tests (Test Project)   - Unit tests
```

### Key Projects

#### Musicefy (UI Layer)
The WPF presentation layer following MVVM pattern:

```
Musicefy/
├── App.xaml/xaml.cs              - Application entry point & DI setup
├── Assets/                       - Icons, images, fonts
├── ViewModels/                   - MVVM view models (if used)
└── Views/                        - XAML views and code-behind
    ├── MainWindow.xaml           - Main shell with navigation
    ├── HomeControl.xaml          - Dashboard/home screen
    ├── LibraryControl.xaml       - Music library browser
    ├── NowPlayingControl.xaml    - Current track display & controls
    ├── SearchControl.xaml        - Search functionality
    ├── SettingsWindow.xaml       - Settings dialog
    ├── CreatePlaylistWindow.xaml - Playlist creation dialog
    └── *SettingsControl.xaml     - Various settings panels
```

#### Musicefy.Core (Business Logic)
Core functionality independent of UI:

```
Musicefy.Core/
├── Interfaces/                   - Service contracts
│   ├── IPlaybackService.cs
│   ├── ILibraryService.cs
│   ├── IDownloadService.cs
│   └── ...
├── Models/                       - Data models
│   ├── MusicFile.cs             - Represents a music track with metadata
│   └── StreamingSource.cs       - Streaming service configuration
├── Library/                      - Library scanning and indexing
│   └── LibraryScanner.cs        - Background scanner with progress reporting
├── Services/                     - Business services
│   ├── PlaylistManager.cs       - Playlist CRUD operations
│   ├── QueueManager.cs          - Playback queue management
│   ├── LibraryServiceImpl.cs    - Library operations with SQLite backend
│   ├── DownloadServiceImpl.cs   - Download management
│   ├── SubsonicClient.cs        - Subsonic API client
│   └── StreamingSourceManager.cs - Multi-source orchestration
└── Providers/                    - External service implementations
    └── YouTubeMusicProvider.cs   - YouTube Music integration
```

### Key Components Deep Dive

#### LibraryScanner - Background Indexing Engine
Located in `Musicefy.Core/Library/LibraryScanner.cs`:
- **Deep Scan Mode**: Recursively scans folders, extracts metadata, updates database
- **Shallow Scan Mode**: Returns immediate children for folder browsing
- **Change Detection**: Uses file LastModified timestamps to skip unchanged files
- **Batch Processing**: Commits changes in batches of 50 for optimal performance
- **Progress Reporting**: Reports `ScanProgressInfo` with percent, counts, and current file
- **Cancellation Support**: Responds to CancellationToken for graceful interruption
- **Artwork Management**: Caches embedded and folder-level artwork to temp directory

#### SQLite Database Schema
The Tracks table includes:
- **Core Metadata**: Title, Artist, Album, Year, Genre, Duration, TrackNumber
- **Technical Info**: Bitrate, FileSize, CoverPath, Lyrics, SourceUri, SourceType
- **Change Tracking**: LastModified timestamp for incremental scans
- **User Data**: IsFavourite, PlayCount, LastPlayed, IsDownloaded (preserved during updates)

**Database Indexes** (auto-created on first run):
- `idx_tracks_artist` - Fast artist sorting/filtering
- `idx_tracks_album` - Fast album sorting
- `idx_tracks_title` - Fast title search
- `idx_tracks_isfavourite` - Quick favourites view
- `idx_tracks_lastplayed` - Recently played history

#### PathToImageConverter - Lazy Image Loading
Located in `Musicefy/Converters/PathToImageConverter.cs`:
- Converts file paths to BitmapImage on-demand
- Uses `BitmapCacheOption.OnLoad` to release file handles immediately
- Calls `Freeze()` for cross-thread UI access safety
- Falls back to default_cover.png if image not found
- Handles both local file paths and pack:// URIs

### Architecture Highlights

- **Dependency Injection:** Uses Microsoft.Extensions.DependencyInjection for loose coupling
- **Repository Pattern:** Data access abstracted through interfaces (ILibraryService, etc.)
- **Event-driven:** Components communicate via events and progress reporting
- **Async/Await:** Non-blocking I/O operations throughout (scanning, database, network)
- **MVVM Pattern:** Separation of concerns between Views, ViewModels, and Models
- **Modular Design:** Core library is UI-agnostic and testable

## 🔧 Development Tips

### Building Efficiently

```bash
# Clean and rebuild
dotnet clean Musicefy.sln
dotnet build Musicefy.sln --configuration Release

# Build only specific project
dotnet build Musicefy.Core/Musicefy.Core.csproj

# Watch for changes (requires dotnet-watch)
dotnet watch --project Musicefy/Musicefy.csproj run
```

### Running Tests

```bash
# Run all tests
dotnet test Musicefy.Tests/Musicefy.Tests.csproj

# Run with coverage (requires coverlet)
dotnet test Musicefy.Tests/Musicefy.Tests.csproj /p:CollectCoverage=true
```

### Debugging Tips

1. **Enable WPF Debugging:**
   - Tools → Options → Debugging → General
   - Check "Enable Managed Source Link Support"

2. **XAML Hot Reload:**
   - Available in VS 2022+ for real-time UI updates

3. **Debug Output:**
   - Check the Output window (View → Output)
   - Select "Debug" from the dropdown

## 🐛 Common Issues & Solutions

### Issue: Build fails with "Reference not found"
**Solution:**
```bash
# Clear NuGet cache and restore
dotnet nuget locals all --clear
dotnet restore Musicefy.sln
dotnet build Musicefy.sln
```

### Issue: "Could not load file or assembly" at runtime
**Solution:**
- Ensure all projects target the same .NET Framework version (net472)
- Check that Copy Local is set correctly for references
- Rebuild the entire solution (not just incremental build)

### Issue: SQLite database errors
**Solution:**
- The database is created on first run in the app data folder
- Delete the existing database to reset: `%APPDATA%\Musicefy\music.db`
- Check file permissions on the database file
- Use DB Browser for SQLite to inspect table structure and indexes
- Verify schema migrations ran successfully (check for idx_tracks_* indexes)

### Issue: Slow library scanning or search
**Solution:**
- Ensure SQLite indexes exist (run `SELECT name FROM sqlite_master WHERE type='index'`)
- Check if metadata caching is working (scan should skip unchanged files)
- For large libraries (>10,000 tracks), consider SSD storage for faster I/O
- Monitor scan progress toast for bottlenecks

### Issue: Album art not displaying
**Solution:**
- Verify artwork cache folder exists: `%TEMP%\MusicefyArtworkCache`
- Check if embedded art exists in file (use TagLibSharp directly to test)
- Ensure folder-level artwork follows naming convention (cover.jpg, folder.jpg, etc.)
- PathToImageConverter should fall back to default_cover.png if no art found

### Issue: Audio playback not working
**Solution:**
- Verify NAudio is properly referenced
- Check Windows audio settings and default playback device
- Try different audio file formats to isolate the issue

### Issue: YouTube Music provider not working
**Solution:**
- Internet connection required
- Check firewall/antivirus settings
- Review the output log for specific error messages

## 📝 Making Your First Change

### Example: Adding a New Feature

1. **Create a feature branch:**
   ```bash
   git checkout -b feature/my-new-feature
   ```

2. **Make your changes** following the existing patterns:
   - Add new service interface in `Musicefy.Core/Interfaces/`
   - Implement the service in `Musicefy.Core/Services/`
   - Register in DI container (App.xaml.cs)
   - Create UI in `Musicefy/Views/`

3. **Write tests** (if applicable):
   - Add test class in `Musicefy.Tests/`
   - Follow naming convention: `ClassName_Method_Scenario`

4. **Test your changes:**
   - Run unit tests
   - Manually test the UI
   - Verify no regressions

5. **Commit and push:**
   ```bash
   git add .
   git commit -m "feat: add my new feature"
   git push origin feature/my-new-feature
   ```

6. **Create a Pull Request** on GitHub

## 📚 Learning Resources

### Core Technologies
- [WPF Documentation](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- [MVVM Pattern Guide](https://docs.microsoft.com/en-us/archive/msdn-magazine/2009/february/patterns-wpf-apps-with-the-model-view-viewmodel-design-pattern)
- [NAudio Tutorial](https://github.com/naudio/NAudio/wiki)
- [Dapper Documentation](https://github.com/DapperLib/Dapper#dapper)
- [Dependency Injection in .NET](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Microsoft.Data.Sqlite Documentation](https://docs.microsoft.com/en-us/dotnet/standard/data/sqlite/)
- [TagLibSharp API Reference](https://github.com/mono/taglib-sharp)

### Music & Audio
- [ID3 Tag Specification](https://id3.org/)
- [Subsonic API Reference](http://www.subsonic.org/pages/api.jsp)
- [Audio File Formats Guide](https://en.wikipedia.org/wiki/Audio_file_format)

### Performance & Optimization
- [SQLite Indexing Best Practices](https://www.sqlite.org/speed.html)
- [Async/Await Best Practices](https://docs.microsoft.com/en-us/archive/msdn-magazine/2015/july/async-programming-brownfield-async-development)
- [WPF Performance Guidelines](https://docs.microsoft.com/en-us/dotnet/framework/wpf/optimization)

### Best Practices
- [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Repository Pattern in .NET](https://docs.microsoft.com/en-us/aspnet/mvc/overview/older-versions/getting-started-with-ef-5-using-mvc-4/implementing-the-repository-and-unit-of-work-patterns)

## 🤝 Getting Help

- **GitHub Issues:** Report bugs or request features
- **Discussions:** Ask questions in GitHub Discussions (if enabled)
- **Code Review:** Study existing code to understand patterns
- **Documentation:** Check inline XML comments in the codebase

## ✅ Next Steps

Now that you're set up, consider:

1. ✅ Explore the codebase by navigating through key files
2. ✅ Run the application and test existing features
3. ✅ Read through `PlaylistManager.cs` to understand service patterns
4. ✅ Examine `LibraryControl.xaml` for UI structure
5. ✅ Write a small feature or fix a bug
6. ✅ Submit your first pull request!

---

**Happy coding!** 🎵

If you encounter any issues not covered here, please check the existing GitHub issues or create a new one with detailed information about your problem.
