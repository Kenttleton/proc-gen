using Godot;
public class CoordinateConversion
{
    // public static Vector2I WorldToChunkCoord(Vector3 worldPos, Vector2I ChunksPerRegion, Vector2I chunkSize)
    // {
    //     int chunkX = Mathf.FloorToInt(worldPos.X / ChunksPerRegion.X / (chunkSize.X - 1));
    //     int chunkZ = Mathf.FloorToInt(worldPos.Z / ChunksPerRegion.Y / (chunkSize.Y - 1));
    //     return new Vector2I(chunkX, chunkZ);
    // }

    // public static Vector3 ChunkToWorldCoord(Vector2I chunkCoord, Vector2I region, Vector2I chunksPerRegion, Vector2I chunkSize)
    // {
    //     float worldX = (region.X * chunksPerRegion.X * chunkSize.X) + chunkCoord.X;
    //     float worldZ = (region.Y * chunksPerRegion.Y * chunkSize.Y) + chunkCoord.Y;
    //     float worldY = 0;
    //     return new Vector3(worldX, worldY, worldZ);
    // }

    public static Vector2I WorldOffsetToChunkCoord(Vector3 worldPos, Vector2I chunkSize)
    {
        int chunkX = Mathf.FloorToInt(worldPos.X / (chunkSize.X - 1)); // -1 to match generation logic
        int chunkZ = Mathf.FloorToInt(worldPos.Z / (chunkSize.Y - 1)); // -1 to match generation logic
        return new Vector2I(chunkX, chunkZ);
    }

    public static Vector2I ChunkCoordToWorldOffset(Vector2I chunkCoord, Vector2I chunkSize)
    {
        int worldOffsetX = chunkCoord.X * (chunkSize.X - 1); // -1 to prevent gaps between chunks
        int worldOffsetZ = chunkCoord.Y * (chunkSize.Y - 1); // -1 to prevent gaps between chunks
        return new Vector2I(worldOffsetX, worldOffsetZ);
    }
}