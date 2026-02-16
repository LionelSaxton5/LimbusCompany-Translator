using Godot;
using System;

public partial class ProgressWindow : Window
{
	private ProgressBar progressBar; //进度条
	private Label label; //翻译完成标签

    public override void _Ready()
	{
		progressBar = GetNode<ProgressBar>("Control/ProgressBar"); //获取ProgressBar节点
		label = GetNode<Label>("Control/Label"); //获取Label节点

		label.Visible = false; //初始时隐藏标签

        CloseRequested += _Exit; //订阅窗口关闭事件
    }

	public void OnProgressBarValueChanged(int value, int maxvalue) //进度条值改变时调用
	{
		progressBar.Value = value; //更新进度条的值
		progressBar.MaxValue = maxvalue; //更新进度条的最大值

		if (value >= maxvalue) //如果进度完成
		{
			ShowLabelText();
		}
    }

	public void ShowLabelText() //设置标签文本
	{
		label.Visible = true; //显示标签
    }

	private void _Exit()
	{
		this.QueueFree(); //释放窗口资源
    }
}
