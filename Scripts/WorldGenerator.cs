using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class WorldGenerator
{
	private WorldGenerationSettings _settings;
	private Dictionary<Vector2I, FileMap> _worldDataLookup = new();
	private FastNoiseLite _noise;
	private RegionGenerator _regionGenerator;
	private MacroTerrainGenerator _macroTerrainGenerator;
	private MacroTerrain _macroTerrain;
	private int _maxLevel = 75;
	private int _tutorialRadiusMin = 13;
	private int _regionRadiusMin = 18;

	/// <summary> 
	/// World generation occurs once at for "New Games", then we load/save individual chunks as needed.
	/// </summary>
	public WorldGenerator(WorldGenerationSettings settings)
	{
		_settings = settings;
	}

	public async Task InitializeWorldMetadata()
	{
		var worldMetadata = new WorldMetadata
		{
			Seed = _settings.Seed,
			WorldSizeChunks = _settings.WorldSizeChunks,
			ChunkSize = _settings.ChunkSize,
			HeightScale = _settings.HeightScale,
			WorldDataLookupPath = _settings.WorldDataLookupPath,
			PlayerStartPosition = _settings.PlayerStartPosition
		};
		var metadataFile = FileAccess.Open(_settings.WorldMetadataPath, FileAccess.ModeFlags.Write);
		worldMetadata.Serialize(metadataFile);
		metadataFile.Close();


	}
	public class LookupData
	{
		public Vector2I ChunkCoord;
		public string FilePath;
		public int StartPosition;
	}

	public void GenerateWorld()
	{
		GD.Print("=== Starting World Generation ===");
		InitializeNoise();
		GenerateMacroTerrain();
		GenerateRegions();

		List<LookupData> lookup = new List<LookupData>();
		var regionChunks = _regionGenerator.GetAllRegions();
		foreach (var kvp in regionChunks)
		{
			string filePath = $"res://Data/world_data/regions/{kvp.Key}.dat";
			var poi = GeneratePOIs(kvp, _macroTerrain);
			var pod = GeneratePODs(kvp, _macroTerrain);
			List<ChunkData> chunks = new List<ChunkData>();
			foreach (var chunkCoord in kvp.Value)
			{
				chunks.Add(GenerateChunkData(chunkCoord, _macroTerrain, poi, pod));
				lookup.Add(new LookupData
				{
					ChunkCoord = chunkCoord,
					FilePath = filePath
				});
			}
			// TODO: Save chunk data to files
		}
		// TODO: write chunk lookup in lookup file.
	}

	private void InitializeNoise()
	{
		_noise = new FastNoiseLite();
		_noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noise.Seed = _settings.Seed;
		_noise.Frequency = 0.05f;
		_noise.FractalOctaves = 3;
	}

	#region Generate Macro Terrain
	private void GenerateMacroTerrain()
	{
		GD.Print("  Starting Macro Terrain Generation");
		_macroTerrainGenerator = new MacroTerrainGenerator(
		_settings.Seed,
		new Vector2I(_settings.WorldSizeChunks.X * _settings.ChunkSize.X,
					 _settings.WorldSizeChunks.Y * _settings.ChunkSize.Y),
		resolution: 200);
		_macroTerrain = _macroTerrainGenerator.GenerateMacroTerrain();
	}
	#endregion

	#region Generate Region
	private void GenerateRegions()
	{
		DirAccess.MakeDirRecursiveAbsolute("res://Data/world_data/regions");
		_regionGenerator = new RegionGenerator(_settings.Seed, _settings.WorldSizeChunks * _settings.ChunkSize, _settings.WorldSizeChunks, _regionRadiusMin, _tutorialRadiusMin, _settings.ChunkSize, _maxLevel);
		_regionGenerator.GenerateRegionSeeds();
	}
	#endregion

	#region Generate Chunk Data
	public ChunkData GenerateChunkData(Vector2I chunkCoord, MacroTerrain terrain, RegionType region, List<Placement> poi, List<Placement> pod)
	{
		var chunkData = new ChunkData
		{
			ChunkCoord = chunkCoord,
			HeightData = new float[_settings.ChunkSize.X * _settings.ChunkSize.Y]
		};

		int worldOffsetX = chunkCoord.X * (_settings.ChunkSize.X - 1);
		int worldOffsetZ = chunkCoord.Y * (_settings.ChunkSize.Y - 1);

		chunkData.WorldCoord = new Vector2I(worldOffsetX, worldOffsetZ);

		var vertices = new List<Vector3>();
		var normals = new List<Vector3>();
		var uvs = new List<Vector2>();
		var indices = new List<int>();


		// Generate vertices in 
		for (int z = 0; z < _settings.ChunkSize.Y; z++)
		{
			for (int x = 0; x < _settings.ChunkSize.X; x++)
			{
				int worldX = worldOffsetX + x;
				int worldZ = worldOffsetZ + z;

				float height = GetHeightAt(worldX, worldZ);
				Vector3 vertex = new Vector3(x, height, z);

				chunkData.HeightData[z * _settings.ChunkSize.X + x] = height;
				vertices.Add(vertex);

				Vector3 normal = CalculateNormal(worldX, worldZ);
				normals.Add(normal);

				Vector2 uv = new Vector2((float)x / _settings.ChunkSize.X, (float)z / _settings.ChunkSize.Y);
				uvs.Add(uv);
			}
		}

		// Generate indices
		for (int z = 0; z < _settings.ChunkSize.Y - 1; z++)
		{
			for (int x = 0; x < _settings.ChunkSize.X - 1; x++)
			{
				int topLeft = z * _settings.ChunkSize.X + x;
				int topRight = topLeft + 1;
				int bottomLeft = (z + 1) * _settings.ChunkSize.X + x;
				int bottomRight = bottomLeft + 1;

				// First triangle
				indices.Add(topLeft);
				indices.Add(topRight);
				indices.Add(bottomLeft);


				// Second triangle
				indices.Add(topRight);
				indices.Add(bottomRight);
				indices.Add(bottomLeft);
			}
		}

		var collisionFaces = new Vector3[indices.Count];
		for (int i = 0; i < indices.Count; i++)
		{
			collisionFaces[i] = vertices[indices[i]];
		}

		chunkData.Vertices = vertices.ToArray();
		chunkData.Normals = normals.ToArray();
		chunkData.UVs = uvs.ToArray();
		chunkData.Indices = indices.ToArray();
		chunkData.CollisionFaces = collisionFaces;
		chunkData.POI = poi;
		chunkData.POD = pod;

		return chunkData;
	}

	private float GetHeightAt(int worldX, int worldZ)
	{
		float macroHeight = _macroTerrain.GetHeightAtWorldPos(worldX, worldZ);
		float detailNoise = _noise.GetNoise2D(worldX, worldZ) * 5f;

		float mountainMask = _macroTerrain.GetMountainMaskAt(worldX, worldZ);
		float waterMask = _macroTerrain.GetWaterMaskAt(worldX, worldZ);

		// Enhance mountains with more detail
		if (mountainMask > 0.5f)
		{
			detailNoise *= 1f + mountainMask; // More detail on mountains
		}

		// Flatten water areas
		if (waterMask > 0.5f)
		{
			macroHeight = Mathf.Lerp(macroHeight, 0f, waterMask);
			detailNoise *= 0.5f; // Less detail underwater
		}

		return macroHeight + detailNoise;
	}

	private Vector3 CalculateNormal(int worldX, int worldZ)
	{
		float heightL = GetHeightAt(worldX - 1, worldZ);
		float heightR = GetHeightAt(worldX + 1, worldZ);
		float heightD = GetHeightAt(worldX, worldZ - 1);
		float heightU = GetHeightAt(worldX, worldZ + 1);

		Vector3 normal = new Vector3(heightL - heightR, 2.0f, heightD - heightU);
		return normal.Normalized();
	}
	#endregion

	#region Generate POIs
	public List<Placement> GeneratePOIs(KeyValuePair<RegionType, List<Vector2I>> region, MacroTerrain terrain)
	{
		var pois = new List<Placement>();

		var adaptivePOIs = POIPlacementGenerator.GenerateIdealPOIDistribution(region, count: 14);
		_regionGenerator.CountRegionSizes();

		return pois;
	}
	#endregion

	#region Generate PODs
	public List<Placement> GeneratePODs(KeyValuePair<RegionType, List<Vector2I>> region, MacroTerrain terrain)
	{
		var pods = new List<Placement>();

		var adaptivePOIs = POIPlacementGenerator.GenerateIdealPOIDistribution(region, count: 14);
		_regionGenerator.CountRegionSizes();

		return pods;
	}
	#endregion

	#region Best Fit Delaunay Position
	private Vector2 PickBestDelaunayPosition(List<Vector2> candidates, List<Placement> existingPOIs)
	{
		// Pick the candidate that maintains best Delaunay distribution
		return candidates.OrderByDescending(candidate =>
		{
			var tempPOIs = existingPOIs.Select(p => p.Position).ToList();
			tempPOIs.Add(candidate);
			var triangulation = DelaunayTriangulation(tempPOIs);
			return ScoreDistribution(tempPOIs, triangulation);
		}).First();
	}
	#endregion

	// 		// Optional slope check (prevents cliffs)
	// 		float slope = GetSlope(chunk, x, z);
	// 		if (slope > 0.8f) // tweak threshold as needed
	// 			continue;

	// 		float slope = GetSlope(chunk, x, z);
	// 		if (slope > 0.8f)
	// 			continue;

	// public void PlaceWeatherZones()
	// {
	// 	GD.Print("Placing weather zones...");

	// 	var random = new Random(Seed);

	// 	// Place 3-5 permanent weather zones
	// 	int zoneCount = random.Next(3, 6);

	// 	for (int i = 0; i < zoneCount; i++)
	// 	{
	// 		Vector2 center = new Vector2(
	// 			random.Next(0, WorldSizeChunks.X * ChunkSize.X),
	// 			random.Next(0, WorldSizeChunks.Y * ChunkSize.Y)
	// 		);

	// 		float radius = random.Next(100, 300);

	// 		WeatherType weather = (WeatherType)random.Next(0, 5);

	// 		var zone = new WeatherZone(center, radius, weather, isPermanent: true);

	// 		// Store in WorldData for saving/loading
	// 		// You'll need to add this to your ChunkData or WorldData

	// 		GD.Print($"Placed {weather} zone at {center} with radius {radius}");
	// 	}
	// }

	private float GetSlope(ChunkData chunk, int x, int z)
	{
		int w = _settings.ChunkSize.X;
		int h = _settings.ChunkSize.Y;

		int i = z * w + x;

		float center = chunk.HeightData[i];
		float maxDiff = 0f;

		if (x > 0) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[z * w + (x - 1)]));
		if (x < w - 1) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[z * w + (x + 1)]));
		if (z > 0) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[(z - 1) * w + x]));
		if (z < h - 1) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[(z + 1) * w + x]));

		return maxDiff;
	}

	// public void SaveWorldData()
	// {
	// 	GD.Print($"Saving world data to {WorldDataPath}...");

	// 	using var file = FileAccess.Open(WorldDataPath, FileAccess.ModeFlags.Write);
	// 	if (file == null)
	// 	{
	// 		var error = FileAccess.GetOpenError();
	// 		GD.PrintErr($"Failed to open file for writing with {error}");
	// 		return;
	// 	}

	// 	_worldData.Serialize(file);
	// 	GD.Print($"Finished serializing with file size {file.GetPosition() / (1024 * 1024)} MB");
	// 	file.Close();
	// }

	// public void LoadWorldData()
	// {
	// 	GD.Print($"Loading world data from {WorldDataPath}...");

	// 	using var file = FileAccess.Open(WorldDataPath, FileAccess.ModeFlags.Read);
	// 	if (file == null)
	// 	{
	// 		var error = FileAccess.GetOpenError();
	// 		GD.PrintErr($"Failed to open file for reading with {error}");
	// 		return;
	// 	}

	// 	_worldData = WorldData.Deserialize(file);
	// 	GD.Print($"Loaded {_worldData.Chunks.Count} chunks successfully with file size {file.GetPosition() / (1024 * 1024)} MB");
	// 	file.Close();
	// }
}
