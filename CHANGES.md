# Fix: App Stays in Task Manager After Close

## File Changed (1)

```
Musicefy/App.xaml
```

## Root Cause

WPF's default `ShutdownMode` is `OnLastWindowClose`. The app starts with `SplashScreen` → opens `MainWindow` → closes `SplashScreen`. When you close `MainWindow`, WPF checks "is this the last window?" — but if any background thread or hidden window is still alive, it doesn't shut down.

## Fix

Added `ShutdownMode="OnMainWindowClose"` to `<Application>`:

```xml
<Application ... ShutdownMode="OnMainWindowClose">
```

This tells WPF: "When the MainWindow closes, shut down the ENTIRE application immediately — don't wait for other windows or threads."

## Testing

1. Upload `Musicefy/App.xaml`.
2. Build and launch.
3. Close the app → it should disappear from Task Manager immediately
