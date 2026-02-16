using Godot;
using System;

public partial class TranslationWindow : Window //翻译窗口
{
	private bool isDragging = false; //是否正在拖拽窗口
	private Vector2 dragOffset; //拖拽偏移量

    // 调整大小相关变量
    private int resizeBorderThickness = 5; //调整大小边框厚度
	private bool isResizing = false; // 是否正在调整大小
	private ResizeDirection resizeDirection = ResizeDirection.None; // 当前调整方向
    private Vector2I originalPosition; // 记录开始调整时的窗口位置
    private Vector2I originalSize;     // 记录开始调整时的窗口大小

    public TranslationResult translationResult; //翻译结果节点

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
		// 检查鼠标位置并设置光标形状
		Vector2 mousePos = DisplayServer.MouseGetPosition(); //获取鼠标位置
        ResizeDirection dir = GetResizeDirection(mousePos);

        if (dir != ResizeDirection.None)
            DisplayServer.CursorSetShape(GetCursorShapeForDirection(dir));
        else
            DisplayServer.CursorSetShape(DisplayServer.CursorShape.Arrow);
    }

    public override void _Input(InputEvent @event)
    {
        // 获取鼠标在屏幕上的绝对坐标，统一坐标系
        Vector2 screenMousePos = DisplayServer.MouseGetPosition();

        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    //使用屏幕绝对坐标来判断方向
                    resizeDirection = GetResizeDirection(screenMousePos);

                    if (resizeDirection != ResizeDirection.None)
                    {
                        //边缘区域：开始调整大小
                        isResizing = true;
                        originalPosition = Position;
                        originalSize = Size;
                        dragOffset = screenMousePos; // 记录按下的屏幕坐标
                    }
                    else
                    {
                        //内容区域：开始拖拽
                        isDragging = true;
                        dragOffset = screenMousePos - (Vector2)Position; // 计算鼠标与窗口左上角的偏移量
                    }
                }
                else
                {
                    isDragging = false;
                    isResizing = false;
                    resizeDirection = ResizeDirection.None;
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion)
        {
            if (isResizing)
            {
                // 调整大小时也传入屏幕绝对坐标
                ResizeWindow(screenMousePos);
            }
            else if (isDragging)
            {
                Position = (Vector2I)(screenMousePos - dragOffset);
            }
        }
    }

    private ResizeDirection GetResizeDirection(Vector2 mousePos) //调整边框大小
	{
        Rect2I windowRect = new Rect2I(Position, Size);

        //鼠标不在窗口内,无调整方向
        if (!windowRect.HasPoint((Vector2I)mousePos))
            return ResizeDirection.None;

        //计算到各边的距离
        float distLeft = Mathf.Abs(mousePos.X - windowRect.Position.X); //鼠标到左边的距离
        float distRight = Mathf.Abs(mousePos.X - (windowRect.Position.X + windowRect.Size.X)); //鼠标到右边的距离
        float distTop = Mathf.Abs(mousePos.Y - windowRect.Position.Y); //鼠标到上边的距离
        float distBottom = Mathf.Abs(mousePos.Y - (windowRect.Position.Y + windowRect.Size.Y)); //鼠标到下边的距离

        bool nearLeft = distLeft <= resizeBorderThickness; //是否靠近左边
        bool nearRight = distRight <= resizeBorderThickness; //是否靠近右边
        bool nearTop = distTop <= resizeBorderThickness;  //是否靠近上边
        bool nearBottom = distBottom <= resizeBorderThickness;  //是否靠近下边

        // 角优先（同时靠近两条边）
        if (nearTop && nearLeft) return ResizeDirection.TopLeft; //靠近左上角
        if (nearTop && nearRight) return ResizeDirection.TopRight;  //靠近右上角
        if (nearBottom && nearLeft) return ResizeDirection.BottomLeft;  //靠近左下角
        if (nearBottom && nearRight) return ResizeDirection.BottomRight; //靠近右下角

        // 单边（确保只靠近一条边）
        if (nearLeft && !nearTop && !nearBottom) return ResizeDirection.Left; //靠近左边
        if (nearRight && !nearTop && !nearBottom) return ResizeDirection.Right; //靠近右边
        if (nearTop && !nearLeft && !nearRight) return ResizeDirection.Top; //靠近上边
        if (nearBottom && !nearLeft && !nearRight) return ResizeDirection.Bottom; //靠近下边

        return ResizeDirection.None;
    }

	private void ResizeWindow(Vector2 mousePos)
	{
        Vector2 delta = mousePos - dragOffset; //鼠标移动的距离

        //根据调整方向调整窗口大小
        Vector2 newPos = Position;
        Vector2I newSize = Size;

		switch (resizeDirection)
		{
			case ResizeDirection.Left:
                // 左边调整：改变窗口左边和宽度
                newPos.X = (int)(originalPosition.X + delta.X);
                newSize.X = (int)(originalSize.X - delta.X);
                break;
			case ResizeDirection.Right:
                // 右边调整：只改变宽度
                newSize.X = (int)(originalSize.X + delta.X);
                break;
			case ResizeDirection.Top:
                // 上边调整：改变窗口上边和高度
                newPos.Y = (int)(originalPosition.Y + delta.Y);
                newSize.Y = (int)(originalSize.Y - delta.Y);
                break;
            case ResizeDirection.Bottom:
                // 下边调整：只改变高度
                newSize.Y = (int)(originalSize.Y + delta.Y);
                break;
            case ResizeDirection.TopLeft:
                // 左上角调整：同时改变位置和大小
                newPos.X = (int)(originalPosition.X + delta.X);
                newPos.Y = (int)(originalPosition.Y + delta.Y);
                newSize.X = (int)(originalSize.X - delta.X);
                newSize.Y = (int)(originalSize.Y - delta.Y);
                break;
            case ResizeDirection.TopRight:
                // 右上角调整：改变上边位置、宽度和高度
                newPos.Y = (int)(originalPosition.Y + delta.Y);
                newSize.X = (int)(originalSize.X + delta.X);
                newSize.Y = (int)(originalSize.Y - delta.Y);
                break;
            case ResizeDirection.BottomLeft:
                // 左下角调整：改变左边位置、宽度和高度
                newPos.X = (int)(originalPosition.X + delta.X);
                newSize.X = (int)(originalSize.X - delta.X);
                newSize.Y = (int)(originalSize.Y + delta.Y);
                break;
            case ResizeDirection.BottomRight:
                // 右下角调整：只改变宽度和高度
                newSize.X = (int)(originalSize.X + delta.X);
                newSize.Y = (int)(originalSize.Y + delta.Y);
                break;
        }

        // 限制最小大小
        newSize.X = Mathf.Max(newSize.X, 100);
        newSize.Y = Mathf.Max(newSize.Y, 100);

        // 确保调整后位置有效（不能为负数）
        newPos.X = Mathf.Max(newPos.X, 0);
        newPos.Y = Mathf.Max(newPos.Y, 0);

		Position = (Vector2I)newPos;
		Size = newSize;
    }

	private DisplayServer.CursorShape GetCursorShapeForDirection(ResizeDirection dir) //获取光标形状
    {
		switch (dir)
		{
			case ResizeDirection.Left:
			case ResizeDirection.Right:
				return DisplayServer.CursorShape.Hsize;
			case ResizeDirection.Top:
			case ResizeDirection.Bottom:
				return DisplayServer.CursorShape.Vsize;
			case ResizeDirection.TopLeft:
			case ResizeDirection.BottomRight:
				return DisplayServer.CursorShape.Fdiagsize;
			case ResizeDirection.TopRight:
			case ResizeDirection.BottomLeft:
				return DisplayServer.CursorShape.Bdiagsize;
			default:
				return DisplayServer.CursorShape.Arrow;
		}
	}
}
