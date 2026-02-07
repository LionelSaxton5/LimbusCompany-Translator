using System;
using System.Runtime.InteropServices;

/// <summary>
/// 翻译结果叠加窗口 - 半透明灰色背景，显示翻译文字和按钮
/// 特点：不显示在任务栏、半透明背景、可拖动、支持点击事件
/// </summary>
public class TranslationOverlayWindow : IDisposable
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

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool TextOutW(IntPtr hdc, int x, int y, string lpString, int c);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(IntPtr hdc, uint color);

 [DllImport("gdi32.dll")]
    private static extern uint SetBkMode(IntPtr hdc, uint mode);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontW(int nHeight, int nWidth, int nEscapement,
int nOrientation, int fnWeight, uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut,
   uint fdwCharSet, uint fdwOutputPrecision, uint fdwClipPrecision,
        uint fdwQuality, uint fdwPitchAndFamily, string lpszFace);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
 {
        public int left, top, right, bottom;
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
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0201;
    private static readonly IntPtr HTCAPTION = new IntPtr(2);
    private static readonly IntPtr HTCLIENT = new IntPtr(1);

    private const string CLASS_NAME = "TranslationOverlayWindowClass";

    private const int TRANSPARENT = 1;

    #endregion

private static bool _classRegistered = false;
    private static WndProcDelegate _wndProcDelegateStatic;
    private static TranslationOverlayWindow _currentInstance;

    private IntPtr _hWnd;
    private bool _isDisposed;
    private int _x, _y, _width, _height;
    private string _translationText = "";

    // 按钮区域定义
    private RECT _translateButtonRect;
    private RECT _drawButtonRect;
    private bool _isTranslateButtonHovered = false;
    private bool _isDrawButtonHovered = false;

 public IntPtr Handle => _hWnd;
    public bool IsCreated => _hWnd != IntPtr.Zero;

    // 事件
    public event Action TranslateButtonClicked;
    public event Action DrawButtonClicked;

    public bool Create(int x, int y, int width, int height)
    {
        _x = x;
        _y = y;
        _width = width;
      _height = height;
  _currentInstance = this;

        // 计算按钮位置（底部）
        int buttonY = height - 40;
  int buttonHeight = 30;
        int buttonWidth = 80;
     int buttonMargin = 10;

        _translateButtonRect = new RECT
        {
  left = buttonMargin,
            top = buttonY,
            right = buttonMargin + buttonWidth,
bottom = buttonY + buttonHeight
        };

        _drawButtonRect = new RECT
        {
            left = buttonMargin + buttonWidth + 10,
   top = buttonY,
          right = buttonMargin + buttonWidth * 2 + 10,
        bottom = buttonY + buttonHeight
        };

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
            if (error != 1410) // 类已注册
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
        "翻译结果窗口",
     WS_POPUP,
       x, y, width, height,
   IntPtr.Zero,
     IntPtr.Zero,
            hInstance,
  IntPtr.Zero
        );

 if (_hWnd == IntPtr.Zero)
        {
  Godot.GD.PrintErr($"创建翻译窗口失败: {Marshal.GetLastWin32Error()}");
      return false;
        }

        DrawWindow();
        ShowWindow(_hWnd, SW_SHOW);
        Godot.GD.Print($"翻译叠加窗口已创建: {_hWnd}");
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
     // 获取鼠标位置
   int x = (short)(lParam.ToInt32() & 0xFFFF);
      int y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);

         // 转换为窗口坐标
      x -= _x;
            y -= _y;

 // 检查是否在按钮区域
         if (IsPointInRect(x, y, _translateButtonRect) || IsPointInRect(x, y, _drawButtonRect))
            {
     return HTCLIENT; // 按钮区域可以点击
            }

     return HTCAPTION; // 其他区域可拖动
        }
     else if (uMsg == WM_LBUTTONDOWN)
        {
 // 获取点击位置
  int x = (short)(lParam.ToInt32() & 0xFFFF);
         int y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);

            if (IsPointInRect(x, y, _translateButtonRect))
  {
   Godot.GD.Print("翻译按钮被点击");
    TranslateButtonClicked?.Invoke();
       }
        else if (IsPointInRect(x, y, _drawButtonRect))
      {
                Godot.GD.Print("绘制按钮被点击");
         DrawButtonClicked?.Invoke();
     }
     }

        return DefWindowProcW(hWnd, uMsg, wParam, lParam);
    }

    private bool IsPointInRect(int x, int y, RECT rect)
    {
        return x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom;
    }

    private void DrawWindow()
    {
    if (_hWnd == IntPtr.Zero) return;

        IntPtr screenDC = GetDC(IntPtr.Zero);
    IntPtr memDC = CreateCompatibleDC(screenDC);

        BITMAPINFO bi = new BITMAPINFO();
        bi.bmiHeader.biSize = Marshal.SizeOf<BITMAPINFOHEADER>();
        bi.bmiHeader.biWidth = _width;
        bi.bmiHeader.biHeight = -_height; // 负数表示从上到下
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

        // 绘制内容
        DrawContent(ppvBits);

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

    private void DrawContent(IntPtr pixels)
    {
        unsafe
  {
            byte* ptr = (byte*)pixels.ToPointer();

       // 1. 绘制半透明灰色背景
  byte bgR = 50, bgG = 50, bgB = 50;
      byte bgAlpha = 200; // 半透明

         for (int y = 0; y < _height; y++)
            {
       for (int x = 0; x < _width; x++)
                {
           SetPixel(ptr, x, y, _width, bgR, bgG, bgB, bgAlpha);
            }
            }

            // 2. 绘制翻译按钮
 DrawButton(ptr, _translateButtonRect, "翻译", 100, 150, 255, _isTranslateButtonHovered);

     // 3. 绘制绘制按钮
            DrawButton(ptr, _drawButtonRect, "绘制", 100, 150, 255, _isDrawButtonHovered);

            // 4. 绘制翻译文字区域（顶部）
            if (!string.IsNullOrEmpty(_translationText))
{
            // 这里简化处理，实际可以用 GDI 的 TextOut 或者手动绘制文字
            // 暂时用一个浅色矩形表示文字区域
        for (int y = 10; y < _height - 50; y++)
             {
            for (int x = 10; x < _width - 10; x++)
        {
     if (y < 100) // 文字区域
        {
       SetPixel(ptr, x, y, _width, 200, 200, 200, 50);
            }
                }
      }
     }
      }
    }

    private unsafe void DrawButton(byte* ptr, RECT rect, string text, byte r, byte g, byte b, bool isHovered)
    {
        byte buttonAlpha = (byte)(isHovered ? 255 : 200);

        // 绘制按钮背景
      for (int y = rect.top; y < rect.bottom; y++)
     {
            for (int x = rect.left; x < rect.right; x++)
  {
     if (x >= 0 && x < _width && y >= 0 && y < _height)
         {
             SetPixel(ptr, x, y, _width, r, g, b, buttonAlpha);
                }
     }
        }

    // 绘制按钮边框
        byte borderR = 255, borderG = 255, borderB = 255;
        for (int x = rect.left; x < rect.right; x++)
        {
   SetPixel(ptr, x, rect.top, _width, borderR, borderG, borderB, 255);
            SetPixel(ptr, x, rect.bottom - 1, _width, borderR, borderG, borderB, 255);
        }
        for (int y = rect.top; y < rect.bottom; y++)
{
      SetPixel(ptr, rect.left, y, _width, borderR, borderG, borderB, 255);
            SetPixel(ptr, rect.right - 1, y, _width, borderR, borderG, borderB, 255);
        }
    }

    private unsafe void SetPixel(byte* ptr, int x, int y, int width, byte r, byte g, byte b, byte a)
    {
        if (x < 0 || x >= width || y < 0 || y >= _height) return;
  int offset = (y * width + x) * 4;
        ptr[offset + 0] = b; // BGR 格式
     ptr[offset + 1] = g;
        ptr[offset + 2] = r;
      ptr[offset + 3] = a;
    }

    public void SetTranslationText(string text)
    {
        _translationText = text;
        DrawWindow(); // 重绘窗口
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
Godot.GD.Print("翻译叠加窗口已销毁");
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
