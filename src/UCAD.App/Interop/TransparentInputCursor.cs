using Microsoft.UI.Input;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace UCAD.Interop;

/// <summary>
/// Creates one fully transparent Windows cursor and projects it into the
/// Windows App SDK InputCursor system. CadViewport then assigns that cursor
/// through UIElement.ProtectedCursor while Win2D renders the visible CAD
/// crosshair/pickbox itself.
/// </summary>
internal static class TransparentInputCursor
{
    private const string InputCursorRuntimeClass = "Microsoft.UI.Input.InputCursor";
    private const int CursorSize = 32;
    private static readonly object Sync = new();
    private static InputCursor? _cursor;

    public static InputCursor GetOrCreate()
    {
        lock (Sync)
        {
            return _cursor ??= CreateCore();
        }
    }

    private static InputCursor CreateCore()
    {
        // CreateCursor uses monochrome AND/XOR masks. AND=1 + XOR=0 leaves the
        // underlying screen pixel unchanged, so an all-1 AND plane and all-0 XOR
        // plane produce a completely invisible cursor while retaining a real cursor
        // object for WinUI to own.
        var bytesPerRow = ((CursorSize + 15) / 16) * 2;
        var planeLength = bytesPerRow * CursorSize;
        var andPlane = new byte[planeLength];
        Array.Fill(andPlane, (byte)0xFF);
        var xorPlane = new byte[planeLength];

        var hCursor = CreateCursor(
            IntPtr.Zero,
            CursorSize / 2,
            CursorSize / 2,
            CursorSize,
            CursorSize,
            andPlane,
            xorPlane);

        if (hCursor == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateCursor failed for the transparent CAD cursor.");
        }

        try
        {
            return CreateInputCursorFromHCursor(hCursor);
        }
        finally
        {
            // CreateFromHCursor copies a custom cursor into an InputCustomCursor,
            // therefore the temporary native HCURSOR can be destroyed immediately.
            _ = DestroyCursor(hCursor);
        }
    }

    private static InputCursor CreateInputCursorFromHCursor(IntPtr hCursor)
    {
        var hr = WindowsCreateString(InputCursorRuntimeClass, InputCursorRuntimeClass.Length, out var classId);
        Marshal.ThrowExceptionForHR(hr);

        try
        {
            hr = RoGetActivationFactory(classId, typeof(IActivationFactory).GUID, out var factory);
            Marshal.ThrowExceptionForHR(hr);

            if (factory is not IInputCursorStaticsInterop interop)
            {
                throw new InvalidOperationException("Microsoft.UI.Input.InputCursor does not expose IInputCursorStaticsInterop.");
            }

            hr = interop.CreateFromHCursor(hCursor, out var cursorAbi);
            Marshal.ThrowExceptionForHR(hr);
            if (cursorAbi == IntPtr.Zero)
            {
                throw new InvalidOperationException("CreateFromHCursor returned a null InputCursor.");
            }

            return WinRT.MarshalInspectable<InputCursor>.FromAbi(cursorAbi);
        }
        finally
        {
            _ = WindowsDeleteString(classId);
        }
    }

    [ComImport]
    [Guid("AC6F5065-90C4-46CE-BEB7-05E138E54117")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IInputCursorStaticsInterop
    {
        // IInspectable vtable slots.
        void GetIids();
        void GetRuntimeClassName();
        void GetTrustLevel();

        [PreserveSig]
        int CreateFromHCursor(IntPtr hCursor, out IntPtr inputCursor);
    }

    [ComImport]
    [Guid("00000035-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivationFactory
    {
        // IInspectable vtable slots.
        void GetIids();
        void GetRuntimeClassName();
        void GetTrustLevel();

        [PreserveSig]
        int ActivateInstance(out IntPtr instance);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateCursor(
        IntPtr hInstance,
        int xHotSpot,
        int yHotSpot,
        int width,
        int height,
        byte[] andPlane,
        byte[] xorPlane);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyCursor(IntPtr hCursor);

    [DllImport("api-ms-win-core-winrt-l1-1-0.dll")]
    private static extern int RoGetActivationFactory(
        IntPtr runtimeClassId,
        [MarshalAs(UnmanagedType.LPStruct)] Guid iid,
        out IActivationFactory factory);

    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(
        string sourceString,
        int length,
        out IntPtr hString);

    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll")]
    private static extern int WindowsDeleteString(IntPtr hString);
}
