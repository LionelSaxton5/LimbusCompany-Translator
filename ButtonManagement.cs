using Godot;
using System;
using System.Threading.Tasks;

public partial class ButtonManagement : VBoxContainer //按钮管理容器
{
	private Button startButton; //启动按钮
    private Button timeTranslationButton; //实时翻译按钮
	private Button warehouseButton; //翻译源仓库按钮
    private Button embeddedButton; //嵌入按钮
	private Button oCRButton; //OCR设置按钮

	//UI
	private Panel startPanel;
	private Panel warehousePanel;
	private Panel embeddedPanel;
    private Panel oCRPanel;

	//汉化选择按钮
	private SpinBox chapterSpinBox;
	private SpinBox levelSpinBox;
	private Button starButton; //主线汉化启动按钮
	private CheckButton skillsEnemyCheckButton; //主线敌人技能汉化启用按钮

    //汉化间章按钮
    private SpinBox interludeChapterSpinBox;
	private SpinBox interludeLevelSpinBox;
	private Button interludeStarButton; //间章汉化启动按钮

	//翻译源设置相关
	private Button microsoftButton; //微软设置按钮
	private Button baiduButton; //百度设置按钮
	private Button tengxunButton; //腾讯设置按钮
	private Button huoshanButton; //火山设置按钮

    private CheckButton microsoftCheckButton; //微软翻译源启用按钮
	private CheckButton baiduCheckButton; //百度启用按钮
	private CheckButton tengxunCheckButton; //腾讯启用按钮
	private CheckButton huoshanCheckButton; //火山启用按钮

    private GetGame getGame; //获取游戏节点

    //API设置窗口
    private Window apiSettingsWindow; //API设置窗口
	private LineEdit portLineEdit; //端口输入框
	private LineEdit keyLineEdit; //密钥输入框

    //OCR启动按钮
	private CheckButton umiOcrCheckButton; //Umi-OCR启用按钮
	private LineEdit umiOcrPortLineEdit; //Umi-OCR路径输入框
	private CheckButton paddOcrCheckButton; //Paddle-OCR启用按钮

	//音乐按钮
	private AudioStreamPlayer audioStreamPlayer; //音乐播放器
	private TextureButton musicToggleButton; //音乐开关按钮

    //游戏路径
    private LineEdit gamePathLineEdit; //游戏路径输入框

