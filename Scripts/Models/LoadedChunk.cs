using System.Collections.Generic;
using Godot;
public class RenderedChunk
{
    public Vector2I ChunkCoord;
    public MeshInstance3D MeshInstance;
    public StaticBody3D Body;
    public CollisionShape3D CollisionShape;
    public List<PropInstanceData> Props = new List<PropInstanceData>();
}