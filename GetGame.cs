using Godot;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Godot.HttpRequest;

public partial class GetGame : Node //获取边狱巴士游戏路径
{
	public static readonly Dictionary<string, int> STEAM_APP_IDS = new Dictionary<string, int>
	{
		{ "Limbus Company", 1973530 }
	};

	public string gameInstallPath = ""; //游戏安装路径

    //章节选择
    private int selectedChapter = 9; //默认章节9
    private int selectedLevel = 1;    //默认关卡1

    //间章选择
    private int interludeSelectedChapter = 8;
    private int interludeSelectedLevel = 1;

    private bool isFileOperated = false; //文件是否已操作标志
    private InlineTranslation inlineTranslation; //内嵌式翻译节点引用
    private SkillFile skillFile; //技能文件节点引用
    private ProgressWindow progressWindow; //进度窗口节点引用

    public override void _Ready()
	{       
        try
        {
            if (SaveManager.Instance != null && SaveManager.Instance.saveData != null &&
                !string.IsNullOrWhiteSpace(SaveManager.Instance.saveData.gameExePath))
            {
                gameInstallPath = SaveManager.Instance.saveData.gameExePath;                                
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"读取保存的游戏路径时出错: {ex.Message}");
        }

        // 未能从保存读取到有效路径，自动检测 Steam 路径并保存（若找到）
        gameInstallPath = GetGameInstallPath("Limbus Company");

        if (!string.IsNullOrEmpty(gameInstallPath))
        {
            try
            {
                if (SaveManager.Instance != null && SaveManager.Instance.saveData != null)
                {
                    SaveManager.Instance.saveData.gameExePath = gameInstallPath;
                    SaveManager.Instance.SaveDataToFile();
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"保存检测到的游戏路径时出错: {ex.Message}");
            }
        }
        else
        {            
            ErrorWindow.ShowError("未能自动检测到游戏路径，请在设置里手动填写或检查 Steam 安装位置。");
        }
    }

    public async Task OnGetMainStorylineOriginalText() //获取主线原文
    {
        OperateFile(); //操作文件
       
        if (!isFileOperated)
        {
            ErrorWindow.ShowError("文件操作未成功，无法获取故事文件");
            return;
        }
               
        List<string> storyFiles = FindStoryFiles(selectedChapter, selectedLevel); //查找故事文件
        List<string> mazeFiles = FindMazePlot(selectedChapter, selectedLevel); //查找迷宫剧情文件        

        List<string> allFiles = new List<string>();
        allFiles.AddRange(storyFiles);
        allFiles.AddRange(mazeFiles);
        
        inlineTranslation = GetParent() as InlineTranslation; //获取InlineTranslation节点引用       
        inlineTranslation.StartBatchTranslation(allFiles); //加载剧情原文
        
        skillFile = inlineTranslation.GetNodeOrNull<SkillFile>("SkillFile"); //获取SkillFile节点引用
        if (skillFile != null)
        {
            skillFile.inlineTranslation = inlineTranslation; //将InlineTranslation引用传递给SkillFile
            
            skillFile.FindEnemyPassive(selectedChapter); //查找敌方被动技能文件
            skillFile.FindEnemySkills(selectedChapter); //查找敌方技能文件
            skillFile.FindEnemyBubble(selectedChapter); //查找敌方技能气泡文件
            skillFile.FindEnemyPanicInfo(selectedChapter); //查找敌方恐慌信息文件
            skillFile.FildStageNode(selectedChapter); //查找关卡节点名称文件           
        }
        else
        {
            GD.PrintErr("未找到 SkillFile 节点");
        }


        if (progressWindow != null && !progressWindow.IsQueuedForDeletion())
        {
            progressWindow.QueueFree();
            progressWindow = null;
        }

        var progressScene = GD.Load<PackedScene>("res://changjing/ProgressWindow.tscn"); //显示汉化进度窗口
        if (progressScene != null)
        {
            progressWindow = progressScene.Instantiate<ProgressWindow>();
            // 订阅进度事件
            inlineTranslation.OnProgressUpdated += progressWindow.OnProgressBarValueChanged;
            // 窗口关闭时自动取消订阅
            progressWindow.CloseRequested += () =>
            {
                inlineTranslation.OnProgressUpdated -= progressWindow.OnProgressBarValueChanged;
                progressWindow.QueueFree();
                progressWindow = null;
            };
            GetTree().Root.AddChild(progressWindow);
            progressWindow.Show();
        }

        await inlineTranslation.StartProcessingIfNeededAsync(); //等待加入队列的文件处理完成     
    }

