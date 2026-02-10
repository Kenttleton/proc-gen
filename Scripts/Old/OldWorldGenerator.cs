using Godot;
using System;
using System.Collections.Generic;

public partial class OldWorldGenerator : Node3D
{
	private int Seed;
	private int WorldSizeChunks;
	private int ChunkSize;
	private float HeightScale;
	private float NoiseScale;
	private bool Generate;
	private string WorldDataPath;

	public OldWorldGenerator(int seed, int worldSizeChunks, int chunkSize, float heightScale, float noiseScale, bool generate, string worldDataPath)
	{
		Seed = seed;
		WorldSizeChunks = worldSizeChunks;
		ChunkSize = chunkSize;
		HeightScale = heightScale;
		NoiseScale = noiseScale;
		Generate = generate;
		WorldDataPath = worldDataPath;
	}

	private FastNoiseLite _noise;
	private WorldData _worldData;

	public class ChunkData
	{
		public Vector2I ChunkCoord;
		public float[] HeightData;
		public Vector3[] Vertices;
		public int[] Indices;
		public Vector3[] Normals;
		public Vector2[] UVs;

		// Biome/feature data
		public List<Vector3> DungeonEntrances = new();
		public List<Vector3> BossSpawns = new();
	}

	public class WorldData
	{
		public int Seed;
		public int ChunkSize;
		public int WorldSizeChunks;
		public float HeightScale;
		public Dictionary<Vector2I, ChunkData> Chunks = new();
	}

	public override void _Ready()
	{
		if (Generate)
		{
			if (FileAccess.FileExists(WorldDataPath))
			{
				GD.Print("World data already exists. Delete it to regenerate.");
				LoadWorldData();
			}
			else
			{
				GenerateWorld();
			}
		}
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
			HeightScale = HeightScale
		};

		int totalChunks = WorldSizeChunks * WorldSizeChunks;
		int processedChunks = 0;

