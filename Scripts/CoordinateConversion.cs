using Godot;
public class CoordinateConversion
{
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

    public static float DistanceBetween(Vector2 a, Vector2 b)
    {
        return Mathf.Sqrt(Mathf.Pow(b.X - a.X, 2) + Mathf.Pow(b.Y - a.Y, 2));
    }
}