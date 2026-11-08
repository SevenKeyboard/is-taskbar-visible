using System.Runtime.InteropServices;
using SevenKeyboard;

internal static partial class Program
{
    private static int _checks;

    private static void Main(string[] args)
    {
        LayoutAndBranches();
        AutoHideBoundaries();
        FailedNativeOutputs();
        if (args.Contains("--native-read-only"))
            NativeReadOnly();
        Console.WriteLine($"PASS: {_checks} visibility checks.");
    }

    private static void LayoutAndBranches()
    {
        Check(Marshal.SizeOf<TaskbarRect>() == 16, "RECT layout");
        Check(Marshal.SizeOf<TaskbarAppBarData>() == (IntPtr.Size == 8 ? 48 : 36), "APPBARDATA size");
        Check(Marshal.OffsetOf<TaskbarAppBarData>(nameof(TaskbarAppBarData.Rectangle)).ToInt32()
            == (IntPtr.Size == 8 ? 24 : 16), "APPBARDATA rectangle offset");

        var missing = new Fake { Taskbar = 0 };
        Check(!IsTaskbarVisible.Check(missing), "missing Shell_TrayWnd");
        Calls(missing, "find");

        foreach (nuint state in new nuint[] { 0, 2, 4 })
        {
            foreach (nint exStyle in new nint[] { 0, 8, 0x80, 0x88 })
            {
                var fake = new Fake { State = state, ExStyle = exStyle };
                Check(IsTaskbarVisible.Check(fake) == ((exStyle & 8) != 0),
                    "without auto-hide only topmost matters");
                Calls(fake, "find", "exstyle", "style", "state");
            }
        }

        var hidden = new Fake { State = 1, ExStyle = 0, Style = unchecked((nint)(int)0x80000000) };
        Check(!IsTaskbarVisible.Check(hidden), "popup style cannot substitute for topmost");
        Calls(hidden, "find", "exstyle", "style", "state");

        var noMonitor = new Fake { State = 1, Number = 0 };
        Check(!IsTaskbarVisible.Check(noMonitor), "auto-hide needs a monitor match");
        Calls(noMonitor, "find", "exstyle", "style", "state", "monitor");

        var noInfo = new Fake { State = 1, Work = null };
        Check(!IsTaskbarVisible.Check(noInfo), "missing cached info returns false");
        Calls(noInfo, "find", "exstyle", "style", "state", "monitor", "work");
    }

    private static void AutoHideBoundaries()
    {
        var exact = new Fake
        {
            State = 3,
            Work = new(-100, -50, 100, 50),
            Client = new() { Right = 200, Bottom = 100 },
            ScreenX = -100,
            ScreenY = -50
        };
        Check(IsTaskbarVisible.Check(exact), "auto-hide includes every equal work-area edge");
        Check(exact.InitialRectWasZero, "RECT starts zeroed");
        Check(exact.BeforeConversion.Right == 200 && exact.BeforeConversion.Bottom == 100,
            "conversion receives client width and height");
        Calls(exact, "find", "exstyle", "style", "state", "monitor", "work", "rect", "screen");

        foreach ((int x, int y, int w, int h) in new[]
        {
            (-101, -50, 200, 100), (-100, -51, 200, 100),
            (-100, -50, 201, 100), (-100, -50, 200, 101)
        })
        {
            var outside = new Fake
            {
                State = 1, Work = new(-100, -50, 100, 50),
                ScreenX = x, ScreenY = y,
                Client = new() { Right = w, Bottom = h }
            };
            Check(!IsTaskbarVisible.Check(outside), "one pixel outside an inclusive work-area edge");
        }

        var overflowing = new Fake
        {
            State = 1,
            Work = new(int.MinValue, int.MinValue, int.MaxValue, int.MaxValue),
            ScreenX = int.MaxValue, ScreenY = 0,
            Client = new() { Right = 1, Bottom = 1 }
        };
        Check(!IsTaskbarVisible.Check(overflowing), "right addition must not wrap at Int32.MaxValue");
        overflowing.ScreenX = 0;
        overflowing.ScreenY = int.MaxValue;
        Check(!IsTaskbarVisible.Check(overflowing), "bottom addition must not wrap at Int32.MaxValue");
    }