    public override void _Ready()
	{
		startButton = GetNode<Button>("StartButton"); //获取启动按钮
        timeTranslationButton = GetNode<Button>("TranslateButton"); //获取实时翻译按钮
		warehouseButton = GetNode<Button>("WarehouseButton"); //获取翻译源仓库按钮
        embeddedButton = GetNode<Button>("EmbeddedButton"); //获取嵌入按钮
		oCRButton = GetNode<Button>("OCRButton"); //获取OCR设置按钮

		startPanel = GetNode<Panel>("StartButton/StartPanel");
		warehousePanel = GetNode<Panel>("WarehouseButton/WarehousePanel");
        embeddedPanel = GetNode<Panel>("EmbeddedButton/EmbeddedPanel");
		oCRPanel = GetNode<Panel>("OCRButton/OCRPanel");

		chapterSpinBox = GetNode<SpinBox>("EmbeddedButton/EmbeddedPanel/ChapterSpinBox");
		levelSpinBox = GetNode<SpinBox>("EmbeddedButton/EmbeddedPanel/LevelSpinBox");
		starButton = GetNode<Button>("EmbeddedButton/EmbeddedPanel/StarButton");
		skillsEnemyCheckButton = GetNode<CheckButton>("EmbeddedButton/EmbeddedPanel/SkillsEnemyCheckButton");

        interludeChapterSpinBox = GetNode<SpinBox>("EmbeddedButton/EmbeddedPanel/InterludeChapterSpinBox");
		interludeLevelSpinBox = GetNode<SpinBox>("EmbeddedButton/EmbeddedPanel/InterludeLevelSpinBox");
		interludeStarButton = GetNode<Button>("EmbeddedButton/EmbeddedPanel/InterludeStarButton");

		microsoftButton = GetNode<Button>("WarehouseButton/WarehousePanel/MicrosoftButton");
		baiduButton = GetNode<Button>("WarehouseButton/WarehousePanel/BaiduButton");
		tengxunButton = GetNode<Button>("WarehouseButton/WarehousePanel/TengxunButton");
		huoshanButton = GetNode<Button>("WarehouseButton/WarehousePanel/HuoshanButton");

        //翻译启动按钮
        microsoftCheckButton = GetNode<CheckButton>("WarehouseButton/WarehousePanel/MicrosoftButton/MicrosoftCheckButton");
		baiduCheckButton = GetNode<CheckButton>("WarehouseButton/WarehousePanel/BaiduButton/BaiduCheckButton");
		tengxunCheckButton = GetNode<CheckButton>("WarehouseButton/WarehousePanel/TengxunButton/TengxunCheckButton");
		huoshanCheckButton = GetNode<CheckButton>("WarehouseButton/WarehousePanel/HuoshanButton/HuoshanCheckButton");

        //OCR相关节点
        umiOcrCheckButton = GetNode<CheckButton>("OCRButton/OCRPanel/UmiOcrPortLineEdit/UmiOcrCheckButton");
		umiOcrPortLineEdit = GetNode<LineEdit>("OCRButton/OCRPanel/UmiOcrPortLineEdit");
		paddOcrCheckButton = GetNode<CheckButton>("OCRButton/OCRPanel/PaddOcrCheckButton");

        //音乐相关节点
        audioStreamPlayer = GetNode<AudioStreamPlayer>("EmbeddedButton/EmbeddedPanel/AudioStreamPlayer");
		musicToggleButton = GetNode<TextureButton>("EmbeddedButton/EmbeddedPanel/MusicToggleButton");

        gamePathLineEdit = GetNode<LineEdit>("EmbeddedButton/EmbeddedPanel/GameLineEdit");

        var inlineScene = GD.Load<PackedScene>("res://changjing/InlineTranslation.tscn").Instantiate();
		if (inlineScene == null)
		{
			GD.PrintErr("错误：无法加载 InlineTranslation 场景！");
			return;
		}
		else
		{
			 AddChild(inlineScene); //将内嵌场景添加到当前节点
        }
		getGame = inlineScene.GetNode<GetGame>("GetGame");

        //连接选择汉化章节和关卡的事件
        chapterSpinBox.ValueChanged += (double value) => getGame.OnChapterValueChanged((int)value);
		levelSpinBox.ValueChanged += (double value) => getGame.OnLevelValueChanged((int)value);
		starButton.Pressed += () => getGame.OnGetMainStorylineOriginalText();
        //连接选择汉化间章章节和关卡的事件
        interludeChapterSpinBox.ValueChanged += (double value) => getGame.OnInterludeChapterValueChanged((int)value);
		interludeLevelSpinBox.ValueChanged += (double value) => getGame.OnInterludeLevelValueChanged((int)value);
		interludeStarButton.Pressed += () => getGame.OnGetInterludeOriginalText();

        microsoftCheckButton.ButtonPressed = SaveManager.Instance.saveData.isMicrosofttranslationEnable; //设置微软翻译源启用状态
		baiduCheckButton.ButtonPressed = SaveManager.Instance.saveData.isBaidutranslationEnable; //设置百度翻译源启用状态
		tengxunCheckButton.ButtonPressed = SaveManager.Instance.saveData.isTengxuntranslationEnable; //设置腾讯翻译源启用状态
		huoshanCheckButton.ButtonPressed = SaveManager.Instance.saveData.isHuoshantranslationEnable; //设置火山翻译源启用状态
        skillsEnemyCheckButton.ButtonPressed = SaveManager.Instance.saveData.isSkillsEnemy; //设置主线敌人技能汉化启用状态

        umiOcrCheckButton.ButtonPressed = SaveManager.Instance.saveData.isUmiOcrEnable; //设置Umi-OCR启用状态
		paddOcrCheckButton.ButtonPressed = SaveManager.Instance.saveData.isPaddleOcrEnable; //设置Paddle-OCR启用状态
		umiOcrPortLineEdit.Text = SaveManager.Instance.saveData.umiOcrPath; //设置Umi-OCR路径输入框初始文本

        startPanel.Visible = true;
		warehousePanel.Visible = false;
		embeddedPanel.Visible = false;
		oCRPanel.Visible = false;
		gamePathLineEdit.Text = SaveManager.Instance.saveData.gameExePath; //设置游戏路径输入框初始文本
    }

