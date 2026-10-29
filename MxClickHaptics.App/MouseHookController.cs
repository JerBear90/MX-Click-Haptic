using System.Runtime.InteropServices;
using MxClickHaptics.Core;

namespace MxClickHaptics.App;

public sealed class MouseHookController : IDisposable
{
    private readonly Action<MouseButton, bool> _onButtonStateChanged;
    private readonly HookProc _hookProc;
    private readonly GCHandle _hookProcHandle;
    private IntPtr _hookHandle;

    public MouseHookController(Action<MouseButton, bool> onButtonStateChanged)
    {
        _onButtonStateChanged = onButtonStateChanged ?? throw new ArgumentNullException(nameof(onButtonStateChanged));
        _hookProc = LowLevelMouseProc;
        _hookProcHandle = GCHandle.Alloc(_hookProc);
        _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, IntPtr.Zero, 0);
    }

    public bool IsHooked => _hookHandle != IntPtr.Zero;

    private IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = (MouseMessage)wParam;
            switch (message)
            {
                case MouseMessage.WM_LBUTTONDOWN:
                    _onButtonStateChanged(MouseButton.Left, true);
                    break;
                case MouseMessage.WM_LBUTTONUP:
                    _onButtonStateChanged(MouseButton.Left, false);
                    break;
                case MouseMessage.WM_RBUTTONDOWN:
                    _onButtonStateChanged(MouseButton.Right, true);
                    break;
                case MouseMessage.WM_RBUTTONUP:
                    _onButtonStateChanged(MouseButton.Right, false);
                    break;
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        if (_hookProcHandle.IsAllocated)
        {
            _hookProcHandle.Free();
        }
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    private enum MouseMessage
    {
        WM_LBUTTONDOWN = 0x0201,
        WM_LBUTTONUP = 0x0202,
        WM_RBUTTONDOWN = 0x0204,
        WM_RBUTTONUP = 0x0205
    }

    private const int WH_MOUSE_LL = 14;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hHook);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hHook, int nCode, IntPtr wParam, IntPtr lParam);
}
