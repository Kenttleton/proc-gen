using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// LEARNING: WorldLoader wraps WorldGenerator and provides async generation
/// with progress callbacks. This allows the UI to stay responsive while
/// the world generates in the background.
/// 
/// Key pattern: We use signals to communicate progress from worker thread
/// back to the main thread (Godot requires UI updates on main thread).
/// </summary>
public partial class WorldLoader : Node
{
	[Signal]
	public delegate void GenerationProgressEventHandler(float progress, string status);

	[Signal]
	public delegate void GenerationCompleteEventHandler(bool success);

	private WorldGenerator _generator;
	private bool _isGenerating = false;

	/// <summary>
	/// Generate world asynchronously with progress reporting
	/// </summary>
	public async Task GenerateWorldAsync(WorldGenerationSettings settings)
	{
		if (_isGenerating)
		{
			GD.PrintErr("Generation already in progress!");
			return;
		}

		_isGenerating = true;

		try
		{
			// Create generator with settings
			_generator = new WorldGenerator(
				settings.Seed,
				settings.WorldSizeChunks,
				settings.ChunkSize,
				settings.HeightScale,
				settings.NoiseScale,
				worldDataPath: "user://world_data/index.dat"
			);

			// Run generation on worker thread
			await Task.Run(() => GenerateWorldWithProgress(settings));

			EmitSignal(SignalName.GenerationComplete, true);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"World generation failed: {ex.Message}");
			EmitSignal(SignalName.GenerationComplete, false);
		}
		finally
		{
			_isGenerating = false;
		}
	}

	/// <summary>
	/// LEARNING: This runs on a worker thread. We can't directly update UI here,
	/// so we use CallDeferred to emit signals that the main thread will receive.
	/// </summary>
	private void GenerateWorldWithProgress(WorldGenerationSettings settings)
	{
		int totalChunks = settings.WorldSizeChunks.X * settings.WorldSizeChunks.Y;

		// Initialize
		ReportProgress(0, "Initializing world generator...");
		_generator.InitializeNoise();

		ReportProgress(5, "Creating world data structure...");
		_generator.InitializeWorldData();

		// Generate chunks
		ReportProgress(10, "Generating terrain...");

		int processedChunks = 0;
		for (int z = 0; z < settings.WorldSizeChunks.Y; z++)
		{
			for (int x = 0; x < settings.WorldSizeChunks.X; x++)
			{
				Vector2I chunkCoord = new Vector2I(x, z);
				_generator.GenerateChunkData(chunkCoord);

				processedChunks++;

				// Report progress every 50 chunks or at completion
				if (processedChunks % 50 == 0 || processedChunks == totalChunks)
				{
					float progress = 10 + (processedChunks / (float)totalChunks * 60); // 10-70%
					string status = $"Generating terrain... {processedChunks}/{totalChunks} chunks";
					ReportProgress(progress, status);
				}
			}
		}

		// Place features
		ReportProgress(70, "Placing dungeons...");
		_generator.PlaceDungeons();

		ReportProgress(75, "Spawning bosses...");
		_generator.PlaceBossSpawns();

		ReportProgress(80, "Creating weather zones...");
		_generator.PlaceWeatherZones();

		// Save to disk (this is the slow part)
		ReportProgress(85, "Saving world data (this may take a moment)...");
		_generator.SaveWorldData();

		ReportProgress(100, "World generation complete!");
	}

	/// <summary>
	/// LEARNING: CallDeferred ensures the signal is emitted on the main thread
	/// This is CRITICAL - Godot UI must be updated from main thread only
	/// </summary>
	private void ReportProgress(float progress, string status)
	{
		// CallDeferred queues this to run on main thread
		CallDeferred(MethodName.EmitProgressSignal, progress, status);
	}

	// This method is called on main thread via CallDeferred
	private void EmitProgressSignal(float progress, string status)
	{
		EmitSignal(SignalName.GenerationProgress, progress, status);
	}
}

/// <summary>
/// Settings for world generation - makes it easy to pass around
/// </summary>
public class WorldGenerationSettings
{
	public int Seed;
	public Vector2I WorldSizeChunks;
	public Vector2I ChunkSize;
	public float HeightScale;
	public float NoiseScale;
}