	private void OnStartButtonPressed() //启动按钮按下事件
	{
		startPanel.Visible = true;
		warehousePanel.Visible = false;
		embeddedPanel.Visible = false;
		oCRPanel.Visible = false;
    }

	private void OnWarehouseButtonPressed() //翻译源仓库按钮按下事件
	{
		startPanel.Visible = false;
		warehousePanel.Visible = true;
		embeddedPanel.Visible = false;
		oCRPanel.Visible = false;
    }

	private void OnEmbeddedButtonPressed() //嵌入按钮按下事件
	{
        startPanel.Visible = false;
        warehousePanel.Visible = false;
        embeddedPanel.Visible = true;
        oCRPanel.Visible = false;
    }

	private void OnOCRButtonPressed() //OCR设置按钮按下事件
	{
        startPanel.Visible = false;
        warehousePanel.Visible = false;
		embeddedPanel.Visible = false;
		oCRPanel.Visible = true;
    }

	//翻译源设置事件
	private void OnMicrosoftButtonPressed() //微软设置按钮按下事件
	{
        apiSettingsWindow = GD.Load<PackedScene>("res://changjing/APISettingsWindow.tscn").Instantiate<Window>();
        portLineEdit = apiSettingsWindow.GetNode<LineEdit>("Control/PortLineEdit");
        keyLineEdit = apiSettingsWindow.GetNode<LineEdit>("Control/KeyLineEdit");

        apiSettingsWindow.Title = "微软翻译API设置";

        if (portLineEdit != null)
            portLineEdit.TextChanged += OnMIPortLineEditTextChanged;
        if (keyLineEdit != null)
            keyLineEdit.TextChanged += OnMIKeyLineEditTextChanged;

        apiSettingsWindow.CloseRequested += () =>
		{
            if (portLineEdit != null)
                portLineEdit.TextChanged -= OnMIPortLineEditTextChanged;
            if (keyLineEdit != null)
                keyLineEdit.TextChanged -= OnMIKeyLineEditTextChanged;

            // 防止重复释放和悬空引用
            if (apiSettingsWindow != null && apiSettingsWindow.IsInsideTree())
                apiSettingsWindow.QueueFree();
				apiSettingsWindow = null;
        };

		portLineEdit.Text = SaveManager.Instance.saveData.MicrosoftranslationUrl; //设置端口输入框初始文本
		keyLineEdit.Text = SaveManager.Instance.saveData.MicrosofttranslationKey; //设置密钥输入框初始文本

        AddChild(apiSettingsWindow);
        apiSettingsWindow.Show();
    }
	private void OnMIPortLineEditTextChanged(string newText) //端口输入框文本变化事件
	{
		if (SaveManager.Instance == null)
		{
			GD.PrintErr("SaveManager.Instance is null");
			return;
        }
		SaveManager.Instance.saveData.MicrosoftranslationUrl = newText; //更新保存数据中的端口
        SaveManager.Instance.SaveDataToFile();
    }
	private void OnMIKeyLineEditTextChanged(string newText) //密钥输入框文本变化事件
	{
		SaveManager.Instance.saveData.MicrosofttranslationKey = newText; //更新保存数据中的密钥
        SaveManager.Instance.SaveDataToFile();
    }

	private void OnBaiduButtonPressed() //百度设置按钮按下事件
	{
        apiSettingsWindow = GD.Load<PackedScene>("res://changjing/APISettingsWindow.tscn").Instantiate<Window>();
        portLineEdit = apiSettingsWindow.GetNode<LineEdit>("Control/PortLineEdit");
        keyLineEdit = apiSettingsWindow.GetNode<LineEdit>("Control/KeyLineEdit");

        apiSettingsWindow.Title = "百度翻译API设置";

		if (portLineEdit != null)
            portLineEdit.TextChanged += OnBaiduPortLineEditTextChanged; //连接端口输入框文本变化事件
		if (keyLineEdit != null)
            keyLineEdit.TextChanged += OnBaiduKeyLineEditTextChanged; //连接密钥输入框文本变化事件

        apiSettingsWindow.CloseRequested += () =>
        {
            if (portLineEdit != null)
                portLineEdit.TextChanged -= OnBaiduPortLineEditTextChanged;
            if (keyLineEdit != null)
                keyLineEdit.TextChanged -= OnBaiduKeyLineEditTextChanged;

            // 防止重复释放和悬空引用
            if (apiSettingsWindow != null && apiSettingsWindow.IsInsideTree())
                apiSettingsWindow.QueueFree();
				apiSettingsWindow = null;
        };

		portLineEdit.Text = SaveManager.Instance.saveData.BaidutranslationUrl; //设置端口输入框初始文本
		keyLineEdit.Text = SaveManager.Instance.saveData.BaidutranslationKey; //设置密钥输入框初始文本

        AddChild(apiSettingsWindow);
		apiSettingsWindow.Show();		
    }
	

