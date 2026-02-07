using System;
using System.Runtime.InteropServices;

/// <summary>
/// Windows API 叠加层窗口 - 用于绘制OCR识别框
/// 特点：不显示在任务栏、几乎透明背景、只显示边框、可拖动
/// </summary>
public class OverlayWindow : IDisposable //OCR识别叠加层窗口(最终识别框)
{
    #region Windows API

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern short RegisterClassW(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int X, int Y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool UpdateLayeredWindow(IntPtr hWnd, IntPtr hdcDst, ref POINT pptDst,
        ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage,
        out IntPtr ppvBits, IntPtr hSection, uint offset);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string lpModuleName);

    #endregion

    #region 结构体

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    #endregion

    #region 常量

    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_POPUP = 0x80000000;

    private const uint ULW_ALPHA = 0x00000002;
    private const byte AC_SRC_OVER = 0x00;
    private const byte AC_SRC_ALPHA = 0x01;

    private const int SW_SHOW = 5;

    private const uint WM_NCHITTEST = 0x0084;
    private static readonly IntPtr HTCAPTION = new IntPtr(2);

    private const string CLASS_NAME = "OverlayWindowClass";

    #endregion

    private static bool _classRegistered = false;
    private static WndProcDelegate _wndProcDelegateStatic;
    private static OverlayWindow _currentInstance;

    private IntPtr _hWnd;
    private bool _isDisposed;
    private int _x, _y, _width, _height;
    private uint _borderColor = 0x00FF0000; // BGR: 蓝色

    public IntPtr Handle => _hWnd;
    public bool IsCreated => _hWnd != IntPtr.Zero;
    public int X => _x;
    public int Y => _y;
    public int Width => _width;
    public int Height => _height;

