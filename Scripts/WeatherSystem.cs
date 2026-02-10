using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class WeatherSystem : Node3D
{
	[Export] public Node3D Player;
	[Export] public SkySystem SkySystem;
	[Export] public float WeatherTransitionSpeed = 0.5f;

	private List<WeatherZone> _weatherZones = new();
	private WeatherType _currentWeather = WeatherType.Clear;
	private WeatherType _targetWeather = WeatherType.Clear;
	private float _weatherTransition = 1.0f; // 0-1, how far into transition

	// Rain particles
	private GpuParticles3D _rainParticles;

	// Weather timers for dynamic zones
	private float _weatherChangeTimer = 0.0f;
	private float _weatherChangeDuration = 300.0f; // Change weather every 5 minutes

	private AudioStreamPlayer _rainSound;
	private AudioStreamPlayer _thunderSound;

	public override void _Ready()
	{
		SetupRainParticles();

		// Example: Create some permanent weather zones
		// These would be tied to your biome generation later
		CreatePermanentWeatherZones();
	}

	private void SetupRainParticles()
	{
		_rainParticles = new GpuParticles3D();
		_rainParticles.Amount = 5000;
		_rainParticles.Lifetime = 2.0f;
		_rainParticles.Emitting = false;
		_rainParticles.VisibilityRangeEnd = 100.0f;

		// Rain material
		var particleMaterial = new ParticleProcessMaterial();
		particleMaterial.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		particleMaterial.EmissionBoxExtents = new Vector3(50, 0, 50);
		particleMaterial.Direction = new Vector3(0, -1, 0);
		particleMaterial.InitialVelocityMin = 20.0f;
		particleMaterial.InitialVelocityMax = 25.0f;
		particleMaterial.Gravity = new Vector3(0, -30, 0);
		particleMaterial.Scale = new Vector2(0.1f, 0.1f);

		_rainParticles.ProcessMaterial = particleMaterial;

		// Visual mesh for raindrops
		var raindropMesh = new QuadMesh();
		raindropMesh.Size = new Vector2(0.02f, 0.2f);
		_rainParticles.DrawPass1 = raindropMesh;

		AddChild(_rainParticles);
	}

	private void CreatePermanentWeatherZones()
	{
		// Example: Rainy zone around player start
		_weatherZones.Add(new WeatherZone(
			new Vector2(Player.GlobalPosition.X, Player.GlobalPosition.Z),
			radius: 150.0f,
			WeatherType.Rainy,
			isPermanent: true
		));

		// _weatherZones.Add(new WeatherZone(
		// 	new Vector2(400, 600),
		// 	radius: 200.0f,
		// 	WeatherType.Foggy,
		// 	isPermanent: true
		// ));
	}

	public void AddWeatherZone(WeatherZone zone)
	{
		_weatherZones.Add(zone);
	}

	public override void _Process(double delta)
	{
		if (Player == null)
			return;

		float deltaTime = (float)delta;

		// Update rain particle position to follow player
		if (_rainParticles != null)
		{
			_rainParticles.GlobalPosition = new Vector3(
				Player.GlobalPosition.X,
				Player.GlobalPosition.Y + 30, // Above player
				Player.GlobalPosition.Z
			);
		}

		// Determine what weather zone player is in
		WeatherType desiredWeather = GetWeatherAtPosition(Player.GlobalPosition);

		// Start transition if weather changed
		if (desiredWeather != _targetWeather)
		{
			_currentWeather = _targetWeather;
			_targetWeather = desiredWeather;
			_weatherTransition = 0.0f;
		}

		// Update transition
		if (_weatherTransition < 1.0f)
		{
			_weatherTransition += WeatherTransitionSpeed * deltaTime;
			_weatherTransition = Mathf.Clamp(_weatherTransition, 0.0f, 1.0f);

			ApplyWeatherTransition(_currentWeather, _targetWeather, _weatherTransition);
		}

		// Update dynamic weather zones (non-permanent)
		UpdateDynamicWeather(deltaTime);
	}

	private WeatherType GetWeatherAtPosition(Vector3 position)
	{
		// Check all zones, prioritize permanent zones
		WeatherZone strongestZone = null;
		float strongestInfluence = 0.0f;

		foreach (var zone in _weatherZones)
		{
			float influence = zone.GetInfluence(position);
			if (influence > strongestInfluence)
			{
				strongestInfluence = influence;
				strongestZone = zone;
			}
		}

		return strongestZone?.Weather ?? WeatherType.Clear;
	}

	private void ApplyWeatherTransition(WeatherType from, WeatherType to, float t)
	{
		// Interpolate weather effects

		// Rain intensity
		float fromRain = GetRainIntensity(from);
		float toRain = GetRainIntensity(to);
		float rainIntensity = Mathf.Lerp(fromRain, toRain, t);

		if (_rainParticles != null)
		{
			_rainParticles.Emitting = rainIntensity > 0.1f;
			_rainParticles.Amount = Mathf.Clamp((int)(5000 * rainIntensity), 1, int.MaxValue);
		}

		// Fog density
		float fromFog = GetFogDensity(from);
		float toFog = GetFogDensity(to);
		float fogDensity = Mathf.Lerp(fromFog, toFog, t);

		if (SkySystem != null)
		{
			SkySystem.SetFogDensity(fogDensity);
		}

		// Sun intensity
		float fromSun = GetSunIntensity(from);
		float toSun = GetSunIntensity(to);
		float sunIntensity = Mathf.Lerp(fromSun, toSun, t);

		if (SkySystem != null)
		{
			SkySystem.SetSunIntensity(sunIntensity);
		}

		// if (_rainSound != null)
		// {
		// 	_rainSound.VolumeDb = Mathf.LinearToDb(rainIntensity);
		// 	if (rainIntensity > 0.1f && !_rainSound.Playing)
		// 		_rainSound.Play();
		// 	else if (rainIntensity <= 0.1f && _rainSound.Playing)
		// 		_rainSound.Stop();
		// }
	}

	private float GetRainIntensity(WeatherType weather)
	{
		return weather switch
		{
			WeatherType.Rainy => 0.6f,
			WeatherType.Stormy => 1.0f,
			_ => 0.0f
		};
	}

	private float GetFogDensity(WeatherType weather)
	{
		return weather switch
		{
			WeatherType.Foggy => 0.01f,
			WeatherType.Rainy => 0.003f,
			WeatherType.Cloudy => 0.002f,
			WeatherType.Clear => 0.001f,
			WeatherType.Stormy => 0.005f,
			_ => 0.001f
		};
	}

	private float GetSunIntensity(WeatherType weather)
	{
		return weather switch
		{
			WeatherType.Clear => 1.0f,
			WeatherType.Cloudy => 0.6f,
			WeatherType.Rainy => 0.3f,
			WeatherType.Foggy => 0.4f,
			WeatherType.Stormy => 0.2f,
			_ => 1.0f
		};
	}

	private void UpdateDynamicWeather(float delta)
	{
		_weatherChangeTimer += delta;

		if (_weatherChangeTimer >= _weatherChangeDuration)
		{
			_weatherChangeTimer = 0.0f;

			// Change weather in non-permanent zones
			foreach (var zone in _weatherZones.Where(z => !z.IsPermanent))
			{
				zone.Weather = GetRandomWeather();
			}
		}
	}

	private WeatherType GetRandomWeather()
	{
		var values = Enum.GetValues<WeatherType>();
		return values[GD.RandRange(0, values.Length - 1)];
	}

	private void SetupAudio()
	{
		_rainSound = new AudioStreamPlayer();
		_rainSound.Bus = "SFX";
		// _rainSound.Stream = GD.Load<AudioStream>("res://sounds/rain_loop.ogg");
		AddChild(_rainSound);

		_thunderSound = new AudioStreamPlayer();
		// _thunderSound.Stream = GD.Load<AudioStream>("res://sounds/thunder.ogg");
		AddChild(_thunderSound);
	}
}