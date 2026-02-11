using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PropSpawner : Node
{
	// Prop catalog (you'll populate this in the editor or via code)
	[Export] public PropDefinition[] PropDefinitions;

	// Performance settings
	[Export] public int MaxPropsPerChunk = 1000;
	[Export] public float POIExclusionRadius = 5.0f; // Don't spawn props too close to POIs
	[Export] public float MinPropDistance = 3.0f;
	[Export] public bool UseMultiMesh = true;

	private Dictionary<string, PropDefinition> _propCatalog = new();
	private List<Vector3> _largePropPositions = new();
	public override void _Ready()
	{
		// Build catalog for fast lookup
		foreach (var prop in PropDefinitions)
		{
			_propCatalog[prop.PropName] = prop;
		}
	}

	/// <summary>
	/// Spawns props for a chunk and returns visual nodes + harvestable entity data
	/// </summary>
	public (Node3D visualNode, List<PropInstanceData> props) SpawnPropsForChunk(
	ChunkData chunkData,
	Vector2I chunkCoord,
	Vector2I chunkSize,
	int seed,
	bool useExistingData = false)
	{
		var visualRoot = new Node3D();
		visualRoot.Name = $"Props_Chunk_{chunkCoord.X}_{chunkCoord.Y}";

		// Group props by definition for MultiMesh batching
		var propGroups = new Dictionary<PropDefinition, List<Transform3D>>();
		var props = new List<PropInstanceData>();

		if (useExistingData && chunkData.Props.Count > 0)
		{
			return LoadPropsFromData(chunkData, visualRoot);
		}

		var random = new Random(HashCode.Combine(seed, chunkCoord.X, chunkCoord.Y));
		_largePropPositions.Clear();

		int worldOffsetX = chunkCoord.X * (chunkSize.X - 1);
		int worldOffsetZ = chunkCoord.Y * (chunkSize.Y - 1);

		var exclusionZones = BuildExclusionZones(chunkData, worldOffsetX, worldOffsetZ);
		int propSpacing = Math.Max(1, (chunkSize.X - 1) / (int)Math.Sqrt(MaxPropsPerChunk));

		for (int z = 0; z < chunkSize.Y - 1; z += propSpacing) // Skip every other for performance
		{
			for (int x = 0; x < chunkSize.X - 1; x += propSpacing)
			{
				int index = z * chunkSize.X + x;

				// Add random offset within the spacing grid to avoid uniform patterns
				float randomOffsetX = (float)(random.NextDouble() * 2.0 - 1.0) * (propSpacing * 0.4f);
				float randomOffsetZ = (float)(random.NextDouble() * 2.0 - 1.0) * (propSpacing * 0.4f);

				float height = chunkData.HeightData[index];
				Vector3 worldPos = new Vector3(
					worldOffsetX + x + randomOffsetX,
					height,
					worldOffsetZ + z + randomOffsetZ
				);

				if (IsInExclusionZone(worldPos, exclusionZones))
					continue;

				// Get terrain normal for slope check
				Vector3 normal = chunkData.Normals[index];
				float slope = 1.0f - normal.Y; // 0 = flat, 1 = vertical

				// Try to spawn props here
				foreach (var propDef in PropDefinitions)
				{
					if (!CanSpawnProp(propDef, height, slope, worldPos, random))
						continue;

					// Create transform
					var transform = CreatePropTransform(propDef, worldPos, random);

					if (!propGroups.ContainsKey(propDef))
						propGroups[propDef] = new List<Transform3D>();

					propGroups[propDef].Add(transform);

					props.Add(new PropInstanceData
					{
						PropName = propDef.PropName,
						Position = worldPos,
						Scale = transform.Basis.Scale,
						RotationY = transform.Basis.GetEuler().Y,
						IsActive = true
					});

					if (propDef.CollisionType == PropCollisionType.Solid)
						_largePropPositions.Add(worldPos);

					break; // Only spawn one prop per location
				}
			}
		}

		// Create MultiMesh instances with LOD
		foreach (var kvp in propGroups)
		{
			var propDef = kvp.Key;
			var transforms = kvp.Value;

			if (transforms.Count == 0)
				continue;

			var multiMeshInstance = CreateMultiMeshInstanceWithLOD(propDef, transforms);

			// Check if creation was successful (null if mesh was missing)
			if (multiMeshInstance != null)
			{
				visualRoot.AddChild(multiMeshInstance);
			}
		}

		return (visualRoot, props);
	}

	private List<(Vector3 center, float radius)> BuildExclusionZones(
	  ChunkData chunkData,
	  int worldOffsetX,
	  int worldOffsetZ)
	{
		var zones = new List<(Vector3, float)>();

		// Add dungeons
		foreach (var dungeon in chunkData.DungeonEntrances)
		{
			zones.Add((dungeon.Position, POIExclusionRadius));
		}

		// Add bosses
		foreach (var boss in chunkData.BossSpawns)
		{
			zones.Add((boss.Position, POIExclusionRadius * 1.5f)); // Bigger for boss arenas
		}

		return zones;
	}

	private (Node3D visualNode, List<PropInstanceData> props) LoadPropsFromData(
	ChunkData chunkData,
	Node3D visualRoot)
	{
		var propGroups = new Dictionary<PropDefinition, List<Transform3D>>();
		var props = new List<PropInstanceData>();

		foreach (var propData in chunkData.Props)
		{
			// Find the prop definition
			if (!_propCatalog.TryGetValue(propData.PropName, out var propDef))
				continue;

			// Skip inactive (harvested) props
			if (!propData.IsActive)
				continue;

			// Recreate transform
			var transform = Transform3D.Identity;
			transform.Origin = propData.Position;
			transform.Basis = transform.Basis.Scaled(propData.Scale);
			transform.Basis = transform.Basis.Rotated(Vector3.Up, propData.RotationY);

			if (!propGroups.ContainsKey(propDef))
				propGroups[propDef] = new List<Transform3D>();

			propGroups[propDef].Add(transform);

			if (propDef.Harvestable)
			{
				props.Add(new PropInstanceData
				{
					PropName = propDef.PropName,
					Position = propData.Position,
					IsActive = propData.IsActive,
					RespawnTime = propData.RespawnTime,
				});
			}
		}

		// Create MultiMesh instances
		foreach (var kvp in propGroups)
		{
			var propDef = kvp.Key;
			var transforms = kvp.Value;

			if (transforms.Count == 0)
				continue;

			var multiMeshInstance = CreateMultiMeshInstanceWithLOD(propDef, transforms);

			if (multiMeshInstance != null)
			{
				visualRoot.AddChild(multiMeshInstance);
			}
		}

		return (visualRoot, props);
	}

	private bool IsInExclusionZone(Vector3 position, List<(Vector3 center, float radius)> zones)
	{
		foreach (var zone in zones)
		{
			float distSq = (position - zone.center).LengthSquared();
			if (distSq < zone.radius * zone.radius)
				return true;
		}
		return false;
	}

	private bool CanSpawnProp(PropDefinition prop, float height, float slope, Vector3 position, Random random)
	{
		// Height check
		if (height < prop.MinHeight || height > prop.MaxHeight)
			return false;

		// Slope check
		if (slope > prop.MaxSlope)
			return false;

		// Density/probability check
		if (random.NextDouble() > prop.SpawnDensity)
			return false;

		if (prop.CollisionType == PropCollisionType.Solid)
		{
			foreach (var existingPos in _largePropPositions)
			{
				float distSq = (position - existingPos).LengthSquared();
				if (distSq < MinPropDistance * MinPropDistance)
					return false;
			}
		}

		return true;
	}

	private Transform3D CreatePropTransform(PropDefinition prop, Vector3 position, Random random)
	{
		var transform = Transform3D.Identity;

		// Position
		transform.Origin = position;

		// Scale
		float scale = (float)(random.NextDouble() * (prop.ScaleRange.Y - prop.ScaleRange.X) + prop.ScaleRange.X);
		transform.Basis = transform.Basis.Scaled(Vector3.One * scale);

		// Rotation
		if (prop.RandomRotation)
		{
			float rotationY = (float)(random.NextDouble() * Mathf.Tau);
			transform.Basis = transform.Basis.Rotated(Vector3.Up, rotationY);
		}

		return transform;
	}

	private MultiMeshInstance3D CreateMultiMeshInstanceWithLOD(
	PropDefinition propDef,
	List<Transform3D> transforms)
	{
		if (propDef.Mesh == null)
		{
			GD.PrintErr($"PropDefinition '{propDef.PropName}' has no mesh assigned! Skipping.");
			return null;
		}

		var multiMesh = new MultiMesh();
		multiMesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
		multiMesh.Mesh = propDef.Mesh;
		multiMesh.InstanceCount = transforms.Count;

		for (int i = 0; i < transforms.Count; i++)
		{
			multiMesh.SetInstanceTransform(i, transforms[i]);
		}

		var instance = new MultiMeshInstance3D();
		instance.Multimesh = multiMesh;
		instance.Name = $"MultiMesh_{propDef.PropName}";
		instance.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;

		if (propDef.AffectedByWind)
		{
			var windMaterial = new ShaderMaterial();
			var shader = GD.Load<Shader>("res://Shaders/vegetation_wind.gdshader");

			if (shader == null)
			{
				GD.PrintErr("Wind shader not found! Using standard material instead.");
				instance.MaterialOverride = propDef.Material;
			}
			else
			{
				windMaterial.Shader = shader;
				windMaterial.SetShaderParameter("sway_stiffness", propDef.SwayStiffness);

				// Set color/texture parameters
				if (propDef.Material is StandardMaterial3D stdMat)
				{
					if (stdMat.AlbedoTexture != null)
					{
						windMaterial.SetShaderParameter("albedo_texture", stdMat.AlbedoTexture);
						windMaterial.SetShaderParameter("use_texture", true);
					}
					else
					{
						// Use the color from the material
						windMaterial.SetShaderParameter("albedo_color", stdMat.AlbedoColor);
						windMaterial.SetShaderParameter("use_texture", false);
					}
				}
				else
				{
					// Fallback to a default color if material isn't StandardMaterial3D
					windMaterial.SetShaderParameter("albedo_color", new Color(0.5f, 0.8f, 0.3f));
					windMaterial.SetShaderParameter("use_texture", false);
				}

				instance.MaterialOverride = windMaterial;
				instance.AddToGroup("vegetation");
			}
		}
		else
		{
			instance.MaterialOverride = propDef.Material;
		}

		// Enable Godot's built-in visibility range (LOD)
		instance.VisibilityRangeBegin = 0.0f;
		instance.VisibilityRangeEnd = 100.0f; // Adjust per prop type
		instance.VisibilityRangeBeginMargin = 5.0f;
		instance.VisibilityRangeEndMargin = 10.0f;
		instance.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self;

		// Collision
		AddCollisionToMultiMesh(instance, propDef, transforms);

		return instance;
	}

	private void AddCollisionToMultiMesh(MultiMeshInstance3D instance, PropDefinition propDef, List<Transform3D> transforms)
	{
		if (propDef.CollisionType == PropCollisionType.None)
			return;

		// For solid/partial collision, create a StaticBody3D with simple shapes
		// This is more efficient than per-instance collision

		var staticBody = new StaticBody3D();
		staticBody.Name = "Collision";

		// Set collision layers based on type
		if (propDef.CollisionType == PropCollisionType.Solid)
		{
			staticBody.CollisionLayer = 1; // World layer
			staticBody.CollisionMask = 0;
		}
		else // Partial
		{
			staticBody.CollisionLayer = 4; // Custom "partial" layer
			staticBody.CollisionMask = 0;
		}

		instance.AddChild(staticBody);

		// Create simple collision shapes for each instance
		foreach (var transform in transforms)
		{
			var shape = new CollisionShape3D();

			// Use simple capsule for trees, sphere for bushes
			if (propDef.PropName.Contains("Tree"))
			{
				var capsule = new CapsuleShape3D();
				capsule.Radius = 0.3f;
				capsule.Height = 3.0f;
				shape.Shape = capsule;
			}
			else
			{
				var sphere = new SphereShape3D();
				sphere.Radius = 0.5f;
				shape.Shape = sphere;
			}

			shape.GlobalTransform = transform;
			staticBody.AddChild(shape);
		}
	}
}
