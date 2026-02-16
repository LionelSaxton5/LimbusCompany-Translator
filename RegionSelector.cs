using Godot;
using System;

public partial class RegionSelector : Control //区域选择器(过渡OCR识别半透明框)
{
	private Vector2 _startPosition; //起始位置
    private Vector2 _endPosition; //结束位置
    private bool _isDragging = false; //是否正在拖动
	private bool _hasSelection = false; //是否已有选区

    public Rect2I SelectedRegion { get; private set; } //存储选区结果

	[Signal] public delegate void RegionSelectedEventHandler(); //区域选择完成信号

    public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop; //阻止鼠标事件传递到下层节点
    }    
   	
    public override void _GuiInput(InputEvent @event) //处理输入事件
    {       
        if (@event is InputEventMouseButton mouseButton) //检查鼠标按键
        {           
            if (mouseButton.ButtonIndex == MouseButton.Left) //左键
            {
                if (mouseButton.Pressed) //按下
                {
                    _isDragging = true; //开始拖动
                    _hasSelection = false; //重置选区状态
                    _startPosition = mouseButton.Position; //记录起始位置
                    _endPosition = mouseButton.Position; //初始化结束位置
                }
                else
                {
                    _isDragging = false;
                    _hasSelection = true;
                    _endPosition = mouseButton.Position; //记录结束位置

                    SelectedRegion = new Rect2I(
                    (int)Mathf.Min(_startPosition.X, _endPosition.X), //X坐标
                    (int)Mathf.Min(_startPosition.Y, _endPosition.Y), //Y坐标
                    (int)Mathf.Abs(_endPosition.X - _startPosition.X), //宽度
                    (int)Mathf.Abs(_endPosition.Y - _startPosition.Y) //高度
                    );

                    EmitSignal(nameof(RegionSelected)); //发出区域选择完成信号(连接到TranslationResult中的OnRegionSelected)
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion) //检查鼠标移动
        {
            if (_isDragging)
            {
                _endPosition = mouseMotion.Position; //更新结束位置
                QueueRedraw(); //请求重绘
            }
        }      
    }

    public override void _Draw() //绘制区域
    {
        DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0, 0, 0, 0.3f), true);

        if ((_isDragging || _hasSelection) && _startPosition != _endPosition)
		{
			var rect = new Rect2(
					new Vector2(Mathf.Min(_startPosition.X, _endPosition.X), Mathf.Min(_startPosition.Y, _endPosition.Y)),
					new Vector2(Mathf.Abs(_endPosition.X - _startPosition.X), Mathf.Abs(_endPosition.Y - _startPosition.Y))
				);

            DrawRect(rect, new Color(1, 1, 1, 0.1f), true); //绘制填充
            DrawRect(rect, new Color(0, 0, 1, 1), false, 2); //绘制矩形边框（蓝色实线，宽度2像素）
        }
    }
}
