using Godot;
using System;

public partial class OCRControl : Control
{
	private Rect2I _rect2I; //选区矩形

    public override void _Ready()
	{
        GD.Print($"初始OCRControl._Ready() 位置={Position}, 大小={Size}");
    }


	public override void _Process(double delta)
	{
	}

	public void SetRect(Rect2I rect)
	{
		_rect2I = rect;
		Position = new Vector2I(rect.Position.X, rect.Position.Y);
		Size = new Vector2I(rect.Size.X, rect.Size.Y);
        GD.Print($"传入OCRControl.SetRect: 位置={Position}, 大小={Size}, 矩形={rect}");
        QueueRedraw();
    }

    public override void _Draw()
	{
        GD.Print($"OCRControl._Draw: 绘制矩形 {_rect2I}");

        // 确保有矩形数据
        if (_rect2I.Size.X > 0 && _rect2I.Size.Y > 0)
        {
            // 绘制一个红色边框
            DrawRect(new Rect2(0, 0, Size.X, Size.Y), new Color(1, 0, 0, 1), false, 2);
            GD.Print($"成功绘制：矩形大小为 {_rect2I.Size}");
        }
        else
        {
            GD.PrintErr($"无法绘制：矩形大小为 {_rect2I.Size}");
        }
    }
}
