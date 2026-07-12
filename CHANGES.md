# Performance Fix — Cache Pages (No Lag on Navigation)

## Files Changed (2)

```
Musicefy/App.xaml.cs                — change page DI from Transient to Singleton
Musicefy/Views/MainWindow.xaml.cs   — cache page instances with ??= operator
```

## Root Cause

Every time you switched pages (Home → Search → Library → Settings), the app created a **brand new page instance** from scratch. This means:
1. XAML was re-parsed (expensive)
2. All data was re-loaded from database/API
3. All cover images were re-decoded from disk
4. The old page wasn't garbage collected yet (memory pressure)

Pages were registered as `AddTransient` in DI = new instance every time.

## Fix

### 1. Changed DI registration from Transient to Singleton
```csharp
// Before (creates new instance every time):
services.AddTransient<HomeControl>();
services.AddTransient<SearchControl>();
services.AddTransient<LibraryControl>();

// After (creates once, reuses):
services.AddSingleton<HomeControl>();
services.AddSingleton<SearchControl>();
services.AddSingleton<LibraryControl>();
```

### 2. Cache pages in MainWindow with ??= operator
```csharp
// Before (creates new if DI returns null):
0 => new HomeControl(),
1 => new SearchControl(),

// After (creates once, reuses cached instance):
0 => _homePage ??= new HomeControl(),
1 => _searchPage ??= new SearchControl(),
```

## Result

- First visit to each page: normal speed (creates the page)
- Subsequent visits: **instant** (reuses cached page)
- No re-parsing XAML
- No re-loading data
- No re-decoding images
- No memory pressure from old page instances

## Testing

1. Upload both files.
2. Build and launch.
3. Click Home → Search → Library → Settings → Home → Search
4. First visit to each page may take a moment (creating + loading)
5. Second visit should be INSTANT (cached)
6. No FPS drops, no lag
