using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

public partial class SkillFile : Node //技能文件
{
    private string gameInstallPath = SaveManager.Instance.saveData.gameExePath; //游戏安装路径
    public InlineTranslation inlineTranslation; //内嵌式翻译实例


    public void FindEnemyPassive(int chapter) //敌方被动、效果、我方助战
    {
        List<string> result = new List<string>();

        string enemySkillPath = Path.Combine(gameInstallPath, "LimbusCompany_Data", "Lang", "Jp_zh-cn"); //敌人技能文件夹路径

        if (!Directory.Exists(enemySkillPath))
            return;

        for (int i = 1; i <= 6; i++)
        {
            string[] files = {
                $"JP_BattleKeywords-a1c{chapter}p{i}.json",
                $"JP_Bufs-a1c{chapter}p{i}.json",
                $"JP_Passives_Abnormality-a1c{chapter}p{i}.json",
                $"JP_Passives_Assist-a1c{chapter}p{i}.json",
                $"JP_Passives_Enemy-a1c{chapter}p{i}.json"
            };

            foreach (var f in files)
            {               
                string fullPath = Path.Combine(enemySkillPath, f);
                if (File.Exists(fullPath))
                {
                    string newFileName = Path.Combine(enemySkillPath, f.Substring(3));

                    try
                    {
                        File.Move(fullPath, newFileName); // 改名操作
                        result.Add(newFileName);          // 加入列表
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"重命名文件失败: {ex.Message}");
                        result.Add(fullPath); // 失败了就用老名字继续
                    }
                }
            }
        }

