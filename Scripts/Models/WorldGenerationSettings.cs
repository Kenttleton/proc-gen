using Godot;

/// <summary>
/// Settings for world generation
/// </summary>
[GlobalClass]
public partial class WorldGenerationSettings : Resource
{
    [Export] public int Seed;
    [Export] public int Regions;
    [Export] public Vector2I WorldSizeChunks;
    [Export] public Vector2I RegionSizeChunkMin;
    [Export] public Vector2I ChunkSize;
    [Export] public string WorldDataLookupPath = "res://Data/world_data/world_lookup.dat";
    [Export] public string WorldMetadataPath = "res://Data/world_data/world_metadata.dat";
    [Export] public string ChunkDataDirectory = "res://Data/world_data/chunks/";
    [Export] public Vector3 PlayerStartPosition;
    [Export] public float HeightScale;
    [Export] public float NoiseScale;
}