    public bool Create(int x, int y, int width, int height)
    {
        _x = x;
        _y = y;
        _width = width;
        _height = height;
        _currentInstance = this;

        IntPtr hInstance = GetModuleHandleW(null);

        if (!_classRegistered)
        {
            _wndProcDelegateStatic = StaticWndProc;
            WNDCLASS wc = new WNDCLASS
            {
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegateStatic),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = hInstance,
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = CLASS_NAME
            };

            short result = RegisterClassW(ref wc);
            if (result == 0)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 1410)
                {
                    Godot.GD.PrintErr($"注册窗口类失败: {error}");
                    return false;
                }
            }
            _classRegistered = true;
        }

        uint exStyle = WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;

        _hWnd = CreateWindowExW(
            exStyle,
            CLASS_NAME,
            "OCR识别框",
            WS_POPUP,
            x, y, width, height,
            IntPtr.Zero,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero
        );

        if (_hWnd == IntPtr.Zero)
        {
            Godot.GD.PrintErr($"创建窗口失败: {Marshal.GetLastWin32Error()}");
            return false;
        }

        DrawTransparentWindow();
        ShowWindow(_hWnd, SW_SHOW);
        Godot.GD.Print($"叠加层窗口已创建: {_hWnd}");
        return true;
    }

    private static IntPtr StaticWndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
    {
        var instance = _currentInstance;
        if (instance != null && instance._hWnd == hWnd)
        {
            return instance.WndProc(hWnd, uMsg, wParam, lParam);
        }
        return DefWindowProcW(hWnd, uMsg, wParam, lParam);
    }

    private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
    {
        if (uMsg == WM_NCHITTEST)
        {
            return HTCAPTION; // 整个窗口都可拖动
        }
        return DefWindowProcW(hWnd, uMsg, wParam, lParam);
    }

    private void DrawTransparentWindow()
    {
        if (_hWnd == IntPtr.Zero) return;

        IntPtr screenDC = GetDC(IntPtr.Zero);
        IntPtr memDC = CreateCompatibleDC(screenDC);

        BITMAPINFO bi = new BITMAPINFO();
        bi.bmiHeader.biSize = Marshal.SizeOf<BITMAPINFOHEADER>();
        bi.bmiHeader.biWidth = _width;
        bi.bmiHeader.biHeight = -_height;
        bi.bmiHeader.biPlanes = 1;
        bi.bmiHeader.biBitCount = 32;
        bi.bmiHeader.biCompression = 0;

        IntPtr ppvBits;
        IntPtr hBitmap = CreateDIBSection(screenDC, ref bi, 0, out ppvBits, IntPtr.Zero, 0);
        if (hBitmap == IntPtr.Zero)
        {
            ReleaseDC(IntPtr.Zero, screenDC);
            DeleteDC(memDC);
            return;
        }

        IntPtr oldBitmap = SelectObject(memDC, hBitmap);

        DrawDashedBorder(ppvBits, _width, _height, _borderColor);

        POINT ptDst = new POINT { x = _x, y = _y };
        SIZE size = new SIZE { cx = _width, cy = _height };
        POINT ptSrc = new POINT { x = 0, y = 0 };
        BLENDFUNCTION blend = new BLENDFUNCTION
        {
            BlendOp = AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = AC_SRC_ALPHA
        };

        UpdateLayeredWindow(_hWnd, screenDC, ref ptDst, ref size, memDC, ref ptSrc, 0, ref blend, ULW_ALPHA);

        SelectObject(memDC, oldBitmap);
        DeleteObject(hBitmap);
        DeleteDC(memDC);
        ReleaseDC(IntPtr.Zero, screenDC);
    }

    private void DrawDashedBorder(IntPtr pixels, int width, int height, uint color)
    {
        byte b = (byte)(color & 0xFF);
        byte g = (byte)((color >> 8) & 0xFF);
        byte r = (byte)((color >> 16) & 0xFF);

        byte backgroundAlpha = 1; // 几乎透明，但可点击
        byte borderAlpha = 255;

        int borderWidth = 2;
        int dashLength = 6;
        int gapLength = 4;
        int cycleLength = dashLength + gapLength;

        unsafe
        {
            byte* ptr = (byte*)pixels.ToPointer();

            // 填充背景为几乎透明
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    SetPixel(ptr, x, y, width, 0, 0, 0, backgroundAlpha);
                }
            }

            // 绘制虚线边框
            for (int x = 0; x < width; x++)
            {
                bool isDash = (x % cycleLength) < dashLength;
                if (isDash)
                {
                    for (int t = 0; t < borderWidth && t < height; t++)
                    {
                        SetPixel(ptr, x, t, width, r, g, b, borderAlpha);
                        SetPixel(ptr, x, height - 1 - t, width, r, g, b, borderAlpha);
                    }
                }
            }

            for (int y = 0; y < height; y++)
            {
                bool isDash = (y % cycleLength) < dashLength;
                if (isDash)
                {
                    for (int t = 0; t < borderWidth && t < width; t++)
                    {
                        SetPixel(ptr, t, y, width, r, g, b, borderAlpha);
                        SetPixel(ptr, width - 1 - t, y, width, r, g, b, borderAlpha);
                    }
                }
            }
        }
    }

    private unsafe void SetPixel(byte* ptr, int x, int y, int width, byte r, byte g, byte b, byte a)
    {
        int offset = (y * width + x) * 4;
        ptr[offset + 0] = b;
        ptr[offset + 1] = g;
        ptr[offset + 2] = r;
        ptr[offset + 3] = a;
    }

    public void DrawBorder() => DrawTransparentWindow();

    public void Move(int x, int y)
    {
        if (_hWnd == IntPtr.Zero) return;
        _x = x;
        _y = y;
        DrawTransparentWindow();
    }

    public void SetBorderColor(byte r, byte g, byte b)
    {
        _borderColor = (uint)(b | (g << 8) | (r << 16));
        DrawTransparentWindow();
    }

    public void Show()
    {
        if (_hWnd != IntPtr.Zero)
            ShowWindow(_hWnd, SW_SHOW);
    }

    public void Destroy()
    {
        if (_hWnd != IntPtr.Zero)
        {
            DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
            if (_currentInstance == this)
                _currentInstance = null;
            Godot.GD.Print("叠加层窗口已销毁");
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Destroy();
            _isDisposed = true;
        }
    }
}
