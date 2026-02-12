using Godot;
using System;

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
	[Export] public string WorldDataPath = "res://Data/World/Data.bin";

	[ExportGroup("Player Settings")]
	[Export] public CharacterBody3D Player;

	[ExportGroup("Render Settings")]
	[Export] public float UpdateInterval = 0.5f;
	[Export] public int RenderDistanceChunks = 5;
	[Export] public int AIDinstanceChunks = 7;
	[Export] public int MemoryDistanceChunks = 10;

	[ExportGroup("Debug")]
	[Export] public bool ShowDebugInfo = false;

	private RegionGenerator _generator;
	private RuntimeChunkLoader _chunkLoader;
	private DebugUI _debugUI;
	private Label _debugLabel = new Label();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// Create world generator
		_generator = new RegionGenerator(
			Seed,
			new Vector2I(WorldNumChunksX, WorldNumChunksY),
			new Vector2I(ChunkSizeX, ChunkSizeY),
			HeightScale,
			NoiseScale,
			WorldDataPath);

		_generator.LoadWorldData();

		GD.Print($"WorldGenerator initialized with Seed: {Seed}, World Size: {_generator.WorldSizeChunks.X}x{_generator.WorldSizeChunks.Y} chunks, Chunk Size: {_generator.ChunkSize.X}x{_generator.ChunkSize.Y}, Height Scale: {HeightScale}, Noise Scale: {NoiseScale}, Generate: {RegenerateWorld}");

		Player.Position = _generator.WorldData.PlayerStartPosition;

		_chunkLoader = new RuntimeChunkLoader(_generator.WorldData, Player, RenderDistanceChunks, UpdateInterval);
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
		GD.Print($"  World Size: {_generator.WorldData?.WorldSizeChunks.X ?? 0}x{_generator.WorldData?.WorldSizeChunks.Y ?? 0}");
		GD.Print($"  Total World Chunks: {_generator.WorldData.WorldSizeChunks.X * _generator.WorldData.WorldSizeChunks.Y}");
		_debugUI = new DebugUI();
		_debugUI.CreateDebugUI(_debugLabel, HUD.Canvas);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_debugLabel != null && _chunkLoader != null)
		{
			_debugLabel.Text = $"Player World Position: {Player.Position.Round()}\n" +
								$"Player Chunk: {_chunkLoader.LastPlayerChunk}\nChunks Loaded: {_chunkLoader.Count}\n";
		}
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
