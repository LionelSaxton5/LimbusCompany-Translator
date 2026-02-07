using Godot;
using System;
using System.Threading.Tasks;

public partial class OcRwindow : Window //OCR窗口
{
	private bool isDragging = false; //是否正在拖拽窗口
	private Vector2 dragOffset; //拖拽偏移量

	private TranslationManager manager; //翻译管理器引用
    public TranslationWindow translationWindow;


	public override void _Ready()
	{
        var root = GetTree().Root; // 获取场景树根节点
		manager = null;
        for (int i =0; i < root.GetChildCount(); i++)
		{
			var child = root.GetChild(i);
            manager = child as TranslationManager;
			if (manager != null)
				break;
		}

		if (manager != null)
		{
			translationWindow = manager.CurrentTranslationWindow ?? manager.GetOrCreateTranslationWindow(); // 获取或创建 TranslationWindow 实例
            GD.Print($"从 TranslationManager 获取 translationWindow: {(translationWindow != null)}");
		}
		else
		{
			GD.Print("未找到 TranslationManager，请确保场景中有该节点");
		}
	}

	public override void _Process(double delta)
	{      
    }

	public void SetRect(Rect2I rect)
	{
		Position = new Vector2I(rect.Position.X, rect.Position.Y);
		Size = new Vector2I(rect.Size.X, rect.Size.Y);

		// 确保窗口显示正确
		Show();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
		{                      
            if (mouseButton.ButtonIndex == MouseButton.Left)
			{
                manager?.HideTranslationWindow(); // 开始拖拽时临时隐藏翻译窗口
				
                if (mouseButton.Pressed)
				{                    
                    //GD.Print($"OcRwindow的点击: ButtonIndex={mouseButton.ButtonIndex}, Pressed={mouseButton.Pressed}, Position={mouseButton.Position}, GlobalPosition={mouseButton.GlobalPosition}");

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
            //GD.Print($"OcRwindow的移动:  Position={mouseMotion.Position}, GlobalPosition={mouseMotion.GlobalPosition}");
            if (isDragging)
			{
				Position = (Vector2I)(mouseMotion.GlobalPosition - dragOffset); //更新窗口位置
			}
		}
	}

	public void OnMouseEntered()
	{
		manager?.HideTranslationWindow(); // 鼠标进入时隐藏翻译窗口      
        GrabFocus();
    }

    public async void OnMouseExited()
	{
        // 当鼠标移出窗口时，停止拖拽
        GrabFocus();
        isDragging = false;
        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame"); // 等待两帧

        manager?.GetOrCreateTranslationWindow();
    }     
}

