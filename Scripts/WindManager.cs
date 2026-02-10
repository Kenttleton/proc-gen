using Godot;

public partial class WindManager : Node
{
	[Export] public ShaderWeatherSystem WeatherSystem;

	// Wind presets per weather
	private readonly (float strength, float speed, Vector2 direction)[] _windPresets =
	{
		(0.1f, 0.5f, new Vector2(1, 0)),      // Clear
        (0.3f, 0.8f, new Vector2(1, 0.3f)),   // Cloudy
        (0.6f, 1.5f, new Vector2(1, 0.5f)),   // Rainy
        (1.0f, 3.0f, new Vector2(1, 0.8f)),   // Stormy
        (0.05f, 0.3f, new Vector2(0.5f, 1)),  // Foggy
    };

	private float _currentWindStrength = 0.1f;
	private float _currentWindSpeed = 0.5f;
	private Vector2 _currentWindDirection = Vector2.Right;

	public override void _Process(double delta)
	{
		if (WeatherSystem == null)
			return;

		// Get target wind from weather
		var targetWind = GetWindFromWeather();

		// Smooth transition
		float smoothing = 0.5f * (float)delta;
		_currentWindStrength = Mathf.Lerp(_currentWindStrength, targetWind.strength, smoothing);
		_currentWindSpeed = Mathf.Lerp(_currentWindSpeed, targetWind.speed, smoothing);
		_currentWindDirection = _currentWindDirection.Lerp(targetWind.direction, smoothing);

		// Update all vegetation shaders
		UpdateVegetationShaders();
	}

	private (float strength, float speed, Vector2 direction) GetWindFromWeather()
	{
		// This would read from your weather system
		// For now, demonstrate with interpolation based on weather darkness
		float weatherIntensity = WeatherSystem?.WeatherDarkness ?? 0.0f;

		return (
			Mathf.Lerp(0.1f, 1.0f, weatherIntensity),
			Mathf.Lerp(0.5f, 3.0f, weatherIntensity),
			Vector2.Right.Rotated(Mathf.Sin(Time.GetTicksMsec() * 0.0001f) * 0.5f)
		);
	}

	private void UpdateVegetationShaders()
	{
		// Find all MultiMeshInstances with wind shaders
		var vegetation = GetTree().GetNodesInGroup("vegetation");

		foreach (Node node in vegetation)
		{
			if (node is MultiMeshInstance3D multiMesh &&
				multiMesh.MaterialOverride is ShaderMaterial shader)
			{
				shader.SetShaderParameter("wind_strength", _currentWindStrength);
				shader.SetShaderParameter("wind_speed", _currentWindSpeed);
				shader.SetShaderParameter("wind_direction", _currentWindDirection);
			}
		}
	}
}