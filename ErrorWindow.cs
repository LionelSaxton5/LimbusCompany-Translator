using Godot;
using System;

public partial class ErrorWindow : Window //报错弹窗
{
    public static ErrorWindow Instance { get; private set; }

    private Label errorLabel; //错误信息标签
    public override void _Ready()
	{
        Instance = this;
        errorLabel = GetNodeOrNull<Label>("Label");
        if (errorLabel == null)
        {
            errorLabel = new Label();
            AddChild(errorLabel);
        }

        CloseRequested += OnCloseRequested;
    }

	public void ShowErrorInternal(string errorMessage) //显示错误信息
	{
		errorLabel.Text = errorMessage;
		Show();
    }

    public static ErrorWindow EnsureInstance()
    {       
        var packed = ResourceLoader.Load<PackedScene>("res://changjing/ErrorWindow.tscn"); // 改为你的实际路径
        var tree = Engine.GetMainLoop() as SceneTree;
        if (packed != null)
        {
            var errorWindow = packed.Instantiate();
            tree.Root.CallDeferred("add_child", errorWindow);
            Instance = errorWindow as ErrorWindow;
            
            return Instance;
        }
        return null; 
    }

    // 全局快捷显示方法（安全封装）
    public static void ShowError(string message)
    {
        ErrorWindow errorWindow = EnsureInstance();
        
        // 延迟调用实例方法以确保 _Ready 已执行并且节点已在树上
        errorWindow.CallDeferred(nameof(ShowErrorInternal), message);
    }

    // 关闭并释放单例
    public static void CloseInstance()
    {
        if (Instance == null) return;
        if (Instance.IsInsideTree())
            Instance.CallDeferred("queue_free"); // 延迟释放以避免在当前帧中操作树
        Instance = null;
    }

    private void OnCloseRequested()
    {
        // 关闭时使用 CloseInstance 释放并清理单例
        CloseInstance();
    }
}
