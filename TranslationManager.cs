using Godot;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using static Godot.Viewport;
using static System.Formats.Asn1.AsnWriter;

public partial class TranslationManager : Node //翻译管理器(集成)
{
    //启动OCR服务(配置路径)->截图(转格式、请求头)->发送OCR请求(HttpRequest)->接收OCR结果(反序列化)->发送翻译请求

    //OCR服务相关
    private StartOcrService startOcrService; //OCR服务启动节点
    private PaddleOcrService paddleOcrService; //PaddleOCR服务节点

    //窗口相关
    private TranslationWindow translationWindow; //翻译窗口
    private RegionSelector regionSelector;
    private TranslationResult translationResult;

    private Rect2I _lastSelectedRegion;   //上次选择的OCR区域
    private bool _hasValidRegion = false; //是否有有效的OCR区域

    public override void _Ready()
	{
        startOcrService = GetNode<StartOcrService>("StartOCRService"); //获取启动OCR服务节点
        paddleOcrService = GetNode<PaddleOcrService>("PaddleOcrService"); //获取PaddleOCR服务节点                                                             

        if (SaveManager.Instance.saveData.isUmiOcrEnable)
        {
            startOcrService.StartUmiOcrService(); //根据保存的数据决定是否启动OCR服务
        }
        else if(SaveManager.Instance.saveData.isPaddleOcrEnable)
        {
            paddleOcrService.InitializeEngine(); //根据保存的数据决定是否初始化PaddleOCR引擎
        }
    }

    private void OnCaptureHotkeyPressed() //捕获热键按下时的处理函数
    {
        if (!_hasValidRegion)
        {
            ErrorWindow.ShowError("错误：没有有效的OCR区域，请先使用绘制按钮选择区域");
            return;
        }

        //创建获取屏幕截图
        Image fullScreen = CaptureRegion(_lastSelectedRegion); //捕获指定区域的屏幕截图

        if (fullScreen == null)
        {
            ErrorWindow.ShowError("错误：屏幕截图获取失败！");
            return;
        }

        //转换字节流
        byte[] bytes = ImageToPngBytes(fullScreen);
        if (bytes == null || bytes.Length == 0)
        {
            ErrorWindow.ShowError("错误：图像转换为字节流失败！");
            return;
        }

        if (SaveManager.Instance.saveData.isUmiOcrEnable)
        {
            startOcrService.SendToUmiOcr(bytes);//发送OCR请求       
        }
        else if (SaveManager.Instance.saveData.isPaddleOcrEnable)
        {
            paddleOcrService.Recognize(bytes); //发送OCR请求
        }
    }

    public void SetSelectedRegion(Rect2I region)
    {
        _lastSelectedRegion = region;
        _hasValidRegion = true;
    }   
    
    //===辅助函数：捕获屏幕截图===   
	private Image CaptureRegion(Rect2I region)
	{
        return DisplayServer.ScreenGetImageRect(region); //捕获指定区域的图像
    }
	private byte[] ImageToPngBytes(Image godotImage)
	{
		return godotImage.SavePngToBuffer(); //将Godot图像保存为PNG格式的字节数组
    }	
      
    //按钮事件处理函数
    private void OnTranslateButtonPressed() //实时翻译按钮
    {
        if (translationWindow != null && IsInstanceValid(translationWindow))
        {
            translationWindow.Show(); //显示已有的翻译窗口
            translationWindow.GrabFocus(); //获取焦点
            ErrorWindow.ShowError("已有翻译窗口");
            return;
        }

        GetOrCreateTranslationWindow();
    }

    //窗口管理函数
    public TranslationWindow GetOrCreateTranslationWindow() //获取或创建翻译窗口
    {
        if (translationWindow != null && IsInstanceValid(translationWindow))
        {
            translationWindow.Show();
            translationWindow.GrabFocus(); //获取焦点
            return translationWindow;
        }

        var windowScene = GD.Load<PackedScene>("res://changjing/TranslationWindow.tscn");
        if (windowScene == null)
        {
            return null;
        }

        translationWindow = windowScene.Instantiate<TranslationWindow>();    

        GetTree().Root.AddChild(translationWindow); //将翻译窗口添加到场景
        translationWindow.Show();

        translationResult = translationWindow.translationResult;
        if (translationResult != null)
        {
            translationResult._TranslateButtonPressed += OnCaptureHotkeyPressed; //订阅翻译按钮事件
        }
        return translationWindow;
    }
    
    public TranslationWindow CurrentTranslationWindow => 
        (translationWindow != null && IsInstanceValid(translationWindow)) ? translationWindow : null; //当前翻译窗口

    public void HideTranslationWindow() //隐藏翻译窗口
    {
        if (translationWindow != null && IsInstanceValid(translationWindow))
        {
            translationWindow.Hide();
        }
    }
    
    public void DestroyTranslationWindow() //销毁翻译窗口
    {
        if (translationWindow != null && IsInstanceValid(translationWindow))
        {
            translationWindow.QueueFree();
            translationWindow = null;
        }
    }

    public void OnMicrosoftCheckButtonToggled(bool pressed) //微软翻译启用切换
    {      
        SaveManager.Instance.saveData.isMicrosofttranslationEnable = pressed;   
        SaveManager.Instance.SaveDataToFile();
    }

    public void OnBaiduCheckButtonToggled(bool pressed) //百度翻译启用切换
    {
        SaveManager.Instance.saveData.isBaidutranslationEnable = pressed;
        SaveManager.Instance.SaveDataToFile();
    }
    public void OnTengxunCheckButtonToggled(bool pressed) //腾讯翻译启用切换
    {
        SaveManager.Instance.saveData.isTengxuntranslationEnable = pressed;
        SaveManager.Instance.SaveDataToFile();
    }
    public void OnHuoshanCheckButtonToggled(bool pressed) //火山翻译启用切换
    {
        SaveManager.Instance.saveData.isHuoshantranslationEnable = pressed;
        SaveManager.Instance.SaveDataToFile();
    }
}
