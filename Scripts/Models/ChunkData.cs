using Godot;
using System;
using System.Collections.Generic;
public class ChunkData
{
    public Vector2I ChunkCoord;
    public Vector2I WorldCoord;
    public RegionType[] Region;
    public float Precipitation;
    public float[] HeightData;
    public Vector3[] Vertices;
    public int[] Indices;
    public Vector3[] Normals;
    public Vector2[] UVs;
    public Vector3[] CollisionFaces;

    // POI = Points of Interest - Bosses, Towns, Dungeons, etc (big destinations).
    public List<Placement> POI = new();
    // POD = Points of Disinterest - Props to fill the world and harvestable resources.
    public List<Placement> POD = new();

    // Write to file
    public byte[] Serialize()
    {
        var writer = new BinaryStreamWriter();

        writer.WriteVector2I(ChunkCoord);
        writer.WriteVector2I(WorldCoord);
        writer.WriteFloatArray(HeightData);
        writer.WriteVector3Array(Vertices);
        writer.WriteIntArray(Indices);
        writer.WriteVector3Array(Normals);
        writer.WriteVector2Array(UVs);
        writer.WriteVector3Array(CollisionFaces);

        writer.WriteInt(POI.Count);
        foreach (var poi in POI)
        {
            writer.WriteGuid(poi.ID);
            writer.WriteVector3(poi.Position);
            writer.WriteFloat(poi.RotationY);
            writer.WriteVector3(poi.Scale);
        }

        writer.WriteInt(POD.Count);
        foreach (var pod in POD)
        {
            writer.WriteGuid(pod.ID);
            writer.WriteVector3(pod.Position);
            writer.WriteFloat(pod.RotationY);
            writer.WriteVector3(pod.Scale);
        }

        return writer.GetBytes();
    }

    public static ChunkData Deserialize(byte[] data)
    {
        var reader = new BinaryStreamReader(data);
        var chunk = new ChunkData();

        chunk.ChunkCoord = reader.ReadVector2I();
        chunk.WorldCoord = reader.ReadVector2I();
        chunk.HeightData = reader.ReadFloatArray();
        chunk.Vertices = reader.ReadVector3Array();
        chunk.Indices = reader.ReadIntArray();
        chunk.Normals = reader.ReadVector3Array();
        chunk.UVs = reader.ReadVector2Array();
        chunk.CollisionFaces = reader.ReadVector3Array();

        int poiCount = reader.ReadInt();
        for (int i = 0; i < poiCount; i++)
        {
            chunk.POI.Add(new Placement
            {
                ID = reader.ReadGuid(),
                Position = reader.ReadVector3(),
                RotationY = reader.ReadFloat(),
                Scale = reader.ReadVector3()
            });
        }

        int podCount = reader.ReadInt();
        for (int i = 0; i < podCount; i++)
        {
            chunk.POI.Add(new Placement
            {
                ID = reader.ReadGuid(),
                Position = reader.ReadVector3(),
                RotationY = reader.ReadFloat(),
                Scale = reader.ReadVector3()
            });
        }

        return chunk;
    }
}