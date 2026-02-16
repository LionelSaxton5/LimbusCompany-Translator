using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using static SaveManager;

public partial class SaveManager : Node
{
	private static SaveManager _instance; //单例实例
	public static SaveManager Instance => _instance;

	public override void _Ready()
	{
		_instance = this;
		LoadData();
    }

	public class SaveData
	{
		public string umiOcrPath { get; set; } = ""; //Umi-OCR路径
		public bool isUmiOcrEnable { get; set; } = false; //是否启用Umi-OCR
		public string paddleOcrDetPathEPath { get; set; } = ""; //paddle-OCR检测模型路径
		public string paddleOcrRecPath { get; set; } = ""; //paddle-OCR识别模型路径
		public string paddleOcrKeysPath { get; set; } = ""; //paddle-OCR字符集路径
        public bool isPaddleOcrEnable { get; set; } = false; //是否启用paddle-OCR
        public string gameExePath { get; set; } = ""; //游戏可执行文件路径

		public string MicrosofttranslationKey { get; set; } = ""; //微软翻译API密钥
		public string MicrosoftranslationUrl { get; set; } = ""; //微软翻译API端点
		public bool isMicrosofttranslationEnable { get; set; } = false; //微软翻译是否启用

		public string BaidutranslationKey { get; set; } = ""; //百度翻译API密钥
		public string BaidutranslationUrl { get; set; } = ""; //百度翻译API端点
		public bool isBaidutranslationEnable { get; set; } = false; //百度翻译是否启用

		public string TengxuntranslationKey { get; set; } = ""; //腾讯翻译API密钥
		public string TengxuntranslationUrl { get; set; } = ""; //腾讯翻译API端点
		public bool isTengxuntranslationEnable { get; set; } = false; //腾讯翻译是否启用
    }

	public SaveData saveData = new SaveData();
	private const string savePath = "user://save_data.json"; //保存文件路径

	public void SaveDataToFile() //保存数据到文件
	{
        try
		{
            var options = new JsonSerializerOptions { WriteIndented = true }; // 美化输出的JSON格式
            string jsonString = JsonSerializer.Serialize(saveData, options);  //将SaveData对象序列化为JSON字符串

            using (FileAccess file = FileAccess.Open(savePath, FileAccess.ModeFlags.Write)) //打开文件进行写入
			{
				file.StoreString(jsonString); //将JSON字符串写入文件
				file.Close(); //关闭文件
            }
		}
		catch (Exception ex)
		{
			GD.PrintErr("保存配置失败: " + ex.Message);
		}
	}

	public void LoadData() //加载保存数据
	{
        try
		{
			if (!FileAccess.FileExists(savePath))
			{
				GD.Print("未找到保存文件，使用默认配置");
				return;
			}
			using (FileAccess file = FileAccess.Open(savePath, FileAccess.ModeFlags.Read)) //打开文件进行读取
			{
				string jsonString = file.GetAsText(); //读取文件内容为字符串
				file.Close(); //关闭文件

				if (string.IsNullOrEmpty(jsonString))
				{
					GD.Print("保存文件为空，跳过加载");
					return;
				}

                var loaded = JsonSerializer.Deserialize<SaveData>(jsonString); //将JSON字符串反序列化为SaveData对象
                if (loaded != null)
                {
                    saveData = loaded;
                    
                }
            }
		}
		catch (Exception ex)
		{
			GD.PrintErr("加载配置失败: " + ex.Message);
		}
	}	
}
