using Godot;

public partial class ShaderWeatherSystem : Node3D
{
	public Node3D Player;
	public float WeatherTransitionSpeed = 0.3f;
	public float WeatherDarkness => _fogIntensity * 0.3f + _rainIntensity * 0.5f;

	// Weather intensity (0 = clear, 1 = storm)
	private float _rainIntensity = 0.0f;
	private float _fogIntensity = 0.0f;
	private float _cloudCoverage = 0.2f;
	private float _targetRainIntensity = 0.0f;
	private float _targetFogIntensity = 0.0f;
	private float _targetCloudCoverage = 0.2f;

	// Shader components
	private MeshInstance3D _rainMesh;
	private MeshInstance3D _fogVolume;
	private MeshInstance3D _cloudPlane;
	private WorldEnvironment _worldEnvironment;
	private DirectionalLight3D _sun;

	private ShaderMaterial _rainMaterial;
	private ShaderMaterial _fogMaterial;
	private ShaderMaterial _cloudMaterial;
	private DayNightCycleManager _dayNightCycleManager;

	public ShaderWeatherSystem(Node3D player, DayNightCycleManager dayNightCycleManager)
	{
		Player = player;
		_dayNightCycleManager = dayNightCycleManager;
	}

	public override void _Ready()
	{
		SetupShaderWeather();
		// TODO: Change later. For debugging weather
		SetWeather(WeatherType.Rainy);
	}

	private void SetupShaderWeather()
	{
		// Rain mesh (quad that follows player)
		_rainMesh = new MeshInstance3D();
		var rainQuad = new QuadMesh();
		rainQuad.Size = new Vector2(100, 100);
		_rainMesh.Mesh = rainQuad;
		_rainMesh.RotationDegrees = new Vector3(-90, 0, 0);

		_rainMaterial = new ShaderMaterial();
		_rainMaterial.Shader = GD.Load<Shader>("res://Shaders/rain.gdshader");
		_rainMesh.MaterialOverride = _rainMaterial;
		_rainMesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		AddChild(_rainMesh);
		if (Player != null)
		{
			_rainMesh.GlobalPosition = new Vector3(Player.GlobalPosition.X, Player.GlobalPosition.Y + 15, Player.GlobalPosition.Z);
		}

		// Fog volume (box around player)
		_fogVolume = new MeshInstance3D();
		var fogBox = new BoxMesh();
		fogBox.Size = new Vector3(200, 100, 200);
		_fogVolume.Mesh = fogBox;

		_fogMaterial = new ShaderMaterial();
		_fogMaterial.Shader = GD.Load<Shader>("res://Shaders/volumetric_fog.gdshader");
		_fogVolume.MaterialOverride = _fogMaterial;
		_fogVolume.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		AddChild(_fogVolume);

		if (Player != null)
		{
			_fogVolume.GlobalPosition = new Vector3(Player.GlobalPosition.X, 25, Player.GlobalPosition.Z);
		}



		// Cloud plane (high above world)
		_cloudPlane = new MeshInstance3D();
		var cloudQuad = new QuadMesh();
		cloudQuad.Size = new Vector2(1000, 1000);
		_cloudPlane.Mesh = cloudQuad;
		_cloudPlane.Position = new Vector3(0, 100, 0);
		_cloudPlane.RotationDegrees = new Vector3(-90, 0, 0);

		_cloudMaterial = new ShaderMaterial();
		_cloudMaterial.Shader = GD.Load<Shader>("res://Shaders/clouds.gdshader");

		// Create noise texture for clouds
		var noise = new FastNoiseLite();
		noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		noise.Frequency = 0.05f;
		var noiseTexture = new NoiseTexture2D();
		noiseTexture.Noise = noise;
		noiseTexture.Width = 512;
		noiseTexture.Height = 512;

		_cloudMaterial.SetShaderParameter("noise_texture", noiseTexture);
		_cloudPlane.MaterialOverride = _cloudMaterial;
		_cloudPlane.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		AddChild(_cloudPlane);

		GD.Print("Shader weather system initialized");
	}

