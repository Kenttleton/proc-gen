using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class OldRuntimeChunkLoader : Node3D
{
	private Node3D Player;
	private OldWorldGenerator WorldGenerator;
	private int RenderDistanceChunks;
	private float UpdateInterval;

	public OldRuntimeChunkLoader(Node3D player, OldWorldGenerator worldGenerator, int renderDistanceChunks, float updateInterval)
	{
		Player = player;
		WorldGenerator = worldGenerator;
		RenderDistanceChunks = renderDistanceChunks;
		UpdateInterval = updateInterval;
	}

	private OldWorldGenerator.WorldData _worldData;
	private Dictionary<Vector2I, LoadedChunk> _loadedChunks = new();
	private Vector2I _lastPlayerChunk = new Vector2I(int.MaxValue, int.MaxValue);
	private float _updateTimer = 0.0f;

	private class LoadedChunk
	{
		public Vector2I ChunkCoord;
		public StaticBody3D Body;
		public MeshInstance3D MeshInstance;
		public CollisionShape3D CollisionShape;
	}

	public override void _Ready()
	{
		if (WorldGenerator == null)
		{
			GD.PrintErr("WorldGenerator not assigned!");
			return;
		}

		if (Player == null)
		{
			GD.PrintErr("Player not assigned!");
			return;
		}

		// Load world data
		_worldData = WorldGenerator.GetWorldData();

		if (_worldData == null || _worldData.Chunks.Count == 0)
		{
			GD.PrintErr("No world data available! Generate world first.");
			return;
		}

		GD.Print($"Runtime loader initialized with {_worldData.Chunks.Count} chunks available");

		// Initial load
		UpdateVisibleChunks(true);
	}

	public override void _Process(double delta)
	{
		_updateTimer += (float)delta;

		if (_updateTimer >= UpdateInterval)
		{
			_updateTimer = 0.0f;
			UpdateVisibleChunks(false);
		}
	}

	private void UpdateVisibleChunks(bool forceUpdate)
	{
		Vector2I playerChunk = WorldToChunkCoord(Player.GlobalPosition);

		if (playerChunk == _lastPlayerChunk && !forceUpdate)
			return;

		_lastPlayerChunk = playerChunk;

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
					if (_worldData.Chunks.ContainsKey(chunkCoord))
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
		int chunkSize = _worldData.ChunkSize - 1;
		int chunkX = Mathf.FloorToInt(worldPos.X / chunkSize);
		int chunkZ = Mathf.FloorToInt(worldPos.Z / chunkSize);
		return new Vector2I(chunkX, chunkZ);
	}

	private void LoadChunk(Vector2I chunkCoord)
	{
		if (!_worldData.Chunks.TryGetValue(chunkCoord, out var chunkData))
		{
			GD.PrintErr($"Chunk {chunkCoord} not found in world data!");
			return;
		}

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

		// Create static body
		var staticBody = new StaticBody3D();
		staticBody.Name = $"Chunk_{chunkCoord.X}_{chunkCoord.Y}";

		int worldOffsetX = chunkCoord.X * (_worldData.ChunkSize - 1);
		int worldOffsetZ = chunkCoord.Y * (_worldData.ChunkSize - 1);
		staticBody.Position = new Vector3(worldOffsetX, 0, worldOffsetZ);

		staticBody.SetCollisionLayerValue(1, true);  // Set bit 1 (layer 1) to TRUE
		staticBody.SetCollisionMaskValue(1, false);  // Terrain doesn't need to detect

		AddChild(staticBody);

		// Create mesh instance
		var meshInstance = new MeshInstance3D();
		meshInstance.Mesh = mesh;

		var material = new StandardMaterial3D();
		material.AlbedoColor = new Color(0.3f, 0.6f, 0.2f);
		meshInstance.MaterialOverride = material;

		staticBody.AddChild(meshInstance);

		var arrayMesh = mesh as ArrayMesh;
		if (arrayMesh != null)
		{
			// Get the mesh surface arrays
			var arrays = arrayMesh.SurfaceGetArrays(0);

			// Create collision shape from mesh
			var collisionShape = new CollisionShape3D();

			// Create concave shape from the mesh data directly
			var shape = arrayMesh.CreateTrimeshShape();

			if (shape != null)
			{
				collisionShape.Shape = shape;
				staticBody.AddChild(collisionShape);

				GD.Print($"Chunk {chunkCoord} loaded with trimesh collision");
			}
			else
			{
				GD.PrintErr($"Failed to create trimesh shape for chunk {chunkCoord}");
			}
		}

		// Debug output
		GD.Print($"Chunk {chunkCoord}:");
		GD.Print($"  Position: {staticBody.GlobalPosition}");
		GD.Print($"  First vertex: {chunkData.Vertices[0]}");
		GD.Print($"  Last vertex: {chunkData.Vertices[chunkData.Vertices.Length - 1]}");
		GD.Print($"  Collision layer: {staticBody.CollisionLayer}");
		GD.Print($"  Vertices: {chunkData.Vertices.Length}");

		// Instantiate world features
		foreach (var dungeonPos in chunkData.DungeonEntrances)
		{
			CreateDungeonMarker(dungeonPos, staticBody);
		}

		foreach (var bossPos in chunkData.BossSpawns)
		{
			CreateBossMarker(bossPos, staticBody);
		}

		_loadedChunks[chunkCoord] = new LoadedChunk
		{
			ChunkCoord = chunkCoord,
			Body = staticBody,
			MeshInstance = meshInstance,
			//CollisionShape = collisionShape
		};

		GD.Print($"Chunk {chunkCoord} loaded successfully!");
	}

	private void UnloadChunk(Vector2I chunkCoord)
	{
		if (_loadedChunks.TryGetValue(chunkCoord, out var chunk))
		{
			chunk.Body.QueueFree();
			_loadedChunks.Remove(chunkCoord);
		}
	}

	private void CreateDungeonMarker(Vector3 position, Node3D parent)
	{
		var marker = new MeshInstance3D();
		var mesh = new CylinderMesh();
		mesh.Height = 3.0f;
		mesh.TopRadius = 0.5f;
		mesh.BottomRadius = 0.5f;

		marker.Mesh = mesh;
		marker.Position = position + new Vector3(0, 1.5f, 0);

		var material = new StandardMaterial3D();
		material.AlbedoColor = new Color(0.8f, 0.3f, 0.1f);
		material.EmissionEnabled = true;
		material.Emission = new Color(0.8f, 0.3f, 0.1f);
		material.EmissionEnergyMultiplier = 2.0f;
		marker.MaterialOverride = material;

		parent.AddChild(marker);
	}

	private void CreateBossMarker(Vector3 position, Node3D parent)
	{
		var marker = new MeshInstance3D();
		var mesh = new SphereMesh();
		mesh.Radius = 1.0f;
		mesh.Height = 2.0f;

		marker.Mesh = mesh;
		marker.Position = position + new Vector3(0, 1.0f, 0);

		var material = new StandardMaterial3D();
		material.AlbedoColor = new Color(1.0f, 0.0f, 0.0f);
		material.EmissionEnabled = true;
		material.Emission = new Color(1.0f, 0.0f, 0.0f);
		material.EmissionEnergyMultiplier = 3.0f;
		marker.MaterialOverride = material;

		parent.AddChild(marker);
	}

	public int GetLoadedChunkCount()
	{
		return _loadedChunks.Count;
	}

	public float GetHeightAtWorldPosition(Vector3 worldPos)
	{
		Vector2I chunkCoord = WorldToChunkCoord(worldPos);

		if (!_worldData.Chunks.TryGetValue(chunkCoord, out var chunkData))
			return 0.0f;

		// Convert world position to local chunk position
		int chunkSize = _worldData.ChunkSize - 1;
		int localX = Mathf.FloorToInt(worldPos.X) % chunkSize;
		int localZ = Mathf.FloorToInt(worldPos.Z) % chunkSize;

		if (localX < 0) localX += chunkSize;
		if (localZ < 0) localZ += chunkSize;

		if (localX >= 0 && localX < _worldData.ChunkSize &&
			localZ >= 0 && localZ < _worldData.ChunkSize)
		{
			return chunkData.HeightData[localZ * _worldData.ChunkSize + localX];
		}

		return 0.0f;
	}
}