# Fix: Remove Sidebar Line + Restore Window Animations

## Files Changed (2)

```
Musicefy/Themes/Base.xaml       — remove AllowsTransparency, use GlassFrameThickness for shadow
Musicefy/Views/MainWindow.xaml  — remove border between sidebar and content
```

## Fix 1: White line between navigation bar and content

**Root cause:** The sidebar Border had `BorderBrush="{DynamicResource OutlineBrush}"` with `BorderThickness="0,0,1,0"` — drawing a visible line on the right edge of the sidebar.

**Fix:** Changed to `BorderBrush="Transparent"` + `BorderThickness="0"`.

## Fix 2: Window animations removed

**Root cause:** `AllowsTransparency="True"` disables ALL native Windows window animations (minimize/restore slide, snap effects).

**Fix:** Removed `AllowsTransparency="True"`. Kept `WindowStyle="None"` (removes the border). Changed `GlassFrameThickness` from `"0"` to `"0,0,0,1"` — this extends a tiny sliver of glass frame on the bottom edge, which tells the DWM to draw the native drop shadow. This gives us:
- No visible border (WindowStyle="None")
- Native drop shadow (GlassFrameThickness)
- Native window animations (no AllowsTransparency)
- Resizable (ResizeMode="CanResize")

Also removed the `DropShadowEffect` from the template (the OS shadow replaces it).

## Testing

1. Upload both files.
2. Build and launch.
3. No white line between sidebar and content
4. No white outline around window
5. Window has a native drop shadow
6. Minimize/restore should have the native Windows slide animation
7. Window snapping (Aero Snap) should work
