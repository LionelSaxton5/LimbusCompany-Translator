using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public partial class InlineTranslation : Node //内嵌式翻译
{
    private Queue<TranslationTask> taskQueue = new Queue<TranslationTask>(); //翻译任务队列
    private bool isProcessing = false; //是否正在处理任务队列

    public int totalTasks; //当前队列中的任务总数
    public int completedTasks = 0; //已完成的任务数
    private static readonly object _fileLock = new object(); //任务锁,防止多个任务同时写同一个文件导致冲突

    //JSON内存缓存字典，防止重复读取硬盘
    private Dictionary<string, Godot.Collections.Dictionary> _jsonCache = new Dictionary<string, Godot.Collections.Dictionary>(); //文件路径 -> JSON内容的字典

    public event Action<int, int> OnProgressUpdated; //翻译进度更新事件(已完成任务数,总任务数)

    public class TranslationTask //翻译任务
    {
        public string FilePath { get; set; } //来自哪个文件
        public int ElementIndex { get; set; } //来自文件中的第几个元素
        public string OriginalText { get; set; } //原文
        public Dictionary<string, string> TagMap { get; set; } //标签映射（如果需要处理文本中的特殊标签，可以在这里存储原文中的标签及其位置，翻译完成后再将标签插回译文中）
        public Action<string> WriteBack { get; set; } //写回回调，参数为译文
    }

    public void EnqueueTask(string filePath, int elementIndex, string originalText, Dictionary<string, string> tagMap, Action<string> writeBack)
    {
        taskQueue.Enqueue(new TranslationTask
        {
            FilePath = filePath,
            ElementIndex = elementIndex,
            OriginalText = originalText,
            TagMap = tagMap,
            WriteBack = writeBack ?? (translated => WriteTranslationToFile(filePath, elementIndex, translated)) //委托,提供回调方法
        });

        totalTasks++;
        OnProgressUpdated?.Invoke(completedTasks, totalTasks);
    }

    public void StartBatchTranslation(List<string> filePaths)  //获取原文
    {
        //把所有文件的翻译任务加入队列
        foreach (var item in filePaths)
        {
            CreateTasksFromFile(item);
        }
    }

    public async Task StartProcessingIfNeededAsync()
    {
        if (isProcessing || taskQueue.Count == 0) return;
        isProcessing = true;

        var tasksToProcess = new List<TranslationTask>(taskQueue);  //复制当前队列中的任务到一个新的列表，避免在处理过程中修改原队列
        taskQueue.Clear();

        totalTasks = tasksToProcess.Count;
        completedTasks = 0;

        // 立即触发一次UI更新，让进度条显示 0 / 总任务数，避免一开始UI没有反应
        Callable.From(() => TriggerProgress(completedTasks, totalTasks)).CallDeferred();

        //调用并行管理器,传入任务列表和一个回调来更新已完成任务数
        await AsyncTranslationManager.Instance.ProcessTasksParallel(
            tasksToProcess, 16, (done, total) => 
            {
                completedTasks = done;
                CallDeferred(nameof(TriggerProgress), done, total);
            }); //等待所有任务完成

        SaveAllTranslatedFiles();

        isProcessing = false;
    }

    public void TriggerProgress(int done, int total)
    {
        OnProgressUpdated?.Invoke(done, total);
    }

    private void CreateTasksFromFile(string filePath) //一般文件的任务创建逻辑，处理"content"键
    {
        string jsonSteing = File.ReadAllText(filePath, Encoding.UTF8); // 读取文件内容,转UTF-8编码
        var json = Json.ParseString(jsonSteing); // 解析JSON字符串
        var jsonDict = json.AsGodotDictionary(); // 转换为Godot字典
        var dataArray = jsonDict["dataList"].AsGodotArray(); // 拿到键的值并转换为Godot数组

        int index = 0;
        foreach (var item in dataArray) //遍历数组，每个元素都是一个字典
        {
            int capturedIndex = index;

            var itemDict = item.AsGodotDictionary(); //转换为字典

            if (itemDict.ContainsKey("content"))
            {
                string originalText = itemDict["content"].ToString(); //获取原文

                //直接发送HTTP请求翻译，会阻塞主线程,创建任务加入队列
                EnqueueTask(filePath, capturedIndex, originalText, null,
                            translated => WriteTranslationToFile(filePath, capturedIndex, translated));
            }
            index++;
        }
    }

    public void SaveAllTranslatedFiles()
    {
        lock (_fileLock)
        {
            foreach (var kvp in _jsonCache)
            {
                string filePath = kvp.Key;
                Godot.Collections.Dictionary jsonDict = kvp.Value;

                try
                {
                    // 转回带缩进的 JSON
                    string jsonString = Godot.Json.Stringify(jsonDict, "\t");
                    
                    // 保留你原先很好的逻辑：修复Godot Json化后把整数变成 X.0 的毛病
                    jsonString = Regex.Replace(jsonString, @"(\d+)\.0(?!\d)", "$1");

                    // 覆盖写入
                    System.IO.File.WriteAllText(filePath, jsonString, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"保存文件失败 [{filePath}]: {ex.Message}");
                }
            }

            _jsonCache.Clear(); 
        }
    }


    //辅助方法
    //获取或加载JSON到缓存
    private Godot.Collections.Dictionary GetOrLoadJson(string filePath)
    {
        if (!_jsonCache.TryGetValue(filePath, out var jsonDict)) //获取缓存，如果没有就加载
        {
            string jsonString = File.ReadAllText(filePath, Encoding.UTF8); //读取文件内容,转UTF-8编码
            jsonDict = Json.ParseString(jsonString).AsGodotDictionary(); //解析JSON字符串并转换为Godot字典
            _jsonCache[filePath] = jsonDict; //存入缓存
        }
        return jsonDict;
    }

    //写回文件方法
    private void WriteTranslationToFile(string filePath, int elementIndex, string translatedText) //将翻译结果写回文件
    {
        try
        {
            if (string.IsNullOrEmpty(translatedText))
            {
                return;
            }

            lock (_fileLock)
            {
                //从缓存读取和修改，不再实时操作硬盘
                var jsonDict = GetOrLoadJson(filePath);
                var dataArray = jsonDict["dataList"].AsGodotArray();

                if (elementIndex >= 0 && elementIndex < dataArray.Count)
                {
                    var item = dataArray[elementIndex].AsGodotDictionary();
                    if (item.ContainsKey("content"))
                    {
                        item["content"] = translatedText;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"写回文件失败 [{filePath}]: {ex.Message}");
        }
    }

    // --- 通用写回方法：适用于大多数 dataList[i].key = value 的结构 ---
    public void WriteGenericField(string filePath, int elementIndex, string fieldName, string translatedText)
    {
        if (string.IsNullOrWhiteSpace(translatedText)) return;

        lock (_fileLock) //确保同一时间只有一个线程在操作文件
        {
            try
            {
                //修改缓存，不再实时操作硬盘
                var jsonDict = GetOrLoadJson(filePath);
                var dataArray = jsonDict["dataList"].AsGodotArray();

                if (elementIndex >= 0 && elementIndex < dataArray.Count)
                {
                    var item = dataArray[elementIndex].AsGodotDictionary();
                    if (item.ContainsKey(fieldName))
                    {
                        item[fieldName] = translatedText;
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"通用写回失败 [{filePath}] 字段 {fieldName}: {ex.Message}");
            }
        }
    }


    // --- 针对技能系统的特殊写回（嵌套结构较深） ---
    public void WriteSkillLevelName(string filePath, int dataIndex, int levelIndex, string translatedText)
    {
        lock (_fileLock)
        {
            try
            {
                var jsonDict = GetOrLoadJson(filePath);
                var dataArray = jsonDict["dataList"].AsGodotArray();
                var item = dataArray[dataIndex].AsGodotDictionary();
                var levelList = item["levelList"].AsGodotArray();
                var level = levelList[levelIndex].AsGodotDictionary();
                level["name"] = translatedText;
            }
            catch (Exception ex) { GD.PrintErr($"写回技能名失败: {ex.Message}"); }
        }
    }

    public void WriteSkillLevelDesc(string filePath, int dataIndex, int levelIndex, string translatedText)
    {
        lock (_fileLock)
        {
            try
            {
                var jsonDict = GetOrLoadJson(filePath);

                var dataArray = jsonDict["dataList"].AsGodotArray();
                var item = dataArray[dataIndex].AsGodotDictionary();
                var levelList = item["levelList"].AsGodotArray();
                var level = levelList[levelIndex].AsGodotDictionary();
                level["desc"] = translatedText;
            }
            catch (Exception ex) { GD.PrintErr($"写回技能描述失败: {ex.Message}"); }
        }
    }

    public void WriteCoinDesc(string filePath, int dataIndex, int levelIndex, int coinIndex, int coindescIndex, string translatedText)
    {
        lock (_fileLock)
        {
            try
            {
                var jsonDict = GetOrLoadJson(filePath);

                var dataArray = jsonDict["dataList"].AsGodotArray();
                var item = dataArray[dataIndex].AsGodotDictionary();
                var levelList = item["levelList"].AsGodotArray();
                var level = levelList[levelIndex].AsGodotDictionary();
                var coinlist = level["coinlist"].AsGodotArray();
                var coin = coinlist[coinIndex].AsGodotDictionary();
                var coindescs = coin["coindescs"].AsGodotArray();
                var coindesc = coindescs[coindescIndex].AsGodotDictionary();
                coindesc["desc"] = translatedText;
            }
            catch (Exception ex) { GD.PrintErr($"写回硬币描述失败: {ex.Message}"); }
        }
    }
}
