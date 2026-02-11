using Godot;
public partial class HUD : Control
{
	public string TimeInGame;
	public CanvasLayer Canvas;

	private CharacterBody3D _player;
	private Camera3D _camera;
	private RuntimeChunkLoader _chunkLoader;
	private DayNightCycleManager _dayNightManager;
	private ShaderWeatherSystem _weatherSystem;

	private Compass _compass;
	private TimeWeatherWidget _timeWeatherWidget;

	public HUD(CharacterBody3D player, Camera3D camera, RuntimeChunkLoader chunkLoader, DayNightCycleManager dayNightManager, ShaderWeatherSystem weatherSystem)
	{
		_player = player;
		_camera = camera;
		_chunkLoader = chunkLoader;
		_dayNightManager = dayNightManager;
		_weatherSystem = weatherSystem;
	}

	public override void _Ready()
	{
		Canvas = new CanvasLayer();
		AddChild(Canvas);

		_compass = new Compass(_camera);
		Canvas.AddChild(_compass);

		_timeWeatherWidget = new TimeWeatherWidget(_dayNightManager, _weatherSystem);
		Canvas.AddChild(_timeWeatherWidget);
	}

	public override void _Process(double delta)
	{
		if (_compass != null && _chunkLoader != null && _player != null)
		{
			var pois = _chunkLoader.GetPOI();
			_compass.UpdatePOIs(_player.GlobalPosition, pois.positions, pois.types);
		}
	}
}
