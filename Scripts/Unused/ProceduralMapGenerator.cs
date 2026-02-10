using Godot;
using System;
using System.Collections.Generic;

public partial class ProceduralMapGenerator : Node3D
{
	[Export] public int MapWidth = 100;
	[Export] public int MapDepth = 100;
	[Export] public float HeightScale = 20.0f;
	[Export] public float NoiseScale = 0.05f;
	[Export] public int Seed = 0;

	public Vector3[] GetDungeonEntrances() => _dungeonEntrances.ToArray();
	public Vector3[] GetBossLocations() => _bossLocations.ToArray();

	private FastNoiseLite _noise;
	private MeshInstance3D _terrain;
	private List<Vector3> _dungeonEntrances = new();
	private List<Vector3> _bossLocations = new();

	public override void _Ready()
	{
		InitializeNoise();
		GenerateTerrain();
		PlaceDungeonEntrances(5); // 5 dungeon entrances
		PlaceBossLocations(3); // 3 boss spawn points
		VisualizeSpecialLocations();


		_terrain.CreateTrimeshCollision();
	}

	// Get nearest dungeon entrance to a position
	public Vector3 GetNearestDungeonEntrance(Vector3 position)
	{
		Vector3 nearest = _dungeonEntrances[0];
		float minDist = float.MaxValue;

		foreach (var entrance in _dungeonEntrances)
		{
			float dist = position.DistanceTo(entrance);
			if (dist < minDist)
			{
				minDist = dist;
				nearest = entrance;
			}
		}

		return nearest;
	}

	private void InitializeNoise()
	{
		_noise = new FastNoiseLite();
		_noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noise.Seed = Seed;
		_noise.Frequency = NoiseScale;
	}

	private void GenerateTerrain()
	{
		// Create arrays for mesh data
		var surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

		// Generate vertices
		for (int z = 0; z < MapDepth; z++)
		{
			for (int x = 0; x < MapWidth; x++)
			{
				float height = _noise.GetNoise2D(x, z) * HeightScale;
				Vector3 vertex = new Vector3(x, height, z);

				// Calculate normal (for lighting)
				Vector3 normal = CalculateNormal(x, z);

				surfaceTool.SetNormal(normal);
				surfaceTool.SetUV(new Vector2((float)x / MapWidth, (float)z / MapDepth));
				surfaceTool.AddVertex(vertex);
			}
		}

		// Generate indices for triangles
		for (int z = 0; z < MapDepth - 1; z++)
		{
			for (int x = 0; x < MapWidth - 1; x++)
			{
				int topLeft = z * MapWidth + x;
				int topRight = topLeft + 1;
				int bottomLeft = (z + 1) * MapWidth + x;
				int bottomRight = bottomLeft + 1;

				// First triangle
				surfaceTool.AddIndex(topLeft);
				surfaceTool.AddIndex(bottomLeft);
				surfaceTool.AddIndex(topRight);

				// Second triangle
				surfaceTool.AddIndex(topRight);
				surfaceTool.AddIndex(bottomLeft);
				surfaceTool.AddIndex(bottomRight);
			}
		}

		// Create mesh and add to scene
		var mesh = surfaceTool.Commit();

		// Create StaticBody3D for terrain
		var staticBody = new StaticBody3D();
		staticBody.Name = "Terrain";
		staticBody.CollisionLayer = 1; // World geometry layer
		staticBody.CollisionMask = 0; // Not colliding with anything
		AddChild(staticBody);

		// Create and add mesh instance
		_terrain = new MeshInstance3D();
		_terrain.Mesh = mesh;

		// Add a material
		var material = new StandardMaterial3D();
		material.AlbedoColor = new Color(0.3f, 0.6f, 0.2f); // Green terrain
		_terrain.MaterialOverride = material;

		_terrain.CreateTrimeshCollision();
		staticBody.AddChild(_terrain);
	}

	private Vector3 CalculateNormal(int x, int z)
	{
		// Sample neighboring heights for normal calculation
		float heightL = GetHeight(x - 1, z);
		float heightR = GetHeight(x + 1, z);
		float heightD = GetHeight(x, z - 1);
		float heightU = GetHeight(x, z + 1);

		Vector3 normal = new Vector3(heightL - heightR, 2.0f, heightD - heightU);
		return normal.Normalized();
	}

