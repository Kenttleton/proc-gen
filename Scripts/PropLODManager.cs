using Godot;
using System.Collections.Generic;

public partial class PropLODManager : Node3D
{
	[Export] public Node3D Player;
	[Export] public float UpdateInterval = 0.5f;
	[Export] public float LOD0Distance = 30.0f;  // Full detail
	[Export] public float LOD1Distance = 60.0f;  // Medium detail
	[Export] public float LOD2Distance = 120.0f; // Low detail
	[Export] public float CullDistance = 200.0f; // Hide completely

	private float _updateTimer = 0.0f;
	private List<LODGroup> _lodGroups = new();

	public override void _Process(double delta)
	{
		if (Player == null)
			return;

		_updateTimer += (float)delta;
		if (_updateTimer < UpdateInterval)
			return;

		_updateTimer = 0.0f;
		UpdateLODs();
	}

	public void RegisterLODGroup(LODGroup group)
	{
		_lodGroups.Add(group);
	}

	public void UnregisterLODGroup(LODGroup group)
	{
		_lodGroups.Remove(group);
	}

	private void UpdateLODs()
	{
		Vector3 playerPos = Player.GlobalPosition;

		foreach (var group in _lodGroups)
		{
			if (!IsInstanceValid(group.Node))
				continue;

			float distance = playerPos.DistanceTo(group.ChunkCenter);

			// Determine LOD level
			int targetLOD = -1; // -1 = culled

			if (distance < LOD0Distance)
				targetLOD = 0;
			else if (distance < LOD1Distance)
				targetLOD = 1;
			else if (distance < LOD2Distance)
				targetLOD = 2;
			else if (distance < CullDistance)
				targetLOD = 3; // Ultra low

			// Update visibility
			if (targetLOD != group.CurrentLOD)
			{
				ApplyLOD(group, targetLOD);
			}
		}
	}

	private void ApplyLOD(LODGroup group, int lodLevel)
	{
		group.CurrentLOD = lodLevel;

		if (lodLevel == -1)
		{
			group.Node.Visible = false;
			return;
		}

		group.Node.Visible = true;

		// For MultiMeshInstance, we can't change mesh directly, but we can:
		// 1. Hide high-poly instances
		// 2. Show simplified versions
		// 3. Reduce draw distance via AABB manipulation

		if (group.Node is MultiMeshInstance3D multiMesh)
		{
			// Adjust visibility range (Godot's built-in LOD)
			multiMesh.VisibilityRangeBegin = GetLODRangeBegin(lodLevel);
			multiMesh.VisibilityRangeEnd = GetLODRangeEnd(lodLevel);
		}
	}

	private float GetLODRangeBegin(int lod)
	{
		return lod switch
		{
			0 => 0.0f,
			1 => LOD0Distance,
			2 => LOD1Distance,
			3 => LOD2Distance,
			_ => 0.0f
		};
	}

	private float GetLODRangeEnd(int lod)
	{
		return lod switch
		{
			0 => LOD0Distance,
			1 => LOD1Distance,
			2 => LOD2Distance,
			3 => CullDistance,
			_ => CullDistance
		};
	}
}

public class LODGroup
{
	public Node3D Node;
	public Vector3 ChunkCenter;
	public int CurrentLOD = 0;
}