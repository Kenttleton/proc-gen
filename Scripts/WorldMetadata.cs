using Godot;
using System.Collections.Generic;

public class RegionData
{
    public Vector2I RegionCoord;
    public Dictionary<Vector2I, ChunkData> Chunks = new();
    public void Serialize(BinaryStreamWriter writer)
    {
        writer.WriteVector2I(RegionCoord);
        writer.WriteInt(Chunks.Count);
        foreach (var chunk in Chunks.Values)
        {
            var chunkData = chunk.Serialize();
            writer.WriteByteArray(chunkData);
        }
    }

    public static RegionData Deserialize(byte[] data)
    {
        var reader = new BinaryStreamReader(data);
        var regionData = new RegionData
        {
            RegionCoord = reader.ReadVector2I()
        };
        int chunkCount = reader.ReadInt();
        for (int i = 0; i < chunkCount; i++)
        {
            var chunk = ChunkData.Deserialize(reader.ReadByteArray());
            regionData.Chunks[chunk.ChunkCoord] = chunk;
        }
        return regionData;
    }
}

public class ChunkMetadata
{
    public bool IsEmpty;
    public bool HasWater;
    public bool HasTrees;
    public bool HasRocks;
}

public class WorldMetadata
{
    public int Seed;
    public Vector2I WorldSizeChunks;
    public Vector2I ChunkSize; // Number of meters in X and Z per chunk
    public float HeightScale;
    public string WorldDataLookupPath;
    public Vector3 PlayerStartPosition = Vector3.Zero;

    public void Serialize(FileAccess file)
    {
        var writer = new BinaryStreamWriter();

        writer.WriteInt(Seed);
        writer.WriteVector2I(WorldSizeChunks);
        writer.WriteVector2I(ChunkSize);
        writer.WriteFloat(HeightScale);
        writer.WriteString(WorldDataLookupPath);
        writer.WriteVector3(PlayerStartPosition);

        var success = file.StoreBuffer(writer.GetBytes());
        GD.Print($"Serialized world metadata {(success ? "successfully" : "unsuccessfully")}");
    }

    public static WorldMetadata Deserialize(FileAccess file)
    {
        var fileSize = (int)(file.GetLength() - file.GetPosition());
        var reader = new BinaryStreamReader(file.GetBuffer(fileSize));
        var worldMetadata = new WorldMetadata
        {
            Seed = reader.ReadInt(),
            WorldSizeChunks = reader.ReadVector2I(),
            ChunkSize = reader.ReadVector2I(),
            HeightScale = reader.ReadFloat(),
            WorldDataLookupPath = reader.ReadString(),
            PlayerStartPosition = reader.ReadVector3()
        };
        GD.Print($"Deserialized world metadata successfully");
        return worldMetadata;
    }
}