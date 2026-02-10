using Godot;
using System.Collections.Generic;

public class WorldData
{
    public int Seed;
    // X, Z size per chunk
    public Vector2I ChunkSize;
    // X, Z order chunk coordinate system
    public Vector2I WorldSizeChunks;
    public float HeightScale;
    // 
    public Dictionary<Vector2I, ChunkData> Chunks = new();

    public void Serialize(FileAccess file)
    {
        var writer = new BinaryStreamWriter();

        writer.WriteInt(Seed);
        writer.WriteVector2I(ChunkSize);
        writer.WriteVector2I(WorldSizeChunks);
        writer.WriteFloat(HeightScale);

        // Write chunks
        writer.WriteInt(Chunks.Count);
        var count = 1;
        foreach (var chunk in Chunks.Values)
        {
            var chunkData = chunk.Serialize();
            writer.WriteByteArray(chunkData);
            count++;
        }
        var success = file.StoreBuffer(writer.GetBytes());
        GD.Print($"Serialized {count}/{Chunks.Count} chunks {(success ? "successfully" : "unsuccessfully")}");
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

        // Read chunks
        int expectedChunkCount = reader.ReadInt();
        int count = 0;
        for (int i = 0; i < expectedChunkCount; i++)
        {
            var chunk = ChunkData.Deserialize(reader.ReadByteArray());
            worldData.Chunks[chunk.ChunkCoord] = chunk;
            count++;
        }
        GD.Print($"Deserialized {count}/{expectedChunkCount} chunks");
        return worldData;
    }
}