    public async Task OnGetInterludeOriginalText() //获取间章原文
    {
        OperateFile(); //操作文件
               
        if (!isFileOperated)
        {
            ErrorWindow.ShowError("文件操作未成功，无法获取间章文件");
            return;
        }

        List<string> interludeFiles = FindInterludePlot(interludeSelectedChapter, interludeSelectedLevel); //查找间章剧情文件

        if (interludeFiles != null)
        {
            //GD.Print($"找到 {interludeFiles.Count} 个间章文件:");            
        }
        else
        {
            ErrorWindow.ShowError("未找到任何间章文件");
        }

        inlineTranslation = GetParent() as InlineTranslation; //获取InlineTranslation节点引用
        inlineTranslation.StartBatchTranslation(interludeFiles); //加载原文

        if (progressWindow != null && !progressWindow.IsQueuedForDeletion())
        {
            progressWindow.QueueFree();
            progressWindow = null;
        }

        var progressScene = GD.Load<PackedScene>("res://changjing/ProgressWindow.tscn");
        if (progressScene != null)
        {
            progressWindow = progressScene.Instantiate<ProgressWindow>();
            // 订阅进度事件
            inlineTranslation.OnProgressUpdated += progressWindow.OnProgressBarValueChanged;
            // 窗口关闭时自动取消订阅
            progressWindow.CloseRequested += () =>
            {
                inlineTranslation.OnProgressUpdated -= progressWindow.OnProgressBarValueChanged;
                progressWindow.QueueFree();
                progressWindow = null;
            };
            GetTree().Root.AddChild(progressWindow);
            progressWindow.Show();
        }

        await inlineTranslation.StartProcessingIfNeededAsync(); //等待加入队列的文件处理完成 
    }

    //章节和关卡值变化处理
    public void OnChapterValueChanged(int chapter)
	{
		selectedChapter = chapter;
    }
	public void OnLevelValueChanged(int level)
	{		
		selectedLevel = level;
    }

    public void OnInterludeChapterValueChanged(int chapter)
    {
        interludeSelectedChapter = chapter;
    }
    public void OnInterludeLevelValueChanged(int level)
    {
        interludeSelectedLevel = level;
    }

