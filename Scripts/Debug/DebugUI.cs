using Godot;
using System;
public class DebugUI
{
    public void CreateDebugUI(Label debugLabel, CanvasLayer canvas)
    {
        debugLabel.Position = new Vector2(10, 10);
        debugLabel.AddThemeColorOverride("font_color", Colors.White);
        debugLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        debugLabel.AddThemeConstantOverride("outline_size", 2);
        canvas.AddChild(debugLabel);
    }
}