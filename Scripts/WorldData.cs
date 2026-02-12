using Godot;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;

public class WorldData
{
    public int Seed;
    public Vector2I WorldRegions; // Number of regions in X and Z
    public Vector2I RegionSize; // Number of chunks per region in X and Z
    public Vector2I ChunkSize; // Number of meters in X and Z per chunk
    public Vector2I WorldSizeChunks => WorldRegions * RegionSize; // Total world size in chunks
    public float HeightScale;
    public Vector3 PlayerStartPosition = Vector3.Zero;
    public Dictionary<Vector2I, RegionData> Regions = new();


    public Dictionary<Vector2I, ChunkMetadata> Serialize(FileAccess file)
    {
        var writer = new BinaryStreamWriter();

        writer.WriteInt(Seed);
        writer.WriteVector2I(WorldRegions);
        writer.WriteVector2I(RegionSize);
        writer.WriteVector2I(ChunkSize);
        writer.WriteVector2I(WorldSizeChunks);
        writer.WriteFloat(HeightScale);

        // Write regions
        writer.WriteInt(Regions.Count);
        var count = 1;
        foreach (var region in Regions.Values)
        {
            var regionData = region.Serialize();
            writer.WriteByteArray(regionData);
            count++;
        }
        var success = file.StoreBuffer(writer.GetBytes());
        GD.Print($"Serialized {count}/{Regions.Count} regions {(success ? "successfully" : "unsuccessfully")}");
    }

    public static WorldData Deserialize(FileAccess file)
    {
        var fileSize = (int)(file.GetLength() - file.GetPosition());
        var reader = new BinaryStreamReader(file.GetBuffer(fileSize));
        var worldData = new WorldData
        {
            Seed = reader.ReadInt(),
            ChunkSize = reader.ReadVector2I(),
            WorldSizeChunks = reader.ReadVector2I(),
            HeightScale = reader.ReadFloat()
        };

        // Read regions
        int expectedRegionCount = reader.ReadInt();
        int count = 0;
        for (int i = 0; i < expectedRegionCount; i++)
        {
            var region = RegionData.Deserialize(reader.ReadByteArray());
            worldData.Regions[region.RegionCoord] = region;
            count++;
        }
        GD.Print($"Deserialized {count}/{expectedRegionCount} regions successfully");
        return worldData;
    }
}

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

public class FileMap
{
    public string FilePath;
    public ulong FilePosition;
    public bool IsCompressed;
    public ChunkMetadata Metadata;
}

public class WorldMetadata
{
    public int Seed;
    public Vector2I WorldRegions; // Number of regions in X and Z
    public Vector2I RegionSize; // Number of chunks per region in X and Z
    public Vector2I ChunkSize; // Number of meters in X and Z per chunk
    public Vector2I WorldSizeChunks => WorldRegions * RegionSize; // Total world size in chunks
    public float HeightScale;
    public string WorldDataLookupPath;
    public Vector3 PlayerStartPosition = Vector3.Zero;

    public void Serialize(FileAccess file)
    {
        var writer = new BinaryStreamWriter();

        writer.WriteInt(Seed);
        writer.WriteVector2I(WorldRegions);
        writer.WriteVector2I(RegionSize);
        writer.WriteVector2I(ChunkSize);
        writer.WriteVector2I(WorldSizeChunks);
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
            WorldRegions = reader.ReadVector2I(),
            RegionSize = reader.ReadVector2I(),
            ChunkSize = reader.ReadVector2I(),
            HeightScale = reader.ReadFloat(),
            WorldDataLookupPath = reader.ReadString(),
            PlayerStartPosition = reader.ReadVector3()
        };
        GD.Print($"Deserialized world metadata successfully");
        return worldMetadata;
    }
}