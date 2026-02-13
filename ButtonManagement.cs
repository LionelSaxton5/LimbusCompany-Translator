using Godot;
using System;

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

	//汉化间章按钮
	private SpinBox interludeChapterSpinBox;
	private SpinBox interludeLevelSpinBox;
	private Button interludeStarButton; //间章汉化启动按钮

	//翻译源设置相关
	private Button microsoftButton; //微软设置按钮
	private Button baiduButton; //百度设置按钮

	private CheckButton microsoftCheckButton; //微软翻译源启用按钮
	private CheckButton baiduCheckButton; //百度启用按钮

    private GetGame getGame; //获取游戏节点

    //API设置窗口
    private Window apiSettingsWindow; //API设置窗口
	private LineEdit portLineEdit; //端口输入框
	private LineEdit keyLineEdit; //密钥输入框

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

		interludeChapterSpinBox = GetNode<SpinBox>("EmbeddedButton/EmbeddedPanel/InterludeChapterSpinBox");
		interludeLevelSpinBox = GetNode<SpinBox>("EmbeddedButton/EmbeddedPanel/InterludeLevelSpinBox");
		interludeStarButton = GetNode<Button>("EmbeddedButton/EmbeddedPanel/InterludeStarButton");

		microsoftButton = GetNode<Button>("WarehouseButton/WarehousePanel/MicrosoftButton");
		baiduButton = GetNode<Button>("WarehouseButton/WarehousePanel/BaiduButton");
        microsoftCheckButton = GetNode<CheckButton>("WarehouseButton/WarehousePanel/MicrosoftButton/MicrosoftCheckButton");
		baiduCheckButton = GetNode<CheckButton>("WarehouseButton/WarehousePanel/BaiduButton/BaiduCheckButton");

        var inlineScene = GD.Load<PackedScene>("res://changjing/InlineTranslation.tscn").Instantiate();
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
		GD.Print("微软翻译源启用状态：" + microsoftCheckButton.ButtonPressed);

        startPanel.Visible = true;
		warehousePanel.Visible = false;
		embeddedPanel.Visible = false;
		oCRPanel.Visible = false;
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

        AddChild(apiSettingsWindow);
		apiSettingsWindow.Show();		
    }
	private void OnBaiduPortLineEditTextChanged(string newText) //端口输入框文本变化事件
	{
		SaveManager.Instance.saveData.BaidutranslationUrl = newText; //更新保存数据中的端口
        SaveManager.Instance.SaveDataToFile();
    }
	private void OnBaiduKeyLineEditTextChanged(string newText) //密钥输入框文本变化事件
	{
		SaveManager.Instance.saveData.BaidutranslationKey = newText; //更新保存数据中的密钥
        SaveManager.Instance.SaveDataToFile();
    }

}
