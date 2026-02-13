using Godot;
using System;

public partial class ErrorWindow : Window //报错弹窗
{
    public static ErrorWindow Instance { get; private set; }
    private const string ScenePath = "res://jiaoben/ErrorWindow.tscn";

    private Label errorLabel; //错误信息标签
    public override void _Ready()
	{
        Instance = this;
        errorLabel = GetNode<Label>("ErrorLabel");

        CloseRequested += OnCloseRequested;
    }

	public void ShowErrorInternal(string errorMessage) //显示错误信息
	{
		errorLabel.Text = errorMessage;
		Show();
    }

    public static ErrorWindow EnsureInstance()
    {
        if (Instance != null && Instance.IsInsideTree())
            return Instance;

        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed == null)
        {
            GD.PrintErr($"无法加载 ErrorWindow 场景: {ScenePath}");
            return null;
        }

        // 实例化并加入场景树（通过 Root 延迟加入以保证在主线程安全）
        var node = packed.Instantiate();
        if (node is not ErrorWindow win) //把win转换为ErrorWindow类型
        {
            GD.PrintErr("ErrorWindow.tscn 未绑定到 ErrorWindow 脚本或类型不匹配");
            return null;
        }

        // 获取 SceneTree 根节点并延迟加入
        var tree = Engine.GetMainLoop() as SceneTree; // 主线程的 SceneTree
        if (tree == null)
        {
            GD.PrintErr("找不到 SceneTree，无法加入 ErrorWindow");
            return null;
        }

        tree.Root.CallDeferred("add_child", win);
 
        Instance = win; // 设置单例实例
        return Instance;
    }

    // 全局快捷显示方法（安全封装）
    public static void ShowError(string message)
    {
        var win = EnsureInstance();
        if (win == null)
        {
            GD.PrintErr("无法创建 ErrorWindow，错误信息: " + message);
            return;
        }

        // 延迟调用实例方法以确保 _Ready 已执行并且节点已在树上
        win.CallDeferred(nameof(ShowErrorInternal), message);
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