         foreach (var file in result)
        {
            SklisCreateTasksFromFile(file); //对找到的文件进行任务创建
        }
    }

    private void SklisCreateTasksFromFile(string filePath) //技能敌人文件的任务创建逻辑，处理"name"和"desc"两个键
    {
        string jsonString = File.ReadAllText(filePath, Encoding.UTF8); // 读取文件内容,转UTF-8编码
        var json = Json.ParseString(jsonString); // 解析JSON字符串
        var jsonDict = json.AsGodotDictionary(); // 转换为Godot字典
        var dataArray = jsonDict["dataList"].AsGodotArray();

        int index = 0;
        foreach (var item in dataArray)
        {
            int capturedIndex = index;
            var itemDict = item.AsGodotDictionary();
          
            QueueTranslationForField(itemDict, filePath, capturedIndex, "name");
            QueueTranslationForField(itemDict, filePath, capturedIndex, "desc");
            QueueTranslationForField(itemDict, filePath, capturedIndex, "summary");
            QueueTranslationForField(itemDict, filePath, capturedIndex, "flavor");

            index++;
        }
    }

    //辅助方法：统一处理不同字段的入队任务
    private void QueueTranslationForField(Godot.Collections.Dictionary itemDict, string filePath, int elementIndex, string fieldName)
    {
        if (itemDict.ContainsKey(fieldName))
        {
            string originalText = itemDict[fieldName].ToString();

            // 跳过空文本或占位符，节省 API 额度
            if (string.IsNullOrWhiteSpace(originalText) || originalText == "-") return;

            var (processed, tagMap) = ProtectTags(originalText);

            inlineTranslation.EnqueueTask(filePath, elementIndex, processed, tagMap, translated =>
            {
                // 精确写回对应字段名
                inlineTranslation.WriteGenericField(filePath, elementIndex, fieldName, translated);
            });
        }
    }

    public void FindEnemySkills(int chapter) //敌方技能
    {
        List<string> result = new List<string>();

        string enemySkillPath = Path.Combine(gameInstallPath, "LimbusCompany_Data", "Lang", "Jp_zh-cn"); //敌人技能文件夹路径

        if (!Directory.Exists(enemySkillPath))
            return;

        for (int i = 1; i <= 5; i++)
        {
            string[] files =
            { 
                $"JP_Skills_Abnormality-a1c{chapter}p{i}.json", //敌方技能效果
                $"JP_Skills_Assist-a1c{chapter}p{i}.json", //我方助战技能
                $"JP_Skills_Enemy-a1c{chapter}p{i}.json"  //敌方技能         
            };

            foreach (var f in files)
            {
                string fullPath = Path.Combine(enemySkillPath, f);
                if (File.Exists(fullPath))
                {
                    string newFileName = Path.Combine(enemySkillPath, f.Substring(3)); //去掉开头的 "JP_"

                    try
                    {
                        File.Move(fullPath, newFileName); // 改名操作
                        result.Add(newFileName);          // 加入列表
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"重命名文件失败: {ex.Message}");
                        result.Add(fullPath); // 失败了就用老名字继续
                    }
                }
            }
        }

        foreach (var file in result)
        {
            EnemySkillsFromFile(file); //对找到的文件进行任务创建
        }
    }

    private void EnemySkillsFromFile(string filePath) //敌方技能创建任务
    {
        string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
        var json = Json.ParseString(jsonString);
        var jsonDict = json.AsGodotDictionary();
        var dataArray = jsonDict["dataList"].AsGodotArray();

        int dataIndex = 0;
        foreach (var item in dataArray)
        {
            var itemDict = item.AsGodotDictionary();
            var levelList = itemDict["levelList"].AsGodotArray();

            int levelIndex = 0;
            foreach (var levelItem in levelList)
            {
                var levelDict = levelItem.AsGodotDictionary();

                // ---- name ----
                if (levelDict.ContainsKey("name"))
                {
                    string original = levelDict["name"].ToString();
                    var (processed, tagMap) = ProtectTags(original);
                    int capData = dataIndex, capLevel = levelIndex;
                    inlineTranslation.EnqueueTask(
                        filePath,
                        -1, // 这里传 -1 表示不用这个索引，或者你可以传 levelIndex 但实际不用
                        processed,
                        tagMap,
                        translated => inlineTranslation.WriteSkillLevelName(filePath, capData, capLevel, translated)
                    );
                }

                // ---- desc ----
                if (levelDict.ContainsKey("desc"))
                {
                    string original = levelDict["desc"].ToString();
                    var (processed, tagMap) = ProtectTags(original);
                    int capData = dataIndex, capLevel = levelIndex;
                    inlineTranslation.EnqueueTask(
                        filePath,
                        -1,
                        processed,
                        tagMap,
                        translated => inlineTranslation.WriteSkillLevelDesc(filePath, capData, capLevel, translated)
                    );
                }

                // ---- coinlist ----
                if (levelDict.ContainsKey("coinlist"))
                {
                    var coinlist = levelDict["coinlist"].AsGodotArray();
                    int coinIndex = 0;
                    foreach (var coinItem in coinlist)
                    {
                        var coinDict = coinItem.AsGodotDictionary();
                        if (coinDict.ContainsKey("coindescs"))
                        {
                            var coindescs = coinDict["coindescs"].AsGodotArray();
                            int coindescIndex = 0;
                            foreach (var coindescItem in coindescs)
                            {
                                var coindescDict = coindescItem.AsGodotDictionary();
                                if (coindescDict.ContainsKey("desc"))
                                {
                                    string original = coindescDict["desc"].ToString();
                                    var (processed, tagMap) = ProtectTags(original);
                                    int capData = dataIndex, capLevel = levelIndex, capCoin = coinIndex, capCoindesc = coindescIndex;
                                    inlineTranslation.EnqueueTask(
                                        filePath,
                                        -1,
                                        processed,
                                        tagMap,
                                        translated => inlineTranslation.WriteCoinDesc(filePath, capData, capLevel, capCoin, capCoindesc, translated)
                                    );
                                }
                                coindescIndex++;
                            }
                        }
                        coinIndex++;
                    }
                }
                levelIndex++;
            }
            dataIndex++;
        }
    }

    public void FindEnemyBubble(int chapter) //敌方技能气泡
    {
        List<string> result = new List<string>();

        string enemySkillPath = Path.Combine(gameInstallPath, "LimbusCompany_Data", "Lang", "Jp_zh-cn"); //敌人技能文件夹路径

        if (!Directory.Exists(enemySkillPath))
            return;

        for (int i = 1; i <= 4; i++)
        {
            string battleSpeechBubbleDlg = $"JP_BattleSpeechBubbleDlg-a1c{chapter}p{i}.json"; //敌方技能气泡

            string fullPath = Path.Combine(enemySkillPath, battleSpeechBubbleDlg);
            if (File.Exists(fullPath))
            {
                string newfileName = Path.Combine(enemySkillPath, $"BattleSpeechBubbleDlg-a1c{chapter}p{i}.json");
                try
                {
                    File.Move(fullPath, newfileName); //改名操作
                    result.Add(newfileName);
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"重命名文件失败: {ex.Message}");
                    result.Add(fullPath); // 如果重命名失败，仍然添加原文件
                }
            }
        }

        foreach (var file in result)
        {
            EnemyBubbleFromFile(file); //对找到的文件进行任务创建
        }
    }

    private void EnemyBubbleFromFile(string filePath) //气泡创建任务
    {
        string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
        var json = Json.ParseString(jsonString);
        var jsonDict = json.AsGodotDictionary();
        var dataArray = jsonDict["dataList"].AsGodotArray();

        int index = 0;
        foreach (var item in dataArray)
        {
            var itemDict = item.AsGodotDictionary();
            if (itemDict.ContainsKey("dlg"))
            {
                string originalText = itemDict["dlg"].ToString();
                var (processed, tagMap) = ProtectTags(originalText);
                int capturedIndex = index;

                inlineTranslation.EnqueueTask(filePath, capturedIndex, processed, tagMap, translated =>
                {
                    inlineTranslation.WriteGenericField(filePath, capturedIndex, "dlg", translated);
                });
            }
            index++;
        }
    }

    public void FindEnemyPanicInfo(int chapter) //敌方恐慌信息
    {
        List<string> result = new List<string>();

        string enemySkillPath = Path.Combine(gameInstallPath, "LimbusCompany_Data", "Lang", "Jp_zh-cn"); //敌人技能文件夹路径

        if (!Directory.Exists(enemySkillPath))
            return;

        for (int i = 1; i <= 4; i++)
        {
            string panicInfo = $"JP_PanicInfo-a1c{chapter}p{i}.json"; //敌方恐慌信息
            string fullPath = Path.Combine(enemySkillPath, panicInfo);

            if (File.Exists(fullPath))
            {
                string newfileName = Path.Combine(enemySkillPath, $"PanicInfo-a1c{chapter}p{i}.json");
                try
                {
                    File.Move(fullPath, newfileName); //改名操作
                    result.Add(newfileName);
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"重命名文件失败: {ex.Message}");
                    result.Add(fullPath); // 如果重命名失败，仍然添加原文件
                }
            }
        }

        foreach (var file in result)
        {

            EnemyPanicInfoFromFile(file); //对找到的文件进行任务创建
        }
    }

    private void EnemyPanicInfoFromFile(string filePath) //恐慌创建任务
    {
        string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
        var json = Json.ParseString(jsonString);
        var jsonDict = json.AsGodotDictionary();
        var dataArray = jsonDict["dataList"].AsGodotArray();

        int index = 0;
        foreach (var item in dataArray)
        {
            var itemDict = item.AsGodotDictionary();
            if (itemDict.ContainsKey("panicName"))
            {
                string originalText = itemDict["panicName"].ToString();
                var (processed, tagMap) = ProtectTags(originalText);
                int capturedIndex = index;

                inlineTranslation.EnqueueTask(filePath, capturedIndex, processed, tagMap, translated =>
                {
                    inlineTranslation.WriteGenericField(filePath, capturedIndex, "panicName", translated);
                });
            }
            if (itemDict.ContainsKey("lowMoraleDescription"))
            {
                string originalText = itemDict["lowMoraleDescription"].ToString();
                var (processed, tagMap) = ProtectTags(originalText);
                int capturedIndex = index;
                inlineTranslation.EnqueueTask(filePath, capturedIndex, processed, tagMap, translated =>
                {
                    inlineTranslation.WriteGenericField(filePath, capturedIndex, "lowMoraleDescription", translated);
                });
            }
            if (itemDict.ContainsKey("panicDescription"))
            {
                string originalText = itemDict["panicDescription"].ToString();
                var (processed, tagMap) = ProtectTags(originalText);
                int capturedIndex = index;
                inlineTranslation.EnqueueTask(filePath, capturedIndex, processed, tagMap, translated =>
                {
                    inlineTranslation.WriteGenericField(filePath, capturedIndex, "panicDescription", translated);
                });
            }
            index++;
        }
    }

    public void FildStageNode(int chapter) //关卡名字
    {
        List<string> result = new List<string>();

        string stageNodePath = Path.Combine(gameInstallPath, "LimbusCompany_Data", "Lang", "Jp_zh-cn"); //关卡节点文件夹路径
        if (!Directory.Exists(stageNodePath))
            return;

        for (int i = 1; i <= 4; i++)
        {
            string stageNode = $"JP_StageNode-a1c{chapter}p{i}.json"; //关卡节点
            string fullPath = Path.Combine(stageNodePath, stageNode);
            if (File.Exists(fullPath))
            {
                string newfileName = Path.Combine(stageNodePath, $"StageNode-a1c{chapter}p{i}.json");
                try
                {
                    File.Move(fullPath, newfileName); //改名操作
                    result.Add(newfileName);
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"重命名文件失败: {ex.Message}");
                    result.Add(fullPath); // 如果重命名失败，仍然添加原文件
                }
            }
        }

        foreach (var file in result)
        {
            StageNodeFromFile(file); //对找到的文件进行任务创建
        }
    }

    private void StageNodeFromFile(string filePath) //关卡节点创建任务
    {
        string jsonString = File.ReadAllText(filePath, Encoding.UTF8);
        var json = Json.ParseString(jsonString);
        var jsonDict = json.AsGodotDictionary();
        var dataArray = jsonDict["dataList"].AsGodotArray();

        int index = 0;
        foreach (var item in dataArray)
        {
            var itemDict = item.AsGodotDictionary();
            if (itemDict.ContainsKey("title"))
            {
                string originalText = itemDict["title"].ToString();
                var (processed, tagMap) = ProtectTags(originalText);
                int capturedIndex = index;

                inlineTranslation.EnqueueTask(filePath, capturedIndex, processed, tagMap, translated =>
                {
                    inlineTranslation.WriteGenericField(filePath, capturedIndex, "title", translated);
                });
            }
            index++;
        }
    }

    //辅助方法
    // 保护标签：将 [xxx] 替换为 {{tag_0}}、{{tag_1}} 等，并返回映射字典
    private (string processedText, Dictionary<string, string> tagMap) ProtectTags(string text)
    {
        var tagMap = new Dictionary<string, string>(); // 存储占位符与原标签的映射
        var regex = new Regex(@"\[[^\]]+\]|<[^>]+>"); // 匹配 [xxx]和 <> 内部的任意文本
        int index = 0; // 用于生成占位符的索引
        string result = regex.Replace(text, match =>
        {
            string placeholder = $"@@TAG{index}@@"; // 占位符格式：{{TAG0}}
            tagMap[index.ToString()] = match.Value; //match代表当前找到的标签,将占位符与原标签的映射存储到字典中
            index++;
            return placeholder; // 替换为占位符
        });
        return (result, tagMap); // 返回处理后的文本和映射字典
    }
}
