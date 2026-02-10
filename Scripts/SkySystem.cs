using Godot;

public partial class SkySystem : Node3D
{
	[ExportGroup("Sky Settings")]
	[Export] public Gradient SkyTopColor;
	[Export] public Gradient SkyHorizonColor;
	[Export] public Color GroundBottomColor = new Color(0.1f, 0.1f, 0.1f);
	[Export] public Color GroundHorizonColor = new Color(0.4f, 0.35f, 0.3f);

	[ExportGroup("Sun Settings")]
	[Export] public float SunEnergy = 1.0f;
	[Export] public Color SunColor = Colors.White;
	[Export] public float SunAngle = 45.0f; // Degrees from horizon

	[ExportGroup("Fog Settings")]
	[Export] public bool EnableFog = true;
	[Export] public Color FogColor = new Color(0.5f, 0.6f, 0.7f);
	[Export] public float FogDensity = 0.001f;
	[Export] public float FogStart = 50.0f;
	[Export] public float FogEnd = 500.0f;

	private WorldEnvironment _worldEnvironment;
	private DirectionalLight3D _sun;
	private Sky _sky;
	private ProceduralSkyMaterial _skyMaterial;

	public override void _Ready()
	{
		SetupEnvironment();
		SetupSun();
	}

	private void SetupEnvironment()
	{
		_worldEnvironment = new WorldEnvironment();
		AddChild(_worldEnvironment);

		var environment = new Environment();
		_worldEnvironment.Environment = environment;

		// Background
		environment.BackgroundMode = Environment.BGMode.Sky;

		// Create sky
		_sky = new Sky();
		_skyMaterial = new ProceduralSkyMaterial();
		_skyMaterial.SkyTopColor = new Color(0.385f, 0.454f, 0.55f);
		_skyMaterial.SkyHorizonColor = new Color(0.646f, 0.656f, 0.67f);
		_skyMaterial.GroundBottomColor = GroundBottomColor;
		_skyMaterial.GroundHorizonColor = GroundHorizonColor;
		_skyMaterial.SunAngleMax = 30.0f;

		_sky.SkyMaterial = _skyMaterial;
		environment.Sky = _sky;

		// Ambient light
		environment.AmbientLightSource = Environment.AmbientSource.Sky;
		environment.AmbientLightSkyContribution = 0.5f;

		// Fog
		if (EnableFog)
		{
			environment.FogEnabled = true;
			environment.FogLightColor = FogColor;
			environment.FogDensity = FogDensity;
			environment.FogDepthBegin = FogStart;
			environment.FogDepthEnd = FogEnd;
		}

		// Tonemap for better visuals
		environment.TonemapMode = Environment.ToneMapper.Aces;
		environment.TonemapExposure = 1.0f;
	}

	private void SetupSun()
	{
		_sun = new DirectionalLight3D();
		_sun.LightEnergy = SunEnergy;
		_sun.LightColor = SunColor;

		// Position sun based on angle
		float angleRad = Mathf.DegToRad(SunAngle);
		_sun.RotationDegrees = new Vector3(-SunAngle, 45, 0);

		// Enable shadows
		_sun.ShadowEnabled = true;
		_sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
		_sun.DirectionalShadowMaxDistance = 200.0f;

		AddChild(_sun);
	}

	// Public methods to control weather
	public void SetFogDensity(float density)
	{
		if (_worldEnvironment?.Environment != null)
		{
			_worldEnvironment.Environment.FogDensity = density;
		}
	}

	public void SetSunIntensity(float intensity)
	{
		if (_sun != null)
		{
			_sun.LightEnergy = intensity;
		}
	}
}