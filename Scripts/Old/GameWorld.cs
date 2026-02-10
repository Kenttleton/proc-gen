using Godot;

public partial class GameWorld : Node3D
{
	[ExportGroup("World Settings")]
	[Export] public int Seed = 12345;
	[Export] public int WorldSizeChunks = 20; // 20x20 chunks = 400 chunks total
	[Export] public int ChunkSize = 32; // Vertices per chunk
	[Export] public float HeightScale = 30.0f;
	[Export] public float NoiseScale = 0.02f;


	[ExportGroup("Generation")]
	[Export] public string WorldDataPath = "res://World/Data.bin";
	[Export] public bool RegenerateWorld = false;
	[Export] public Node3D Player;
	[Export] public int PlayerStartPositionX = 20 ^ 2 / 2;
	[Export] public int PlayerStartPositionZ = 20 ^ 2 / 2;
	[Export] public int PlayerStartPositionY = 50;


	[ExportGroup("Render Settings")]
	[Export] public int RenderDistanceChunks = 4;
	[Export] public float UpdateInterval = 0.5f;

	private OldWorldGenerator _worldGenerator;
	private OldRuntimeChunkLoader _chunkLoader;

	public override void _Ready()
	{
		// Create world generator
		_worldGenerator = new OldWorldGenerator(Seed, WorldSizeChunks, ChunkSize, HeightScale, NoiseScale, RegenerateWorld, WorldDataPath);
		AddChild(_worldGenerator);

		// Check if we need to regenerate
		if (RegenerateWorld || !FileAccess.FileExists(WorldDataPath))
		{
			GD.Print("Generating new world...");
			_worldGenerator.GenerateWorld();
		}
		else
		{
			GD.Print("Loading existing world...");
			_worldGenerator.LoadWorldData();
		}

		Player.Position = new Vector3(PlayerStartPositionX, PlayerStartPositionY, PlayerStartPositionZ); // Start in middle of world

		// Create runtime chunk loader
		_chunkLoader = new OldRuntimeChunkLoader(Player, _worldGenerator, RenderDistanceChunks, UpdateInterval);
		AddChild(_chunkLoader);

		// Add lighting
		var sun = new DirectionalLight3D();
		sun.Rotation = new Vector3(-0.5f, 0.3f, 0);
		sun.ShadowEnabled = true;
		AddChild(sun);

		// Add debug UI
		CreateDebugUI();
	}

	private Label _debugLabel;

	private void CreateDebugUI()
	{
		_debugLabel = new Label();
		_debugLabel.Position = new Vector2(10, 10);
		_debugLabel.AddThemeColorOverride("font_color", Colors.White);
		_debugLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_debugLabel.AddThemeConstantOverride("outline_size", 2);

		var canvasLayer = new CanvasLayer();
		canvasLayer.AddChild(_debugLabel);
		AddChild(canvasLayer);
	}

	public override void _Process(double delta)
	{
		if (_debugLabel != null && _chunkLoader != null)
		{
			var worldData = _worldGenerator.GetWorldData();
			_debugLabel.Text = $"Player Position: {Player.Position}\n" +
							  $"Loaded Chunks: {_chunkLoader.GetLoadedChunkCount()}\n" +
							  $"Total World Chunks: {worldData?.Chunks.Count ?? 0}\n" +
							  $"World Size: {worldData?.WorldSizeChunks ?? 0}x{worldData?.WorldSizeChunks ?? 0}";
		}
	}
}