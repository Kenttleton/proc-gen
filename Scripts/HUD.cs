using Godot;
public partial class HUD : Control
{
	public string TimeInGame;
	public CanvasLayer Canvas;
	private CharacterBody3D _player;
	private Camera3D _camera;
	private RuntimeChunkLoader _chunkLoader;
	private Control _root;
	private Compass _compass;

	public HUD(CharacterBody3D player, Camera3D camera, RuntimeChunkLoader chunkLoader)
	{
		_player = player;
		_camera = camera;
		_chunkLoader = chunkLoader;
	}

	public override void _Ready()
	{
		Canvas = new CanvasLayer();
		AddChild(Canvas);

		// _root = new Control();
		// _root.AnchorLeft = 0;
		// _root.AnchorRight = 1;
		// _root.AnchorTop = 0;
		// _root.AnchorBottom = 0;
		// _root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		// _root.CustomMinimumSize = new Vector2(0, 80);
		// AddChild(_root);

		_compass = new Compass(this, _camera);
		AddChild(_compass);
	}

	public override void _Process(double delta)
	{
		if (_compass != null && _chunkLoader != null)
		{
			var pois = _chunkLoader.GetPOI();
			_compass.UpdatePOIs(_player.GlobalPosition, pois);
		}
	}
}
