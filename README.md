# is-taskbar-visible

Checks the primary Windows taskbar using .NET 10.

```csharp
bool visible = SevenKeyboard.IsTaskbarVisible.Check();
```

Version 1.0.1 preserves the original heuristic: find `Shell_TrayWnd`; without
auto-hide, return its topmost bit. With auto-hide, require topmost, a cached
monitor match, and the converted client rectangle inside that monitor's work
area (inclusive edges). This is not a general window-visibility query.

The unused style read is retained. Failed client-rectangle/conversion calls
still contribute their zeroed or partially written output, exactly as in AHK.
The caller's DPI context is unchanged.

Dependencies are external project references: MonitorExGetUtils 2.x (>=2.0.0),
WinGetWhichMonitor 2.x (>=2.0.0), and GetTaskbarHwnd 1.x (>=1.0.0).
The shared `library-build` target checks minimum versions and matching majors.
Override the corresponding `*ProjectPath` or `LibraryBuildTargetsPath`
MSBuild properties when projects are elsewhere. No dependency sources are copied.

[Build and test instructions](tests/README.md).
