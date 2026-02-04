using System;
using System.Runtime.InteropServices;

/// <summary>
/// Windows API 叠加层窗口 - 用于绘制OCR识别框
/// 特点：不显示在任务栏、透明、可穿透鼠标
/// </summary>
public class OverlayWindow : IDisposable
{
    #region Windows API 导入

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName,
 uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
  private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
      int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey,
        byte bAlpha, uint dwFlags);

  [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

  [DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    [DllImport("user32.dll")]
    private static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreatePen(int fnPenStyle, int nWidth, uint crColor);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool Rectangle(IntPtr hdc, int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetStockObject(int fnObject);

    [DllImport("user32.dll")]
    private static extern short RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

  [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    #endregion

    #region 常量定义

    // 窗口扩展样式
    private const uint WS_EX_LAYERED = 0x00080000;      // 分层窗口（支持透明）
    private const uint WS_EX_TRANSPARENT = 0x00000020;  // 鼠标穿透
    private const uint WS_EX_TOOLWINDOW = 0x00000080;   // 工具窗口（不显示在任务栏）
    private const uint WS_EX_TOPMOST = 0x00000008;      // 始终置顶
    private const uint WS_EX_NOACTIVATE = 0x08000000;   // 不激活

    // 窗口样式
    private const uint WS_POPUP = 0x80000000;           // 弹出窗口（无边框）
    private const uint WS_VISIBLE = 0x10000000;         // 可见

    // SetLayeredWindowAttributes 标志
    private const uint LWA_COLORKEY = 0x00000001;       // 颜色键透明
    private const uint LWA_ALPHA = 0x00000002;          // Alpha透明

    // ShowWindow 命令
    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;

    // SetWindowPos 标志
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    // GDI 常量
    private const int PS_DASH = 1;        // 虚线
    private const int PS_SOLID = 0;         // 实线
    private const int NULL_BRUSH = 5;       // 空画刷（不填充）

    // 窗口消息
    private const uint WM_PAINT = 0x000F;
    private const uint WM_DESTROY = 0x0002;

    #endregion

    #region 结构体定义

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
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
        public IntPtr hIconSm;
    }

    #endregion

    private IntPtr _hWnd;       // 窗口句柄
    private bool _isDisposed;     // 是否已释放
    private int _x, _y, _width, _height;// 窗口位置和大小
    private uint _borderColor = 0x00FF0000; // 边框颜色 (BGR格式，蓝色)
    private bool _mousePassThrough = true;  // 鼠标穿透

 public IntPtr Handle => _hWnd;
    public bool IsCreated => _hWnd != IntPtr.Zero;

    /// <summary>
    /// 创建叠加层窗口
    /// </summary>
    public bool Create(int x, int y, int width, int height)
    {
 _x = x;
        _y = y;
      _width = width;
        _height = height;

        // 窗口扩展样式
        uint exStyle = WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
    if (_mousePassThrough)
  exStyle |= WS_EX_TRANSPARENT;

     // 创建窗口
        _hWnd = CreateWindowEx(
            exStyle,
            "STATIC",         // 使用系统预定义的静态控件类
  "OCR识别框",
            WS_POPUP | WS_VISIBLE,
 x, y, width, height,
            IntPtr.Zero,
  IntPtr.Zero,
        GetModuleHandle(null),
       IntPtr.Zero
        );

    if (_hWnd == IntPtr.Zero)
        {
            Godot.GD.PrintErr($"创建窗口失败: {Marshal.GetLastWin32Error()}");
            return false;
        }

        // 设置窗口透明度（完全透明背景）
    SetLayeredWindowAttributes(_hWnd, 0x00000000, 0, LWA_COLORKEY);

        // 显示窗口
        ShowWindow(_hWnd, SW_SHOW);

        // 绘制边框
        DrawBorder();

Godot.GD.Print($"叠加层窗口已创建: {_hWnd}");
        return true;
    }

    /// <summary>
    /// 绘制虚线边框
    /// </summary>
    public void DrawBorder()
    {
    if (_hWnd == IntPtr.Zero) return;

        IntPtr hdc = GetDC(_hWnd);
        if (hdc == IntPtr.Zero) return;

        try
        {
            // 创建虚线画笔（蓝色）
          IntPtr pen = CreatePen(PS_DASH, 2, _borderColor);
            IntPtr oldPen = SelectObject(hdc, pen);

          // 使用空画刷（不填充）
     IntPtr nullBrush = GetStockObject(NULL_BRUSH);
            IntPtr oldBrush = SelectObject(hdc, nullBrush);

     // 绘制矩形边框
    Rectangle(hdc, 0, 0, _width, _height);

            // 恢复和清理
            SelectObject(hdc, oldPen);
    SelectObject(hdc, oldBrush);
            DeleteObject(pen);
   }
        finally
      {
            ReleaseDC(_hWnd, hdc);
      }
    }

    /// <summary>
    /// 移动窗口位置
    /// </summary>
    public void Move(int x, int y)
    {
        if (_hWnd == IntPtr.Zero) return;

        _x = x;
    _y = y;
MoveWindow(_hWnd, x, y, _width, _height, true);
        DrawBorder();
    }

    /// <summary>
    /// 调整窗口大小
    /// </summary>
    public void Resize(int width, int height)
{
        if (_hWnd == IntPtr.Zero) return;

     _width = width;
   _height = height;
        MoveWindow(_hWnd, _x, _y, width, height, true);
    DrawBorder();
    }

    /// <summary>
    /// 设置边框颜色 (BGR格式)
    /// </summary>
    public void SetBorderColor(byte r, byte g, byte b)
    {
        _borderColor = (uint)(b | (g << 8) | (r << 16));
      DrawBorder();
    }

    /// <summary>
    /// 设置鼠标穿透
    /// </summary>
    public void SetMousePassThrough(bool passThrough)
    {
        // 需要重新创建窗口才能改变这个属性
        _mousePassThrough = passThrough;
    }

    /// <summary>
    /// 显示窗口
    /// </summary>
    public void Show()
    {
        if (_hWnd != IntPtr.Zero)
            ShowWindow(_hWnd, SW_SHOW);
    }

    /// <summary>
    /// 隐藏窗口
    /// </summary>
  public void Hide()
    {
        if (_hWnd != IntPtr.Zero)
  ShowWindow(_hWnd, SW_HIDE);
    }

    /// <summary>
    /// 销毁窗口
    /// </summary>
    public void Destroy()
    {
        if (_hWnd != IntPtr.Zero)
        {
     DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
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
