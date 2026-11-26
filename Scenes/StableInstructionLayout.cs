using Godot;

internal sealed class StableInstructionLayout
{
    private readonly Label _status;
    private readonly Label _detail;
    private readonly (string[] Texts, Font Font, int Size, int Spacing)[] _messages;
    private float _width;

    internal StableInstructionLayout(Label status, Label detail, string[] instructions, StringName instructionStyle,
        string[] rejections, StringName rejectionStyle)
    {
        _status = status;
        _detail = detail;
        Slot = BindSlot(detail);
        _messages = [GetMetrics(instructions, instructionStyle), GetMetrics(rejections, rejectionStyle)];
    }

    internal Control Slot { get; }

    internal void Layout(float width)
    {
        if (width <= 0 || width == _width)
        {
            return;
        }

        _width = width;
        _status.CustomMaximumSize = _detail.CustomMaximumSize = new Vector2(width, -1);
        var breaks = TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound |
            TextServer.LineBreakFlag.Adaptive | _detail.AutowrapTrimFlags;
        float height = 0f;
        foreach (var (texts, font, size, spacing) in _messages)
        {
            float lineHeight = font.GetHeight(size);
            foreach (string text in texts)
            {
                float textHeight = font.GetMultilineStringSize(text, HorizontalAlignment.Left, width, size, -1, breaks).Y;
                // Font measurements give the unspaced line count. Apply the theme spacing only between lines.
                float extraLines = Mathf.Max(0f, Mathf.Round(textHeight / lineHeight) - 1f);
                height = Mathf.Max(height, Mathf.Ceil(textHeight + extraLines * spacing));
            }
        }

        Slot.CustomMinimumSize = new Vector2(0, height);
    }

    internal static Vector2 MeasureButtonMinimum(Button button, params string[] captions)
    {
        // Keep caption changes from invalidating the panel's minimum size during play.
        string current = button.Text;
        Vector2 minimum = button.CustomMinimumSize;
        foreach (string caption in captions)
        {
            button.Text = caption;
            minimum = minimum.Max(button.GetMinimumSize());
        }

        button.Text = current;
        return minimum;
    }

    private (string[], Font, int, int) GetMetrics(string[] texts, StringName style) =>
        (texts, _detail.GetThemeFont("font", style), _detail.GetThemeFontSize("font_size", style),
            _detail.GetThemeConstant("line_spacing", style));

    private static Control BindSlot(Label detail)
    {
        detail.VerticalAlignment = VerticalAlignment.Top;
        Control parent = detail.GetParent<Control>();
        if (parent is not Container)
        {
            return parent;
        }

        // A plain control isolates the label's minimum size from the panel's container layout.
        var slot = new Control
        {
            Name = "InstructionSlot",
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        int index = detail.GetIndex();
        parent.AddChild(slot);
        parent.MoveChild(slot, index);
        detail.Reparent(slot, keepGlobalTransform: false);
        detail.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return slot;
    }
}
