using Godot;
using System;

public partial class TranslationWindow : Window //翻译窗口
{
	private bool isDragging = false; //是否正在拖拽窗口
	private Vector2 dragOffset; //拖拽偏移量

    private TranslationResult translationResult; //翻译结果节点

	private enum ResizeDirection //调整方向
    {
		None,
		Left,
		Right,
		Top,
		Bottom,
		TopLeft,
		TopRight,
		BottomLeft,
        BottomRight
    }

    public override void _Ready()
	{
        var translationResultScene = GD.Load<PackedScene>("res://changjing/TranslationResultUI.tscn");
		translationResult = translationResultScene.Instantiate<TranslationResult>();
        
        AddChild(translationResult); //添加翻译结果节点	
        
		translationResult.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); //设置填满窗口
        translationResult.Position = Vector2.Zero;

		translationResult.SetParentWindow(this); //设置父窗口引用
    }

	public override void _Process(double delta)
	{
		
	}

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
		{
            //GD.Print($"TranslationWindow: ButtonIndex={mouseButton.ButtonIndex}, Pressed={mouseButton.Pressed}, Position={mouseButton.Position}, GlobalPosition={mouseButton.GlobalPosition}");
            if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				if (mouseButton.Pressed)
				{
					var localPos = mouseButton.Position;
                    
                    if (localPos.X >= 0 && localPos.X <= Size.X &&
						localPos.Y >= 0 && localPos.Y <= Size.Y)
                    {
                        isDragging = true;
                        dragOffset = mouseButton.GlobalPosition - (Vector2)Position;
                    }
                }
				else
				{
					isDragging = false; //停止拖拽
                }
            }
		}
		else if (@event is InputEventMouseMotion mouseMotion)
		{
			if (isDragging)
			{
				Position = (Vector2I)(mouseMotion.GlobalPosition - dragOffset); //更新窗口位置
            }
		}
    }
}