    private void OnBaiduPortLineEditTextChanged(string newText) //百度端口输入框文本变化事件
	{
		SaveManager.Instance.saveData.BaidutranslationUrl = newText; //更新保存数据中的端口
        SaveManager.Instance.SaveDataToFile();
    }
	private void OnBaiduKeyLineEditTextChanged(string newText) //百度密钥输入框文本变化事件
	{
		SaveManager.Instance.saveData.BaidutranslationKey = newText; //更新保存数据中的密钥
        SaveManager.Instance.SaveDataToFile();
    }

    private void OnTengxunButtonPressed() //腾讯设置按钮按下事件
    {
        apiSettingsWindow = GD.Load<PackedScene>("res://changjing/APISettingsWindow.tscn").Instantiate<Window>();
        portLineEdit = apiSettingsWindow.GetNode<LineEdit>("Control/PortLineEdit");
        keyLineEdit = apiSettingsWindow.GetNode<LineEdit>("Control/KeyLineEdit");

        apiSettingsWindow.Title = "腾讯翻译API设置";
        if (portLineEdit != null)
            portLineEdit.TextChanged += OnTengxunPortLineEditTextChanged; //连接端口输入框文本变化事件
        if (keyLineEdit != null)
            keyLineEdit.TextChanged += OnTengxunKeyLineEditTextChanged; //连接密钥输入框文本变化事件

        apiSettingsWindow.CloseRequested += () =>
        {
            if (portLineEdit != null)
                portLineEdit.TextChanged -= OnTengxunPortLineEditTextChanged;
            if (keyLineEdit != null)
                keyLineEdit.TextChanged -= OnTengxunKeyLineEditTextChanged;

            // 防止重复释放和悬空引用
            if (apiSettingsWindow != null && apiSettingsWindow.IsInsideTree())
                apiSettingsWindow.QueueFree();
            apiSettingsWindow = null;
        };

		portLineEdit.Text = SaveManager.Instance.saveData.TengxuntranslationUrl; //设置端口输入框初始文本
		keyLineEdit.Text = SaveManager.Instance.saveData.TengxuntranslationKey; //设置密钥输入框初始文本

		AddChild(apiSettingsWindow);
		apiSettingsWindow.Show();
    }

	private void OnTengxunPortLineEditTextChanged(string newText) //腾讯端口输入框文本变化事件
	{
		SaveManager.Instance.saveData.TengxuntranslationUrl = newText; //更新保存数据中的端口
		SaveManager.Instance.SaveDataToFile();
    }
	private void OnTengxunKeyLineEditTextChanged(string newText) //腾讯密钥输入框文本变化事件
	{
		SaveManager.Instance.saveData.TengxuntranslationKey = newText; //更新保存数据中的密钥
		SaveManager.Instance.SaveDataToFile();
    }