		// Generate all chunks
		for (int chunkZ = 0; chunkZ < WorldSizeChunks; chunkZ++)
		{
			for (int chunkX = 0; chunkX < WorldSizeChunks; chunkX++)
			{
				Vector2I chunkCoord = new Vector2I(chunkX, chunkZ);
				ChunkData chunkData = GenerateChunkData(chunkCoord);
				_worldData.Chunks[chunkCoord] = chunkData;

				processedChunks++;

				if (processedChunks % 50 == 0)
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
		GD.Print($"World size: {WorldSizeChunks * (ChunkSize - 1)} x {WorldSizeChunks * (ChunkSize - 1)} units");
	}

	private ChunkData GenerateChunkData(Vector2I chunkCoord)
	{
		var chunkData = new ChunkData
		{
			ChunkCoord = chunkCoord,
			HeightData = new float[ChunkSize * ChunkSize]
		};

		int worldOffsetX = chunkCoord.X * (ChunkSize - 1);
		int worldOffsetZ = chunkCoord.Y * (ChunkSize - 1);

		var vertices = new List<Vector3>();
		var normals = new List<Vector3>();
		var uvs = new List<Vector2>();
		var indices = new List<int>();

		// Generate vertices in 
		for (int z = 0; z < ChunkSize; z++)
		{
			for (int x = 0; x < ChunkSize; x++)
			{
				int worldX = worldOffsetX + x;
				int worldZ = worldOffsetZ + z;

				float height = GetHeightAt(worldX, worldZ);
				Vector3 vertex = new Vector3(x, height, z);

				chunkData.HeightData[z * ChunkSize + x] = height;
				vertices.Add(vertex);

				Vector3 normal = CalculateNormal(worldX, worldZ);
				normals.Add(normal);

				Vector2 uv = new Vector2((float)x / (ChunkSize - 1), (float)z / (ChunkSize - 1));
				uvs.Add(uv);
			}
		}

		// Generate indices
		for (int z = 0; z < ChunkSize - 1; z++)
		{
			for (int x = 0; x < ChunkSize - 1; x++)
			{
				int topLeft = z * ChunkSize + x;
				int topRight = topLeft + 1;
				int bottomLeft = (z + 1) * ChunkSize + x;
				int bottomRight = bottomLeft + 1;

				// First triangle
				indices.Add(topLeft);
				indices.Add(bottomLeft);
				indices.Add(topRight);

				// Second triangle
				indices.Add(topRight);
				indices.Add(bottomLeft);
				indices.Add(bottomRight);
			}
		}

		chunkData.Vertices = vertices.ToArray();
		chunkData.Normals = normals.ToArray();
		chunkData.UVs = uvs.ToArray();
		chunkData.Indices = indices.ToArray();

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

		var random = new Random(Seed);

		// Place dungeons (5-10 per world)
		int dungeonCount = random.Next(5, 11);
		for (int i = 0; i < dungeonCount; i++)
		{
			Vector2I chunkCoord = new Vector2I(
				random.Next(0, WorldSizeChunks),
				random.Next(0, WorldSizeChunks)
			);

			if (_worldData.Chunks.TryGetValue(chunkCoord, out var chunk))
			{
				// Find suitable location in chunk
				int x = random.Next(5, ChunkSize - 5);
				int z = random.Next(5, ChunkSize - 5);
				float height = chunk.HeightData[z * ChunkSize + x];

				// Only place on suitable terrain
				if (height > 5.0f && height < 20.0f)
				{
					Vector3 worldPos = new Vector3(
						chunkCoord.X * (ChunkSize - 1) + x,
						height,
						chunkCoord.Y * (ChunkSize - 1) + z
					);
					chunk.DungeonEntrances.Add(worldPos);
				}
			}
		}

		// Place boss spawns (3-5 per world)
		int bossCount = random.Next(3, 6);
		for (int i = 0; i < bossCount; i++)
		{
			Vector2I chunkCoord = new Vector2I(
				random.Next(0, WorldSizeChunks),
				random.Next(0, WorldSizeChunks)
			);

			if (_worldData.Chunks.TryGetValue(chunkCoord, out var chunk))
			{
				int x = random.Next(5, ChunkSize - 5);
				int z = random.Next(5, ChunkSize - 5);
				float height = chunk.HeightData[z * ChunkSize + x];

				// Bosses spawn on high ground
				if (height > 20.0f)
				{
					Vector3 worldPos = new Vector3(
						chunkCoord.X * (ChunkSize - 1) + x,
						height,
						chunkCoord.Y * (ChunkSize - 1) + z
					);
					chunk.BossSpawns.Add(worldPos);
				}
			}
		}

		GD.Print($"Placed {dungeonCount} dungeons and {bossCount} boss spawns");
	}

	private void SaveWorldData()
	{
		GD.Print($"Saving world data to {WorldDataPath}...");

		using var file = FileAccess.Open(WorldDataPath, FileAccess.ModeFlags.Write);

		// Write header
		file.Store32((uint)_worldData.Seed);
		file.Store32((uint)_worldData.ChunkSize);
		file.Store32((uint)_worldData.WorldSizeChunks);
		file.StoreFloat(_worldData.HeightScale);
		file.Store32((uint)_worldData.Chunks.Count);

		// Write each chunk
		foreach (var kvp in _worldData.Chunks)
		{
			var coord = kvp.Key;
			var chunk = kvp.Value;

			// Chunk coordinate
			file.Store32((uint)coord.X);
			file.Store32((uint)coord.Y);

			// Height data
			file.Store32((uint)chunk.HeightData.Length);
			foreach (float height in chunk.HeightData)
			{
				file.StoreFloat(height);
			}

			// Vertices
			file.Store32((uint)chunk.Vertices.Length);
			foreach (var vertex in chunk.Vertices)
			{
				file.StoreFloat(vertex.X);
				file.StoreFloat(vertex.Y);
				file.StoreFloat(vertex.Z);
			}

			// Normals
			file.Store32((uint)chunk.Normals.Length);
			foreach (var normal in chunk.Normals)
			{
				file.StoreFloat(normal.X);
				file.StoreFloat(normal.Y);
				file.StoreFloat(normal.Z);
			}

			// UVs
			file.Store32((uint)chunk.UVs.Length);
			foreach (var uv in chunk.UVs)
			{
				file.StoreFloat(uv.X);
				file.StoreFloat(uv.Y);
			}

			// Indices
			file.Store32((uint)chunk.Indices.Length);
			foreach (int index in chunk.Indices)
			{
				file.Store32((uint)index);
			}

			// Dungeon entrances
			file.Store32((uint)chunk.DungeonEntrances.Count);
			foreach (var pos in chunk.DungeonEntrances)
			{
				file.StoreFloat(pos.X);
				file.StoreFloat(pos.Y);
				file.StoreFloat(pos.Z);
			}

			// Boss spawns
			file.Store32((uint)chunk.BossSpawns.Count);
			foreach (var pos in chunk.BossSpawns)
			{
				file.StoreFloat(pos.X);
				file.StoreFloat(pos.Y);
				file.StoreFloat(pos.Z);
			}
		}

		GD.Print("World data saved successfully!");
	}

	public void LoadWorldData()
	{
		GD.Print($"Loading world data from {WorldDataPath}...");

		using var file = FileAccess.Open(WorldDataPath, FileAccess.ModeFlags.Read);

		_worldData = new WorldData();

		// Read header
		_worldData.Seed = (int)file.Get32();
		_worldData.ChunkSize = (int)file.Get32();
		_worldData.WorldSizeChunks = (int)file.Get32();
		_worldData.HeightScale = file.GetFloat();
		int chunkCount = (int)file.Get32();

		GD.Print($"Loading {chunkCount} chunks...");

		// Read each chunk
		for (int i = 0; i < chunkCount; i++)
		{
			var chunkData = new ChunkData();

			// Chunk coordinate
			int x = (int)file.Get32();
			int y = (int)file.Get32();
			chunkData.ChunkCoord = new Vector2I(x, y);

			// Height data
			int heightDataLength = (int)file.Get32();
			chunkData.HeightData = new float[heightDataLength];
			for (int j = 0; j < heightDataLength; j++)
			{
				chunkData.HeightData[j] = file.GetFloat();
			}

			// Vertices
			int vertexCount = (int)file.Get32();
			chunkData.Vertices = new Vector3[vertexCount];
			for (int j = 0; j < vertexCount; j++)
			{
				chunkData.Vertices[j] = new Vector3(
					file.GetFloat(),
					file.GetFloat(),
					file.GetFloat()
				);
			}

			// Normals
			int normalCount = (int)file.Get32();
			chunkData.Normals = new Vector3[normalCount];
			for (int j = 0; j < normalCount; j++)
			{
				chunkData.Normals[j] = new Vector3(
					file.GetFloat(),
					file.GetFloat(),
					file.GetFloat()
				);
			}

			// UVs
			int uvCount = (int)file.Get32();
			chunkData.UVs = new Vector2[uvCount];
			for (int j = 0; j < uvCount; j++)
			{
				chunkData.UVs[j] = new Vector2(
					file.GetFloat(),
					file.GetFloat()
				);
			}

			// Indices
			int indexCount = (int)file.Get32();
			chunkData.Indices = new int[indexCount];
			for (int j = 0; j < indexCount; j++)
			{
				chunkData.Indices[j] = (int)file.Get32();
			}

			// Dungeon entrances
			int dungeonCount = (int)file.Get32();
			for (int j = 0; j < dungeonCount; j++)
			{
				chunkData.DungeonEntrances.Add(new Vector3(
					file.GetFloat(),
					file.GetFloat(),
					file.GetFloat()
				));
			}

			// Boss spawns
			int bossCount = (int)file.Get32();
			for (int j = 0; j < bossCount; j++)
			{
				chunkData.BossSpawns.Add(new Vector3(
					file.GetFloat(),
					file.GetFloat(),
					file.GetFloat()
				));
			}

			_worldData.Chunks[chunkData.ChunkCoord] = chunkData;
		}

		GD.Print($"Loaded {_worldData.Chunks.Count} chunks successfully!");
	}

	public WorldData GetWorldData()
	{
		return _worldData;
	}
}