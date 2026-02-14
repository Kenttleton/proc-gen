using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class RegionGenerator
{
    // Region settings
    public class RegionSeed
    {
        public Vector2I Center; // In world space coordinates
        public RegionType Type;
        public float Influence;
        public float Radius; // In chunks
        public int MinLevel;
        public int MaxLevel;
    };
    private List<RegionSeed> _regionSeeds;
    private Vector2I _worldSize;
    private Vector2I _worldSizeChunks;
    private Vector2I _chunkSize;
    private int _seed;
    private int _regionRadiusMin; // In chunks
    private int _tutorialRadiusMin; // In chunks
    private int _maxLevel = 75;
    public RegionGenerator(int seed, Vector2I worldSize, Vector2I worldSizeChunks, int regionRadiusMin, int tutorialRadiusMin, Vector2I chunkSize, int maxLevel)
    {
        _seed = seed;
        _worldSize = worldSize;
        _worldSizeChunks = worldSizeChunks;
        _regionRadiusMin = regionRadiusMin;
        _tutorialRadiusMin = tutorialRadiusMin;
        _chunkSize = chunkSize;
        _maxLevel = maxLevel;
        GenerateRegionSeeds();
    }

    /// <summary>
    /// Generates region seeds with a focus on creating a natural progression from a central Tutorial region to other genres. 
    /// The Tutorial region is placed at the center of the world and has a smaller influence to ensure it covers around 500 chunks.
    /// This is calculated into world space. Will need converted to chunk space when assigning regions to chunks.
    /// </summary>
    public void GenerateRegionSeeds()
    {
        _regionSeeds = new List<RegionSeed>();
        var random = new Random(_seed);

        var regionTypes = new List<RegionType>([.. Enum.GetValues(typeof(RegionType)).Cast<RegionType>()]);

        // Create center points for each region type, with some random offset to avoid perfect grid layout
        List<Vector2I> seedPositions = new List<Vector2I> { new Vector2I((int)(_worldSize.X / 2), (int)(_worldSize.Y / 2)) };
        for (int i = 1; i < regionTypes.Count; i++)
        {
            var randX = random.Next((int)_regionRadiusMin, (int)(_worldSize.X - _regionRadiusMin));
            var randY = random.Next((int)_regionRadiusMin, (int)(_worldSize.Y - _regionRadiusMin));
            if (seedPositions.Any(pos => pos.DistanceTo(new Vector2I(randX, randY)) < _regionRadiusMin * 2))
            {
                i--; // Too close to existing seed, try again
                continue;
            }
            seedPositions.Add(new Vector2I(randX, randY));
        }

        // Sort seeds by distance from previous to create more natural progression (Tutorial -> SciFi -> Fantasy -> ...)
        List<Vector2I> sortedSeeds = new List<Vector2I> { seedPositions[0] };
        seedPositions.RemoveAt(0);
        for (int i = 0; i < seedPositions.Count; i++)
        {
            var nextSeed = seedPositions.OrderBy(pos => pos.DistanceTo(sortedSeeds.Last())).First();
            sortedSeeds.Add(nextSeed);
            seedPositions.Remove(nextSeed);
        }

        int levelRoundingError = _maxLevel % sortedSeeds.Count;
        int levelRangePerRegion = (_maxLevel - levelRoundingError) / sortedSeeds.Count;

        // Assign region seeds based on generated points, with random influence values to create more organic borders
        for (int i = 0; i < sortedSeeds.Count; i++)
        {
            var newLevelRange = levelRangePerRegion;
            if (levelRoundingError > 0)
            {
                newLevelRange++;
                levelRoundingError--;
            }
            if (i == 0)
            {
                regionTypes.Remove(RegionType.Tutorial);
                _regionSeeds.Add(new RegionSeed
                {
                    Center = sortedSeeds[i],
                    Type = RegionType.Tutorial,
                    Radius = _tutorialRadiusMin,
                    Influence = 1.0f,
                    MinLevel = 1,
                    MaxLevel = newLevelRange
                });
                continue;
            }
            var regionTypeIndex = random.Next(regionTypes.Count);
            _regionSeeds.Add(new RegionSeed
            {
                Center = sortedSeeds[i],
                Type = regionTypes[regionTypeIndex],
                Radius = _regionRadiusMin,
                Influence = Mathf.Clamp((float)random.NextDouble(), 0.0f, 0.9f),
                MinLevel = _regionSeeds.Last().MaxLevel,
                MaxLevel = _regionSeeds.Last().MaxLevel + newLevelRange
            });
            regionTypes.RemoveAt(regionTypeIndex);
        }

        GD.Print("=== Region Seeds Generated ===");
        foreach (var seed in _regionSeeds)
        {
            GD.Print($"{seed.Type}: Center at ({seed.Center.X:F0}, {seed.Center.Y:F0}), " +
                     $"Influence: {seed.Influence}" +
                     $"Level Range: {seed.MinLevel}-{seed.MaxLevel}");
        }
    }

    public RegionType GetRegionForChunk(Vector2I chunkCoord)
    {
        Vector2 chunkZeroPos = CoordinateConversion.ChunkCoordToWorldOffset(chunkCoord, _chunkSize);
        Vector2 chunkPos = new Vector2(chunkZeroPos.X + _chunkSize.X / 2f, chunkZeroPos.Y + _chunkSize.Y / 2f);

        float minDistance = (_chunkSize.X > _chunkSize.Y ? _chunkSize.X : _chunkSize.Y) * _regionRadiusMin;
        RegionType closestRegion = RegionType.Tutorial;

        foreach (var seed in _regionSeeds)
        {
            // Calculate weighted distance
            float distance = chunkPos.DistanceTo(seed.Center);

            // Apply influence weight (smaller influence = effectively farther away)
            float weightedDistance = distance / seed.Influence;

            if (weightedDistance < minDistance)
            {
                minDistance = weightedDistance;
                closestRegion = seed.Type;
            }
        }

        return closestRegion;
    }

    public float GetTransitionStrength(Vector2I chunkCoord)
    {
        Vector2 chunkZeroPos = CoordinateConversion.ChunkCoordToWorldOffset(chunkCoord, _chunkSize);
        Vector2 chunkPos = new Vector2(chunkZeroPos.X + _chunkSize.X / 2f, chunkZeroPos.Y + _chunkSize.Y / 2f);

        // Find closest and second-closest regions
        float closest = (_chunkSize.X > _chunkSize.Y ? _chunkSize.X : _chunkSize.Y) * _regionRadiusMin;
        float secondClosest = (_chunkSize.X > _chunkSize.Y ? _chunkSize.X : _chunkSize.Y) * _regionRadiusMin;

        foreach (var seed in _regionSeeds)
        {
            float distance = chunkPos.DistanceTo(seed.Center) / seed.Influence;

            if (distance < closest)
            {
                secondClosest = closest;
                closest = distance;
            }
            else if (distance < secondClosest)
            {
                secondClosest = distance;
            }
        }

        // If we're equally distant from two regions, we're on a border
        // Return 0.0 = pure region, 1.0 = perfect border/transition
        float borderDistance = secondClosest - closest;
        float transitionStrength = Mathf.Clamp(1.0f - (borderDistance / 20.0f), 0.0f, 1.0f);

        return transitionStrength;
    }

    public RegionType GetSecondaryRegion(Vector2I chunkCoord)
    {
        Vector2 chunkZeroPos = CoordinateConversion.ChunkCoordToWorldOffset(chunkCoord, _chunkSize);
        Vector2 chunkPos = new Vector2(chunkZeroPos.X + _chunkSize.X / 2f, chunkZeroPos.Y + _chunkSize.Y / 2f);

        float closest = (_chunkSize.X > _chunkSize.Y ? _chunkSize.X : _chunkSize.Y) * _regionRadiusMin;
        float secondClosest = (_chunkSize.X > _chunkSize.Y ? _chunkSize.X : _chunkSize.Y) * _regionRadiusMin;
        RegionType closestRegion = RegionType.Tutorial;
        RegionType secondClosestRegion = RegionType.Tutorial;

        foreach (var seed in _regionSeeds)
        {
            float distance = chunkPos.DistanceTo(seed.Center) / seed.Influence;

            if (distance < closest)
            {
                secondClosest = closest;
                secondClosestRegion = closestRegion;
                closest = distance;
                closestRegion = seed.Type;
            }
            else if (distance < secondClosest)
            {
                secondClosest = distance;
                secondClosestRegion = seed.Type;
            }
        }

        return secondClosestRegion;
    }

    public Dictionary<RegionType, int> CountRegionSizes()
    {
        var counts = new Dictionary<RegionType, int>();

        foreach (RegionType type in Enum.GetValues(typeof(RegionType)))
        {
            counts[type] = 0;
        }

        for (int y = 0; y < _worldSizeChunks.Y; y++)
        {
            for (int x = 0; x < _worldSizeChunks.X; x++)
            {
                var region = GetRegionForChunk(new Vector2I(x, y));
                counts[region]++;
            }
        }

        GD.Print("\n=== Region Size Verification ===");
        foreach (var kvp in counts)
        {
            bool meetsRequirement = kvp.Key == RegionType.Tutorial
                ? kvp.Value >= 500
                : kvp.Value >= 1000;

            string status = meetsRequirement ? "✓" : "✗";
            GD.Print($"{status} {kvp.Key}: {kvp.Value} chunks");
        }

        return counts;
    }
}
