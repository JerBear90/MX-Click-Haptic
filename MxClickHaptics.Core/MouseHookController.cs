using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MxClickHaptics.Core;

public sealed class MouseHookController : IDisposable
{
    private readonly Action<MouseButton, bool> _onButtonStateChanged;
    private readonly Action<Exception> _onError;
    private readonly BlockingCollection<MouseButton> _pendingClicks = new();
    private readonly TaskCompletionSource<bool> _hookReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _hookThread;
    private readonly Thread _callbackThread;
    private HookProc? _hookProc;
    private IntPtr _hookHandle;
    private uint _hookThreadId;
    private int _disposed;
    private volatile bool _isHooked;

    public MouseHookController(Action<MouseButton, bool> onButtonStateChanged, Action<Exception> onError)
    {
        _onButtonStateChanged = onButtonStateChanged ?? throw new ArgumentNullException(nameof(onButtonStateChanged));
        _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        _callbackThread = new Thread(ProcessClicks)
        {
            IsBackground = true,
            Name = "MX Click Haptics callback"
        };
        _hookThread = new Thread(RunHookMessageLoop)
        {
            IsBackground = true,
            Name = "MX Click Haptics mouse hook"
        };

        _callbackThread.Start();
        _hookThread.Start();
        _hookReady.Task.GetAwaiter().GetResult();
    }

    public bool IsHooked => _isHooked;

    private void RunHookMessageLoop()
    {
        _hookThreadId = GetCurrentThreadId();
        _ = PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE);

        try
        {
            _hookProc = LowLevelMouseProc;
            _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, IntPtr.Zero, 0);
            if (_hookHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            _isHooked = true;
            _hookReady.TrySetResult(true);

            while (true)
            {
                var result = GetMessage(out _, IntPtr.Zero, 0, 0);
                if (result == 0)
                {
                    break;
                }

                if (result == -1)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }
        catch (Exception ex)
        {
            _hookReady.TrySetException(ex);
            _onError(ex);
        }
        finally
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }

            _isHooked = false;
            _hookProc = null;
            _pendingClicks.CompleteAdding();
        }
    }

    private IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = (MouseMessage)wParam;
            switch (message)
            {
                case MouseMessage.WM_LBUTTONDOWN:
                    _pendingClicks.Add(MouseButton.Left);
                    break;
                case MouseMessage.WM_RBUTTONDOWN:
                    _pendingClicks.Add(MouseButton.Right);
                    break;
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void ProcessClicks()
    {
        foreach (var button in _pendingClicks.GetConsumingEnumerable())
        {
            try
            {
                _onButtonStateChanged(button, true);
            }
            catch (Exception ex)
            {
                _onError(ex);
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_isHooked && !PostThreadMessage(_hookThreadId, WM_QUIT, UIntPtr.Zero, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        _hookThread.Join();
        _callbackThread.Join();
        _pendingClicks.Dispose();
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    private enum MouseMessage
    {
        WM_LBUTTONDOWN = 0x0201,
        WM_RBUTTONDOWN = 0x0204
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr WindowHandle;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public NativePoint Point;
    }

    private const int WH_MOUSE_LL = 14;
    private const uint PM_NOREMOVE = 0;
    private const uint WM_QUIT = 0x0012;

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PeekMessage(
        out NativeMessage message,
        IntPtr windowHandle,
        uint minimumMessage,
        uint maximumMessage,
        uint removeMessage);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(
        out NativeMessage message,
        IntPtr windowHandle,
        uint minimumMessage,
        uint maximumMessage);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, UIntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr moduleHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int nCode, IntPtr wParam, IntPtr lParam);
}
