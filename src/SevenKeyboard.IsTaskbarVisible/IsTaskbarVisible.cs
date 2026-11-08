//==============================================================
// isTaskbarVisible — Check whether the Windows taskbar is currently visible
//
// GitHub: https://github.com/SevenKeyboard/is-taskbar-visible
// Author: SevenKeyboard Ltd. (2025)
// License: MIT License
//
// Documentation / References:
//   https://learn.microsoft.com/en-us/windows/win32/shell/abm-getstate
//==============================================================

using System.Runtime.InteropServices;

namespace SevenKeyboard;

/// <summary>Applies the original primary-taskbar topmost and auto-hide heuristic.</summary>
public static class IsTaskbarVisible
{
    public static bool Check() => Check(TaskbarVisibilityNative.Instance);

    internal static bool Check(ITaskbarVisibilityNative native)
    {
        nint hwnd = native.FindTaskbar();
        if (hwnd == 0)
            return false;

        nint exStyle = native.GetWindowLong(hwnd, -20);
        _ = native.GetWindowLong(hwnd, -16); // Preserve the original unused style query.
        bool topmost = (exStyle & 0x00000008) != 0;
        bool autoHide = (native.GetAppBarState() & 0x1) != 0;
        if (!autoHide)
            return topmost;
        if (!topmost)
            return false;

        int number = native.GetMonitor(hwnd);
        if (number == 0 || native.GetWorkArea(number) is not { } work)
            return false;

        TaskbarRect rect = default;
        // AHK ignores both BOOL results and consumes the zeroed/partly written buffer.
        _ = native.GetClientRect(hwnd, ref rect);
        _ = native.ClientToScreen(hwnd, ref rect);
        // ClientToScreen changes only the first POINT, leaving width/height in place.
        long right = (long)rect.Left + rect.Right;
        long bottom = (long)rect.Top + rect.Bottom;
        return work.Left <= rect.Left && work.Top <= rect.Top
            && right <= work.Right && bottom <= work.Bottom;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct TaskbarRect
{
    internal int Left;
    internal int Top;
    internal int Right;
    internal int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TaskbarAppBarData
{
    internal uint Size;
    internal nint Window;
    internal uint CallbackMessage;
    internal uint Edge;
    internal TaskbarRect Rectangle;
    internal nint Param;
}

internal interface ITaskbarVisibilityNative
{
    nint FindTaskbar();
    nint GetWindowLong(nint hwnd, int index);
    nuint GetAppBarState();
    int GetMonitor(nint hwnd);
    MonitorRectangle? GetWorkArea(int number);
    int GetClientRect(nint hwnd, ref TaskbarRect rect);
    int ClientToScreen(nint hwnd, ref TaskbarRect rect);
}

internal sealed partial class TaskbarVisibilityNative : ITaskbarVisibilityNative
{
    internal static readonly TaskbarVisibilityNative Instance = new();

    public nint FindTaskbar() => GetTaskbarHwnd.Get();

    public nint GetWindowLong(nint hwnd, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtrW(hwnd, index) : GetWindowLongW(hwnd, index);

    public nuint GetAppBarState()
    {
        TaskbarAppBarData data = default;
        data.Size = (uint)Marshal.SizeOf<TaskbarAppBarData>();
        return SHAppBarMessage(4, ref data);
    }

    public int GetMonitor(nint hwnd) => WinGetWhichMonitor.Get(hwnd);

    public MonitorRectangle? GetWorkArea(int number) => MonitorExGetUtils.GetInfo(number)?.WorkArea;

    public int GetClientRect(nint hwnd, ref TaskbarRect rect) => GetClientRectNative(hwnd, ref rect);

    public int ClientToScreen(nint hwnd, ref TaskbarRect rect) => ClientToScreenNative(hwnd, ref rect);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetWindowLongPtrW(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int GetWindowLongW(nint hwnd, int index);

    [LibraryImport("shell32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nuint SHAppBarMessage(uint message, ref TaskbarAppBarData data);

    [LibraryImport("user32.dll", EntryPoint = "GetClientRect")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int GetClientRectNative(nint hwnd, ref TaskbarRect rect);

    // Passing RECT's address is intentional: the native function touches its first POINT only.
    [LibraryImport("user32.dll", EntryPoint = "ClientToScreen")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int ClientToScreenNative(nint hwnd, ref TaskbarRect rect);
}
