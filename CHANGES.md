# Fix: White Outlines + AMOLED Improvement

## Files Changed (2)

```
Musicefy/Themes/Modes/Dark.xaml      — fix BorderBrush color (white outline)
Musicefy/Themes/Modes/DarkPure.xaml  — true AMOLED (pure black, invisible borders)
```

## Fix 1: White outlines on MainWindow

**Root cause:** The window template uses `BorderBrush="{DynamicResource BorderBrush}"` with `BorderThickness="1"`. The `BorderBrush` color was `#938F99` (light grey) in Dark mode — that's the white outline you see around the window.

**Fix:** Changed `BorderBrush` in Dark.xaml from `#938F99` → `#49454F` (dark grey — barely visible, matches OutlineVariant).

## Fix 2: AMOLED mode improvement

**Before:** AMOLED surfaces were `#000000`, `#050505`, `#0A0A0A`, `#0F0F0F`, `#141414` — nearly identical to Dark mode's `#141218`, `#1D1B20`, `#211F26`. Borders were `#938F99` (same as Dark — visible white outline).

**After:**
- ALL surfaces → pure black (`#000000`) or very near-black (`#0A0A0A`, `#111111`, `#1A1A1A`)
- `BackgroundBrush` → `#000000` (was `#000000` — kept)
- `SecondaryBackgroundBrush` → `#000000` (was `#050505`)
- `TextBrush` → `#FFFFFF` (was `#E6E0E9` — brighter for maximum contrast on pure black)
- `MutedTextBrush` → `#AAAAAA` (was `#CAC4D0`)
- `BorderBrush` → `#1A1A1A` (was `#938F99` — now invisible)
- `OutlineBrush` → `#1A1A1A` (was `#938F99` — now invisible)
- `OutlineVariantBrush` → `#0A0A0A` (was `#49454F`)
- `HoverBrush` → `#0A0A0A` (was `#1A1A1A`)
- `SurfaceVariantBrush` → `#1A1A1A` (was `#49454F`)
- `OnSurfaceVariantBrush` → `#AAAAAA` (was `#CAC4D0`)

The AMOLED mode is now clearly different from Dark mode:
- Dark mode: purple-tinted dark grey surfaces + visible borders
- AMOLED mode: pure black surfaces + invisible borders + brighter text

## Testing

1. Upload both files to the repo (preserve the `Musicefy/Themes/Modes/` directory structure).
2. Build and launch.
3. Switch to Dark mode → window border should be barely visible (no white outline)
4. Switch to AMOLED mode → should be clearly pure black (not just "dark grey like Dark mode")
5. Compare Dark vs AMOLED side by side — the difference should be obvious
