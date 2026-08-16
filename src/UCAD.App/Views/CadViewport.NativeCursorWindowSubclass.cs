using Microsoft.UI;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace UCAD.Views;

public sealed partial class CadViewport
{
    private const uint WmSetCursor = 0x0020;
    private static long _nextCursorSubclassId;

    private SubclassProc? _cursorSubclassProc;
    private IntPtr _cursorSubclassHwnd;
    private UIntPtr _cursorSubclassId;
    private bool _cursorSubclassInstalled;

    private delegate IntPtr SubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr uIdSubclass,
        UIntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        UIntPtr uIdSubclass,
        UIntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        UIntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private void InstallNativeCursorSuppression()
    {
        if (_cursorSubclassInstalled || XamlRoot is null)
        {
            return;
        }

        var hwnd = Win32Interop.GetWindowFromWindowId(XamlRoot.ContentIslandEnvironment.AppWindowId);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        _cursorSubclassProc ??= NativeCursorSubclassProc;
        if (_cursorSubclassId == UIntPtr.Zero)
        {
            _cursorSubclassId = new UIntPtr((ulong)Interlocked.Increment(ref _nextCursorSubclassId));
        }

        if (!SetWindowSubclass(hwnd, _cursorSubclassProc, _cursorSubclassId, UIntPtr.Zero))
        {
            return;
        }

        _cursorSubclassHwnd = hwnd;
        _cursorSubclassInstalled = true;
    }

    private void RemoveNativeCursorSuppression()
    {
        if (!_cursorSubclassInstalled ||
            _cursorSubclassHwnd == IntPtr.Zero ||
            _cursorSubclassProc is null)
        {
            return;
        }

        _ = RemoveWindowSubclass(_cursorSubclassHwnd, _cursorSubclassProc, _cursorSubclassId);
        _cursorSubclassInstalled = false;
        _cursorSubclassHwnd = IntPtr.Zero;
    }

    private IntPtr NativeCursorSubclassProc(
        IntPtr hWnd,
        uint uMsg,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr uIdSubclass,
        UIntPtr dwRefData)
    {
        if (uMsg == WmSetCursor && IsPointerInsideCanvas())
        {
            // WM_SETCURSOR is where DefWindowProc/WinUI would normally restore an
            // Arrow or class cursor on every mouse move. Returning TRUE after
            // SetCursor(NULL) prevents that restore, leaving only the Win2D CAD
            // crosshair and pickbox visible.
            _ = SetCursor(IntPtr.Zero);
            return new IntPtr(1);
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private bool IsPointerInsideCanvas()
    {
        if (!IsLoaded ||
            Visibility != Visibility.Visible ||
            Canvas.Visibility != Visibility.Visible ||
            Canvas.ActualWidth <= 0 ||
            Canvas.ActualHeight <= 0 ||
            XamlRoot is null ||
            !GetCursorPos(out var screenPoint) ||
            XamlRoot.Content is not UIElement rootElement)
        {
            return false;
        }

        try
        {
            var rootPoint = XamlRoot.CoordinateConverter.ConvertScreenToLocal(
                new PointInt32 { X = screenPoint.X, Y = screenPoint.Y });
            var canvasPoint = rootElement
                .TransformToVisual(Canvas)
                .TransformPoint(new Windows.Foundation.Point(rootPoint.X, rootPoint.Y));

            return canvasPoint.X >= 0 &&
                   canvasPoint.Y >= 0 &&
                   canvasPoint.X < Canvas.ActualWidth &&
                   canvasPoint.Y < Canvas.ActualHeight;
        }
        catch
        {
            // A transient visual-tree detach should never break normal window input.
            return false;
        }
    }
}
