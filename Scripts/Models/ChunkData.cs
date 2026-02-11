using Godot;
using System;
using System.Collections.Generic;
public class ChunkData
{
    // Chunk coordinate in chunk space (not world space)
    public Vector2I ChunkCoord;
    public float[] HeightData;
    public Vector3[] Vertices;
    public int[] Indices;
    public Vector3[] Normals;
    public Vector2[] UVs;
    public Vector3[] CollisionFaces;

    // Chunk feature data
    public List<DungeonEntrance> DungeonEntrances = new();
    public List<BossSpawn> BossSpawns = new();
    public List<PropInstanceData> Props = new();

    // Write to file
    public byte[] Serialize()
    {
        var writer = new BinaryStreamWriter();

        writer.WriteVector2I(ChunkCoord);
        writer.WriteFloatArray(HeightData);
        writer.WriteVector3Array(Vertices);
        writer.WriteIntArray(Indices);
        writer.WriteVector3Array(Normals);
        writer.WriteVector2Array(UVs);
        writer.WriteVector3Array(CollisionFaces);

        writer.WriteInt(DungeonEntrances.Count);
        foreach (var dungeon in DungeonEntrances)
        {
            writer.WriteVector3(dungeon.Position);
            writer.WriteVector3(dungeon.FacingDirection);
            writer.WriteByte((byte)dungeon.Biome);
        }

        writer.WriteInt(BossSpawns.Count);
        foreach (var boss in BossSpawns)
        {
            writer.WriteVector3(boss.Position);
            writer.WriteByte((byte)boss.BossType);
            writer.WriteByte((byte)boss.Biome);
        }

        writer.WriteInt(Props.Count);
        foreach (var prop in Props)
        {
            writer.WriteString(prop.PropName);
            writer.WriteVector3(prop.Position);
            writer.WriteVector3(prop.Scale);
            writer.WriteFloat(prop.RotationY);
            writer.WriteBool(prop.IsActive);
            writer.WriteDouble(prop.RespawnTime);
        }

        return writer.GetBytes();
    }

    // Read from file
    public static ChunkData Deserialize(byte[] data)
    {
        var reader = new BinaryStreamReader(data);
        var chunk = new ChunkData();

        chunk.ChunkCoord = reader.ReadVector2I();
        chunk.HeightData = reader.ReadFloatArray();
        chunk.Vertices = reader.ReadVector3Array();
        chunk.Indices = reader.ReadIntArray();
        chunk.Normals = reader.ReadVector3Array();
        chunk.UVs = reader.ReadVector2Array();
        chunk.CollisionFaces = reader.ReadVector3Array();

        int dungeonCount = reader.ReadInt();
        for (int i = 0; i < dungeonCount; i++)
        {
            chunk.DungeonEntrances.Add(new DungeonEntrance(
                reader.ReadVector3(),
                reader.ReadVector3(),
                (BiomeType)reader.ReadSingleByte()
            ));
        }

        int bossCount = reader.ReadInt();
        for (int i = 0; i < bossCount; i++)
        {
            chunk.BossSpawns.Add(new BossSpawn(
                reader.ReadVector3(),
                (BossType)reader.ReadSingleByte(),
                (BiomeType)reader.ReadSingleByte()
            ));
        }

        int propCount = reader.ReadInt();
        for (int i = 0; i < propCount; i++)
        {
            chunk.Props.Add(new PropInstanceData
            {
                PropName = reader.ReadString(),
                Position = reader.ReadVector3(),
                Scale = reader.ReadVector3(),
                RotationY = reader.ReadFloat(),
                IsActive = reader.ReadBool(),
                RespawnTime = reader.ReadDouble()
            });
        }

        return chunk;
    }
}