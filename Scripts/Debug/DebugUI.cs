using Godot;
using System;
public class DebugUI
{
    public void CreateDebugUI(Label debugLabel, CanvasLayer canvas)
    {
        // Anchor to bottom right corner
        debugLabel.AnchorBottom = 1.0f;
        debugLabel.AnchorLeft = 0.0f;
        debugLabel.AnchorRight = 1.0f;
        debugLabel.AnchorTop = 0.0f;

        debugLabel.HorizontalAlignment = HorizontalAlignment.Right;
        debugLabel.VerticalAlignment = VerticalAlignment.Bottom;

        debugLabel.AddThemeColorOverride("font_color", Colors.White);
        debugLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        debugLabel.AddThemeConstantOverride("outline_size", 2);
        canvas.AddChild(debugLabel);
    }
}