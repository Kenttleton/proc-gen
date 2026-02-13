using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RuntimeChunkLoader : Node3D
{
	public WorldMetadata WorldData;

	public Node3D Player;
	public Vector2I LastPlayerChunk = new Vector2I(int.MaxValue, int.MaxValue);

	public int InMemoryRadiusChunks;
	public int AIRadiusChunks;
	public int RenderRadiusChunks;
	public float UpdateInterval;

	public Dictionary<Vector2I, ChunkData> InMemoryChunks = new();
	public List<Vector2I> AIChunks = new();
	public Dictionary<Vector2I, RenderedChunk> RenderedChunks = new();

	private float _updateTimer = 0.0f;
	private ShaderMaterial _terrainMaterial;
	private PropSpawner _propSpawner;

	public RuntimeChunkLoader(Node3D player, int inMemoryRadiusChunks, int airadiusChunks, int renderRadiusChunks, float updateInterval)
	{
		Player = player;
		InMemoryRadiusChunks = inMemoryRadiusChunks;
		AIRadiusChunks = airadiusChunks;
		RenderRadiusChunks = renderRadiusChunks;
		UpdateInterval = updateInterval;
	}

	public override void _Ready()
	{
		if (Player == null)
		{
			GD.PrintErr("Player not assigned!");
			return;
		}

		if (WorldData == null)
		{
			GD.PrintErr("No world data available! Generate world first.");
			return;
		}

		var materialBuilder = new TerrainMaterialBuilder();
		_terrainMaterial = materialBuilder.CreateTerrainMaterial();

		_propSpawner = new PropSpawner();
		_propSpawner.PropDefinitions = LoadPropDefinitions();
		AddChild(_propSpawner);
		UpdateInMemoryChunks();
		UpdateAIChunks();
		UpdateRenderedChunks();
	}

	public override void _Process(double delta)
	{
		_updateTimer += (float)delta;
		if (_updateTimer >= UpdateInterval)
		{
			_updateTimer = 0.0f;
			// MUST update player location for chunk calculations before updating chunks
			LastPlayerChunk = CoordinateConversion.WorldOffsetToChunkCoord(Player.GlobalPosition, WorldData.ChunkSize);
			UpdateInMemoryChunks();
			UpdateAIChunks();
			UpdateRenderedChunks();
		}
	}

	public void UpdateInMemoryChunks()
	{
		for (int z = -InMemoryRadiusChunks; z <= InMemoryRadiusChunks; z++)
		{
			for (int x = -InMemoryRadiusChunks; x <= InMemoryRadiusChunks; x++)
			{
				Vector2I chunkCoord = LastPlayerChunk + new Vector2I(x, z);
				if (new Vector2(x, z).Length() <= InMemoryRadiusChunks)
				{
					if (!InMemoryChunks.ContainsKey(chunkCoord))
					{
						// TODO: read chunk data from disk if not already in memory, then add to InMemoryChunks
						InMemoryChunks[chunkCoord] = null;
					}
				}
				// Unload chunks that are now out of range
				else if (InMemoryChunks.ContainsKey(chunkCoord))
				{
					InMemoryChunks.Remove(chunkCoord);
				}
			}
		}
	}

	public void UpdateAIChunks()
	{
		for (int z = -AIRadiusChunks; z <= AIRadiusChunks; z++)
		{
			for (int x = -AIRadiusChunks; x <= AIRadiusChunks; x++)
			{
				if (new Vector2(x, z).Length() <= AIRadiusChunks)
				{
					Vector2I chunkCoord = LastPlayerChunk + new Vector2I(x, z);
					if (!AIChunks.Contains(chunkCoord) && InMemoryChunks.ContainsKey(chunkCoord))
					{
						var inMemChunk = InMemoryChunks[chunkCoord];
						AIChunks.Add(chunkCoord);
						// TODO: initialize AI systems for this chunk (e.g. spawn enemies, fast forward pathfinding, etc.)
					}
				}
			}
		}
	}

	private void UpdateRenderedChunks()
	{
		var chunksToUnload = new List<Vector2I>();
		for (int z = -RenderRadiusChunks; z <= RenderRadiusChunks; z++)
		{
			for (int x = -RenderRadiusChunks; x <= RenderRadiusChunks; x++)
			{
				Vector2I chunkCoord = LastPlayerChunk + new Vector2I(x, z);
				if (new Vector2(x, z).Length() <= RenderRadiusChunks)
				{
					if (!RenderedChunks.ContainsKey(chunkCoord) && InMemoryChunks.ContainsKey(chunkCoord))
					{
						var renderedChunk = RenderChunk(InMemoryChunks[chunkCoord]);
						RenderedChunks.Add(chunkCoord, renderedChunk);
					}
					if (!InMemoryChunks.ContainsKey(chunkCoord))
						GD.PrintErr($"Chunk {chunkCoord} is being rendered but is not in memory!");
				}
				else if (RenderedChunks.ContainsKey(chunkCoord))
				{
					chunksToUnload.Add(chunkCoord);
				}
			}
		}

		foreach (var coord in chunksToUnload)
		{
			UnloadChunk(coord);
		}
	}



	private RenderedChunk RenderChunk(ChunkData chunkData)
	{
		Vector2I worldOffset = CoordinateConversion.ChunkCoordToWorldOffset(chunkData.ChunkCoord, WorldData.ChunkSize);

		var surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

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
		meshInstance.Name = $"Chunk_{chunkData.ChunkCoord.X}_{chunkData.ChunkCoord.Y}";
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
			chunkData.ChunkCoord,
			WorldData.ChunkSize,
			WorldData.Seed,
			hasExistingProps
		);

		if (!hasExistingProps)
		{
			chunkData.Props = props;
		}

		meshInstance.AddChild(propsNode);
		return new RenderedChunk
		{
			ChunkCoord = chunkData.ChunkCoord,
			MeshInstance = meshInstance,
			Body = staticBody,
			CollisionShape = collisionShape,
			Props = chunkData.Props
		};
	}

	private void UnloadChunk(Vector2I chunkCoord)
	{
		if (RenderedChunks.TryGetValue(chunkCoord, out var chunk))
		{
			chunk.Body.QueueFree();
			RenderedChunks.Remove(chunkCoord);
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

		foreach (var chunk in RenderedChunks.Values)
		{
			if (!InMemoryChunks.TryGetValue(chunk.ChunkCoord, out var chunkData))
			{
				GD.PrintErr($"Chunk {chunk.ChunkCoord} not found in memory!");
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