    public static string GetSteamInstallPath() //获取Steam安装路径
    {
		using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) //打开注册表键
		{
			if (key != null)
			{
				object value = key.GetValue("SteamPath"); //获取Steam安装路径
				if (value != null)
				{
					return value.ToString();
                }
            }
		}
		return null; //未找到Steam安装路径
    }

	public static string GetGameInstallPath(string gameKey)
	{
		if (!STEAM_APP_IDS.ContainsKey(gameKey))
		{
			GD.Print($"游戏键 '{gameKey}' 不存在");
			return null;
		}

		string steamPath = GetSteamInstallPath();
        if (steamPath == null)
        {
            return null;
        }

		string libraryPath = steamPath; //默认Steam库路径
		int appId = STEAM_APP_IDS[gameKey]; //获取游戏的Steam应用ID(获取对应键的值)
        string gamePath = Path.Combine(libraryPath, "steamapps", "common", gameKey); //构建游戏路径

		if (Directory.Exists(gamePath))
		{
            return gamePath; //返回游戏路径
		}
		else
		{
			return null; //未找到游戏路径
        }
    }

	public void OperateFile() //操作文件
    {
		if (string.IsNullOrEmpty(gameInstallPath))
		{
			gameInstallPath = GetGameInstallPath("Limbus Company"); //获取游戏安装路径
            try
            {
                if (SaveManager.Instance != null && SaveManager.Instance.saveData != null)
                {
                    SaveManager.Instance.saveData.gameExePath = gameInstallPath; //保存游戏路径
                    SaveManager.Instance.SaveDataToFile();
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"保存游戏路径失败: {ex.Message}");
            }

            if (string.IsNullOrEmpty(gameInstallPath))
            {
                ErrorWindow.ShowError("无法获取游戏安装路径");
                return;
            }
        }

        SyncLanguageFiles();

        //复制语言文件,改名
        string Document = System.IO.Path.Combine(gameInstallPath, "LimbusCompany_Data", "Lang", "Jp_zh-cn"); //目标文件路径              

        //复制字体文件
        string fontDocument = System.IO.Path.Combine(Document, "Font"); //字体文件路径

        if (!Directory.Exists(fontDocument)) //检查字体文件夹是否存在
		{
            string projectRootPath = ProjectSettings.GlobalizePath("res://");
            string fontSourcePath = System.IO.Path.Combine(projectRootPath, "Font");
            string exePath = OS.GetExecutablePath();  //获取Godot可执行文件路径
            string exeDir = System.IO.Path.GetDirectoryName(exePath); //获取Godot可执行文件目录


            string[] possibleFontPaths =
			{
				System.IO.Path.Combine(exeDir,"Font"),
                System.IO.Path.Combine(projectRootPath, "Font"),
				System.IO.Path.Combine(fontSourcePath),
                System.IO.Path.Combine(exeDir,"..","Font"),
            }; //源字体文件路径

			string fontSource = null;
			foreach (var path in possibleFontPaths)
			{
				if (System.IO.Directory.Exists(path))
				{
					fontSource = path;
					break;
                }
            }

            if (fontSource != null)
            {
                string fontDestinationPath = System.IO.Path.Combine(Document, "Font"); //目标字体文件路径
                try
                {
                    CopyDirectoryRecursive(fontSource, fontDestinationPath); //复制字体文件并覆盖
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"复制字体文件失败: {ex.Message}");
                }
            }
            else
            {
                ErrorWindow.ShowError("未找到任何字体文件路径");
            }
        }

        //修改josn文件
        UpdateConfigJson();

        isFileOperated = true; //标记文件已操作
    }
	
	public List<string> FindStoryFiles(int chapter, int startLevel) //根据章节和关卡查找对应的故事文件
    {
		List<string> result = new List<string>(); //存储找到的文件路径

        int currentNumber = chapter * 100 + startLevel; //计算当前章节和关卡的数字表示，例如 901

        string storyDataPath = Path.Combine(gameInstallPath, "LimbusCompany_Data", "Lang", "Jp_zh-cn", "StoryData"); //故事数据文件夹路径
		if(!Directory.Exists(storyDataPath))
			return new List<string>(); //如果目录不存在，返回空列表
        
        while (true)
		{
            bool foundFilesForThisNumber = false;

			foreach (char suffix in new char[] { 'A', 'B', 'I' })
			{
				string pattern = $"JP_S{currentNumber}{suffix}.json"; //构建文件名模式，例如 "JP_S901A.json"
                string fullPath = Path.Combine(storyDataPath, pattern); //构建完整文件路径

                if (File.Exists(fullPath)) //如果文件存在
                {
                    string newfileName = Path.Combine(storyDataPath, $"S{currentNumber}{suffix}.json");
                    try
                    {
                        File.Move(fullPath, newfileName); //改名操作
                        result.Add(newfileName);
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"重命名文件失败: {ex.Message}");
                        result.Add(fullPath);
                    }

                    foundFilesForThisNumber = true; //标记找到文件
                }
                if(suffix == 'I') //处理特殊的 I 文件情况
                {                   
                     int idx = 1;
                     const int maxIdx = 8;
                     while (idx < maxIdx)
                     {
                        string numbered = $"JP_S{currentNumber}I{idx}.json";
                        string numberedFullPath = Path.Combine(storyDataPath, numbered);
                        if (File.Exists(numberedFullPath))
                        {
                            string newfileName = Path.Combine(storyDataPath, $"S{currentNumber}I{idx}.json");
                            try
                            {
                                File.Move(numberedFullPath, newfileName); //改名操作
                                result.Add(newfileName);
                            }
                            catch (Exception ex)
                            {
                                GD.PrintErr($"重命名文件失败: {ex.Message}");
                                result.Add(numberedFullPath);
                            }

                            foundFilesForThisNumber = true; //标记找到文件
                            idx++;
                        }
                        else
                        {
                            break; //没有更多的I文件，退出循环
                        }
                     }                   
                }
            }

			if (!foundFilesForThisNumber) //如果没有找到任何文件，停止搜索
			{
				currentNumber += 1; //增加关卡数字，例如从901增加到902

				int newChapter = currentNumber / 100; //计算当前章节
				if (newChapter > chapter)
				{
                    //GD.Print($"到达下一章节 {newChapter}，停止搜索");
                    break;
                }

                // 防止无限循环
                if (currentNumber > (chapter * 100 + 60)) // 假设每章最多60关
                {
                    //GD.Print($"超过最大关卡数，停止搜索");
                    break;
                }
            }
            else
            {
                // 找到文件，继续检查下一个数字
                currentNumber++;
            }
        }
        return result; //返回找到的文件路径列表
    }

    public List<string> FindMazePlot(int chapter, int startLevel)  //查找迷宫剧情文件
    {
        List<string> result =new List<string>();

        //迷宫剧情文件查找逻辑(类似于FindStoryFiles)
        string storyDataPath = Path.Combine(gameInstallPath, "LimbusCompany_Data", "Lang", "Jp_zh-cn", "StoryData");
        if(!Directory.Exists(storyDataPath))
            return new List<string>();

        var segments = new List<(int start, int end)>
        {
            (101, 116),// 第一段: 101-116
            (201, 216), // 第二段: 201-216
            (301, 316)  // 第三段: 301-316
        };

        foreach (var segment in segments)
        {
            for (int currentNumber = segment.start; currentNumber < segment.end; currentNumber++)
            {
                foreach (char suffix in new char[] { 'A', 'B', 'I' })
                {
                    string pattern = $"JP_{chapter}D{currentNumber}{suffix}.json";
                    string fullPath = Path.Combine(storyDataPath, pattern);

                    if (File.Exists(fullPath))
                    {
                        string newfileName = Path.Combine(storyDataPath, $"{chapter}D{currentNumber}{suffix}.json");
                        try
                        {
                            File.Move(fullPath, newfileName); //改名操作
                            result.Add(newfileName);
                        }
                        catch (Exception ex)
                        {
                            GD.PrintErr($"重命名文件失败: {ex.Message}");
                            result.Add(fullPath);
                        }
                    }
                    if (suffix == 'I')
                    {
                        int idx = 2;
                        const int maxIdx = 8;
                        while (idx < maxIdx)
                        {
                            string numbered = $"JP_{chapter}D{currentNumber}I{idx}.json";
                            string numberedFullPath = Path.Combine(storyDataPath, numbered);
                            if (File.Exists(numberedFullPath))
                            {
                                string newfileName = Path.Combine(storyDataPath, $"{chapter}D{currentNumber}I{idx}.json");
                                try
                                {
                                    File.Move(numberedFullPath, newfileName); //改名操作
                                    result.Add(newfileName);
                                }
                                catch (Exception ex)
                                {
                                    GD.PrintErr($"重命名文件失败: {ex.Message}");
                                    result.Add(numberedFullPath);
                                }

                                idx++;
                            }
                            else
                            {
                                break; //没有更多的I文件，退出循环
                            }
                        }
                    }
                }
            }            
        }

        return result;
    }

    public List<string> FindInterludePlot(int chapter, int startLevel) //查找间章剧情文件
    {
        List<string> result = new List<string>();
        
        string storyDataPath = Path.Combine(gameInstallPath, "LimbusCompany_Data", "Lang", "Jp_zh-cn", "StoryData"); //故事数据文件夹路径

        if (!Directory.Exists(storyDataPath))
            return new List<string>(); //如果目录不存在，返回空列表

        int currentNumber = chapter * 100 + startLevel; //计算当前章节和关卡的数字表示，例如 801
        int maxNumber = chapter * 100 + 99;  //设置一个合理的最大关卡数，防止无限循环

        while (currentNumber <= maxNumber)
        {
            bool foundFilesForThisNumber = false;

            foreach (char suffix in new char[] { 'A', 'B', 'I' })
            {
                string pattern = $"JP_E{currentNumber}{suffix}.json"; //构建文件名模式，例如 "JP_E801A.json"
                string fllPath = Path.Combine(storyDataPath, pattern);

                if (File.Exists(fllPath)) //如果文件存在
                {
                    string newfileName = Path.Combine(storyDataPath, $"E{currentNumber}{suffix}.json"); //改名

                    try
                    {
                        File.Move(fllPath, newfileName); //改名操作
                        result.Add(newfileName);                       
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"重命名文件失败: {ex.Message}");
                        result.Add(fllPath); // 如果重命名失败，仍然添加原文件
                    }
                    foundFilesForThisNumber = true; //标记找到文件
                }
                if (suffix == 'I')
                {
                    int idx = 1;
                    const int maxIdx = 5;
                    while (idx < maxIdx)
                    {
                        string numbered = $"JP_E{currentNumber}I{idx}.json";
                        string numberedFullPath = Path.Combine(storyDataPath, numbered);
                        if (File.Exists(numberedFullPath))
                        {
                            string newfileName = Path.Combine(storyDataPath, $"E{currentNumber}I{idx}.json"); //改名

                            try
                            {
                                File.Move(numberedFullPath, newfileName); //改名操作
                                result.Add(newfileName);
                            }
                            catch (Exception ex)
                            {
                                GD.PrintErr($"重命名文件失败: {ex.Message}");
                                result.Add(numberedFullPath); // 如果重命名失败，仍然添加原文件
                            }

                            foundFilesForThisNumber = true; //标记找到文件
                            idx++;
                        }
                        else
                        {
                            break; //没有更多的I文件，退出循环
                        }
                    }
                }
            }
            if (!foundFilesForThisNumber) //如果没有找到任何文件，停止搜索
            {
                // 防止无限循环
                if (!CheckNextNumberExists(currentNumber, storyDataPath))
                {
                    break;
                }

                currentNumber += 1; //增加关卡数字，例如从801增加到802              
            }
            
            currentNumber++; //继续检查下一个数字
        }

        return result;
    }   

    // 递归复制文件夹的辅助方法
    private void CopyDirectoryRecursive(string sourceDir, string destDir) //sourceDir:源目录, destDir:目标目录
    {
        // 创建目标路径目录,检查是否存在
        Directory.CreateDirectory(destDir);

        // 复制所有文件
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file)); //目标文件路径
           
            File.Copy(file, destFile, true); //复制文件并覆盖
        }

        // 复制所有子目录
        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
           
            CopyDirectoryRecursive(dir, destSubDir);
        }
    }

    private void UpdateConfigJson() //更新配置文件为Jp_zh-cn
    {
        try
        {
            string jsonFilePath = Path.Combine(gameInstallPath, "LimbusCompany_Data", "Lang", "config.json");

            if (File.Exists(jsonFilePath)) //如果文件存在
            {
                // 读取现有内容
                string jsonContent = File.ReadAllText(jsonFilePath);

                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent); //反序列化为字典
                if (config != null && config.TryGetValue("lang", out var langValue) && langValue is string langStr && langStr == "Jp_zh-cn")
                {
                    return; // 跳过更新
                }

                // 修改为使用 Jp_zh-cn
                var newJson = JsonSerializer.Serialize(new { 
                    lang = "Jp_zh-cn",
                    titleFont = "",
                    contextFont = "",
                    samplingPointSize = 78,
                    padding = 5
                });
                File.WriteAllText(jsonFilePath, newJson);
            }
            else
            {
                // 如果不存在，创建新的
                var newJson = JsonSerializer.Serialize(new
                {
                    lang = "Jp_zh-cn",
                    titleFont = "",
                    contextFont = "",
                    samplingPointSize = 78,
                    padding = 5
                });
                File.WriteAllText(jsonFilePath, newJson);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"更新配置文件失败: {ex.Message}");
        }
    }

    private bool CheckNextNumberExists(int num, string dir)
    {
        // 简单检查下两个数字的 A/B/I 是否存在
        int next = num + 1;
        int maxnext = num + 2;

        return File.Exists(Path.Combine(dir, $"JP_E{next}A.json")) ||
               File.Exists(Path.Combine(dir, $"JP_E{next}B.json")) ||
               File.Exists(Path.Combine(dir, $"JP_E{next}I.json")) ||
               File.Exists(Path.Combine(dir, $"JP_E{maxnext}A.json")) ||
               File.Exists(Path.Combine(dir, $"JP_E{maxnext}B.json")) ||
               File.Exists(Path.Combine(dir, $"JP_E{maxnext}I.json"));
    }

    private void SyncLanguageFiles() //同步语言文件，更新新文件并保留已汉化文件
    {
        string sourceRoot = Path.Combine(gameInstallPath, "LimbusCompany_Data", "Assets", "Resources_moved", "Localize", "jp"); //源语言文件根目录
        string targetRoot = Path.Combine(gameInstallPath, "LimbusCompany_Data", "Lang", "Jp_zh-cn"); //目标语言文件根目录

        if (!Directory.Exists(sourceRoot))
        {
            GD.PrintErr($"源目录不存在: {sourceRoot}");
            return;
        }

        Directory.CreateDirectory(targetRoot); //确保目标目录存在
                                               
        var allSourceFiles = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);// 获取所有源文件（包括子目录）

        foreach (string sourceFile in allSourceFiles)
        {
            string fileName = Path.GetFileName(sourceFile); //获取文件名
            if (!fileName.StartsWith("JP_")) //只处理以 "JP_" 开头的文件
                continue;

           string fileNameWithoutPrefix = fileName.Substring(3); //去掉 "JP_" 前缀

            // 计算相对路径（保持目录结构）
            string relativePath = Path.GetRelativePath(sourceRoot, sourceFile); //相对于源根目录的路径
            string relativeDir = Path.GetDirectoryName(relativePath) ?? ""; //相对于源根目录的目录

            string targetFileWithoutPrefix = Path.Combine(targetRoot, relativeDir, fileNameWithoutPrefix); //目标文件路径（去掉前缀）

            if (File.Exists(targetFileWithoutPrefix)) //如果目标文件已存在,则以汉化
            {
                continue; //如果目标文件已存在，跳过
            }

            // 目标 JP_ 文件路径（与源相对路径一致）
            string targetJpFileToCopy = Path.Combine(targetRoot, relativePath);
            // 如果目标 JP_ 文件已存在
            if (File.Exists(targetJpFileToCopy))
                continue;

            // 确保目标目录存在
            string targetDir = Path.GetDirectoryName(targetJpFileToCopy);
            Directory.CreateDirectory(targetDir);

            // 复制源文件到目标（保留原名 JP_）
            File.Copy(sourceFile, targetJpFileToCopy, false); // false = 不覆盖
        }
    }
}
