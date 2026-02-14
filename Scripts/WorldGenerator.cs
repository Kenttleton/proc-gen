using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class WorldGenerator
{
	private WorldGenerationSettings _settings;
	private Dictionary<Vector2I, FileMap> _worldDataLookup = new();
	private RegionGenerator _regionGenerator;

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
		DirAccess.MakeDirRecursiveAbsolute("res://Data/world_data/regions");
		var maxLevel = 75;
		var tutorialRadiusMin = Mathf.RoundToInt(Mathf.Sqrt(500 / Mathf.Pi));
		var regionRadiusMin = Mathf.RoundToInt(Mathf.Sqrt(1000 / Mathf.Pi));
		_regionGenerator = new RegionGenerator(_settings.Seed, _settings.WorldSizeChunks * _settings.ChunkSize, _settings.WorldSizeChunks, regionRadiusMin, tutorialRadiusMin, _settings.ChunkSize, maxLevel);
	}

	public async Task GenerateWorld()
	{
		GD.Print("=== Starting World Generation ===");
		var startTime = Time.GetTicksMsec();
		await InitializeWorldMetadata();


		int processedRegions = 0;
		for (int i = 0; i < regionMap.Count; i++)
		{
			var regionEntry = regionMap.ElementAt(i);
			Vector2I regionCoord = regionEntry.Key;
			Vector2I[] chunkCoords = regionEntry.Value;

			GD.Print($"Generating Region {regionCoord} with {chunkCoords.Length} chunks...");
			regionGenerator.RegionCoord = regionCoord;
			regionGenerator.RegionDataPath = $"res://Data/world_data/regions/region_{regionCoord.X}_{regionCoord.Y}.dat";
			regionGenerator.InitializeRegionData(i);
			regionGenerator.PlaceDungeons();
			regionGenerator.PlaceBossSpawn();
			regionGenerator.PlaceWeatherZones();

			foreach (var chunkCoord in chunkCoords)
			{
				regionGenerator.GenerateChunkData(chunkCoord);
			}

			processedRegions++;
			GD.Print($"Completed {processedRegions}/{regionMap.Count} regions.");
		}

		var elapsed = (Time.GetTicksMsec() - startTime) / 1000.0f;
		GD.Print($"=== World Generation Complete ===");
		GD.Print($"Generated {totalChunks} chunks in {elapsed:F2} seconds");
		GD.Print($"World size: {WorldSizeChunks.X * ChunkSize.X} x {WorldSizeChunks.Y * ChunkSize.Y} units");
	}

	public ChunkData GenerateChunkData(Vector2I chunkCoord)
	{
		var chunkData = new ChunkData
		{
			ChunkCoord = chunkCoord,
			HeightData = new float[_settings.ChunkSize.X * _settings.ChunkSize.Y]
		};

		int worldOffsetX = chunkCoord.X * (_settings.ChunkSize.X - 1);
		int worldOffsetZ = chunkCoord.Y * (_settings.ChunkSize.Y - 1);

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

		return chunkData;
	}

	private float GetHeightAt(int worldX, int worldZ)
	{
		return _settings.Noise.GetNoise2D(worldX, worldZ) * _settings.HeightScale;
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

	// In RegionGenerator or separate POIGenerator class
	public class POIPlacement
	{
		public Vector2I ChunkCoord;
		public Vector2 IdealPosition; // From Delaunay
		public POIType Type;
		public TerrainPreference Preference;
	}

	public List<POIPlacement> GeneratePOILayout(RegionType region)
	{
		// Get all chunks in this region
		var regionChunks = GetChunksForRegion(region);

		// Use Delaunay to determine POI density and distribution
		var poiPositions = DelaunayPOIGenerator.Generate(
			regionChunks,
			minDistance: 3, // chunks
			maxDistance: 10 // chunks
		);

		// Score the distribution
		var score = ScorePOIDistribution(poiPositions);

		// Convert to POI placements with requirements
		return poiPositions.Select(pos => new POIPlacement
		{
			ChunkCoord = WorldToChunk(pos),
			IdealPosition = pos,
			Type = DeterminePOIType(region, pos),
			Requirements = GetRequirements(POIType)
		}).ToList();
	}

	public class POIType
	{
		public string Name { get; set; }
		public TerrainPreference Preference { get; set; }
	}

	public enum TerrainPreference
	{
		RequiresFlat,      // Boss arenas, towns
		RequiresMountain,  // Dragon lairs, cliff monasteries
		RequiresWater,     // Underwater dungeons, docks
		RequiresValley,    // Hidden villages, ambush points
		FlexibleAny        // Camps, resource nodes
	}
	#region Terrain First with Smart match
	public void GenerateWorld()
	{
		foreach (var region in _regions)
		{
			// 1. Generate terrain features
			var terrain = GenerateMacroTerrain(region);

			// 2. Identify terrain-specific locations
			var mountainPeaks = terrain.FindMountainPeaks();
			var lakeBottoms = terrain.FindLakeBeds();
			var riverBanks = terrain.FindRiverBanks();
			var flatPlains = terrain.FindFlatAreas();
			var valleyFloors = terrain.FindValleys();

			// 3. Match POI types to terrain
			var pois = new List<POIPlacement>();

			// Mountain POIs - use Delaunay on mountain peaks only
			var mountainPOIs = PlacePOIsWithDelaunay(
				mountainPeaks,
				GetPOITypesForTerrain(TerrainPreference.RequiresMountain),
				count: CalculatePOICount(region, mountainPeaks.Count)
			);
			pois.AddRange(mountainPOIs);

			// Water POIs - use Delaunay on underwater/coastal positions
			var waterPOIs = PlacePOIsWithDelaunay(
				lakeBottoms.Concat(riverBanks).ToList(),
				GetPOITypesForTerrain(TerrainPreference.RequiresWater),
				count: CalculatePOICount(region, lakeBottoms.Count)
			);
			pois.AddRange(waterPOIs);

			// Plains POIs - use Delaunay on flat areas
			var plainsPOIs = PlacePOIsWithDelaunay(
				flatPlains,
				GetPOITypesForTerrain(TerrainPreference.RequiresFlat),
				count: CalculatePOICount(region, flatPlains.Count)
			);
			pois.AddRange(plainsPOIs);
		}
	}

	private List<POIPlacement> PlacePOIsWithDelaunay(
		List<Vector2> candidatePositions,
		List<POIType> poiTypes,
		int count)
	{
		if (candidatePositions.Count < count)
		{
			GD.Print($"Warning: Not enough suitable terrain. Requested {count}, found {candidatePositions.Count}");
			count = candidatePositions.Count;
		}

		// Generate multiple distributions and score them
		float bestScore = float.MinValue;
		List<Vector2> bestPositions = null;

		for (int attempt = 0; attempt < 10; attempt++)
		{
			// Randomly select 'count' positions from candidates
			var selectedPositions = candidatePositions
				.OrderBy(x => _random.Next())
				.Take(count)
				.ToList();

			// Score using Delaunay
			var triangulation = DelaunayTriangulation(selectedPositions);
			var score = ScoreDistribution(selectedPositions, triangulation);

			if (score > bestScore)
			{
				bestScore = score;
				bestPositions = selectedPositions;
			}
		}

		// Assign POI types to positions
		return bestPositions.Select((pos, i) => new POIPlacement
		{
			Position = pos,
			Type = poiTypes[i % poiTypes.Count],
			ChunkCoord = WorldToChunk(pos)
		}).ToList();
	}

	#endregion

	#region Dulaunay-First with Terrain Adaptation
	public void GenerateWorld()
	{
		foreach (var region in _regions)
		{
			// 1. Generate ideal POI distribution with Delaunay (ignoring terrain)
			var idealPOIs = GenerateIdealPOIDistribution(region, desiredCount: 20);

			// 2. Generate terrain
			var terrain = GenerateMacroTerrain(region);

			// 3. Adapt POI types to whatever terrain they landed on
			foreach (var poi in idealPOIs)
			{
				var terrainType = terrain.GetTerrainTypeAt(poi.Position);
				var slope = terrain.GetSlopeAt(poi.Position);
				var elevation = terrain.GetElevationAt(poi.Position);

				// Intelligently assign POI type based on terrain
				poi.Type = DetermineBestPOIType(terrainType, slope, elevation, region);
			}
		}
	}

	private POIType DetermineBestPOIType(TerrainType terrain, float slope, float elevation, RegionType region)
	{
		// Mountain terrain
		if (elevation > 100 && slope > 45)
		{
			return PickRandom(new[]
			{
			POIType.DragonLair,
			POIType.MountainMonastery,
			POIType.GiantCamp,
			POIType.CliffDungeon
		});
		}

		// Underwater
		if (terrain == TerrainType.Water && elevation < -10)
		{
			return PickRandom(new[]
			{
			POIType.UnderwaterRuins,
			POIType.MerfolkCity,
			POIType.SunkenShip,
			POIType.SeaCave
		});
		}

		// Riverside/lakeside
		if (terrain == TerrainType.Water || IsNearWater(poi.Position))
		{
			return PickRandom(new[]
			{
			POIType.FishingVillage,
			POIType.Docks,
			POIType.WaterMill,
			POIType.Bridge
		});
		}

		// Valley floor
		if (elevation < 20 && slope < 15 && IsSurroundedByHighTerrain(poi.Position))
		{
			return PickRandom(new[]
			{
			POIType.HiddenVillage,
			POIType.BanditCamp,
			POIType.SecretGrove
		});
		}

		// Default: flat plains
		return PickRandom(new[]
		{
		POIType.Town,
		POIType.BossArena,
		POIType.Farm,
		POIType.CampSite
	});
	}
	#endregion

	#region Hybrid Approach
	public List<POIPlacement> GeneratePOIs(RegionType region, MacroTerrain terrain)
	{
		var pois = new List<POIPlacement>();

		// 70% adaptive (Delaunay placement, terrain determines type)
		var adaptivePOIs = GenerateIdealPOIDistribution(region, count: 14);
		foreach (var poi in adaptivePOIs)
		{
			poi.Type = DetermineBestPOIType(terrain.GetAt(poi.Position), region);
		}
		pois.AddRange(adaptivePOIs);

		// 30% specific (terrain-first for special features)
		// "I NEED a dragon on a mountain peak somewhere"
		var mountainPeaks = terrain.FindMountainPeaks();
		if (mountainPeaks.Count > 0)
		{
			var dragonLair = PickBestDelaunayPosition(mountainPeaks, pois);
			pois.Add(new POIPlacement
			{
				Position = dragonLair,
				Type = POIType.DragonLair
			});
		}

		// "I NEED an underwater dungeon if there's a lake"
		var lakeBeds = terrain.FindDeepWater();
		if (lakeBeds.Count > 0)
		{
			var underwaterDungeon = PickBestDelaunayPosition(lakeBeds, pois);
			pois.Add(new POIPlacement
			{
				Position = underwaterDungeon,
				Type = POIType.UnderwaterRuins
			});
		}

		return pois;
	}

	private Vector2 PickBestDelaunayPosition(List<Vector2> candidates, List<POIPlacement> existingPOIs)
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

	// public void PlaceDungeons()
	// {
	// 	var random = new Random(Seed);
	// 	int dungeonTarget = random.Next(5, 11);
	// 	int dungeonPlaced = 0;
	// 	int dungeonAttempts = 0;

	// 	while (dungeonPlaced < dungeonTarget && dungeonAttempts < dungeonTarget * 20)
	// 	{
	// 		dungeonAttempts++;

	// 		Vector2I chunkCoord = new Vector2I(
	// 			random.Next(0, WorldSizeChunks.X),
	// 			random.Next(0, WorldSizeChunks.Y)
	// 		);

	// 		if (!_worldData.Chunks.TryGetValue(chunkCoord, out var chunk))
	// 			continue;

	// 		int x = random.Next(2, ChunkSize.X - 2);
	// 		int z = random.Next(2, ChunkSize.Y - 2);
	// 		int index = z * ChunkSize.X + x;
	// 		float height = chunk.HeightData[index];

	// 		float minH = chunk.HeightData.Min();
	// 		float maxH = chunk.HeightData.Max();

	// 		float midLow = Mathf.Lerp(minH, maxH, 0.3f);
	// 		float midHigh = Mathf.Lerp(minH, maxH, 0.6f);

	// 		if (height < midLow || height > midHigh)
	// 			continue;

	// 		// Optional slope check (prevents cliffs)
	// 		float slope = GetSlope(chunk, x, z);
	// 		if (slope > 0.8f) // tweak threshold as needed
	// 			continue;

	// 		Vector3 worldPos = new Vector3(
	// 			chunkCoord.X * ChunkSize.X + x,
	// 			height,
	// 			chunkCoord.Y * ChunkSize.Y + z
	// 		);

	// 		chunk.DungeonEntrances.Add(new DungeonEntrance(worldPos, Vector3.Forward, BiomeType.Forest));
	// 		dungeonPlaced++;

	// 		GD.Print($"Placed dungeon at {worldPos} in chunk {chunkCoord}");
	// 	}
	// 	GD.Print($"Placed {dungeonPlaced}/{dungeonTarget} dungeons after {dungeonAttempts} attempts");
	// }

	// public void PlaceBossSpawns()
	// {
	// 	var random = new Random(Seed);
	// 	int bossTarget = random.Next(3, 6);
	// 	int bossPlaced = 0;
	// 	int bossAttempts = 0;

	// 	while (bossPlaced < bossTarget && bossAttempts < bossTarget * 20)
	// 	{
	// 		bossAttempts++;

	// 		Vector2I chunkCoord = new Vector2I(
	// 			random.Next(0, WorldSizeChunks.X),
	// 			random.Next(0, WorldSizeChunks.Y)
	// 		);

	// 		if (!_worldData.Chunks.TryGetValue(chunkCoord, out var chunk))
	// 			continue;

	// 		int x = random.Next(2, ChunkSize.X - 2);
	// 		int z = random.Next(2, ChunkSize.Y - 2);
	// 		int index = z * ChunkSize.X + x;
	// 		float height = chunk.HeightData[index];

	// 		float minH = chunk.HeightData.Min();
	// 		float maxH = chunk.HeightData.Max();
	// 		float highGround = Mathf.Lerp(minH, maxH, 0.75f);

	// 		if (height < highGround)
	// 			continue;

	// 		float slope = GetSlope(chunk, x, z);
	// 		if (slope > 0.8f)
	// 			continue;

	// 		Vector3 worldPos = new Vector3(
	// 			chunkCoord.X * ChunkSize.X + x,
	// 			height,
	// 			chunkCoord.Y * ChunkSize.Y + z
	// 		);

	// 		chunk.BossSpawns.Add(new BossSpawn(worldPos, BossType.Giant, BiomeType.Forest));
	// 		bossPlaced++;

	// 		GD.Print($"Placed boss spawn at {worldPos} in chunk {chunkCoord}");
	// 	}
	// 	GD.Print($"Placed {bossPlaced}/{bossTarget} boss spawns after {bossAttempts} attempts");
	// }

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

	// private float GetSlope(ChunkData chunk, int x, int z)
	// {
	// 	int w = ChunkSize.X;
	// 	int h = ChunkSize.Y;

	// 	int i = z * w + x;

	// 	float center = chunk.HeightData[i];
	// 	float maxDiff = 0f;

	// 	if (x > 0) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[z * w + (x - 1)]));
	// 	if (x < w - 1) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[z * w + (x + 1)]));
	// 	if (z > 0) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[(z - 1) * w + x]));
	// 	if (z < h - 1) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[(z + 1) * w + x]));

	// 	return maxDiff;
	// }

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

	// public void GenerateRegion()
	// {
	// 	GD.Print("=== Starting World Generation ===");
	// 	var startTime = Time.GetTicksMsec();

	// 	_worldData = new WorldData
	// 	{
	// 		Seed = Seed,
	// 		ChunkSize = ChunkSize,
	// 		WorldSizeChunks = WorldSizeChunks,
	// 		HeightScale = HeightScale,
	// 		Chunks = new Dictionary<Vector2I, ChunkData>()
	// 	};

	// 	int totalChunks = WorldSizeChunks.X * WorldSizeChunks.Y;
	// 	int processedChunks = 0;

	// 	// Generate all chunks
	// 	for (int chunkZ = 0; chunkZ < WorldSizeChunks.Y; chunkZ++)
	// 	{
	// 		for (int chunkX = 0; chunkX < WorldSizeChunks.X; chunkX++)
	// 		{
	// 			Vector2I chunkCoord = new Vector2I(chunkX, chunkZ);
	// 			ChunkData chunkData = GenerateChunkData(chunkCoord);
	// 			_worldData.Chunks[chunkCoord] = chunkData;

	// 			processedChunks++;

	// 			if (processedChunks % 7 == 0 || processedChunks % 50 == 0 || processedChunks == totalChunks)
	// 			{
	// 				GD.Print($"Generated {processedChunks}/{totalChunks} chunks ({(float)processedChunks / totalChunks * 100:F1}%)");
	// 			}
	// 		}
	// 	}

	// 	PlaceDungeons();
	// 	PlaceBossSpawns();
	// 	PlaceWeatherZones();

	// 	// Save to disk
	// 	SaveWorldData();

	// 	var elapsed = (Time.GetTicksMsec() - startTime) / 1000.0f;
	// 	GD.Print($"=== World Generation Complete ===");
	// 	GD.Print($"Generated {totalChunks} chunks in {elapsed:F2} seconds");
	// 	GD.Print($"World size: {WorldSizeChunks.X * ChunkSize.X} x {WorldSizeChunks.Y * ChunkSize.Y} units");
	// }

	// public ChunkData GenerateChunkData(Vector2I chunkCoord)
	// {
	// 	var chunkData = new ChunkData
	// 	{
	// 		ChunkCoord = chunkCoord,
	// 		HeightData = new float[ChunkSize.X * ChunkSize.Y]
	// 	};

	// 	int worldOffsetX = chunkCoord.X * (ChunkSize.X - 1);
	// 	int worldOffsetZ = chunkCoord.Y * (ChunkSize.Y - 1);

	// 	var vertices = new List<Vector3>();
	// 	var normals = new List<Vector3>();
	// 	var uvs = new List<Vector2>();
	// 	var indices = new List<int>();


	// 	// Generate vertices in 
	// 	for (int z = 0; z < ChunkSize.Y; z++)
	// 	{
	// 		for (int x = 0; x < ChunkSize.X; x++)
	// 		{
	// 			int worldX = worldOffsetX + x;
	// 			int worldZ = worldOffsetZ + z;

	// 			float height = GetHeightAt(worldX, worldZ);
	// 			Vector3 vertex = new Vector3(x, height, z);

	// 			chunkData.HeightData[z * ChunkSize.X + x] = height;
	// 			vertices.Add(vertex);

	// 			Vector3 normal = CalculateNormal(worldX, worldZ);
	// 			normals.Add(normal);

	// 			Vector2 uv = new Vector2((float)x / ChunkSize.X, (float)z / ChunkSize.Y);
	// 			uvs.Add(uv);
	// 		}
	// 	}

	// 	// Generate indices
	// 	for (int z = 0; z < ChunkSize.Y - 1; z++)
	// 	{
	// 		for (int x = 0; x < ChunkSize.X - 1; x++)
	// 		{
	// 			int topLeft = z * ChunkSize.X + x;
	// 			int topRight = topLeft + 1;
	// 			int bottomLeft = (z + 1) * ChunkSize.X + x;
	// 			int bottomRight = bottomLeft + 1;

	// 			// First triangle
	// 			indices.Add(topLeft);
	// 			indices.Add(topRight);
	// 			indices.Add(bottomLeft);


	// 			// Second triangle
	// 			indices.Add(topRight);
	// 			indices.Add(bottomRight);
	// 			indices.Add(bottomLeft);
	// 		}
	// 	}

	// 	var collisionFaces = new Vector3[indices.Count];
	// 	for (int i = 0; i < indices.Count; i++)
	// 	{
	// 		collisionFaces[i] = vertices[indices[i]];
	// 	}

	// 	chunkData.Vertices = vertices.ToArray();
	// 	chunkData.Normals = normals.ToArray();
	// 	chunkData.UVs = uvs.ToArray();
	// 	chunkData.Indices = indices.ToArray();
	// 	chunkData.CollisionFaces = collisionFaces;

	// 	_worldData.Chunks[chunkCoord] = chunkData;
	// 	return chunkData;
	// }

	// private float GetHeightAt(int worldX, int worldZ)
	// {
	// 	return Noise.GetNoise2D(worldX, worldZ) * HeightScale;
	// }

	// private Vector3 CalculateNormal(int worldX, int worldZ)
	// {
	// 	float heightL = GetHeightAt(worldX - 1, worldZ);
	// 	float heightR = GetHeightAt(worldX + 1, worldZ);
	// 	float heightD = GetHeightAt(worldX, worldZ - 1);
	// 	float heightU = GetHeightAt(worldX, worldZ + 1);

	// 	Vector3 normal = new Vector3(heightL - heightR, 2.0f, heightD - heightU);
	// 	return normal.Normalized();
	// }

	// public void PlaceDungeons()
	// {
	// 	var random = new Random(Seed);
	// 	int dungeonsTarget = random.Next(5, 11);
	// 	int dungeonsPlaced = 0;
	// 	int dungeonsAttempts = 0;

	// 	while (dungeonsPlaced < dungeonsTarget && dungeonsAttempts < dungeonsTarget * 20)
	// 	{
	// 		dungeonsAttempts++;

	// 		Vector2I chunkCoord = new Vector2I(
	// 			random.Next(0, WorldSizeChunks.X),
	// 			random.Next(0, WorldSizeChunks.Y)
	// 		);

	// 		if (!_worldData.Chunks.TryGetValue(chunkCoord, out var chunk))
	// 			continue;

	// 		int x = random.Next(2, ChunkSize.X - 2);
	// 		int z = random.Next(2, ChunkSize.Y - 2);
	// 		int index = z * ChunkSize.X + x;
	// 		float height = chunk.HeightData[index];

	// 		float minH = chunk.HeightData.Min();
	// 		float maxH = chunk.HeightData.Max();

	// 		float midLow = Mathf.Lerp(minH, maxH, 0.3f);
	// 		float midHigh = Mathf.Lerp(minH, maxH, 0.6f);

	// 		if (height < midLow || height > midHigh)
	// 			continue;

	// 		// Optional slope check (prevents cliffs)
	// 		float slope = GetSlope(chunk, x, z);
	// 		if (slope > 0.8f) // tweak threshold as needed
	// 			continue;

	// 		Vector3 worldPos = new Vector3(
	// 			chunkCoord.X * ChunkSize.X + x,
	// 			height,
	// 			chunkCoord.Y * ChunkSize.Y + z
	// 		);

	// 		chunk.DungeonEntrances.Add(new DungeonEntrance(worldPos, Vector3.Forward, BiomeType.Forest));
	// 		dungeonPlaced++;

	// 		GD.Print($"Placed dungeon at {worldPos} in chunk {chunkCoord}");
	// 	}
	// 	GD.Print($"Placed {dungeonPlaced}/{dungeonTarget} dungeons after {dungeonAttempts} attempts");
	// }

	// // One boss per region, placed in high ground areas with low slope
	// public void PlaceBossSpawn()
	// {
	// 	var random = new Random(Seed);
	// 	int bossTarget = random.Next(3, 6);
	// 	int bossPlaced = 0;
	// 	int bossAttempts = 0;

	// 	while (bossPlaced < bossTarget && bossAttempts < bossTarget * 20)
	// 	{
	// 		bossAttempts++;

	// 		Vector2I chunkCoord = new Vector2I(
	// 			random.Next(0, WorldSizeChunks.X),
	// 			random.Next(0, WorldSizeChunks.Y)
	// 		);

	// 		if (!_worldData.Chunks.TryGetValue(chunkCoord, out var chunk))
	// 			continue;

	// 		int x = random.Next(2, ChunkSize.X - 2);
	// 		int z = random.Next(2, ChunkSize.Y - 2);
	// 		int index = z * ChunkSize.X + x;
	// 		float height = chunk.HeightData[index];

	// 		float minH = chunk.HeightData.Min();
	// 		float maxH = chunk.HeightData.Max();
	// 		float highGround = Mathf.Lerp(minH, maxH, 0.75f);

	// 		if (height < highGround)
	// 			continue;

	// 		float slope = GetSlope(chunk, x, z);
	// 		if (slope > 0.8f)
	// 			continue;

	// 		Vector3 worldPos = new Vector3(
	// 			chunkCoord.X * ChunkSize.X + x,
	// 			height,
	// 			chunkCoord.Y * ChunkSize.Y + z
	// 		);

	// 		chunk.BossSpawns.Add(new BossSpawn(worldPos, BossType.Giant, BiomeType.Forest));
	// 		bossPlaced++;

	// 		GD.Print($"Placed boss spawn at {worldPos} in chunk {chunkCoord}");
	// 	}
	// 	GD.Print($"Placed {bossPlaced}/{bossTarget} boss spawns after {bossAttempts} attempts");
	// }

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

	// private float GetSlope(ChunkData chunk, int x, int z)
	// {
	// 	int w = ChunkSize.X;
	// 	int h = ChunkSize.Y;

	// 	int i = z * w + x;

	// 	float center = chunk.HeightData[i];
	// 	float maxDiff = 0f;

	// 	if (x > 0) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[z * w + (x - 1)]));
	// 	if (x < w - 1) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[z * w + (x + 1)]));
	// 	if (z > 0) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[(z - 1) * w + x]));
	// 	if (z < h - 1) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(center - chunk.HeightData[(z + 1) * w + x]));

	// 	return maxDiff;
	// }

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
