using Godot;
using System.Collections.Generic;
using System;
using System.Security.Cryptography;
using System.Text;


public partial class TranslationSource : Node //翻译源
{
    //配置翻译服务->选择语言->获取文本->发送翻译请求->接收翻译结果->显示翻译结果

    //翻译服务配置
    private static string endpoint = "";       //端点
    private static string translationKey = ""; //翻译密钥
    private static string region = "";         //区域

    //语言选项
    private static string fromLang = "ja"; //源语言默认日语
    private static string toLang = "zh-Hans"; //目标语言默认简体中文

    private HttpRequest translateHTTPRequest; //微软翻译HTTP请求节点
    private HttpRequest baiduHTTPRequest; //百度翻译HTTP请求节点
    private HttpRequest tengxunHTTPRequest; //腾讯翻译HTTP请求节点

    private static TranslationSource _instance; //单例实例
    public static TranslationSource Instance => _instance;

    //原文、译文、用户自定义数据(InlineTranslation中的TranslationTask任务)回调委托
    public delegate void TranslationCallback(string originalText, string translatedText, object userData); //翻译回调委托

    private Dictionary<HttpRequest, (string original, TranslationCallback callback, object userData)> _pendingCallbacks = new();

    public override void _Ready()
	{
        _instance = this;
        translateHTTPRequest = GetNode<HttpRequest>("TranslateHTTPRequest"); //获取翻译HTTPRequest节点
        baiduHTTPRequest = GetNode<HttpRequest>("BaiduHTTPRequest"); //获取百度翻译HTTPRequest节点
        tengxunHTTPRequest = GetNode<HttpRequest>("TengxunHTTPRequest"); //获取腾讯翻译HTTPRequest节点

        region = "eastasia"; //默认区域东亚
    }
	

    public void OnFromLangOptionButtonItemEelected(int index)
    {
        switch (index)
        {
            case 0:
                fromLang = "ja";//日语
                break;
            case 1:
                fromLang = "en";//英语
                break;
            case 2:
                fromLang = "zh-Hans";//简体中文
                break;
            case 3:
                fromLang = "ko";//韩语
                break;
        }
    }

    public void OnToLangOptionButtonItemEelected(int index)
    {
        switch (index)
        {
            case 0:
                toLang = "zh-Hans";//简体中文
                break;
            case 1:
                toLang = "en";//英语
                break;
            case 2:
                toLang = "ja";//日语
                break;
            case 3:
                toLang = "ko";//韩语
                break;
        }
    }

    private string MapToBaiduLangCode(string lang)
    {
        return lang switch
        {
            "zh-Hans" => "zh",      // 简体中文
            "ja" => "jp",       // 日语
            "ko" => "kor",      // 韩语
            "en" => "en",       // 英语            
            _ => lang        // 其他语言保持原样，或根据需要补充
        };
    }

    private string MapTOTengxuLangCode(string Lang) //腾讯翻译
    {
        return Lang switch
        {
            "zh-Hans" => "zh",      // 简体中文
            "ja" => "ja",       // 日语
            "ko" => "ko",      // 韩语
            "en" => "en",       // 英语            
            _ => Lang        // 其他语言保持原样，或根据需要补充
        };
    }

    //获取OCR文本字段
    public void GetText(string text)
    {
        if (_instance == null)
        {
            GD.PrintErr("错误：TranslationSource 实例未初始化");
            return;
        }

        if (SaveManager.Instance.saveData.isMicrosofttranslationEnable) //如果微软翻译启用
        {
            SendTranslateRequest(text, fromLang, toLang);
        }
        else if (SaveManager.Instance.saveData.isBaidutranslationEnable) //如果百度翻译启用
        {
            BaidutranslateRequest(text, fromLang, toLang);
        }
        else if (SaveManager.Instance.saveData.isTengxuntranslationEnable) //如果腾讯翻译启用
        {
            TengxuntranslateRequest(text, fromLang, toLang);
        }
        else
        {
            ErrorWindow.ShowError("未启用任何翻译服务");
        }
    }

