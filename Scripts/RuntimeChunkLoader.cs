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

		_loadedChunks[chunkCoord] = new LoadedChunk
		{
			ChunkCoord = chunkCoord,
			Body = staticBody,
			MeshInstance = meshInstance,
			CollisionShape = collisionShape
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

	public Vector2[] GetPOI()
	{
		Vector2[] nearest = [];

		foreach (var chunk in _loadedChunks.Values)
		{
			if (!WorldData.Chunks.TryGetValue(chunk.ChunkCoord, out var chunkData))
			{
				GD.PrintErr($"Chunk {chunk.ChunkCoord} not found in world data!");
				return nearest;
			}

			foreach (var dungeon in chunkData.DungeonEntrances)
			{
				var position = new Vector2(dungeon.Position.X, dungeon.Position.Z);
				nearest = nearest.Append(position).ToArray();
			}

			foreach (var boss in chunkData.BossSpawns)
			{
				var position = new Vector2(boss.Position.X, boss.Position.Z);
				nearest = nearest.Append(position).ToArray();
			}
		}

		return nearest;
	}
}
