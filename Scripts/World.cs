using Godot;

public partial class World : Node3D
{
	[ExportCategory("World Generation Settings")]
	[Export] public int Seed = 12345;

	[ExportGroup("Region Settings")]
	[Export] public int RegionsX = 3;
	[Export] public int RegionsZ = 3;
	[Export] public int ChunksPerRegionX = 40;
	[Export] public int ChunksPerRegionZ = 40;

	[ExportGroup("Chunk Settings")]
	[Export] public int ChunkSizeX = 64;
	[Export] public int ChunkSizeZ = 64;

	[ExportGroup("Noise Settings")]
	[Export] public float HeightScale = 10.0f;
	[Export] public float NoiseScale = 0.02f;

	[ExportGroup("Generation")]
	[Export] public string WorldMetadataPath = "res://Data/world_data/world_metadata.dat";
	[Export] public string WorldDataLookupPath = "res://Data/world_data/world_lookup.dat";

	[ExportGroup("Player Settings")]
	[Export] public CharacterBody3D Player;

	[ExportGroup("Render Settings")]
	[Export] public float UpdateInterval = 0.5f;
	[Export] public int RenderRadiusChunks = 5;
	[Export] public int AIRadiusChunks = 7;
	[Export] public int InMemoryRadiusChunks = 10;

	[ExportGroup("Debug")]
	[Export] public bool ShowDebugInfo = false;
	private RuntimeChunkLoader _chunkLoader;
	private WorldData _worldData;

	private DebugUI _debugUI;
	private Label _debugLabel = new Label();

	public override void _Ready()
	{
		GetWorldMetadata();
		_chunkLoader = new RuntimeChunkLoader(Player, InMemoryRadiusChunks, AIRadiusChunks, RenderRadiusChunks, UpdateInterval);
		_chunkLoader.WorldData = _worldData;
		AddChild(_chunkLoader);

		var dayNightCycleManager = new DayNightCycleManager();
		dayNightCycleManager.DayLengthMinutes = 10.0f; // 10 real minutes = 1 game day
		dayNightCycleManager.StartTimeOfDay = 0.25f; // Start at dawn
		AddChild(dayNightCycleManager);

		GD.Print("DayNightCycleManager created");

		var shaderWeatherSystem = new ShaderWeatherSystem(Player, dayNightCycleManager);
		AddChild(shaderWeatherSystem);

		GD.Print("ShaderWeatherSystem created");

		dayNightCycleManager.SetWeatherSystem(shaderWeatherSystem);

		var HUD = new HUD(Player, FindNodeRecursive<Camera3D>(Player), _chunkLoader, dayNightCycleManager, shaderWeatherSystem);
		AddChild(HUD);

		GD.Print($"=== World Initialization Complete ===");
		GD.Print($"  Time: {dayNightCycleManager.GetTimeString()}, Weather initialized.");
		GD.Print($"  Total World Chunks: {_chunkLoader.WorldData.WorldSizeChunks.X * _chunkLoader.WorldData.WorldSizeChunks.Y}");
		GD.Print($"  World Size: {_chunkLoader.WorldData.WorldSizeChunks.X * _chunkLoader.WorldData.ChunkSize.X}x{_chunkLoader.WorldData.WorldSizeChunks.Y * _chunkLoader.WorldData.ChunkSize.Y} meters");

		_debugUI = new DebugUI();
		_debugUI.CreateDebugUI(_debugLabel, HUD.Canvas);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_debugLabel != null && _chunkLoader != null)
		{
			_debugLabel.Text = $"Player World Position: {Player.Position.Round()}\n" +
								$"Player Chunk: {_chunkLoader.LastPlayerChunk}\nChunks Loaded: {_chunkLoader.InMemoryChunks.Count}\n";
		}
	}

	private void GetWorldMetadata()
	{
		FileAccess worldMetadataFile = FileAccess.Open(WorldMetadataPath, FileAccess.ModeFlags.Read);
		if (worldMetadataFile == null || worldMetadataFile.GetError() != Error.Ok)
		{
			GD.PrintErr($"Failed to open world metadata file at {WorldMetadataPath}");
			return;
		}
		var worldMetadata = WorldMetadata.Deserialize(worldMetadataFile);
		worldMetadataFile.Close();
		var worldData = new WorldData
		{
			Seed = worldMetadata.Seed,
			WorldRegions = worldMetadata.WorldRegions,
			RegionSize = worldMetadata.RegionSize,
			ChunkSize = worldMetadata.ChunkSize,
			HeightScale = worldMetadata.HeightScale,
			PlayerStartPosition = worldMetadata.PlayerStartPosition
		};
		_worldData = worldData;
	}

	public static T FindNodeRecursive<T>(Node parent) where T : Node
	{
		foreach (Node child in parent.GetChildren())
		{
			if (child is T match)
				return match;

			var result = FindNodeRecursive<T>(child);
			if (result != null)
				return result;
		}
		return null;
	}
}
