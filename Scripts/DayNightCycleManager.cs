using Godot;

public partial class DayNightCycleManager : Node3D
{
	public float DayLengthMinutes = 20.0f; // Real-time minutes for full day
	public float StartTimeOfDay = 0.25f; // Start at dawn (0.0 = midnight, 0.5 = noon)
	public bool PauseTime = false;
	public DirectionalLight3D Sun;
	public WorldEnvironment WorldEnvironment;

	// Current time (0.0 = midnight, 0.5 = noon, 1.0 = midnight next day)
	private float _timeOfDay;

	// Sky shader material
	private ShaderMaterial _skyMaterial;

	// Weather system reference
	private ShaderWeatherSystem _weatherSystem;

	public float TimeOfDay => _timeOfDay;
	public bool IsDay => _timeOfDay > 0.25f && _timeOfDay < 0.75f;
	public bool IsNight => !IsDay;

	public override void _Ready()
	{
		_timeOfDay = StartTimeOfDay;
		SetupEnvironmentAndSky();
		SetupSun();
		UpdateTimeOfDay(0); // Initial update

		GD.Print($"Day/Night cycle started at time {_timeOfDay:F2}");
	}

	public override void _Process(double delta)
	{
		if (PauseTime) return;

		// Advance time
		float timeSpeed = 1.0f / (DayLengthMinutes * 60.0f);
		_timeOfDay += (float)delta * timeSpeed;

		// Wrap around at end of day
		if (_timeOfDay >= 1.0f)
		{
			_timeOfDay -= 1.0f;
			GD.Print("New day started!");
		}

		UpdateTimeOfDay((float)delta);
	}

	public void SetWeatherSystem(ShaderWeatherSystem weatherSystem)
	{
		_weatherSystem = weatherSystem;
		GD.Print("Weather system linked to Day/Night cycle");
	}

	private void SetupEnvironmentAndSky()
	{
		// Create WorldEnvironment
		WorldEnvironment = new WorldEnvironment();
		var environment = new Environment();

		// Use custom sky shader
		var sky = new Sky();
		_skyMaterial = new ShaderMaterial();
		_skyMaterial.Shader = GD.Load<Shader>("res://Shaders/dynamic_sky.gdshader");

		// Set up star texture
		var starTexture = StarTextureGenerator.GenerateStarTexture();
		_skyMaterial.SetShaderParameter("stars_texture", starTexture);

		sky.SkyMaterial = _skyMaterial;

		environment.BackgroundMode = Environment.BGMode.Sky;
		environment.Sky = sky;
		environment.AmbientLightSource = Environment.AmbientSource.Sky;

		// FIX #5: Set reasonable default ambient light
		environment.AmbientLightColor = new Color(0.7f, 0.8f, 0.9f);
		environment.AmbientLightEnergy = 0.3f;

		WorldEnvironment.Environment = environment;
		AddChild(WorldEnvironment);

		GD.Print("WorldEnvironment and sky created");
	}

	private void SetupSun()
	{
		// Directional light (sun)
		Sun = new DirectionalLight3D();
		Sun.LightEnergy = 1.0f;
		Sun.LightColor = Colors.White;
		Sun.ShadowEnabled = true;
		AddChild(Sun);

		GD.Print("DirectionalLight (Sun) created");
	}

	private void UpdateTimeOfDay(float delta)
	{
		// Update sky shader
		if (_skyMaterial != null)
		{
			_skyMaterial.SetShaderParameter("time_of_day", _timeOfDay);
		}

		// Update sun position and intensity
		if (Sun != null)
		{
			UpdateSunLight();
		}

		// Update ambient lighting
		UpdateAmbientLight();
	}

	private void UpdateSunLight()
	{
		// Calculate sun angle (rises in east, sets in west)
		float angle = _timeOfDay * Mathf.Pi * 2.0f;
		Vector3 sunDirection = new Vector3(
			Mathf.Sin(angle) * 0.3f,
			Mathf.Cos(angle),
			0.2f
		).Normalized();

		// Set sun rotation to point in direction
		Sun.LookAt(Sun.GlobalPosition + sunDirection, Vector3.Up);

		// Sun intensity based on height
		float sunHeight = sunDirection.Y;
		float sunIntensity;

		if (sunHeight > 0.0f)
		{
			// Daytime - sun is up
			sunIntensity = Mathf.Clamp(sunHeight * 2.0f, 0.3f, 1.0f);
			Sun.LightColor = GetSunColor();
		}
		else
		{
			// Nighttime - moon light (very dim blue)
			sunIntensity = Mathf.Clamp(-sunHeight * 0.3f, 0.05f, 0.15f);
			Sun.LightColor = new Color(0.6f, 0.7f, 1.0f);
		}

		// Apply weather influence if available
		if (_weatherSystem != null)
		{
			float weatherDarkness = GetWeatherDarkness();
			sunIntensity *= (1.0f - weatherDarkness * 0.7f);
		}

		Sun.LightEnergy = sunIntensity;
	}

	private Color GetSunColor()
	{
		// Sun color changes throughout the day
		if (_timeOfDay < 0.25f || _timeOfDay > 0.75f)
		{
			// Night (shouldn't normally be visible, but just in case)
			return new Color(0.6f, 0.7f, 1.0f);
		}
		else if (_timeOfDay < 0.3f || _timeOfDay > 0.7f)
		{
			// Sunrise/Sunset - orange
			return new Color(1.0f, 0.7f, 0.4f);
		}
		else
		{
			// Midday - white/yellow
			return new Color(1.0f, 0.98f, 0.9f);
		}
	}

	private void UpdateAmbientLight()
	{
		if (WorldEnvironment?.Environment == null) return;

		// Ambient light intensity
		float ambientIntensity;
		Color ambientColor;

		if (IsDay)
		{
			ambientIntensity = 0.3f;
			ambientColor = new Color(0.7f, 0.8f, 0.9f);
		}
		else
		{
			ambientIntensity = 0.05f;
			ambientColor = new Color(0.2f, 0.3f, 0.5f);
		}

		WorldEnvironment.Environment.AmbientLightColor = ambientColor;
		WorldEnvironment.Environment.AmbientLightEnergy = ambientIntensity;
	}

	private float GetWeatherDarkness()
	{
		// You'll need to expose this from ShaderWeatherSystem
		// For now, return 0
		return 0.0f;
	}

	// Public methods to control time
	public void SetTimeOfDay(float time)
	{
		_timeOfDay = Mathf.Clamp(time, 0.0f, 1.0f);
	}

	public void SetTime(int hour, int minute = 0)
	{
		float totalMinutes = hour * 60 + minute;
		_timeOfDay = totalMinutes / 1440.0f; // 1440 minutes in a day
	}

	public string GetTimeString()
	{
		int totalMinutes = (int)(_timeOfDay * 1440);
		int hours = totalMinutes / 60;
		int minutes = totalMinutes % 60;
		return $"{hours:D2}:{minutes:D2}";
	}
}
