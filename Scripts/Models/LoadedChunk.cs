using System.Collections.Generic;
using Godot;
public class LoadedChunk
{
    public Vector2I ChunkCoord;
    public MeshInstance3D MeshInstance;
    public StaticBody3D Body;
    public CollisionShape3D CollisionShape;
    public List<HarvestableEntity> Harvestables = new List<HarvestableEntity>();
}