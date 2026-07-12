# Fix: Remove White Outline + Add Drop Shadow

## File Changed (1)

```
Musicefy/Themes/Base.xaml
```

## Root Cause

The window used `WindowChrome` for a custom title bar but did NOT set `WindowStyle="None"` or `AllowsTransparency="True"`. This means WPF still drew the **default Windows window border** under the custom chrome — a thin white/light-grey line around the entire window edge.

Additionally, the window template had `BorderBrush="{DynamicResource BorderBrush}"` with `BorderThickness="1"` — drawing a SECOND visible border line on top of the Windows one.

## Fix (3 changes in Base.xaml)

### 1. Remove the default Windows border
Added to `EchoCustomWindowStyle`:
```xml
<Setter Property="WindowStyle" Value="None"/>
<Setter Property="AllowsTransparency" Value="True"/>
<Setter Property="ResizeMode" Value="CanResize"/>
```

`WindowStyle="None"` removes the default Windows border entirely.
`AllowsTransparency="True"` enables the window to have transparent areas (needed for drop shadow).

### 2. Remove the visible border line
Changed the window template's outer Border:
- `BorderBrush="{DynamicResource BorderBrush}"` → `BorderBrush="Transparent"`
- `BorderThickness="1"` → `BorderThickness="0"`

### 3. Add a drop shadow
Added a `DropShadowEffect` to the outer Border:
```xml
<Border.Effect>
    <DropShadowEffect Color="#000000" 
                      BlurRadius="12" 
                      ShadowDepth="0" 
                      Opacity="0.5" 
                      Direction="270"/>
</Border.Effect>
```

This creates a soft shadow around all 4 edges of the window (ShadowDepth=0 = centered shadow, BlurRadius=12 = soft spread).

## Testing

1. Upload `Musicefy/Themes/Base.xaml`.
2. Build and launch.
3. The white outline around the window should be GONE.
4. A soft drop shadow should appear around the window edges instead.
5. The window should still be resizable (drag any edge).
6. The custom title bar should still work (drag to move, min/max/close buttons).
