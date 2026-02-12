using Godot;
using System;
using System.Collections.Generic;

public partial class BiomeTerrainGenerator : Node3D
{
	[Export] public int MapWidth = 200;
	[Export] public int MapDepth = 200;
	[Export] public float HeightScale = 30.0f;
	[Export] public int Seed = 0;

	private FastNoiseLite _heightNoise;
	private FastNoiseLite _moistureNoise;
	private FastNoiseLite _temperatureNoise;
	private FastNoiseLite _biomeBlendNoise;

	private Dictionary<string, Biome> _biomes = new();
	private Biome[,] _biomeMap;
	private float[,] _heightMap;

	public override void _Ready()
	{
		InitializeNoises();
		DefineBiomes();
		GenerateMaps();
		GenerateBiomeTerrain();
		PlaceVegetation();
	}

	private void InitializeNoises()
	{
		// Height noise - main terrain features
		_heightNoise = new FastNoiseLite();
		_heightNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_heightNoise.Seed = Seed;
		_heightNoise.Frequency = 0.02f;
		_heightNoise.FractalOctaves = 5; // Multi-octave for detail
		_heightNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;

		// Moisture noise - affects biome type
		_moistureNoise = new FastNoiseLite();
		_moistureNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_moistureNoise.Seed = Seed + 1;
		_moistureNoise.Frequency = 0.03f;
		_moistureNoise.FractalOctaves = 3;

		// Temperature noise - affects biome type
		_temperatureNoise = new FastNoiseLite();
		_temperatureNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_temperatureNoise.Seed = Seed + 2;
		_temperatureNoise.Frequency = 0.015f; // Larger regions
		_temperatureNoise.FractalOctaves = 2;

		// Biome blend noise - smooth transitions
		_biomeBlendNoise = new FastNoiseLite();
		_biomeBlendNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_biomeBlendNoise.Seed = Seed + 3;
		_biomeBlendNoise.Frequency = 0.1f;
	}

	private void DefineBiomes()
	{
		// Ocean/Water
		_biomes["ocean"] = new Biome
		{
			Name = "Ocean",
			TerrainColor = new Color(0.1f, 0.3f, 0.7f),
			MinHeight = -1.0f,
			MaxHeight = 0.1f,
			MinMoisture = -1.0f,
			MaxMoisture = 1.0f,
			MinTemperature = -1.0f,
			MaxTemperature = 1.0f,
			VegetationPrefabs = new string[] { },
			VegetationDensity = 0.0f
		};

		// Beach
		_biomes["beach"] = new Biome
		{
			Name = "Beach",
			TerrainColor = new Color(0.9f, 0.85f, 0.6f),
			MinHeight = 0.1f,
			MaxHeight = 0.15f,
			MinMoisture = -1.0f,
			MaxMoisture = 1.0f,
			MinTemperature = -1.0f,
			MaxTemperature = 1.0f,
			VegetationPrefabs = new string[] { "palm_tree" },
			VegetationDensity = 0.05f
		};

		// Desert
		_biomes["desert"] = new Biome
		{
			Name = "Desert",
			TerrainColor = new Color(0.85f, 0.75f, 0.5f),
			MinHeight = 0.15f,
			MaxHeight = 0.4f,
			MinMoisture = -1.0f,
			MaxMoisture = -0.3f,
			MinTemperature = 0.3f,
			MaxTemperature = 1.0f,
			VegetationPrefabs = new string[] { "cactus", "dead_bush" },
			VegetationDensity = 0.02f
		};

		// Grassland
		_biomes["grassland"] = new Biome
		{
			Name = "Grassland",
			TerrainColor = new Color(0.4f, 0.7f, 0.3f),
			MinHeight = 0.15f,
			MaxHeight = 0.5f,
			MinMoisture = -0.3f,
			MaxMoisture = 0.3f,
			MinTemperature = -0.2f,
			MaxTemperature = 0.6f,
			VegetationPrefabs = new string[] { "grass_tuft", "small_tree" },
			VegetationDensity = 0.3f
		};

		// Forest
		_biomes["forest"] = new Biome
		{
			Name = "Forest",
			TerrainColor = new Color(0.2f, 0.5f, 0.2f),
			MinHeight = 0.2f,
			MaxHeight = 0.6f,
			MinMoisture = 0.0f,
			MaxMoisture = 1.0f,
			MinTemperature = -0.2f,
			MaxTemperature = 0.5f,
			VegetationPrefabs = new string[] { "oak_tree", "pine_tree", "bush" },
			VegetationDensity = 0.6f
		};

		// Tundra
		_biomes["tundra"] = new Biome
		{
			Name = "Tundra",
			TerrainColor = new Color(0.7f, 0.75f, 0.7f),
			MinHeight = 0.2f,
			MaxHeight = 0.5f,
			MinMoisture = -1.0f,
			MaxMoisture = 0.2f,
			MinTemperature = -1.0f,
			MaxTemperature = -0.3f,
			VegetationPrefabs = new string[] { "small_shrub" },
			VegetationDensity = 0.05f
		};

		// Mountain
		_biomes["mountain"] = new Biome
		{
			Name = "Mountain",
			TerrainColor = new Color(0.5f, 0.5f, 0.5f),
			MinHeight = 0.6f,
			MaxHeight = 1.0f,
			MinMoisture = -1.0f,
			MaxMoisture = 1.0f,
			MinTemperature = -1.0f,
			MaxTemperature = 1.0f,
			VegetationPrefabs = new string[] { },
			VegetationDensity = 0.0f
		};

		// Snow Peak
		_biomes["snow"] = new Biome
		{
			Name = "Snow",
			TerrainColor = new Color(0.95f, 0.95f, 0.98f),
			MinHeight = 0.8f,
			MaxHeight = 1.0f,
			MinMoisture = -1.0f,
			MaxMoisture = 1.0f,
			MinTemperature = -1.0f,
			MaxTemperature = 0.0f,
			VegetationPrefabs = new string[] { },
			VegetationDensity = 0.0f
		};
	}

