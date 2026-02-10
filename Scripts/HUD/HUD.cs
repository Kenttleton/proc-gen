using Godot;
public partial class HUD : Control
{
	public string TimeInGame;
	public CanvasLayer Canvas;
	private CharacterBody3D _player;
	private Camera3D _camera;
	private RuntimeChunkLoader _chunkLoader;
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

		_compass = new Compass(_camera);
		Canvas.AddChild(_compass);
	}

	public override void _Process(double delta)
	{
		if (_compass != null && _chunkLoader != null && _player != null)
		{
			var pois = _chunkLoader.GetPOI();
			_compass.UpdatePOIs(_player.GlobalPosition, pois);
		}
		// GD.Print($"Player Position: {_player.GlobalPosition}");
		// GD.Print($"POI: {_chunkLoader.GetPOI()}");
		// GD.Print($"Compass: {_compass == null}");
	}
}
