using Godot;
using System;

public partial class TranslationResult : Control //翻译结果显示
{
	private static TranslationResult _instance; //单例实例
	public static TranslationResult Instance => _instance;

    private Button translateButton; //翻译按钮
    private Button drawButton; //绘制按钮

    private Window _selectionWindow;  //全屏选择窗口
    private RegionSelector _selector; //区域选择器

    public event Action _TranslateButtonPressed;//翻译按钮按下事件
    public event Action _DrawButtonPressed;//绘制按钮按下事件
	
    public override void _Ready()
	{
		_instance = this;

        translateButton = GetNode<Button>("HBoxContainer/TranslateButton"); //获取翻译按钮
		drawButton = GetNode<Button>("HBoxContainer/DrawButton"); //获取绘制按钮

		translateButton.Pressed += () => _TranslateButtonPressed?.Invoke(); //连接翻译按钮按下事件
		drawButton.Pressed += CreateRegionWindow; //连接绘制按钮按下事件
    }
	
    private void CreateRegionWindow() //创建覆盖窗口
    {
        if (_selector != null)
        {
            _selector.QueueFree();
            _selector = null;
        }
        if (_selectionWindow != null)
        {
            _selectionWindow.QueueFree();
            _selectionWindow = null;
        }

        var screenPos = DisplayServer.ScreenGetPosition(0);   // 获取屏幕位置
        var screenSize = DisplayServer.ScreenGetSize(0);       // 获取屏幕尺寸

        // 创建一个新的全屏透明窗口用于选择
        _selectionWindow = new Window();

        _selectionWindow.Transparent = true;      // 窗口透明
        _selectionWindow.TransparentBg = true;    // 背景透明
        _selectionWindow.Borderless = true;   //无边框
        _selectionWindow.AlwaysOnTop = true;  //始终在最前
        _selectionWindow.Unfocusable = false; //允许获取焦点
        _selectionWindow.Position = screenPos;    // 窗口位置设为屏幕起点
        _selectionWindow.Size = screenSize;       // 窗口大小设为全屏

        // 创建选择器并添加到窗口
        _selector = new RegionSelector();
        _selector.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); //设置选择器填满窗口     
        _selector.RegionSelected += OnRegionSelected; //连接区域选择完成事件
        _selectionWindow.AddChild(_selector);
    
        // 添加窗口到场景树并显示
        GetTree().Root.AddChild(_selectionWindow);
        _selectionWindow.Show();
        _selectionWindow.GrabFocus();

        GD.Print("选择窗口已创建");
    }

    private void OnRegionSelected()
    {
        var region = _selector.SelectedRegion; //获取选区

        _selectionWindow.RemoveChild(_selector); //移除选择器
        _selectionWindow.QueueFree(); //移除选择窗口
        _selectionWindow = null;

        // 创建新的小窗口，大小=选区大小
        _selectionWindow = new Window();
        _selectionWindow.Transparent = true;
        _selectionWindow.TransparentBg = true;
        _selectionWindow.Borderless = true;
        _selectionWindow.AlwaysOnTop = true;
        _selectionWindow.Position = region.Position;
        _selectionWindow.Size = region.Size;

        // 把选择器放到新窗口
        _selector.Position = Vector2.Zero;
        _selector.Size = region.Size;
        _selectionWindow.AddChild(_selector);

        GetTree().Root.AddChild(_selectionWindow);
        _selectionWindow.Show();
    }  
}