	private float GetHeight(int x, int z)
	{
		// Clamp to map bounds
		x = Mathf.Clamp(x, 0, MapWidth - 1);
		z = Mathf.Clamp(z, 0, MapDepth - 1);
		return _noise.GetNoise2D(x, z) * HeightScale;
	}

	private void PlaceDungeonEntrances(int count)
	{
		var random = new Random(Seed);
		int attempts = 0;
		int maxAttempts = count * 50;

		while (_dungeonEntrances.Count < count && attempts < maxAttempts)
		{
			attempts++;

			// Random position
			int x = random.Next(10, MapWidth - 10);
			int z = random.Next(10, MapDepth - 10);
			float height = GetHeight(x, z);

			// Placement rules: mid-elevation, not too steep
			if (height > 5.0f && height < 15.0f && !IsTerrainTooSteep(x, z))
			{
				Vector3 position = new Vector3(x, height, z);

				// Check minimum distance from other entrances
				if (IsValidPlacement(position, _dungeonEntrances, 15.0f))
				{
					_dungeonEntrances.Add(position);
				}
			}
		}

		GD.Print($"Placed {_dungeonEntrances.Count} dungeon entrances");
	}

	private bool IsTerrainTooSteep(int x, int z, float maxSteepness = 3.0f)
	{
		float centerHeight = GetHeight(x, z);
		float avgDiff = 0;
		int samples = 0;

		for (int dz = -1; dz <= 1; dz++)
		{
			for (int dx = -1; dx <= 1; dx++)
			{
				if (dx == 0 && dz == 0) continue;
				avgDiff += Mathf.Abs(centerHeight - GetHeight(x + dx, z + dz));
				samples++;
			}
		}

		return (avgDiff / samples) > maxSteepness;
	}

	private void PlaceBossLocations(int count)
	{
		var random = new Random(Seed + 1); // Different seed offset
		int attempts = 0;
		int maxAttempts = count * 50;

		while (_bossLocations.Count < count && attempts < maxAttempts)
		{
			attempts++;

			int x = random.Next(10, MapWidth - 10);
			int z = random.Next(10, MapDepth - 10);
			float height = GetHeight(x, z);

			// Bosses spawn at high elevations or in flat clearings
			bool isHighPeak = height > 15.0f;
			bool isFlatClearing = height > 2.0f && height < 8.0f && !IsTerrainTooSteep(x, z, 1.5f);

			if (isHighPeak || isFlatClearing)
			{
				Vector3 position = new Vector3(x, height, z);

				// Bosses need more space from each other and from dungeons
				if (IsValidPlacement(position, _bossLocations, 20.0f) &&
					IsValidPlacement(position, _dungeonEntrances, 10.0f))
				{
					_bossLocations.Add(position);
				}
			}
		}

		GD.Print($"Placed {_bossLocations.Count} boss locations");
	}

	private bool IsValidPlacement(Vector3 position, List<Vector3> existingPositions, float minDistance)
	{
		foreach (var existing in existingPositions)
		{
			if (position.DistanceTo(existing) < minDistance)
				return false;
		}
		return true;
	}

	private void VisualizeSpecialLocations()
	{
		// Create dungeon entrance markers
		foreach (var pos in _dungeonEntrances)
		{
			var marker = CreateMarker(pos, new Color(0.8f, 0.3f, 0.1f), 2.0f);
			marker.Name = "DungeonEntrance";
		}

		// Create boss spawn markers
		foreach (var pos in _bossLocations)
		{
			var marker = CreateMarker(pos, new Color(1.0f, 0.0f, 0.0f), 3.0f);
			marker.Name = "BossSpawn";
		}
	}

	private MeshInstance3D CreateMarker(Vector3 position, Color color, float height)
	{
		var marker = new MeshInstance3D();
		var mesh = new CylinderMesh();
		mesh.Height = height;
		mesh.TopRadius = 0.5f;
		mesh.BottomRadius = 0.5f;

		marker.Mesh = mesh;
		marker.Position = position + new Vector3(0, height / 2, 0);

		var material = new StandardMaterial3D();
		material.AlbedoColor = color;
		material.EmissionEnabled = true;
		material.Emission = color;
		//material.EmissionIntensity = 2.0f;

		marker.MaterialOverride = material;
		AddChild(marker);

		return marker;
	}

