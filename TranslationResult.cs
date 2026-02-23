using Godot;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

public partial class TranslationResult : Control //翻译结果显示
{
    //普通按钮相关
    private Button translateButton; //翻译按钮
    private Button drawButton; //绘制按钮
    private Button closeButton; //关闭按钮
    private SpinBox spinBox; //调整字体大小的SpinBox
    private Label translationLabel; //翻译结果标签

    //选项按钮相关
    private OptionButton fromLangOptionButton; //源语言选项按钮
    private OptionButton toLangOptionButton;   //目标语言选项按钮

    //区域窗口相关
    public Window _selectionWindow;  //全屏选择窗口
    private RegionSelector _selector; //区域选择器
    private Window parentWindow; //父窗口引用
    private OverlayWindow _overlayWindow; //覆盖窗口

    // 事件
    public event Action _TranslateButtonPressed;//翻译按钮按下事件
    public event Action _DrawButtonPressed;//绘制按钮按下事件

    // 添加对父窗口的引用
    private TranslationWindow _translationWindow;
    private System.Threading.Timer _mouseCheckTimer; // 定时器用于检查鼠标位置
    private bool _mouseInOverlayWindow = false;     // 鼠标是否在OverlayWindow内

    public override void _Ready()
    {
        translateButton = GetNode<Button>("HBoxContainer/TranslateButton"); //获取翻译按钮
        drawButton = GetNode<Button>("HBoxContainer/DrawButton"); //获取绘制按钮
        closeButton = GetNode<Button>("HBoxContainer/CloseButton"); //获取关闭按钮
        spinBox = GetNode<SpinBox>("HBoxContainer/FontSizeSpinBox"); //获取字体大小SpinBox
        translationLabel = GetNode<Label>("Label");
        fromLangOptionButton = GetNode<OptionButton>("HBoxContainer/FromLangOptionButton"); //获取源语言选项按钮
        toLangOptionButton = GetNode<OptionButton>("HBoxContainer/ToLangOptionButton");     //获取目标语言选项按钮

        translateButton.Pressed += () => _TranslateButtonPressed?.Invoke(); //发出翻译按钮按下事件(TranslationManager中连接)
        drawButton.Pressed += CreateRegionWindow; //连接绘制按钮按下事件
        fromLangOptionButton.ItemSelected += (long idx) => TranslationSource.Instance.OnFromLangOptionButtonItemEelected((int)idx); //设置源语言
        toLangOptionButton.ItemSelected += (long idx) => TranslationSource.Instance.OnToLangOptionButtonItemEelected((int)idx);     //设置目标语言

        // 查找父翻译窗口
        _translationWindow = GetParent() as TranslationWindow;
    }

    public void SetParentWindow(Window parent) //设置父窗口引用
    {
        parentWindow = parent;
        _translationWindow = parent as TranslationWindow;
    }

    private void CreateRegionWindow() //创建覆盖窗口
    {       
        if (_selectionWindow != null)
        {
            _selectionWindow.QueueFree();
            _selectionWindow = null;
        }
        if (_overlayWindow != null)
        {
            _overlayWindow.Dispose(); //销毁已有的覆盖窗口
            _overlayWindow = null;
        }

        // 停止定时器
        if (_mouseCheckTimer != null)
        {
            _mouseCheckTimer.Dispose();
            _mouseCheckTimer = null;
        }

        var screenPos = DisplayServer.ScreenGetPosition(0);   // 获取屏幕位置
        var screenSize = DisplayServer.ScreenGetSize(0);       // 获取屏幕尺寸

        // 创建一个新的全屏透明窗口用于选择
        _selectionWindow = new Window();

        _selectionWindow.Transparent = true;      // 窗口透明
        _selectionWindow.TransparentBg = true;    // 背景透明
        _selectionWindow.Borderless = true;   //无边框
        _selectionWindow.Unfocusable = false; //允许获取焦点
        _selectionWindow.Position = screenPos;    // 窗口位置设为屏幕起点
        _selectionWindow.Size = screenSize;       // 窗口大小设为全屏
        _selectionWindow.PopupWindow = true;    // 弹出窗口
        _selectionWindow.AlwaysOnTop = true;    // 窗口置顶

        // 创建选择器并添加到窗口
        var selectorPackedScene = GD.Load<PackedScene>("res://changjing/RegionUI.tscn");
        _selector = selectorPackedScene.Instantiate<RegionSelector>();

        _selector.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); //设置选择器填满窗口     
        _selector.RegionSelected += OnRegionSelected; //连接区域选择完成事件
        _selectionWindow.AddChild(_selector);

