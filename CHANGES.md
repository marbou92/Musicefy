# Animation Lag Fix — Cancel Overlapping Animations

## File Changed (1)

```
Musicefy/Views/MainWindow.xaml.cs
```

## Root Cause

When switching pages fast, the previous animation was still running when the next one started. Multiple DoubleAnimation objects stacked on top of each other, fighting for control of the same property = FPS drop.

Additionally:
- `this.UpdateLayout()` forced a synchronous layout pass mid-animation (expensive)
- Slide animation (`TranslateTransform`) added extra GPU work
- Duration was 330ms total (110 out + 220 in) — too long for rapid switching

## Fix

1. **Cancel in-flight animations** — Call `BeginAnimation(property, null)` at the start of `AnimateToPage` to kill any running animation before starting a new one.

2. **Removed slide animation** — Only fade now (no `TranslateTransform`). Less GPU work.

3. **Removed `UpdateLayout()`** — The synchronous layout pass was unnecessary and caused stutter.

4. **Shorter durations** — 80ms out + 120ms in = 200ms total (was 330ms). Snappier.

5. **Skip animation if same page** — If you click the same nav item, don't animate.

## Testing

1. Upload `Musicefy/Views/MainWindow.xaml.cs`.
2. Build and launch.
3. Click Home → Search → Library → Settings rapidly — no FPS drop
4. Each switch is a quick fade (200ms total)
5. No stuttering, no lag