	private float[,] DiamondSquare(int size, float roughness)
	{
		float[,] map = new float[size, size];
		var random = new Random(Seed);

		// Initialize corners
		map[0, 0] = random.NextSingle();
		map[0, size - 1] = random.NextSingle();
		map[size - 1, 0] = random.NextSingle();
		map[size - 1, size - 1] = random.NextSingle();

		int step = size - 1;

		while (step > 1)
		{
			int halfStep = step / 2;

			// Diamond step
			for (int z = halfStep; z < size; z += step)
			{
				for (int x = halfStep; x < size; x += step)
				{
					float avg = (
						map[x - halfStep, z - halfStep] +
						map[x + halfStep, z - halfStep] +
						map[x - halfStep, z + halfStep] +
						map[x + halfStep, z + halfStep]
					) / 4.0f;

					map[x, z] = avg + (random.NextSingle() - 0.5f) * roughness;
				}
			}

			// Square step (similar logic)
			// ... implementation

			roughness *= 0.5f;
			step /= 2;
		}

		return map;
	}

	private void SimulateErosion(float[,] heightMap, int iterations)
	{
		var random = new Random(Seed);

		for (int i = 0; i < iterations; i++)
		{
			// Drop a water particle
			int x = random.Next(0, MapWidth);
			int z = random.Next(0, MapDepth);

			float sediment = 0;
			float velocity = 1.0f;

			for (int lifetime = 0; lifetime < 30; lifetime++)
			{
				// Find steepest descent
				Vector2 gradient = CalculateGradient(heightMap, x, z);

				// Move particle
				x += (int)gradient.X;
				z += (int)gradient.Y;

				if (x < 1 || x >= MapWidth - 1 || z < 1 || z >= MapDepth - 1)
					break;

				// Erosion/deposition logic
				float heightDiff = heightMap[x, z] - heightMap[x - (int)gradient.X, z - (int)gradient.Y];

				if (heightDiff > 0) // Moving downhill
				{
					float amountToErode = Mathf.Min(heightDiff, velocity * 0.1f);
					heightMap[x, z] -= amountToErode;
					sediment += amountToErode;
				}
				else // Moving uphill or flat
				{
					float amountToDeposit = Mathf.Min(sediment, -heightDiff);
					heightMap[x, z] += amountToDeposit;
					sediment -= amountToDeposit;
				}

				velocity = Mathf.Sqrt(velocity * velocity + heightDiff);
			}
		}
	}

	private Vector2 CalculateGradient(float[,] heightMap, int x, int z)
	{
		float heightL = heightMap[Mathf.Clamp(x - 1, 0, MapWidth - 1), z];
		float heightR = heightMap[Mathf.Clamp(x + 1, 0, MapWidth - 1), z];
		float heightD = heightMap[x, Mathf.Clamp(z - 1, 0, MapDepth - 1)];
		float heightU = heightMap[x, Mathf.Clamp(z + 1, 0, MapDepth - 1)];

		return new Vector2(heightR - heightL, heightU - heightD).Normalized();
	}

	private float GetFractalHeight(int x, int z, int octaves = 4)
	{
		float total = 0;
		float frequency = NoiseScale;
		float amplitude = 1.0f;
		float maxValue = 0;

		for (int i = 0; i < octaves; i++)
		{
			total += _noise.GetNoise2D(x * frequency, z * frequency) * amplitude;

			maxValue += amplitude;
			amplitude *= 0.5f; // Each octave has half the impact
			frequency *= 2.0f; // Each octave doubles the frequency
		}

		return (total / maxValue) * HeightScale;
	}

	private float VoronoiNoise(float x, float z, int cellCount = 10)
	{
		var random = new Random(Seed);
		List<Vector2> points = new();

		// Generate random cell points
		for (int i = 0; i < cellCount; i++)
		{
			points.Add(new Vector2(
				random.Next(0, MapWidth),
				random.Next(0, MapDepth)
			));
		}

		// Find distance to nearest point
		float minDist = float.MaxValue;
		foreach (var point in points)
		{
			float dist = new Vector2(x, z).DistanceTo(point);
			minDist = Mathf.Min(minDist, dist);
		}

		return minDist / 20.0f; // Normalize
	}
}
