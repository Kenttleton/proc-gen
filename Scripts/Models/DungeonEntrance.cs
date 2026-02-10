using Godot;
using System;

public class DungeonEntrance
{
    public Vector3 Position;
    public Vector3 FacingDirection;
    public BiomeType Biome;

    public DungeonEntrance(Vector3 position, Vector3 facingDirection, BiomeType biome)
    {
        Position = position;
        FacingDirection = facingDirection;
        Biome = biome;
    }
}