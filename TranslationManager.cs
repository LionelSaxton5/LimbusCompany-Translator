using Godot;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using static Godot.Viewport;
using static System.Formats.Asn1.AsnWriter;

public partial class TranslationManager : Node //翻译管理器(集成)
{
	private Process _ocrProcess; //OCR进程
	private const string OcRServerUrl = "http://127.0.0.1:1224/api/ocr"; //OCR服务器URL
    private TranslationWindow translationWindow; //翻译窗口

    private HttpRequest hTTPRequest; //HTTP请求节点
    private Button closeButton; //关闭按钮
    private Button timeTranslationButton; //实时翻译按钮

    public override void _Ready()
	{
		hTTPRequest = GetNode<HttpRequest>("HTTPRequest"); //获取HTTPRequest节点
        timeTranslationButton = GetNode<Button>("VBoxContainer/TranslateButton"); //获取翻译按钮
        closeButton = GetNode<Button>("CloseButton"); //获取关闭按钮

        if (hTTPRequest == null)
        {
            GD.PrintErr("错误：未能在场景中找到 HTTPRequest 节点！");
        }
        else
        {
            GD.Print("HTTPRequest 节点获取成功。");
        }
        
        StartUmiOcrService();       
    }

	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("capture_for_translate"))
		{
			OnCaptureHotkeyPressed();
        }
	}

    private async Task StartUmiOcrService() //启动Umi-OCR服务,做为子进程运行
    {
        using (var client = new System.Net.Http.HttpClient()) //创建HTTP客户端(using自动释放)
        {
            client.Timeout = System.TimeSpan.FromSeconds(2); //设置超时时间为2秒
            try
            {
                var response = await client.GetAsync("http://127.0.0.1:1224/api/ocr"); //尝试连接OCR服务
                if (response.IsSuccessStatusCode)
                {
                    GD.Print("Umi-OCR服务已在运行，无需启动新进程");
                    return; //服务已在运行，直接返回
                }
            }
            catch
            {
                GD.Print("Umi-OCR服务未运行，准备启动新进程");
            }
        }

        string umiOcrPath = @"D:\xiazai\Godot sucai\边狱翻译器\Umi-OCR_Rapid_v2.1.5\Umi-OCR.exe"; //Umi-OCR可执行文件路径

        if (_ocrProcess != null && !_ocrProcess.HasExited)
        {
            _ocrProcess.Kill(); //如果进程已存在且未退出，先终止它
            GD.Print("已终止旧的Umi-OCR进程");
        }

        try
        {
            _ocrProcess = new Process(); //创建新进程
            _ocrProcess.StartInfo.FileName = umiOcrPath; //设置可执行文件路径
            _ocrProcess.StartInfo.Arguments = "http --port 1224"; //设置脚本路径参数,HTTP服务脚本
            _ocrProcess.StartInfo.UseShellExecute = false; //不使用外壳执行
            _ocrProcess.StartInfo.CreateNoWindow = true; //不创建窗口
                                   
            _ocrProcess.Start(); //启动进程
            GD.Print("Umi-OCR HTTP服务已隐式启动");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"错误：无法启动Umi-OCR服务 - {ex.Message}");
            GD.PrintErr($"请检查路径是否正确: {umiOcrPath}");
        }
    }

	private void OnCaptureHotkeyPressed() //捕获热键按下时的处理函数
    {
		GD.Print("捕获热键按下，开始截图并发送OCR请求");		
		
        //创建获取屏幕截图
        Image fullScreen = CaptureScreen(); //捕获整个屏幕

        if (fullScreen == null)
        {
            GD.PrintErr("错误：屏幕截图获取失败！");
            return;
        }

		//暂时固定裁剪区间(手动调整)
		int cropX = 100;
		int cropY = 100;
		int cropWidth = 800;
		int cropHeight = 600;
       
		Image croppedImage = CaptureRegion(fullScreen, cropX, cropY, cropWidth, cropHeight); //裁剪指定区域

        //转换字节流
        byte[] bytes = ImageToPngBytes(croppedImage);
        if (bytes == null || bytes.Length == 0)
        {
            GD.PrintErr("错误：图像转换为字节流失败！");
            return;
        }

        GD.Print($"图片处理完成，大小: {bytes.Length} 字节");
        SendToUmiOcr(bytes);//发送OCR请求       
    }

	private void SendToUmiOcr(byte[] imageData) //发送OCR请求的异步函数
	{		        
        //将图片转为Base64，构建JSON请求体
        string base64Image = System.Convert.ToBase64String(imageData); //将图像字节数组转换为Base64字符串
        string jsonBody = $"{{\"base64\": \"{base64Image}\"}}"; //构建JSON请求体
        byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody); //将JSON字符串转换为UTF-8字节数组

        //准备请求头 - 使用 JSON 格式
        string[] headers =
		{
            "Content-Type: application/json", //josn类型
			$"Content-Length: {bodyBytes.Length}", //内容长度
            "Connection: close" //请求完成后关闭连接
        };

        //发送POST请求
        GD.Print($"正在发送请求到 /api/ocr ...");
        Error requestErr = hTTPRequest.RequestRaw(OcRServerUrl, headers, Godot.HttpClient.Method.Post, bodyBytes);

		if (requestErr != Error.Ok)
		{
            GD.PrintErr($"请求发送失败: {requestErr}");
            return;
        }              
    }

    private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body) //HTTP请求完成的回调函数
    {
        GD.Print($"OCR请求完成，结果代码: {result}, HTTP响应代码: {responseCode}");
        //请求成功
        if (responseCode == 200)
        {
            string jsonString = System.Text.Encoding.UTF8.GetString(body); //将UTF-8字节数组转换为JSON字符串
            GD.Print("收到OCR响应");

            //反序列化JSON响应
            var json = Json.ParseString(jsonString).AsGodotDictionary(); //解析JSON字符串为Godot字典
            var data = json["data"].AsGodotArray(); //获取日期数组

            //收集高置信度文本
            System.Text.StringBuilder allText = new System.Text.StringBuilder(); //用于存储所有识别文本

            foreach (var item in data)
            {
                var itemDict = item.AsGodotDictionary(); //将每个项目转换为字典
                string text = itemDict["text"].ToString(); //提取文本字段
                float score = float.Parse(itemDict["score"].ToString()); //提取置信度字段

                if (score > 0.7)
                {
                    allText.AppendLine(text); //添加高置信度文本
                }
            }

            string alltext = allText.ToString(); //获取所有文本字符串

            TranslationSource.Instance.GetText(alltext, "en", "zh-Hans"); //发送文本进行翻译
        }
    }

    //===辅助函数：捕获屏幕截图===
    private Image CaptureScreen()
	{
		return DisplayServer.ScreenGetImage(0); //捕获整个屏幕的图像
    }
	private Image CaptureRegion(Image fullImage,int x, int y, int w, int h)
	{
		Rect2I rect2I = new Rect2I(x, y, w, h); //定义裁剪区域
        return fullImage.GetRegion(rect2I); //裁剪指定区域的图像
    }
	private byte[] ImageToPngBytes(Image godotImage)
	{
		return godotImage.SavePngToBuffer(); //将Godot图像保存为PNG格式的字节数组
    }	

    private void OnTranslateButtonPressed() //实时翻译按钮
    {
        if (translationWindow != null && IsInstanceValid(translationWindow))
        {
            translationWindow.Show(); //显示已有的翻译窗口
            translationWindow.GrabFocus(); //获取焦点
            GD.Print("已有翻译窗口");
            return;
        }

        GetOrCreateTranslationWindow();        
    }

    public override void _ExitTree()
	{
		//退出时终止OCR进程
		if (_ocrProcess != null && !_ocrProcess.HasExited)
		{
			_ocrProcess.Kill();
			_ocrProcess = null;
			GD.Print("已终止Umi-OCR进程");
		}
		base._ExitTree(); //调用基类退出处理
    }

    public void OnCloseButtonPressed() //关闭按钮按下
    {
        GD.Print("关闭按钮按下");
        this.QueueFree(); //销毁翻译管理器节点
    }
  
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
            GD.PrintErr("错误：无法加载 TranslationWindow 场景！");
            return null;
        }

        translationWindow = windowScene.Instantiate<TranslationWindow>();
        GetTree().Root.AddChild(translationWindow); //将翻译窗口添加到场景
        translationWindow.Show();
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
}
