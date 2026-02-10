using Godot;

[GlobalClass]
public partial class CharacterLoadout : Resource
{
    [Export] public string Name;

    [Export] public string HeadItem;
    [Export] public string LeftHandItem;
    [Export] public string RightHandItem;
    [Export] public string TorsoItem;
}
