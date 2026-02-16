using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

public partial class InlineTranslation : Node //内嵌式翻译
{
	private Queue<TranslationTask> taskQueue = new Queue<TranslationTask>(); //翻译任务队列
	private bool isProcessing = false; //是否正在处理任务队列

	public int totalTasks; //当前队列中的任务总数
	public int completedTasks = 0; //已完成的任务数

	public event Action<int, int> OnProgressUpdated; //翻译进度更新事件(已完成任务数,总任务数)

    private class TranslationTask //翻译任务
    {
		public string FilePath { get; set; } //来自哪个文件
        public int ElementIndex { get; set; } //来自文件中的第几个元素
        public string OriginalText { get; set; } //原文
    }

    public void StartBatchTranslation(List<string> filePaths)  //获取原文
    {
        totalTasks = 0; //重置总任务数
        completedTasks = 0; //重置已完成任务数
		taskQueue.Clear(); //清空任务队列

        //把所有文件的翻译任务加入队列
        foreach (var item in filePaths)
		{
            int tasksInFile = CountTasksInFile(item); //计算文件中的任务数
            totalTasks += tasksInFile; //累加到总任务数

            CreateTasksFromFile(item);
        }

		OnProgressUpdated?.Invoke(0, totalTasks); //触发进度更新事件

        if (!isProcessing && taskQueue.Count > 0)
		{
			isProcessing = true;
			ProcessNextTask();
        }
    }

	private void CreateTasksFromFile(string filePath)
	{ 
		string jsonSteing = File.ReadAllText(filePath, Encoding.UTF8); // 读取文件内容,转UTF-8编码
        var json = Json.ParseString(jsonSteing); // 解析JSON字符串
		var jsonDict = json.AsGodotDictionary(); // 转换为Godot字典
		var dataArray = jsonDict["dataList"].AsGodotArray(); // 拿到键的值并转换为Godot数组


		int index = 0;
        foreach (var item in dataArray) //遍历数组，每个元素都是一个字典
		{
			var itemDict = item.AsGodotDictionary(); //转换为字典

			if (itemDict.ContainsKey("content"))
			{
				string originalText = itemDict["content"].ToString(); //获取原文

                //直接发送HTTP请求翻译，会阻塞主线程，改为创建任务加入队列
                taskQueue.Enqueue(new TranslationTask
				{
					FilePath = filePath,
					ElementIndex = index,
					OriginalText = originalText
                }); //创建翻译任务并加入队列
            }

			index++;
        }
    }

	private void ProcessNextTask() //处理队首任务
	{
        if (taskQueue.Count == 0)
		{
			isProcessing = false;
			return;
        }
        TranslationTask currentTask = taskQueue.Dequeue(); //取出队首任务

        if (SaveManager.Instance.saveData.isMicrosofttranslationEnable)
        {
            TranslationSource.Instance.SendTranslateRequest
            (
                currentTask.OriginalText,
                fromLang: "ja",
                toLang: "zh-Hans",
                onCompleted: OnTranslationCompleted, //翻译完成回调,调用时传递原文和译文、用户数据(任务)
                userData: currentTask //传递当前任务作为用户数据
            );
        }
        else if (SaveManager.Instance.saveData.isBaidutranslationEnable)
        {
            TranslationSource.Instance.BaidutranslateRequest
            (
                currentTask.OriginalText,
                fromLang: "ja",
                toLang: "zh-Hans",
                onCompleted: OnTranslationCompleted,
                userData: currentTask
            );
        }
        else if (SaveManager.Instance.saveData.isTengxuntranslationEnable)
        {
            TranslationSource.Instance.TengxuntranslateRequest
            (
                currentTask.OriginalText,
                fromLang: "ja",
                toLang: "zh-Hans",
                onCompleted: OnTranslationCompleted,
                userData: currentTask
            );
        }
    }

    private int CountTasksInFile(string filePath) //计算文件中的任务数（即包含"content"键的元素数量）
    {
        try
        {
            string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
            var json = Json.ParseString(jsonString).AsGodotDictionary();
            var dataArray = json["dataList"].AsGodotArray();
            return dataArray.Count;
        }
        catch
        {
            return 0; // 如果读取出错，视为0个任务
        }
    }

    //翻译完成回调
    private void OnTranslationCompleted(string originalText, string translatedText, object userData)
	{ 
		var task = userData as TranslationTask; //获取传递的任务数据
		if (task == null)
			return;

		bool success = WriteTranslationToFile(task.FilePath, task.ElementIndex, translatedText); //路径、元素索引、译文

        if (success)
        {
			completedTasks++;
			OnProgressUpdated?.Invoke(completedTasks, totalTasks); //触发进度更新事件
        }
        else
        {
            GD.PrintErr($"写入失败: {task.FilePath} [{task.ElementIndex}]");
            // 可选：重试逻辑（重新入队）
        }

        ProcessNextTask(); //处理下一个任务
    }

	private bool WriteTranslationToFile(string filePath, int elementIndex, string translatedText) //将翻译结果写回文件
    {
		try
		{
            if (string.IsNullOrEmpty(translatedText))
            {
                GD.PrintErr($"译文为空，跳过写入：{filePath} [{elementIndex}]");
                return false;
            }

            string jsonString = File.ReadAllText(filePath, Encoding.UTF8); // 读取文件内容,转UTF-8编码
			var json = Json.ParseString(jsonString); // 解析JSON字符串
			var jsonDict = json.AsGodotDictionary(); // 转换为Godot字典
			var dataArray = jsonDict["dataList"].AsGodotArray(); // 拿到键的值并转换为Godot数组

			if (elementIndex >= 0 && elementIndex < dataArray.Count)
			{
				var item = dataArray[elementIndex].AsGodotDictionary(); //转换为字典

				if (item.ContainsKey("content"))
				{               
                    item["content"] = translatedText; //更新翻译文本
                }
            }

            string newJson = Json.Stringify(jsonDict);
            File.WriteAllText(filePath, newJson, Encoding.UTF8); // 将新的JSON字符串写回文件，使用UTF-8编码
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"写回文件失败 [{filePath}]: {ex.Message}");
            return false;
        }
    }
}