	private void GenerateMaps()
	{
		_heightMap = new float[MapWidth, MapDepth];
		_biomeMap = new Biome[MapWidth, MapDepth];

		for (int z = 0; z < MapDepth; z++)
		{
			for (int x = 0; x < MapWidth; x++)
			{
				// Generate normalized values (-1 to 1)
				float height = _heightNoise.GetNoise2D(x, z);
				float moisture = _moistureNoise.GetNoise2D(x, z);
				float temperature = _temperatureNoise.GetNoise2D(x, z);

				// Apply temperature gradient (colder at poles)
				float latitudeFactor = Mathf.Abs((float)z / MapDepth - 0.5f) * 2.0f;
				temperature -= latitudeFactor * 0.5f;

				// Apply moisture near coastlines
				if (height < 0.15f && height > 0.0f)
				{
					moisture += 0.3f;
				}

				_heightMap[x, z] = height;
				_biomeMap[x, z] = DetermineBiome(height, moisture, temperature);
			}
		}
	}

	private Biome DetermineBiome(float height, float moisture, float temperature)
	{
		// Check each biome and find the best match
		foreach (var biome in _biomes.Values)
		{
			if (height >= biome.MinHeight && height <= biome.MaxHeight &&
				moisture >= biome.MinMoisture && moisture <= biome.MaxMoisture &&
				temperature >= biome.MinTemperature && temperature <= biome.MaxTemperature)
			{
				return biome;
			}
		}

		// Default fallback
		return _biomes["grassland"];
	}

	private void GenerateBiomeTerrain()
	{
		var surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

		// Generate vertices with biome colors
		for (int z = 0; z < MapDepth; z++)
		{
			for (int x = 0; x < MapWidth; x++)
			{
				float height = _heightMap[x, z] * HeightScale;
				Vector3 vertex = new Vector3(x, height, z);

				// Blend biome colors from neighboring cells
				Color blendedColor = BlendBiomeColors(x, z);

				Vector3 normal = CalculateNormal(x, z);

				surfaceTool.SetNormal(normal);
				surfaceTool.SetColor(blendedColor);
				surfaceTool.SetUV(new Vector2((float)x / MapWidth, (float)z / MapDepth));
				surfaceTool.AddVertex(vertex);
			}
		}

		// Add indices (same as before)
		for (int z = 0; z < MapDepth - 1; z++)
		{
			for (int x = 0; x < MapWidth - 1; x++)
			{
				int topLeft = z * MapWidth + x;
				int topRight = topLeft + 1;
				int bottomLeft = (z + 1) * MapWidth + x;
				int bottomRight = bottomLeft + 1;

				surfaceTool.AddIndex(topLeft);
				surfaceTool.AddIndex(bottomLeft);
				surfaceTool.AddIndex(topRight);

				surfaceTool.AddIndex(topRight);
				surfaceTool.AddIndex(bottomLeft);
				surfaceTool.AddIndex(bottomRight);
			}
		}

		var mesh = surfaceTool.Commit();
		var meshInstance = new MeshInstance3D();
		meshInstance.Mesh = mesh;

		// Use vertex colors
		var material = new StandardMaterial3D();
		material.VertexColorUseAsAlbedo = true;
		meshInstance.MaterialOverride = material;

		AddChild(meshInstance);
	}

	private Color BlendBiomeColors(int x, int z, int blendRadius = 2)
	{
		Color totalColor = new Color(0, 0, 0);
		float totalWeight = 0;

		for (int dz = -blendRadius; dz <= blendRadius; dz++)
		{
			for (int dx = -blendRadius; dx <= blendRadius; dx++)
			{
				int nx = Mathf.Clamp(x + dx, 0, MapWidth - 1);
				int nz = Mathf.Clamp(z + dz, 0, MapDepth - 1);

				float distance = Mathf.Sqrt(dx * dx + dz * dz);
				float weight = Mathf.Max(0, blendRadius - distance);

				totalColor += _biomeMap[nx, nz].TerrainColor * weight;
				totalWeight += weight;
			}
		}

		return totalColor / totalWeight;
	}