	public override void _Process(double delta)
	{
		if (Player == null) return;

		float dt = (float)delta;

		// Update positions to follow player
		UpdateWeatherPositions();

		// Smooth transitions
		_rainIntensity = Mathf.Lerp(_rainIntensity, _targetRainIntensity, WeatherTransitionSpeed * dt);
		_fogIntensity = Mathf.Lerp(_fogIntensity, _targetFogIntensity, WeatherTransitionSpeed * dt);
		_cloudCoverage = Mathf.Lerp(_cloudCoverage, _targetCloudCoverage, WeatherTransitionSpeed * dt);

		// if (_cloudMaterial != null && _dayNightCycleManager != null)
		// {
		// 	_cloudMaterial.SetShaderParameter("cloud_coverage", _cloudCoverage);
		// 	_cloudMaterial.SetShaderParameter("time_of_day", _dayNightCycleManager.TimeOfDay);
		// }

		// Update shader parameters
		UpdateShaderParameters();
	}

	private void UpdateWeatherPositions()
	{
		Vector3 playerPos = Player.GlobalPosition;

		if (_rainMesh != null)
		{
			_rainMesh.GlobalPosition = new Vector3(playerPos.X, playerPos.Y + 15, playerPos.Z);
		}

		// Rain follows player
		if (_rainMaterial != null)
		{
			_rainMaterial.SetShaderParameter("player_position", playerPos);
		}

		// Fog volume centered on player
		if (_fogVolume != null)
		{
			_fogVolume.GlobalPosition = new Vector3(playerPos.X, 25, playerPos.Z);
		}

		if (_cloudPlane != null)
		{
			Vector3 cloudPos = _cloudPlane.GlobalPosition;
			cloudPos.X = playerPos.X;
			cloudPos.Z = playerPos.Z;
			_cloudPlane.GlobalPosition = cloudPos;
		}
	}

	private void UpdateShaderParameters()
	{
		// Rain
		if (_rainMaterial != null)
		{
			_rainMaterial.SetShaderParameter("rain_intensity", _rainIntensity);
		}

		// Fog
		if (_fogMaterial != null)
		{
			_fogMaterial.SetShaderParameter("fog_density", _fogIntensity);
		}

		// Clouds
		if (_cloudMaterial != null)
		{
			_cloudMaterial.SetShaderParameter("cloud_coverage", _cloudCoverage);
			if (_dayNightCycleManager != null)
			{
				_cloudMaterial.SetShaderParameter("time_of_day", _dayNightCycleManager.TimeOfDay);
			}
		}

		// Update sky shader with weather darkness
		if (_dayNightCycleManager?.WorldEnvironment?.Environment?.Sky?.SkyMaterial is ShaderMaterial skyMaterial)
		{
			skyMaterial.SetShaderParameter("weather_darkness", WeatherDarkness);
		}
	}

	// Public methods to control weather
	public void SetWeather(WeatherType weather)
	{
		GD.Print($"Weather changing to: {weather}");

		switch (weather)
		{
			case WeatherType.Clear:
				_targetRainIntensity = 0.0f;
				_targetFogIntensity = 0.0f;
				_targetCloudCoverage = 0.2f;
				break;

			case WeatherType.Cloudy:
				_targetRainIntensity = 0.0f;
				_targetFogIntensity = 0.1f;
				_targetCloudCoverage = 0.7f;
				break;

			case WeatherType.Rainy:
				_targetRainIntensity = 0.6f;
				_targetFogIntensity = 0.3f;
				_targetCloudCoverage = 0.8f;
				break;

			case WeatherType.Stormy:
				_targetRainIntensity = 1.0f;
				_targetFogIntensity = 0.5f;
				_targetCloudCoverage = 0.95f;
				break;

			case WeatherType.Foggy:
				_targetRainIntensity = 0.0f;
				_targetFogIntensity = 0.8f;
				_targetCloudCoverage = 0.9f;
				break;
		}
	}
}
