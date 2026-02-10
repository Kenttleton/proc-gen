using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class WorldGenerator
{
	private WorldData _worldData;
	public WorldData WorldData
	{
		get
		{
			if (_worldData == null)
			{
				GD.PrintErr("World data not generated or loaded yet!");
			}
			return _worldData;
		}
		set
		{
			_worldData = value;
		}
	}
	// World settings
	public int Seed;
	public bool Generate;
	private FastNoiseLite _noise;
	public string WorldDataPath;
	// Y scaling
	public float HeightScale;
	public float NoiseScale;
	// X, Z scaling
	public Vector2I WorldSizeChunks;
	public Vector2I ChunkSize;

	public WorldGenerator(int seed, Vector2I worldSizeChunks, Vector2I chunkSize, float heightScale, float noiseScale, bool generate, string worldDataPath)
	{
		Seed = seed;
		WorldSizeChunks = worldSizeChunks;
		ChunkSize = chunkSize;
		HeightScale = heightScale;
		NoiseScale = noiseScale;
		Generate = generate;
		WorldDataPath = worldDataPath;
	}

	private void InitializeNoise()
	{
		_noise = new FastNoiseLite();
		_noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noise.Seed = Seed;
		_noise.Frequency = NoiseScale;
		_noise.FractalOctaves = 5;
		_noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
	}

	public void GenerateWorld()
	{
		GD.Print("=== Starting World Generation ===");
		var startTime = Time.GetTicksMsec();

		InitializeNoise();

		_worldData = new WorldData
		{
			Seed = Seed,
			ChunkSize = ChunkSize,
			WorldSizeChunks = WorldSizeChunks,
			HeightScale = HeightScale,
			Chunks = new Dictionary<Vector2I, ChunkData>()
		};

		int totalChunks = WorldSizeChunks.X * WorldSizeChunks.Y;
		int processedChunks = 0;

		// Generate all chunks
		for (int chunkZ = 0; chunkZ < WorldSizeChunks.Y; chunkZ++)
		{
			for (int chunkX = 0; chunkX < WorldSizeChunks.X; chunkX++)
			{
				Vector2I chunkCoord = new Vector2I(chunkX, chunkZ);
				ChunkData chunkData = GenerateChunkData(chunkCoord);
				_worldData.Chunks[chunkCoord] = chunkData;

				processedChunks++;

				if (processedChunks % 7 == 0 || processedChunks % 50 == 0 || processedChunks == totalChunks)
				{
					GD.Print($"Generated {processedChunks}/{totalChunks} chunks ({(float)processedChunks / totalChunks * 100:F1}%)");
				}
			}
		}

		// Place special features (dungeons, bosses, etc.)
		PlaceWorldFeatures();

		// Save to disk
		SaveWorldData();

		var endTime = Time.GetTicksMsec();
		GD.Print($"=== World Generation Complete ===");
		GD.Print($"Generated {totalChunks} chunks in {(endTime - startTime) / 1000.0f:F2} seconds");
		GD.Print($"World size: {WorldSizeChunks.X * ChunkSize.X} x {WorldSizeChunks.Y * ChunkSize.Y} units");
	}

	private ChunkData GenerateChunkData(Vector2I chunkCoord)
	{
		var chunkData = new ChunkData
		{
			ChunkCoord = chunkCoord,
			HeightData = new float[ChunkSize.X * ChunkSize.Y]
		};

		int worldOffsetX = chunkCoord.X * (ChunkSize.X - 1);
		int worldOffsetZ = chunkCoord.Y * (ChunkSize.Y - 1);

		var vertices = new List<Vector3>();
		var normals = new List<Vector3>();
		var uvs = new List<Vector2>();
		var indices = new List<int>();


		// Generate vertices in 
		for (int z = 0; z < ChunkSize.Y; z++)
		{
			for (int x = 0; x < ChunkSize.X; x++)
			{
				int worldX = worldOffsetX + x;
				int worldZ = worldOffsetZ + z;

				float height = GetHeightAt(worldX, worldZ);
				Vector3 vertex = new Vector3(x, height, z);

				chunkData.HeightData[z * ChunkSize.X + x] = height;
				vertices.Add(vertex);

				Vector3 normal = CalculateNormal(worldX, worldZ);
				normals.Add(normal);

				Vector2 uv = new Vector2((float)x / ChunkSize.X, (float)z / ChunkSize.Y);
				uvs.Add(uv);
			}
		}

		// Generate indices
		for (int z = 0; z < ChunkSize.Y - 1; z++)
		{
			for (int x = 0; x < ChunkSize.X - 1; x++)
			{
				int topLeft = z * ChunkSize.X + x;
				int topRight = topLeft + 1;
				int bottomLeft = (z + 1) * ChunkSize.X + x;
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

		return chunkData;
	}

	private float GetHeightAt(int worldX, int worldZ)
	{
		return _noise.GetNoise2D(worldX, worldZ) * HeightScale;
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

	private void PlaceWorldFeatures()
	{
		GD.Print("Placing world features...");

		if (_worldData.Chunks.Count == 0)
		{
			GD.PrintErr("No chunks in world data!");
			return;
		}

		var random = new Random(Seed);

		// ---------------- DUNGEONS ----------------
		int dungeonTarget = random.Next(5, 11);
		int dungeonPlaced = 0;
		int dungeonAttempts = 0;

		while (dungeonPlaced < dungeonTarget && dungeonAttempts < dungeonTarget * 20)
		{
			dungeonAttempts++;

			Vector2I chunkCoord = new Vector2I(
				random.Next(0, WorldSizeChunks.X),
				random.Next(0, WorldSizeChunks.Y)
			);

			if (!_worldData.Chunks.TryGetValue(chunkCoord, out var chunk))
				continue;

			int x = random.Next(2, ChunkSize.X - 2);
			int z = random.Next(2, ChunkSize.Y - 2);
			int index = z * ChunkSize.X + x;
			float height = chunk.HeightData[index];

			float minH = chunk.HeightData.Min();
			float maxH = chunk.HeightData.Max();

			float midLow = Mathf.Lerp(minH, maxH, 0.3f);
			float midHigh = Mathf.Lerp(minH, maxH, 0.6f);

			if (height < midLow || height > midHigh)
				continue;

			// Optional slope check (prevents cliffs)
			float slope = GetSlope(chunk, x, z);
			if (slope > 0.8f) // tweak threshold as needed
				continue;

			Vector3 worldPos = new Vector3(
				chunkCoord.X * ChunkSize.X + x,
				height,
				chunkCoord.Y * ChunkSize.Y + z
			);

			chunk.DungeonEntrances.Add(new DungeonEntrance(worldPos, Vector3.Forward, BiomeType.Forest));
			dungeonPlaced++;

			GD.Print($"Placed dungeon at {worldPos} in chunk {chunkCoord}");
		}

		// ---------------- BOSSES ----------------
		int bossTarget = random.Next(3, 6);
		int bossPlaced = 0;
		int bossAttempts = 0;

		while (bossPlaced < bossTarget && bossAttempts < bossTarget * 20)
		{
			bossAttempts++;

			Vector2I chunkCoord = new Vector2I(
				random.Next(0, WorldSizeChunks.X),
				random.Next(0, WorldSizeChunks.Y)
			);

			if (!_worldData.Chunks.TryGetValue(chunkCoord, out var chunk))
				continue;

			int x = random.Next(2, ChunkSize.X - 2);
			int z = random.Next(2, ChunkSize.Y - 2);
			int index = z * ChunkSize.X + x;
			float height = chunk.HeightData[index];

			float minH = chunk.HeightData.Min();
			float maxH = chunk.HeightData.Max();
			float highGround = Mathf.Lerp(minH, maxH, 0.75f);

			if (height < highGround)
				continue;

			float slope = GetSlope(chunk, x, z);
			if (slope > 0.8f)
				continue;

			Vector3 worldPos = new Vector3(
				chunkCoord.X * ChunkSize.X + x,
				height,
				chunkCoord.Y * ChunkSize.Y + z
			);

			chunk.BossSpawns.Add(new BossSpawn(worldPos, BossType.Giant, BiomeType.Forest));
			bossPlaced++;

			GD.Print($"Placed boss spawn at {worldPos} in chunk {chunkCoord}");
		}

		GD.Print($"Placed {dungeonPlaced}/{dungeonTarget} dungeons and {bossPlaced}/{bossTarget} boss spawns");
	}

	private void PlaceWeatherZones()
	{
		GD.Print("Placing weather zones...");

		var random = new Random(Seed);

		// Place 3-5 permanent weather zones
		int zoneCount = random.Next(3, 6);

		for (int i = 0; i < zoneCount; i++)
		{
			Vector2 center = new Vector2(
				random.Next(0, WorldSizeChunks.X * ChunkSize.X),
				random.Next(0, WorldSizeChunks.Y * ChunkSize.Y)
			);

			float radius = random.Next(100, 300);

			WeatherType weather = (WeatherType)random.Next(0, 5);

			var zone = new WeatherZone(center, radius, weather, isPermanent: true);

			// Store in WorldData for saving/loading
			// You'll need to add this to your ChunkData or WorldData

			GD.Print($"Placed {weather} zone at {center} with radius {radius}");
		}
	}

	private float GetSlope(ChunkData chunk, int x, int z)
	{
		int w = ChunkSize.X;
		int h = ChunkSize.Y;

		int i = z * w + x;

		float center = chunk.HeightData[i];
		float maxDiff = 0f;

		if (x > 0) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[z * w + (x - 1)]));
		if (x < w - 1) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[z * w + (x + 1)]));
		if (z > 0) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[(z - 1) * w + x]));
		if (z < h - 1) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[(z + 1) * w + x]));

		return maxDiff;
	}

	private void SaveWorldData()
	{
		GD.Print($"Saving world data to {WorldDataPath}...");

		using var file = FileAccess.Open(WorldDataPath, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			var error = FileAccess.GetOpenError();
			GD.PrintErr($"Failed to open file for writing with {error}");
			return;
		}

		_worldData.Serialize(file);
		GD.Print($"Finished serializing with file size {file.GetPosition() / (1024 * 1024)} MB");
		file.Close();
	}

	public void LoadWorldData()
	{
		GD.Print($"Loading world data from {WorldDataPath}...");

		using var file = FileAccess.Open(WorldDataPath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			var error = FileAccess.GetOpenError();
			GD.PrintErr($"Failed to open file for reading with {error}");
			return;
		}

		_worldData = WorldData.Deserialize(file);
		GD.Print($"Loaded {_worldData.Chunks.Count} chunks successfully with file size {file.GetPosition() / (1024 * 1024)} MB");
		file.Close();
	}
}
