using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RuntimeChunkLoader : Node3D
{
	public int Count => _loadedChunks.Count;
	private Node3D Player;
	private WorldGenerator WorldGenerator;
	private int RenderDistanceChunks;
	private float UpdateInterval;
	public WorldData WorldData;
	private Dictionary<Vector2I, LoadedChunk> _loadedChunks = new();
	public Vector2I LastPlayerChunk = new Vector2I(int.MaxValue, int.MaxValue);
	private float _updateTimer = 0.0f;
	private ShaderMaterial _terrainMaterial;
	private PropSpawner _propSpawner;

	public RuntimeChunkLoader(WorldData worldData, Node3D player, int renderDistanceChunks, float updateInterval)
	{
		Player = player;
		WorldData = worldData;
		RenderDistanceChunks = renderDistanceChunks;
		UpdateInterval = updateInterval;
	}

	public override void _Ready()
	{
		if (Player == null)
		{
			GD.PrintErr("Player not assigned!");
			return;
		}

		if (WorldData == null || WorldData.Chunks.Count == 0)
		{
			GD.PrintErr("No world data available! Generate world first.");
			return;
		}

		var materialBuilder = new TerrainMaterialBuilder();
		_terrainMaterial = materialBuilder.CreateTerrainMaterial();

		_propSpawner = new PropSpawner();
		_propSpawner.PropDefinitions = LoadPropDefinitions();
		AddChild(_propSpawner);
		// Initial load
		UpdateVisibleChunks(true);
	}

	public override void _Process(double delta)
	{
		_updateTimer += (float)delta;

		if (_updateTimer >= UpdateInterval)
		{
			_updateTimer = 0.0f;
			UpdateVisibleChunks(true);
		}
		UpdateVisibleChunks();
	}

	private void UpdateVisibleChunks(bool forceUpdate = false)
	{
		Vector2I playerChunk = WorldToChunkCoord(Player.GlobalPosition);

		if (playerChunk == LastPlayerChunk && !forceUpdate)
			return;

		LastPlayerChunk = playerChunk;

		// Determine which chunks should be visible
		var chunksToLoad = new HashSet<Vector2I>();

		for (int z = -RenderDistanceChunks; z <= RenderDistanceChunks; z++)
		{
			for (int x = -RenderDistanceChunks; x <= RenderDistanceChunks; x++)
			{
				// Circular render distance
				if (new Vector2(x, z).Length() <= RenderDistanceChunks)
				{
					Vector2I chunkCoord = playerChunk + new Vector2I(x, z);

					// Only add if chunk exists in world data
					if (WorldData.Chunks.ContainsKey(chunkCoord))
					{
						chunksToLoad.Add(chunkCoord);
					}
				}
			}
		}

		// Load new chunks
		foreach (var coord in chunksToLoad)
		{
			if (!_loadedChunks.ContainsKey(coord))
			{
				LoadChunk(coord);
			}
		}

		// Unload distant chunks
		var chunksToUnload = new List<Vector2I>();
		foreach (var coord in _loadedChunks.Keys)
		{
			if (!chunksToLoad.Contains(coord))
			{
				chunksToUnload.Add(coord);
			}
		}

		foreach (var coord in chunksToUnload)
		{
			UnloadChunk(coord);
		}
	}

	private Vector2I WorldToChunkCoord(Vector3 worldPos)
	{
		int chunkX = Mathf.FloorToInt(worldPos.X / (WorldData.ChunkSize.X - 1)); // -1 to match generation logic
		int chunkZ = Mathf.FloorToInt(worldPos.Z / (WorldData.ChunkSize.Y - 1)); // -1 to match generation logic
		return new Vector2I(chunkX, chunkZ);
	}

	private Vector2I ChunkCoordToWorldOffset(Vector2I chunkCoord)
	{
		int worldOffsetX = chunkCoord.X * (WorldData.ChunkSize.X - 1); // -1 to prevent gaps between chunks
		int worldOffsetZ = chunkCoord.Y * (WorldData.ChunkSize.Y - 1); // -1 to prevent gaps between chunks
		return new Vector2I(worldOffsetX, worldOffsetZ);
	}

	private void LoadChunk(Vector2I chunkCoord)
	{
		if (!WorldData.Chunks.TryGetValue(chunkCoord, out var chunkData))
		{
			GD.PrintErr($"Chunk {chunkCoord} not found in world data!");
			return;
		}

		// Calculate world offset for chunk position
		Vector2I worldOffset = ChunkCoordToWorldOffset(chunkCoord);

		// Create mesh from pre-computed data
		var surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

		// Add all vertices with their data
		for (int i = 0; i < chunkData.Vertices.Length; i++)
		{
			surfaceTool.SetNormal(chunkData.Normals[i]);
			surfaceTool.SetUV(chunkData.UVs[i]);
			surfaceTool.AddVertex(chunkData.Vertices[i]);
		}

		// Add indices
		foreach (int index in chunkData.Indices)
		{
			surfaceTool.AddIndex(index);
		}

		var mesh = surfaceTool.Commit();

		// Create mesh instance
		var meshInstance = new MeshInstance3D();
		meshInstance.Mesh = mesh;
		meshInstance.Name = $"Chunk_{chunkCoord.X}_{chunkCoord.Y}";
		meshInstance.Position = new Vector3(worldOffset.X, 0, worldOffset.Y);
		meshInstance.MaterialOverride = _terrainMaterial;
		meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
		AddChild(meshInstance);

		// Create static body for collision
		var staticBody = new StaticBody3D();
		staticBody.CollisionLayer = 1;
		staticBody.CollisionMask = 1;
		meshInstance.AddChild(staticBody);

		// Create collision shape from mesh
		var collisionShape = new CollisionShape3D();
		var concaveShape = new ConcavePolygonShape3D();
		concaveShape.Data = chunkData.CollisionFaces;
		collisionShape.Shape = concaveShape;
		staticBody.AddChild(collisionShape);

		// Instantiate world features
		foreach (var dungeon in chunkData.DungeonEntrances)
		{
			CreateDungeonMarker(dungeon, staticBody);
		}

		foreach (var boss in chunkData.BossSpawns)
		{
			CreateBossMarker(boss, staticBody);
		}

		bool hasExistingProps = chunkData.Props.Count > 0;
		var (propsNode, props) = _propSpawner.SpawnPropsForChunk(
			chunkData,
			chunkCoord,
			WorldData.ChunkSize,
			WorldData.Seed,
			hasExistingProps
		);

		if (!hasExistingProps)
		{
			chunkData.Props = props;
		}

		meshInstance.AddChild(propsNode);

		_loadedChunks[chunkCoord] = new LoadedChunk
		{
			ChunkCoord = chunkCoord,
			Body = staticBody,
			MeshInstance = meshInstance,
			CollisionShape = collisionShape,
			Props = props
		};
		//GD.Print($"[LOAD] Chunk {chunkCoord}: worldOffset = ({worldOffsetX}, {worldOffsetZ}), ChunkSize = ({WorldData.ChunkSize.X}, {WorldData.ChunkSize.Y})");
	}

	private void UnloadChunk(Vector2I chunkCoord)
	{
		if (_loadedChunks.TryGetValue(chunkCoord, out var chunk))
		{
			chunk.Body.QueueFree();
			_loadedChunks.Remove(chunkCoord);
		}
	}

	private void CreateDungeonMarker(DungeonEntrance dungeon, Node3D parent)
	{
		var marker = new MeshInstance3D();
		var mesh = new CylinderMesh
		{
			Height = 3.0f,
			TopRadius = 0.5f,
			BottomRadius = 0.5f
		};

		marker.Mesh = mesh;
		marker.Position = dungeon.Position - parent.GlobalPosition + new Vector3(0, 1.5f, 0);

		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.8f, 0.3f, 0.1f),
			EmissionEnabled = true,
			Emission = new Color(0.8f, 0.3f, 0.1f),
			EmissionEnergyMultiplier = 2.0f
		};

		marker.MaterialOverride = material;
		parent.AddChild(marker);

		GD.Print($"Dungeon marker created at {dungeon.Position}");
	}

	private void CreateBossMarker(BossSpawn boss, Node3D parent)
	{
		var marker = new MeshInstance3D();
		var mesh = new SphereMesh
		{
			Radius = 1.0f,
			Height = 2.0f
		};

		marker.Mesh = mesh;
		marker.Position = boss.Position - parent.GlobalPosition + new Vector3(0, 1.0f, 0);

		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.0f, 0.0f, 0.0f),
			EmissionEnabled = true,
			Emission = new Color(1.0f, 0.0f, 0.0f),
			EmissionEnergyMultiplier = 3.0f
		};

		marker.MaterialOverride = material;
		parent.AddChild(marker);

		GD.Print($"Boss marker created at {boss.Position}");
	}

	public (Vector2[] positions, string[] types) GetPOI()
	{
		Vector2[] nearest = [];
		string[] types = [];

		foreach (var chunk in _loadedChunks.Values)
		{
			if (!WorldData.Chunks.TryGetValue(chunk.ChunkCoord, out var chunkData))
			{
				GD.PrintErr($"Chunk {chunk.ChunkCoord} not found in world data!");
				return (nearest, types);
			}

			foreach (var dungeon in chunkData.DungeonEntrances)
			{
				var position = new Vector2(dungeon.Position.X, dungeon.Position.Z);
				nearest = nearest.Append(position).ToArray();
				types = types.Append("dungeon").ToArray();
			}

			foreach (var boss in chunkData.BossSpawns)
			{
				var position = new Vector2(boss.Position.X, boss.Position.Z);
				nearest = nearest.Append(position).ToArray();
				types = types.Append("boss").ToArray();
			}
		}

		return (nearest, types);
	}

	private PropDefinition[] LoadPropDefinitions()
	{
		// For now, create temporary prop definitions with generated meshes
		// Later you'll replace this with .tres file loading

		var treeDefinition = new PropDefinition
		{
			PropName = "OakTree",
			Mesh = PropMeshGenerator.CreateTreeMesh(), // Generate mesh immediately
			Material = CreateSimpleMaterial(new Color(0.3f, 0.5f, 0.2f)), // Green
			SpawnDensity = 0.05f,
			MinHeight = 0.0f,
			MaxHeight = 15.0f,
			MaxSlope = 0.3f,
			ScaleRange = new Vector2(0.9f, 1.3f),
			RandomRotation = true,
			CollisionType = PropCollisionType.Solid,
			Harvestable = true,
			AffectedByWind = true,
			SwayStiffness = 0.7f,
			ResourceType = "Wood",
			ResourceAmount = 20,
			RespawnTime = 120.0f
		};

		var rockDefinition = new PropDefinition
		{
			PropName = "Rock",
			Mesh = PropMeshGenerator.CreateRockMesh(),
			Material = CreateSimpleMaterial(new Color(0.5f, 0.5f, 0.5f)), // Gray
			SpawnDensity = 0.03f,
			MinHeight = -5.0f,
			MaxHeight = 20.0f,
			MaxSlope = 0.6f,
			ScaleRange = new Vector2(0.8f, 1.4f),
			RandomRotation = true,
			CollisionType = PropCollisionType.Solid,
			Harvestable = true,
			AffectedByWind = false,
			ResourceType = "Stone",
			ResourceAmount = 15,
			RespawnTime = 180.0f
		};

		var bushDefinition = new PropDefinition
		{
			PropName = "Bush",
			Mesh = PropMeshGenerator.CreateBushMesh(),
			Material = CreateSimpleMaterial(new Color(0.2f, 0.6f, 0.2f)), // Darker green
			SpawnDensity = 0.08f,
			MinHeight = 0.0f,
			MaxHeight = 12.0f,
			MaxSlope = 0.4f,
			ScaleRange = new Vector2(0.7f, 1.1f),
			RandomRotation = true,
			CollisionType = PropCollisionType.Partial,
			Harvestable = true,
			AffectedByWind = true,
			SwayStiffness = 0.3f,
			ResourceType = "Berries",
			ResourceAmount = 5,
			RespawnTime = 60.0f
		};

		var grassDefinition = new PropDefinition
		{
			PropName = "Grass",
			Mesh = PropMeshGenerator.CreateGrassPatch(),
			Material = CreateSimpleMaterial(new Color(0.4f, 0.7f, 0.3f)), // Bright green
			SpawnDensity = 0.2f,
			MinHeight = -2.0f,
			MaxHeight = 8.0f,
			MaxSlope = 0.5f,
			ScaleRange = new Vector2(0.9f, 1.2f),
			RandomRotation = true,
			CollisionType = PropCollisionType.None,
			Harvestable = false,
			AffectedByWind = true,
			SwayStiffness = 0.1f
		};

		return new PropDefinition[] { treeDefinition, rockDefinition, bushDefinition, grassDefinition };
	}

	private StandardMaterial3D CreateSimpleMaterial(Color color)
	{
		var material = new StandardMaterial3D();
		material.AlbedoColor = color;
		material.Roughness = 0.8f;
		material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded; // For low-poly look
		return material;
	}
}
