using Godot;
using System;

public partial class TranslationResult : Control //翻译结果显示
{
	private static TranslationResult _instance; //单例实例
	public static TranslationResult Instance => _instance;

    private Button translateButton; //翻译按钮
    private Button drawButton; //绘制按钮

    public Window _selectionWindow;  //全屏选择窗口
    private RegionSelector _selector; //区域选择器
    private OcRwindow ocrWindow; //OCR叠加层窗口
    private Window parentWindow; //父窗口引用

    public event Action _TranslateButtonPressed;//翻译按钮按下事件
    public event Action _DrawButtonPressed;//绘制按钮按下事件
	
    public override void _Ready()
	{
		_instance = this;

        translateButton = GetNode<Button>("HBoxContainer/TranslateButton"); //获取翻译按钮
		drawButton = GetNode<Button>("HBoxContainer/DrawButton"); //获取绘制按钮

		translateButton.Pressed += () => _TranslateButtonPressed?.Invoke(); //发出翻译按钮按下事件
        drawButton.Pressed += CreateRegionWindow; //连接绘制按钮按下事件
    }

    public void SetParentWindow(Window parent) //设置父窗口引用
    {
        parentWindow = parent;
    }

    private void CreateRegionWindow() //创建覆盖窗口
    {
        if (ocrWindow != null)
        {
            ocrWindow.QueueFree(); //销毁已有的叠加窗口
            ocrWindow = null;
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
        _selectionWindow.Unfocusable = false; //允许获取焦点
        _selectionWindow.Position = screenPos;    // 窗口位置设为屏幕起点
        _selectionWindow.Size = screenSize;       // 窗口大小设为全屏
        _selectionWindow.PopupWindow = true;   //弹出窗口模式

        // 创建选择器并添加到窗口
        var selectorPackedScene = GD.Load<PackedScene>("res://changjing/RegionUI.tscn");
        _selector = selectorPackedScene.Instantiate<RegionSelector>();
       
        _selector.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); //设置选择器填满窗口     
        _selector.RegionSelected += OnRegionSelected; //连接区域选择完成事件
        _selectionWindow.AddChild(_selector);
    
        // 添加窗口到场景树并显示
        GetTree().Root.AddChild(_selectionWindow);
        _selectionWindow.Show();

        GD.Print("选择窗口已创建");
    }

    private void OnRegionSelected()
    {
        GD.Print("区域选择完成");
       
        // 创建新的小窗口，大小=选区大小
        var packedScene = GD.Load<PackedScene>("res://changjing/OCRwindow.tscn");       
        ocrWindow = packedScene.Instantiate<OcRwindow>();
        var OCRControlScene = GD.Load<PackedScene>("res://changjing/OCRcontrol.tscn");
        OCRControl oCRControl = OCRControlScene.Instantiate<OCRControl>();
        ocrWindow.SetRect(_selector.SelectedRegion);
        oCRControl.SetRect(_selector.SelectedRegion); //设置窗口位置和大小为选区
        oCRControl.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); //填满窗口
        ocrWindow.AddChild(oCRControl);
        GetTree().Root.AddChild(ocrWindow); //添加到场景树            
        ocrWindow.Show(); //显示窗口        

        _selectionWindow.RemoveChild(_selector); //移除选择器
        _selectionWindow.QueueFree(); //移除选择窗口
        _selectionWindow = null;
        _selector.QueueFree();
        _selector = null;
    }       
}
