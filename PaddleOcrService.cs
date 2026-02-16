using Godot;
using System;
using System.IO;
using System.Drawing;
using PaddleOCRSharp;
using SDBitmap = System.Drawing.Bitmap;

public partial class PaddleOcrService : Node
{
    private PaddleOCREngine _ocrEngine; //PaddleOCR引擎实例
    private bool _isInitialized = false; //是否已初始化
    private string _detPath; //检测模型路径
    private string _recPath; //识别模型路径
    private string _keysPath; //字典文件路径


    // 初始化 OCR 引擎，加载模型
    public void InitializeEngine()
    {
        if (_isInitialized) return;

        // 首先尝试读取用户保存的路径
        string detPath = SaveManager.Instance?.saveData.paddleOcrDetPathEPath;
        string recPath = SaveManager.Instance?.saveData.paddleOcrRecPath;
        string keysPath = SaveManager.Instance?.saveData.paddleOcrKeysPath;

        // 如果保存的路径都存在且有效，直接使用
        if (!string.IsNullOrWhiteSpace(detPath) && !string.IsNullOrWhiteSpace(recPath) && !string.IsNullOrWhiteSpace(keysPath))
        {
            if (Directory.Exists(detPath) && Directory.Exists(recPath) && File.Exists(keysPath))
            {
                _detPath = detPath;
                _recPath = recPath;
                _keysPath = keysPath;
                GD.Print("使用保存的模型路径");
                GD.Print($"检测模型: {_detPath}");
                GD.Print($"识别模型: {_recPath}");
                GD.Print($"字典: {_keysPath}");
            }
            else
            {
                GD.Print("保存的模型路径无效，将尝试自动定位 models目录...");
            }
        }

        // 如果任意一个路径为空或无效，则按优先级查找 models目录
        if (string.IsNullOrWhiteSpace(_detPath) || string.IsNullOrWhiteSpace(_recPath) || string.IsNullOrWhiteSpace(_keysPath))
        {
            // 构造候选路径并打印，方便调试
            var exePath = OS.GetExecutablePath(); // Godot 可执行文件路径（编辑器中为编辑器 exe）
            var exeDir = System.IO.Path.GetDirectoryName(exePath) ?? System.IO.Path.GetFullPath(".");
            var exeModels = System.IO.Path.Combine(exeDir, "models");

            var resModels = ProjectSettings.GlobalizePath("res://models");
            var userModels = ProjectSettings.GlobalizePath("user://models");

            string modelsDir = null;

            // 开发时优先使用 res://models（编辑器中的项目目录）
            if (!string.IsNullOrEmpty(resModels) && Directory.Exists(resModels))
            {
                modelsDir = resModels;
                GD.Print("[PaddleOCR] 使用 res://models（开发环境）");
            }
            // 导出后通常会把 models 放在 exe 同目录
            else if (Directory.Exists(exeModels))
            {
                modelsDir = exeModels;
                GD.Print("[PaddleOCR] 使用 exe 同目录下的 models（导出环境）");
            }
            // 用户目录下可能是解包后的模型（user://models）
            else if (!string.IsNullOrEmpty(userModels) && Directory.Exists(userModels))
            {
                modelsDir = userModels;
            }

            if (modelsDir != null)
            {
                _detPath = System.IO.Path.Combine(modelsDir, "det_infer");
                _recPath = System.IO.Path.Combine(modelsDir, "rec_infer");
                _keysPath = System.IO.Path.Combine(modelsDir, "japan_dict.txt");

                // 把定位到的路径保存到用户设置，方便下次快速使用
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.saveData.paddleOcrDetPathEPath = _detPath;
                    SaveManager.Instance.saveData.paddleOcrRecPath = _recPath;
                    SaveManager.Instance.saveData.paddleOcrKeysPath = _keysPath;
                    SaveManager.Instance.SaveDataToFile();
                }
            }
            else
            {
                GD.PrintErr("未能在候选位置找到 models 文件夹。请把 models 文件夹放到项目根（res://models）用于开发，或放到 exe 同目录用于发布。");
            }
        }

        if (string.IsNullOrWhiteSpace(_detPath) || string.IsNullOrWhiteSpace(_recPath) || string.IsNullOrWhiteSpace(_keysPath)
            || !Directory.Exists(_detPath) || !Directory.Exists(_recPath) || !File.Exists(_keysPath))
        {
            GD.PrintErr("模型文件缺失，请检查：");
            GD.PrintErr($"检测模型: {_detPath}");
            GD.PrintErr($"识别模型: {_recPath}");
            GD.PrintErr($"字典: {_keysPath}");
            GD.PrintErr("请确认 models 文件夹结构为：models/det_infer, models/rec_infer, 并包含字典 japan_dict.txt。\n开发时可放在项目根（res://models），发布时放在 exe 同目录。");
            return;
        }
        GD.Print("模型文件夹和字典文件检查通过");
        
        try
        {            
            _ocrEngine = new PaddleOCREngine();

            _ocrEngine.ModifyParameter(new ModifyParameter
            {
                m_det = true,
                m_rec = true,
            });

            
            _isInitialized = true;
            GD.Print("PaddleOCR 引擎初始化成功");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"PaddleOCR 引擎初始化失败: {ex.Message}");
        }
    }

    //识别图片中的文字
    public void Recognize(byte[] imageData)
    {
        // 在第一次识别时初始化引擎
        if (!_isInitialized || _ocrEngine == null)
        {
            InitializeEngine();
            // 如果初始化后仍然为null，说明初始化失败，直接返回
            if (_ocrEngine == null)
            {
                GD.PrintErr("PaddleOCR 引擎初始化失败，无法识别");
                return;
            }
        }

        try
        {
            using (var ms = new MemoryStream(imageData)) // 将字节数组转换为内存流
            using (SDBitmap bitmap = new SDBitmap(ms))   // 从内存流创建 Bitmap 对象
            {
                var result = _ocrEngine.DetectText(bitmap); //只进行文本检测，后续可以根据需要调用识别方法

                System.Text.StringBuilder sb = new System.Text.StringBuilder(); //使用 StringBuilder 来高效地构建识别结果字符串
                // 遍历识别结果
                foreach (var item in result.TextBlocks) //根据置信度过滤识别结果，默认阈值为0.4，可以根据实际情况调整
                {
                    if (item.Score > 0.4)
                    {
                        sb.Append(item.Text + " "); //将识别到的文本添加到 StringBuilder 中，并在每个文本块之间添加空格
                    }
                }

                string recognizedText = sb.ToString().Trim(); //将 StringBuilder 转换为字符串，并去除首尾的空格
                if (!string.IsNullOrEmpty(recognizedText))
                {
                    TranslationSource.Instance.GetText(recognizedText);
                }
                else
                {
                    ErrorWindow.ShowError("PaddleOCR 未识别到任何文本");
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"PaddleOCR识别失败: {ex.Message}");
        }
    }

    public override void _ExitTree()
    {
        _ocrEngine?.Dispose();
        base._ExitTree();
    }
}