    private static void FailedNativeOutputs()
    {
        var bothFailed = new Fake
        {
            State = 1, Work = new(0, 0, 0, 0),
            ClientResult = 0, ClientWriteMask = 0,
            ScreenResult = 0, ScreenWriteMask = 0
        };
        Check(IsTaskbarVisible.Check(bothFailed), "both failures leave zero RECT which may pass");
        Calls(bothFailed, "find", "exstyle", "style", "state", "monitor", "work", "rect", "screen");
        bothFailed.Work = new(10, 10, 20, 20);
        Check(!IsTaskbarVisible.Check(bothFailed), "failed outputs still undergo containment");

        var rectFailed = new Fake
        {
            State = 1, Work = new(20, 30, 20, 30),
            ClientResult = 0, ClientWriteMask = 0, ScreenX = 20, ScreenY = 30
        };
        Check(IsTaskbarVisible.Check(rectFailed), "conversion still runs after GetClientRect failure");

        var conversionFailed = new Fake
        {
            State = 1, Work = new(0, 0, 10, 10),
            Client = new() { Right = 10, Bottom = 10 },
            ScreenResult = 0, ScreenWriteMask = 0
        };
        Check(IsTaskbarVisible.Check(conversionFailed), "failed conversion retains successful client output");
        conversionFailed.Work = new(10, 10, 20, 20);
        Check(!IsTaskbarVisible.Check(conversionFailed), "unconverted client origin is not repaired");

        var partial = new Fake
        {
            State = 1, Work = new(0, 20, 5, 20),
            ClientResult = 0, ClientWriteMask = 4,
            Client = new() { Left = 99, Top = 99, Right = 5, Bottom = 99 },
            ScreenResult = 0, ScreenWriteMask = 2, ScreenX = 99, ScreenY = 20
        };
        Check(IsTaskbarVisible.Check(partial), "partial failed writes survive both native calls");
        Check(partial.BeforeConversion.Left == 0 && partial.BeforeConversion.Top == 0
            && partial.BeforeConversion.Right == 5 && partial.BeforeConversion.Bottom == 0,
            "only written client fields replace zero initialization");
    }

    private static void NativeReadOnly()
    {
        nint dpi = GetThreadDpiAwarenessContext();
        nint hwnd = GetTaskbarHwnd.Get();
        bool result = IsTaskbarVisible.Check();
        if (hwnd == 0 && GetTaskbarHwnd.Get() == 0)
            Check(!result, "native absent taskbar");
        Check(GetThreadDpiAwarenessContext() == dpi, "public query leaves caller DPI context unchanged");

        if (hwnd != 0)
        {
            TaskbarRect rect = default;
            var native = TaskbarVisibilityNative.Instance;
            _ = native.GetWindowLong(hwnd, -20);
            _ = native.GetWindowLong(hwnd, -16);
            nuint state = native.GetAppBarState();
            int rectResult = native.GetClientRect(hwnd, ref rect);
            int screenResult = native.ClientToScreen(hwnd, ref rect);
            Console.WriteLine($"Native: taskbar={hwnd}, visible={result}, state={state}, rect={rectResult}, screen={screenResult}.");
        }
    }

    private static void Calls(Fake fake, params string[] expected) =>
        Check(fake.Calls.SequenceEqual(expected), "native call order: " + string.Join(", ", expected));

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetThreadDpiAwarenessContext();

    private sealed class Fake : ITaskbarVisibilityNative
    {
        internal nint Taskbar = 10;
        internal nint ExStyle = 8;
        internal nint Style;
        internal nuint State;
        internal int Number = 2;
        internal MonitorRectangle? Work = new(-100, -100, 100, 100);
        internal TaskbarRect Client = new() { Right = 10, Bottom = 10 };
        internal int ScreenX;
        internal int ScreenY;
        internal int ClientResult = 1;
        internal int ClientWriteMask = 15;
        internal int ScreenResult = 1;
        internal int ScreenWriteMask = 3;
        internal bool InitialRectWasZero;
        internal TaskbarRect BeforeConversion;
        internal readonly List<string> Calls = [];

        public nint FindTaskbar() { Calls.Add("find"); return Taskbar; }
        public nint GetWindowLong(nint hwnd, int index)
        {
            Check(hwnd == Taskbar, "style queries use primary taskbar");
            Calls.Add(index == -20 ? "exstyle" : "style");
            return index == -20 ? ExStyle : Style;
        }
        public nuint GetAppBarState() { Calls.Add("state"); return State; }
        public int GetMonitor(nint hwnd)
        {
            Check(hwnd == Taskbar, "monitor query uses taskbar");
            Calls.Add("monitor");
            return Number;
        }
        public MonitorRectangle? GetWorkArea(int number)
        {
            Check(number == Number, "work area uses matched one-based index");
            Calls.Add("work");
            return Work;
        }
        public int GetClientRect(nint hwnd, ref TaskbarRect rect)
        {
            Check(hwnd == Taskbar, "client query uses taskbar");
            Calls.Add("rect");
            InitialRectWasZero = rect.Left == 0 && rect.Top == 0 && rect.Right == 0 && rect.Bottom == 0;
            if ((ClientWriteMask & 1) != 0) rect.Left = Client.Left;
            if ((ClientWriteMask & 2) != 0) rect.Top = Client.Top;
            if ((ClientWriteMask & 4) != 0) rect.Right = Client.Right;
            if ((ClientWriteMask & 8) != 0) rect.Bottom = Client.Bottom;
            return ClientResult;
        }
        public int ClientToScreen(nint hwnd, ref TaskbarRect rect)
        {
            Check(hwnd == Taskbar, "conversion uses taskbar");
            Calls.Add("screen");
            BeforeConversion = rect;
            if ((ScreenWriteMask & 1) != 0) rect.Left = ScreenX;
            if ((ScreenWriteMask & 2) != 0) rect.Top = ScreenY;
            return ScreenResult;
        }
    }
}
