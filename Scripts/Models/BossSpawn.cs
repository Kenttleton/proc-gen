using Godot;
using System;

public class BossSpawn
{
    public Vector3 Position;
    public BossType BossType;
    public BiomeType Biome;
    public BossSpawn(Vector3 position, BossType bossType, BiomeType biome)
    {
        Position = position;
        BossType = bossType;
        Biome = biome;
    }
}