    //发送翻译请求到微软翻译API(OCR翻译使用)
    private void SendTranslateRequest(string text, string fromLang, string toLang)
    {
        //构建请求URL
        string url = $"{SaveManager.Instance.saveData.MicrosoftranslationUrl}translate?api-version=3.0&from={fromLang}&to={toLang}";

        //构建JSON请求体: [{"Text": "要翻译的文本"}]
        string escapedText = EscapeJson(text); //转义特殊字符
        string jsonBody = $"[{{\"Text\": \"{escapedText}\"}}]"; //微软要求数组格式
        byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody); //将JSON变成UTF-8字节数组

        //准备请求头
        string[] headers =
        {
            "Content-Type: application/json",
            $"Ocp-Apim-Subscription-Key: {SaveManager.Instance.saveData.MicrosofttranslationKey}", //翻译密钥
            $"Ocp-Apim-Subscription-Region: {region}"
        };

        //发送POST请求
        Error error = translateHTTPRequest.RequestRaw(url, headers, HttpClient.Method.Post, bodyBytes);

        if (error != Error.Ok)
        {
            ErrorWindow.ShowError($"翻译请求发送失败: {error}");
            return;
        }
    }

    //方法重载，带回调参数(内嵌翻译使用)
    public void SendTranslateRequest(string text, string fromLang, string toLang, TranslationCallback onCompleted, object userData = null)
    {
        string url = $"{SaveManager.Instance.saveData.MicrosoftranslationUrl}translate?api-version=3.0&from={fromLang}&to={toLang}";

        //构建JSON请求体: [{"Text": "要翻译的文本"}]
        string escapedText = EscapeJson(text); //转义特殊字符
        string jsonBody = $"[{{\"Text\": \"{escapedText}\"}}]"; //微软要求数组格式
        byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody); //将JSON变成UTF-8字节数组

        //准备请求头
        string[] headers =
        {
            "Content-Type: application/json",
            $"Ocp-Apim-Subscription-Key: {SaveManager.Instance.saveData.MicrosofttranslationKey}", //翻译密钥
            $"Ocp-Apim-Subscription-Region: {region}"
        };

        //发送POST请求
        Error error = translateHTTPRequest.RequestRaw(url, headers, HttpClient.Method.Post, bodyBytes);

        if (error != Error.Ok)
        {
            ErrorWindow.ShowError($"翻译请求发送失败: {error}");
            return;
        }

        // 存储回调信息
        _pendingCallbacks[translateHTTPRequest] = (text, onCompleted, userData);
    }

    private void BaidutranslateRequest(string text, string fromLang, string toLang) //百度翻译请求(OCR)
    {
        string baiduFromLang = MapToBaiduLangCode(fromLang);
        string baiduToLang = MapToBaiduLangCode(toLang);

        string apiUrl = "https://fanyi-api.baidu.com/api/trans/vip/translate";
        string appId = SaveManager.Instance.saveData.BaidutranslationUrl; //百度翻译应用ID
        string appKey = SaveManager.Instance.saveData.BaidutranslationKey; //百度翻译密钥

        string salt = new Random().Next(100000, 999999).ToString(); //随机盐值
        string signSource = appId + text + salt + appKey;
        string sign = ComputeMD5HexLower(signSource);

        string form = $"q={Uri.EscapeDataString(text)}&from={Uri.EscapeDataString(baiduFromLang)}&to={Uri.EscapeDataString(baiduToLang)}&appid={Uri.EscapeDataString(appId)}&salt={Uri.EscapeDataString(salt)}&sign={Uri.EscapeDataString(sign)}";
        byte[] bodyBytes = Encoding.UTF8.GetBytes(form);

        string[] headers =
        {
            "Content-Type: application/x-www-form-urlencoded",
            $"Content-Length: {bodyBytes.Length}"
        };

        Error err = baiduHTTPRequest.RequestRaw(apiUrl, headers, HttpClient.Method.Post, bodyBytes);
        if (err != Error.Ok)
        {
            ErrorWindow.ShowError($"百度翻译请求发送失败: {err}");
        }
    }

    //方法重载，带回调参数(内嵌翻译使用)
    public void BaidutranslateRequest(string text, string fromLang, string toLang, TranslationCallback onCompleted, object userData = null)
    {
        string baiduFromLang = MapToBaiduLangCode(fromLang);
        string baiduToLang = MapToBaiduLangCode(toLang);

        string apiUrl = "https://fanyi-api.baidu.com/api/trans/vip/translate";
        string appId = SaveManager.Instance.saveData.BaidutranslationUrl; //百度翻译应用ID
        string appKey = SaveManager.Instance.saveData.BaidutranslationKey; //百度翻译密钥

        string salt = new Random().Next(100000, 999999).ToString(); //随机盐值
        string signSource = appId + text + salt + appKey;
        string sign = ComputeMD5HexLower(signSource);

        string form = $"q={Uri.EscapeDataString(text)}&from={Uri.EscapeDataString(baiduFromLang)}&to={Uri.EscapeDataString(baiduToLang)}&appid={Uri.EscapeDataString(appId)}&salt={Uri.EscapeDataString(salt)}&sign={Uri.EscapeDataString(sign)}";
        byte[] bodyBytes = Encoding.UTF8.GetBytes(form);

        string[] headers =
        {
            "Content-Type: application/x-www-form-urlencoded",
            $"Content-Length: {bodyBytes.Length}"
        };

        Error err = baiduHTTPRequest.RequestRaw(apiUrl, headers, HttpClient.Method.Post, bodyBytes);
        if (err != Error.Ok)
        {
            GD.PrintErr($"百度翻译请求发送失败: {err}");
        }

        // 存储回调信息
        _pendingCallbacks[baiduHTTPRequest] = (text, onCompleted, userData);
    }

    private void TengxuntranslateRequest(string text, string fromLang, string toLang) //腾讯翻译请求(OCR)
    {
        string tengxunFromLang = MapTOTengxuLangCode(fromLang);
        string tengxunToLang = MapTOTengxuLangCode(toLang);

        string secretId = SaveManager.Instance.saveData.TengxuntranslationUrl;
        string secretKey = SaveManager.Instance.saveData.TengxuntranslationKey;
        string region = "ap-guangzhou"; //腾讯云翻译服务所在区域，固定为广州

        var requestParams = new Godot.Collections.Dictionary<string, Variant> //构建请求参数
        {
            { "SourceText", text },
            { "Source", tengxunFromLang },
            { "Target", tengxunToLang },
            { "ProjectId", 0 }
        };

        string service = "tmt"; //腾讯云翻译服务标识
        string host = "tmt.tencentcloudapi.com"; // 就近接入域名
        string action = "TextTranslate"; //接口名称
        string version = "2018-03-21"; //接口版本
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(); //当前时间戳

        string authorization = GenerateTC3Signature(
        secretId, secretKey, timestamp, service, host, region, action, version, requestParams);

        string url = $"https://{host}/";

        string[] headers = new string[]
        {
        "Content-Type: application/json; charset=utf-8",
        "Host: " + host,
        "X-TC-Action: " + action,
        "X-TC-Version: " + version,
        "X-TC-Timestamp: " + timestamp,
        "X-TC-Region: " + region, // 固定 Region
        "Authorization: " + authorization
        };

        string jsonBody = Json.Stringify(requestParams);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

        Error err = tengxunHTTPRequest.RequestRaw(url, headers, HttpClient.Method.Post, bodyBytes);
        if (err != Error.Ok)
        {
            GD.PrintErr($"腾讯翻译请求发送失败: {err}");
            return;
        }
    }

    public void TengxuntranslateRequest(string text, string fromLang, string toLang, TranslationCallback onCompleted, object userData = null) //腾讯翻译请求(内嵌翻译使用)
    {
        string tengxunFromLang = MapTOTengxuLangCode(fromLang);
        string tengxunToLang = MapTOTengxuLangCode(toLang);

        string secretId = SaveManager.Instance.saveData.TengxuntranslationUrl;
        string secretKey = SaveManager.Instance.saveData.TengxuntranslationKey;
        string region = "ap-guangzhou"; //腾讯云翻译服务所在区域，固定为广州

        var requestParams = new Godot.Collections.Dictionary<string, Variant> //构建请求参数
        {
            { "SourceText", text },
            { "Source", tengxunFromLang },
            { "Target", tengxunToLang },
            { "ProjectId", 0 }
        };
        string service = "tmt"; //腾讯云翻译服务标识
        string host = "tmt.tencentcloudapi.com"; // 就近接入域名
        string action = "TextTranslate"; //接口名称
        string version = "2018-03-21"; //接口版本
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(); //当前时间戳

        string authorization = GenerateTC3Signature(
        secretId, secretKey, timestamp, service, host, region, action, version, requestParams);

        string url = $"https://{host}/";
        string[] headers = new string[]
        {
            "Content-Type: application/json; charset=utf-8",
            "Host: " + host,
            "X-TC-Action: " + action,
            "X-TC-Version: " + version,
            "X-TC-Timestamp: " + timestamp,
            "X-TC-Region: " + region, // 固定 Region
            "Authorization: " + authorization
        };

        string jsonBody = Json.Stringify(requestParams);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

        Error err = tengxunHTTPRequest.RequestRaw(url, headers, HttpClient.Method.Post, bodyBytes);

        if (err != Error.Ok)
        {
            GD.PrintErr($"腾讯翻译请求发送失败: {err}");
            return;
        }

        // 存储回调信息
        _pendingCallbacks[tengxunHTTPRequest] = (text, onCompleted, userData);
    }

    private static string ComputeMD5HexLower(string input) //计算MD5哈希值并返回小写十六进制字符串
    {
        using (var md5 = MD5.Create())
        {
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    private string GenerateTC3Signature(
    string secretId, string secretKey, string timestamp,
    string service, string host, string region, string action, string version,
    Godot.Collections.Dictionary<string, Variant> requestParams)
    {
        //拼接规范请求字符串（CanonicalRequest）
        string httpRequestMethod = "POST";
        string canonicalUri = "/";
        string canonicalQueryString = "";
        string canonicalHeaders =
            "content-type:application/json; charset=utf-8\n" +
            "host:" + host + "\n";
        string signedHeaders = "content-type;host";

        //请求体哈希（SHA256）
        string payload = Json.Stringify(requestParams);
        string hashedRequestPayload = SHA256Hex(payload);
        string canonicalRequest =
            httpRequestMethod + "\n" +
            canonicalUri + "\n" +
            canonicalQueryString + "\n" +
            canonicalHeaders + "\n" +
            signedHeaders + "\n" +
            hashedRequestPayload;

        //拼接待签名字符串（StringToSign）
        string algorithm = "TC3-HMAC-SHA256";
        string requestTimestamp = timestamp;
        string date = DateTimeOffset.FromUnixTimeSeconds(long.Parse(timestamp)).UtcDateTime.ToString("yyyy-MM-dd");
        string credentialScope = $"{date}/{service}/tc3_request";
        string hashedCanonicalRequest = SHA256Hex(canonicalRequest);
        string stringToSign =
            algorithm + "\n" +
            requestTimestamp + "\n" +
            credentialScope + "\n" +
            hashedCanonicalRequest;

        //计算签名
        byte[] secretDate = HmacSHA256(Encoding.UTF8.GetBytes("TC3" + secretKey), date);
        byte[] secretService = HmacSHA256(secretDate, service);
        byte[] secretSigning = HmacSHA256(secretService, "tc3_request");
        byte[] signatureBytes = HmacSHA256(secretSigning, stringToSign);
        string signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();

        //构造 Authorization 头
        string authorization =
            $"{algorithm} Credential={secretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        return authorization;
    }

    // 辅助函数：SHA256 十六进制
    private string SHA256Hex(string data)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }

    // 辅助函数：HMAC-SHA256
    private byte[] HmacSHA256(byte[] key, string data)
    {
        using (HMACSHA256 hmac = new HMACSHA256(key))
        {
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }
    }



    //微软翻译请求完成的回调函数
    private void OnTranslateCompleted(long result, long responseCode, string[] headers, byte[] body)
    {
        GD.Print($"翻译请求完成，响应代码：{responseCode}");

        // 检查是否有对应的回调信息
        if (_pendingCallbacks.TryGetValue(translateHTTPRequest, out var info))
        {
            _pendingCallbacks.Remove(translateHTTPRequest); // 移除已处理的回调信息
            string translatedText = "";

            if (responseCode == 200)
            {
                string jsonString = System.Text.Encoding.UTF8.GetString(body); //将UTF-8字节数组转换为JSON字符串

                //解析JSON响应
                var jsonArray = Json.ParseString(jsonString).AsGodotArray(); //解析JSON字符串为Godot数组
                if (jsonArray.Count > 0)
                {
                    var firstResult = jsonArray[0].AsGodotDictionary(); //获取第一个结果的字典
                    var translations = firstResult["translations"].AsGodotArray(); //获取翻译数组

                    if (translations.Count > 0)
                    {
                        var translation = translations[0].AsGodotDictionary(); //获取第一个翻译的字典
                        translatedText = translation["text"].ToString(); //获取翻译文本
                    }
                }
            }
            else
            {
                GD.PrintErr($"翻译失败: {responseCode}");
            }

            info.callback?.Invoke(info.original, translatedText, info.userData); //调用回调函数
        }
        else
        {
            if (responseCode == 200)
            {
                string jsonString = System.Text.Encoding.UTF8.GetString(body); //将UTF-8字节数组转换为JSON字符串

                //解析JSON响应
                var jsonArray = Json.ParseString(jsonString).AsGodotArray(); //解析JSON字符串为Godot数组
                if (jsonArray.Count > 0)
                {
                    var firstResult = jsonArray[0].AsGodotDictionary(); //获取第一个结果的字典
                    var translations = firstResult["translations"].AsGodotArray(); //获取翻译数组

                    if (translations.Count > 0)
                    {
                        var translation = translations[0].AsGodotDictionary(); //获取第一个翻译的字典
                        string translatedText = translation["text"].ToString(); //获取翻译文本
                        ShowTranslationResult(translatedText);
                    }
                }
            }
        }
    }

    private void OnBaiduTranslateCompleted(long result, long responseCode, string[] headers, byte[] body) //百度翻译请求完成回调
    {
        GD.Print($"百度翻译请求完成，响应代码：{responseCode}");

        if (_pendingCallbacks.TryGetValue(baiduHTTPRequest, out var info))
        {
            _pendingCallbacks.Remove(baiduHTTPRequest); // 移除已处理的回调信息
            string translatedText = "";

            if (responseCode == 200)
            {
                string jsonString = Encoding.UTF8.GetString(body);

                var json = Json.ParseString(jsonString).AsGodotDictionary(); //解析JSON字符串为Godot字典
                if (json.ContainsKey("trans_result"))
                {
                    var transArray = json["trans_result"].AsGodotArray();
                    var sb = new StringBuilder();
                    foreach (var item in transArray)
                    {
                        sb.Append(item.AsGodotDictionary()["dst"].ToString());
                    }
                    translatedText = sb.ToString();
                }
            }
            else
            {
                string errorBody = Encoding.UTF8.GetString(body);
                GD.PrintErr($"百度翻译请求失败，状态码 {responseCode}: {errorBody}");
            }
            info.callback?.Invoke(info.original, translatedText, info.userData); //调用回调函数，译文为空
        }
        else
        {

            if (responseCode == 200)
            {
                string jsonString = Encoding.UTF8.GetString(body);

                var json = Json.ParseString(jsonString).AsGodotDictionary(); //解析JSON字符串为Godot字典
                if (json.ContainsKey("trans_result"))
                {
                    var transArray = json["trans_result"].AsGodotArray();
                    var translatedTextBuilder = new StringBuilder();
                    foreach (var item in transArray)
                    {
                        var dict = item.AsGodotDictionary();
                        translatedTextBuilder.Append(dict["dst"].ToString());                       
                    }
                    string translatedText = translatedTextBuilder.ToString();

                    ShowTranslationResult(translatedText);
                }
            }
            else
            {
                string errorBody = Encoding.UTF8.GetString(body);
                GD.PrintErr($"百度翻译请求失败，状态码 {responseCode}: {errorBody}");
            }
        }
    }

    private void OnTengxunTranslateCompleted(long result, long responseCode, string[] headers, byte[] body) //腾讯翻译请求完成回调
    {
        GD.Print($"腾讯翻译请求完成，响应代码：{responseCode}");
        if (_pendingCallbacks.TryGetValue(tengxunHTTPRequest, out var info))
        {
            _pendingCallbacks.Remove(tengxunHTTPRequest); // 移除已处理的回调信息
            string translatedText = "";
            if (responseCode == 200)
            {
                string jsonString = Encoding.UTF8.GetString(body);
                var json = Json.ParseString(jsonString).AsGodotDictionary(); //解析JSON字符串为Godot字典
                if (json.ContainsKey("Response"))
                {
                    var responseDict = json["Response"].AsGodotDictionary();
                    if (responseDict.ContainsKey("TargetText"))
                    {
                        translatedText = responseDict["TargetText"].ToString();
                    }
                }
            }
            else
            {
                string errorBody = Encoding.UTF8.GetString(body);
                GD.PrintErr($"腾讯翻译请求失败，状态码 {responseCode}: {errorBody}");
            }
            info.callback?.Invoke(info.original, translatedText, info.userData); //调用回调函数，译文为空
        }
        else
        {
            if (responseCode == 200)
            {
                string jsonString = Encoding.UTF8.GetString(body);
                var json = Json.ParseString(jsonString).AsGodotDictionary(); //解析JSON字符串为Godot字典
                if (json.ContainsKey("Response"))
                {
                    var responseDict = json["Response"].AsGodotDictionary();
                    if (responseDict.ContainsKey("TargetText"))
                    {
                        string translatedText = responseDict["TargetText"].ToString();
                        ShowTranslationResult(translatedText);
                    }
                }
            }
            else
            {
                string errorBody = Encoding.UTF8.GetString(body);
                GD.PrintErr($"腾讯翻译请求失败，状态码 {responseCode}: {errorBody}");
            }
        }
    }

    private void ShowTranslationResult(string translatedText)
    {
        //翻译文本显示UI上
        Node found = GetTree().Root.FindChild("TranslationResult", true, false); //在场景树中查找已有的TranslationResult节点
        if (found is TranslationResult existing)
        {
            existing.SetLabel(translatedText);
            return;
        }

        // 若不存在则加载资源（注意资源路径是否和项目一致）
        var scene = GD.Load<PackedScene>("res://changjing/TranslationResultUI.tscn");
        if (scene == null)
        {           
            return;
        }
        var regionSelector = scene.Instantiate<TranslationResult>();
        if (regionSelector == null)
        {
            GD.PrintErr("实例化 TranslationResult 场景失败");
            return;
        }

        // 将实例加入场景树并设置文本
        GetTree().Root.AddChild(regionSelector);
        regionSelector.SetLabel(translatedText);
    }

    

    //JSON转义特殊字符
    private string EscapeJson(string text)
    {
        return text.Replace("\\", "\\\\")
                   .Replace("\"", "\\\"")
                   .Replace("\n", "\\n")
                   .Replace("\r", "\\r")
                   .Replace("\t", "\\t");
    }
}