	private Vector3 CalculateNormal(int x, int z)
	{
		float heightL = GetHeightAt(x - 1, z);
		float heightR = GetHeightAt(x + 1, z);
		float heightD = GetHeightAt(x, z - 1);
		float heightU = GetHeightAt(x, z + 1);

		Vector3 normal = new Vector3(heightL - heightR, 2.0f, heightD - heightU);
		return normal.Normalized();
	}

	private float GetHeightAt(int x, int z)
	{
		x = Mathf.Clamp(x, 0, MapWidth - 1);
		z = Mathf.Clamp(z, 0, MapDepth - 1);
		return _heightMap[x, z] * HeightScale;
	}

	private void PlaceVegetation()
	{
		var random = new Random(Seed + 100);

		for (int z = 0; z < MapDepth; z += 2) // Sample every 2 units for performance
		{
			for (int x = 0; x < MapWidth; x += 2)
			{
				Biome biome = _biomeMap[x, z];

				if (biome.VegetationPrefabs.Length == 0) continue;

				// Random chance based on vegetation density
				if (random.NextSingle() > biome.VegetationDensity) continue;

				// Pick random vegetation type
				string vegType = biome.VegetationPrefabs[
					random.Next(0, biome.VegetationPrefabs.Length)
				];

				float height = _heightMap[x, z] * HeightScale;
				Vector3 position = new Vector3(x, height, z);

				// Create simple vegetation marker (replace with actual models)
				CreateVegetationMarker(position, vegType, random);
			}
		}
	}

	private void CreateVegetationMarker(Vector3 position, string type, Random random)
	{
		var marker = new MeshInstance3D();

		// Different shapes for different vegetation
		if (type.Contains("tree"))
		{
			var mesh = new CylinderMesh();
			mesh.Height = random.NextSingle() * 3 + 2;
			mesh.TopRadius = 0.2f;
			mesh.BottomRadius = 0.3f;
			marker.Mesh = mesh;

			var material = new StandardMaterial3D();
			material.AlbedoColor = new Color(0.3f, 0.2f, 0.1f); // Brown trunk
			marker.MaterialOverride = material;
		}
		else if (type.Contains("cactus"))
		{
			var mesh = new CylinderMesh();
			mesh.Height = random.NextSingle() * 2 + 1;
			mesh.TopRadius = 0.3f;
			mesh.BottomRadius = 0.3f;
			marker.Mesh = mesh;

			var material = new StandardMaterial3D();
			material.AlbedoColor = new Color(0.2f, 0.6f, 0.2f);
			marker.MaterialOverride = material;
		}
		else // Grass/shrubs
		{
			var mesh = new BoxMesh();
			mesh.Size = new Vector3(0.3f, 0.5f, 0.3f);
			marker.Mesh = mesh;

			var material = new StandardMaterial3D();
			material.AlbedoColor = new Color(0.3f, 0.7f, 0.2f);
			marker.MaterialOverride = material;
		}

		marker.Position = position + new Vector3(0, 0.5f, 0);
		AddChild(marker);
	}

	private Biome WhittakerBiome(float moisture, float temperature)
	{
		// Classic ecology biome classification
		if (temperature > 0.7f)
		{
			if (moisture < -0.5f) return _biomes["desert"];
			if (moisture < 0.0f) return _biomes["savanna"];
			return _biomes["tropical_rainforest"];
		}
		else if (temperature > 0.3f)
		{
			if (moisture < -0.3f) return _biomes["grassland"];
			if (moisture < 0.3f) return _biomes["temperate_forest"];
			return _biomes["temperate_rainforest"];
		}
		else if (temperature > -0.3f)
		{
			if (moisture < 0.0f) return _biomes["taiga"];
			return _biomes["boreal_forest"];
		}
		else
		{
			return _biomes["tundra"];
		}
	}

	private void PlaceBiomeSpecificFeatures()
	{
		var _random = new Random(Seed);
		for (int z = 0; z < MapDepth; z++)
		{
			for (int x = 0; x < MapWidth; x++)
			{
				Biome biome = _biomeMap[x, z];

				// Desert dungeons: ancient tombs
				if (biome.Name == "Desert" && _random.NextSingle() < 0.001f)
				{
					CreateDungeon(new Vector3(x, _heightMap[x, z] * HeightScale, z), "tomb");
				}

				// Forest dungeons: bandit camps
				if (biome.Name == "Forest" && _random.NextSingle() < 0.002f)
				{
					CreateDungeon(new Vector3(x, _heightMap[x, z] * HeightScale, z), "camp");
				}

				// Mountain bosses: dragons
				if (biome.Name == "Mountain" && _heightMap[x, z] > 0.75f && _random.NextSingle() < 0.0005f)
				{
					CreateBossSpawn(new Vector3(x, _heightMap[x, z] * HeightScale, z), "dragon");
				}
			}
		}
	}

	private void CreateDungeon(Vector3 position, string type)
	{
		// Placeholder for dungeon creation logic
		GD.Print($"Dungeon of type '{type}' created at {position}");
	}

	private void CreateBossSpawn(Vector3 position, string type)
	{
		// Placeholder for boss spawn creation logic
		GD.Print($"Boss of type '{type}' spawned at {position}");
	}
}