        // 添加窗口到场景树并显示
        GetTree().Root.AddChild(_selectionWindow);
        _selectionWindow.Show();
    }

    private void OnRegionSelected()
    {
        Rect2I region = _selector.SelectedRegion;

        _selectionWindow.RemoveChild(_selector); //移除选择器
        _selectionWindow.QueueFree(); //移除选择窗口
        _selectionWindow = null;
        _selector.QueueFree();
        _selector = null;

        _overlayWindow = new OverlayWindow();
        _overlayWindow.Create(region.Position.X, region.Position.Y, region.Size.X, region.Size.Y);
        _overlayWindow.Show();

        // 开始定时检查鼠标位置
        StartMouseCheckTimer();

        var translationManager = GetNode<TranslationManager>("/root/TranslationManager");
        if (translationManager != null)
        {
            translationManager.SetSelectedRegion(region);
        }
        else
        {
            //GD.PrintErr("未找到 TranslationManager 节点");
        }
    }

    private void StartMouseCheckTimer()
    {
        if (_mouseCheckTimer != null)
        {
            _mouseCheckTimer.Dispose();
        }

        _mouseCheckTimer = new System.Threading.Timer(
            CheckMousePosition,
            null,
            0,  // 立即开始
            50  // 每50毫秒检查一次
        );
    }

    private void CheckMousePosition(object state)
    {
        // 需要在主线程执行，使用CallDeferred
        CallDeferred(nameof(CheckMousePositionDeferred));
    }

    private void CheckMousePositionDeferred()
    {
        if (_overlayWindow == null || !_overlayWindow.IsCreated)
        {
            return;
        }

        var mousePos = DisplayServer.MouseGetPosition(); // 获取鼠标位置
        var overlayRect = new Rect2(_overlayWindow.X, _overlayWindow.Y,  
                                  _overlayWindow.Width, _overlayWindow.Height); // 获取OverlayWindow的矩形区域

        bool isMouseInOverlay = overlayRect.HasPoint(mousePos); // 检查鼠标是否在OverlayWindow内

        if (isMouseInOverlay && !_mouseInOverlayWindow)
        {
            // 鼠标刚进入OverlayWindow
            _mouseInOverlayWindow = true;
            OnMouseEnteredOverlay();
        }
        else if (!isMouseInOverlay && _mouseInOverlayWindow)
        {
            // 鼠标刚离开OverlayWindow
            _mouseInOverlayWindow = false;
            OnMouseLeftOverlay();
        }
    }

    private void OnMouseEnteredOverlay() //鼠标进入OverlayWindow区域
    {
        // 隐藏翻译窗口
        if (_translationWindow != null && IsInstanceValid(_translationWindow))
        {
            _translationWindow.Hide();
        }
        else if (parentWindow != null && IsInstanceValid(parentWindow))
        {
            parentWindow.Hide();
        }
    }

    private void OnMouseLeftOverlay()
    {
        // 显示翻译窗口并获取焦点
        if (_translationWindow != null && IsInstanceValid(_translationWindow))
        {
            _translationWindow.Show();
            _translationWindow.GrabFocus();
        }
        else if (parentWindow != null && IsInstanceValid(parentWindow))
        {
            parentWindow.Show();
            parentWindow.GrabFocus();
        }
    }

    public override void _ExitTree()
    {
        // 清理定时器
        if (_mouseCheckTimer != null)
        {
            _mouseCheckTimer.Dispose();
            _mouseCheckTimer = null;
        }

        // 清理OverlayWindow
        if (_overlayWindow != null)
        {
            _overlayWindow.Dispose();
            _overlayWindow = null;
        }

        base._ExitTree();
    }

    private void OnFontSizeSpinBoxValueChanged(float size)
    {
        translationLabel.AddThemeFontSizeOverride("font_size", (int)size);
    }

    public void SetLabel(string text)
    {
        translationLabel.Text = text;
    }

    private void OnCloseButtonPressed()
    {
        QueueFree(); //关闭翻译结果节点
        _translationWindow.QueueFree(); //关闭翻译窗口
    }

}