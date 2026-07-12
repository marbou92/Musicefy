# Fix: Settings Sidebar Outline + Window Shadow/Animations

## Files Changed (2)

```
Musicefy/Themes/Base.xaml          — GlassFrameThickness="-1" for native shadow + animations
Musicefy/Views/SettingsPage.xaml   — remove outline from settings sidebar
```

## Fix 1: Settings sidebar outline

The settings sub-sidebar had `BorderBrush="{DynamicResource OutlineBrush}"` with `BorderThickness="0,0,1,0"` — same white line as the main sidebar. Changed to `BorderBrush="Transparent"` + `BorderThickness="0"`.

## Fix 2: Window shadow + animations

Changed `GlassFrameThickness` from `"0,0,0,1"` to `"-1"`:

- **`-1`** = extend glass frame to the ENTIRE window. The DWM draws a native drop shadow on ALL 4 sides. This is the standard approach for borderless windows with shadows in WPF.
- **`"0,0,0,1"`** = only 1px on the bottom — barely visible shadow, no side shadows.

Also added `UseAeroCaptionButtons="False"` to prevent the native caption buttons from showing (we have custom ones).

This approach:
- ✅ Native OS drop shadow on all 4 sides
- ✅ No white outline (WindowStyle="None")
- ✅ Native window animations (no AllowsTransparency)
- ✅ No WPF DropShadowEffect (removed — OS shadow replaces it)

## Testing

1. Upload both files.
2. Build and launch.
3. Settings page → no white line between sub-sidebar and content
4. Window has a native drop shadow on all 4 sides
5. Minimize → restore → slide animation works
6. No white outline around window
