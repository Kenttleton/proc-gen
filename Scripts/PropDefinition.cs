using Godot;

[GlobalClass]
public partial class PropDefinition : Resource
{
	[ExportGroup("Basic Info")]
	[Export] public string PropName = "Tree";
	[Export] public Mesh Mesh;
	[Export] public Material Material;

	[ExportGroup("Spawning")]
	[Export] public float SpawnDensity = 0.1f; // Chance per terrain vertex
	[Export] public float MinHeight = -5.0f;
	[Export] public float MaxHeight = 20.0f;
	[Export] public float MaxSlope = 0.5f; // Steepness threshold
	public BiomeType[] AllowedBiomes = { BiomeType.Forest };

	[ExportGroup("Randomization")]
	[Export] public Vector2 ScaleRange = new Vector2(0.8f, 1.2f);
	[Export] public bool RandomRotation = true;

	[ExportGroup("Physics")]
	[Export] public PropCollisionType CollisionType = PropCollisionType.Solid;
	[Export] public bool Harvestable = false;
	[Export] public string DepletedPropName = ""; // Reference to depleted version

	[ExportGroup("Wind/Animation")]
	[Export] public bool AffectedByWind = true;
	[Export(PropertyHint.Range, "0,1")] public float SwayStiffness = 0.5f; // 0=grass, 1=rigid tree

	[ExportGroup("Harvesting (if applicable)")]
	[Export] public string ResourceType = "Wood";
	[Export] public int ResourceAmount = 10;
	[Export] public float RespawnTime = 60.0f; // Seconds
}

public enum PropCollisionType
{
	None,        // Grass - visual only
	Partial,     // Bushes - can walk through but provides cover
	Solid        // Trees/rocks - full collision
}