	private void OnHuoshanButtonPressed() //火山设置按钮按下事件
	{
		apiSettingsWindow = GD.Load<PackedScene>("res://changjing/APISettingsWindow.tscn").Instantiate<Window>();
		portLineEdit = apiSettingsWindow.GetNode<LineEdit>("Control/PortLineEdit");
		keyLineEdit = apiSettingsWindow.GetNode<LineEdit>("Control/KeyLineEdit");
		apiSettingsWindow.Title = "火山翻译API设置";
		if (portLineEdit != null)
			portLineEdit.TextChanged += OnHuoshanPortLineEditTextChanged; //连接端口输入框文本变化事件
		if (keyLineEdit != null)
			keyLineEdit.TextChanged += OnHuoshanKeyLineEditTextChanged; //连接密钥输入框文本变化事件
		apiSettingsWindow.CloseRequested += () =>
		{
			if (portLineEdit != null)
				portLineEdit.TextChanged -= OnHuoshanPortLineEditTextChanged;
			if (keyLineEdit != null)
				keyLineEdit.TextChanged -= OnHuoshanKeyLineEditTextChanged;
			// 防止重复释放和悬空引用
			if (apiSettingsWindow != null && apiSettingsWindow.IsInsideTree())
				apiSettingsWindow.QueueFree();
			apiSettingsWindow = null;
		};

		portLineEdit.Text = SaveManager.Instance.saveData.HuoshantranslationUrl; //设置端口输入框初始文本
		keyLineEdit.Text = SaveManager.Instance.saveData.HuoshantranslationKey; //设置密钥输入框初始文本

        AddChild(apiSettingsWindow);
        apiSettingsWindow.Show();
    }

	private void OnHuoshanPortLineEditTextChanged(string newText) //火山端口输入框文本变化事件
	{
		SaveManager.Instance.saveData.HuoshantranslationUrl = newText; //更新保存数据中的端口
		SaveManager.Instance.SaveDataToFile();
    }
	private void OnHuoshanKeyLineEditTextChanged(string newText) //火山密钥输入框文本变化事件
	{
		SaveManager.Instance.saveData.HuoshantranslationKey = newText; //更新保存数据中的密钥
		SaveManager.Instance.SaveDataToFile();
    }


    private void OnUmiOcrCheckButtonPressed(bool pressed) //Umi-OCR启用按钮切换事件
	{
		SaveManager.Instance.saveData.isUmiOcrEnable = pressed; //更新保存数据中的Umi-OCR启用状态
		SaveManager.Instance.SaveDataToFile();

		if (pressed)
		{
			StartOcrService startOcrService = GetParent().GetNode<StartOcrService>("StartOCRService");
			if (startOcrService != null)
			{
				startOcrService.StartUmiOcrService(); //启动Umi-OCR服务
			}
		}
		else
		{
            StartOcrService startOcrService = GetParent().GetNode<StartOcrService>("StartOCRService");
            if (startOcrService != null)
			{
				startOcrService._ExitTree(); //停止Umi-OCR服务
            }
		}
    }

	private void OnPaddleOcrCheckButtonPressed(bool pressed) //Paddle-OCR启用按钮切换事件
	{
		SaveManager.Instance.saveData.isPaddleOcrEnable = pressed; //更新保存数据中的Paddle-OCR启用状态
		SaveManager.Instance.SaveDataToFile();

		if (pressed)
		{
			PaddleOcrService paddleOcrService = GetParent().GetNode<PaddleOcrService>("PaddleOcrService");
			if (paddleOcrService != null)
			{
				paddleOcrService.InitializeEngine(); //启动Paddle-OCR服务
			}
		}
		else
		{
			PaddleOcrService paddleOcrService = GetParent().GetNode<PaddleOcrService>("PaddleOcrService");
			if (paddleOcrService != null)
			{
				paddleOcrService._ExitTree(); //停止Paddle-OCR服务
            }
        }
    }

	private void OnUmiOcrPathPortLineEditChanged(string text) //Umi-OCR路径选择按钮按下事件
	{
		SaveManager.Instance.saveData.umiOcrPath = text; //更新保存数据中的Umi-OCR路径
		SaveManager.Instance.SaveDataToFile();
    }

	private void OnGamePathLineEditChanged(string text) //游戏路径输入框文本变化事件
	{
		SaveManager.Instance.saveData.gameExePath = text; //更新保存数据中的游戏路径
		SaveManager.Instance.SaveDataToFile();
    }

	private void OnSkillsEnemyCheckButtonPressed(bool pressed) //主线敌人技能汉化启用按钮切换事件
	{
		SaveManager.Instance.saveData.isSkillsEnemy = pressed; //更新保存数据中的主线敌人技能汉化启用状态
		SaveManager.Instance.SaveDataToFile();
    }

	private void OnMusicToggleButtonToggled(bool toggle)
	{
		if (toggle)
		{
			audioStreamPlayer.Play(); //播放音乐
		}
		else
		{
			audioStreamPlayer.Stop(); //停止音乐
        }
    }

}
