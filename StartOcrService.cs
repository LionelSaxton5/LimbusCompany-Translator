using Godot;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using static SaveManager;
using static System.Net.Mime.MediaTypeNames;

public partial class StartOcrService : Node //启动OCR服务
{
    private Process _ocrProcess; //OCR进程
    public const string OcRServerUrl = "http://127.0.0.1:1224/api/ocr"; //OCR服务器URL

    private HttpRequest hTTPRequest; //HTTP请求节点(发送OCR请求)

    public override void _Ready()
	{
        hTTPRequest = GetNode<HttpRequest>("HTTPRequest"); //获取HTTPRequest节点
    }

    public async Task StartUmiOcrService() //启动Umi-OCR服务,做为子进程运行
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

        string OCRsavedPath = SaveManager.Instance.saveData.umiOcrPath; //从配置中获取保存的Umi-OCR路径
        if (!string.IsNullOrWhiteSpace(OCRsavedPath) && System.IO.File.Exists(OCRsavedPath)) //如果路径有效且文件存在
        {
            GD.Print($"使用保存的 Umi-OCR 路径: '{OCRsavedPath}'");
        }
        else
        {
            string projectRootPath = ProjectSettings.GlobalizePath("res://");
            string fontSourcePath = System.IO.Path.Combine(projectRootPath, "Umi-OCR_Rapid_v2.1.5", "Umi-OCR.exe");
            string exePath = OS.GetExecutablePath();  //获取Godot可执行文件路径
            string exeDir = System.IO.Path.GetDirectoryName(exePath); //获取Godot可执行文件目录

            string[] umiOcrPathList =
            {
            // 优先级1：与程序在同一目录的Umi-OCR文件夹
                System.IO.Path.Combine(exeDir, "Umi-OCR_Rapid_v2.1.5", "Umi-OCR.exe"),
        
                // 优先级2：在程序目录的子目录中
                System.IO.Path.Combine(fontSourcePath),
                System.IO.Path.Combine(exeDir,"..", "Umi-OCR_Rapid_v2.1.5", "Umi-OCR.exe"),
                System.IO.Path.Combine(projectRootPath,  "Umi-OCR_Rapid_v2.1.5", "Umi-OCR.exe"),
        
                // 优先级3：用户选择的路径（从配置文件读取）

        
                // 优先级4：在开发环境中的路径
                //@"D:\xiazai\Godot sucai\边狱翻译器\Umi-OCR_Rapid_v2.1.5\Umi-OCR.exe" //Umi-OCR可执行文件路径
            };

            string found = null;
            foreach (var path in umiOcrPathList)
            {
                if (System.IO.File.Exists(path))
                {
                    found = path;                   
                    GD.Print($"找到Umi-OCR路径: {found}");
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(found)) //如果找到有效路径
            {
                OCRsavedPath = found;

                if (SaveManager.Instance != null && SaveManager.Instance.saveData != null)
                {
                    SaveManager.Instance.saveData.umiOcrPath = OCRsavedPath;
                    SaveManager.Instance.SaveDataToFile();
                }
            }
            else
            {
                ErrorWindow.ShowError("错误：未找到可用的 Umi-OCR 可执行文件。请在设置中手动指定路径。");
                return;
            }
        }
        
        if (_ocrProcess != null && !_ocrProcess.HasExited)
        {
            _ocrProcess.Kill(); //如果进程已存在且未退出，先终止它
            GD.Print("已终止旧的Umi-OCR进程");
        }

        try
        {
            _ocrProcess = new Process(); //创建新进程
            _ocrProcess.StartInfo.FileName = OCRsavedPath; //设置可执行文件路径
            _ocrProcess.StartInfo.Arguments = "http --port 1224 --lang jpn"; ; //设置脚本路径参数,HTTP服务脚本
            _ocrProcess.StartInfo.UseShellExecute = false; //不使用外壳执行
            _ocrProcess.StartInfo.CreateNoWindow = true; //不创建窗口

            _ocrProcess.Start(); //启动进程
            GD.Print("Umi-OCR HTTP服务已隐式启动");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"错误：无法启动Umi-OCR服务 - {ex.Message}");
            GD.PrintErr($"请检查路径是否正确: {OCRsavedPath}");
        }
    }

    public void SendToUmiOcr(byte[] imageData) //发送OCR请求的异步函数
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
        Error requestErr = hTTPRequest.RequestRaw(OcRServerUrl, headers, Godot.HttpClient.Method.Post, bodyBytes);

        if (requestErr != Error.Ok)
        {
            GD.PrintErr($"请求发送失败: {requestErr}");
            return;
        }
    }

    private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body) //OCR:HTTP请求完成的回调函数
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
                string text = itemDict["text"].ToString(); //取出对应键的值,提取文本字段
                float score = float.Parse(itemDict["score"].ToString()); //提取置信度字段

                if (score > 0.7)
                {
                    allText.AppendLine(text); //添加高置信度文本
                }
            }

            string alltext = allText.ToString(); //获取所有文本字符串
            GD.Print($"识别到的文本:\n{alltext}");

            TranslationSource.Instance.GetText(alltext); //发送文本进行翻译
        }
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
}
