using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class RegionGenerator
{
    // Region settings
    public class RegionSeed
    {
        public Vector2I Center;
        public RegionType Type;
        public float Influence;
        public float Radius;
        public int MinLevel;
        public int MaxLevel;
    };
    private List<RegionSeed> _regionSeeds;
    private Vector2I _worldSize;
    private Vector2I _chunkSize;
    private int _seed;
    private Dictionary<RegionType, int> _regionSizeMin = new();

    public RegionGenerator(int seed, Vector2I worldSize, Dictionary<RegionType, int> regionSizeMin, Vector2I chunkSize)
    {
        _seed = seed;
        _worldSize = worldSize;
        _regionSizeMin = regionSizeMin;
        _chunkSize = chunkSize;
        GenerateRegionSeeds();
    }

    public void GenerateRegionSeeds()
    {
        _regionSeeds = new List<RegionSeed>();
        var random = new Random(_seed);

        // Calculate approximate region sizes
        // 5 large regions @ 1000 chunks = 5000 chunks
        // 1 medium region @ 500 chunks = 500 chunks
        // Total = 5500 chunks minimum
        // Remaining ~4500 chunks for transitions/overlap

        // For a 100x100 map, we want regions spread out
        // Use a 3x2 grid layout for the 6 regions

        int gridCols = 3;
        int gridRows = 2;
        float cellWidth = _worldSize.X / (float)gridCols;
        float cellHeight = _worldSize.Y / (float)gridRows;

        var regionTypes = new List<RegionType>
        {
            RegionType.Tutorial,
            RegionType.SciFi,
            RegionType.Fantasy,
            RegionType.Prehistoric,
            RegionType.Mythological,
            RegionType.Horror

        };

        int regionIndex = 0;

        for (int row = 0; row < gridRows; row++)
        {
            for (int col = 0; col < gridCols; col++)
            {
                if (regionIndex >= regionTypes.Count)
                    break;

                // Base center point in grid cell
                float baseCenterX = (col + 0.5f) * cellWidth;
                float baseCenterY = (row + 0.5f) * cellHeight;

                // Add random offset (but keep within cell)
                float offsetX = (float)(random.NextDouble() - 0.5) * cellWidth * 0.4f;
                float offsetY = (float)(random.NextDouble() - 0.5) * cellHeight * 0.4f;

                Vector2 center = new Vector2(baseCenterX + offsetX, baseCenterY + offsetY);

                // Volcano gets smaller influence (will be ~500 chunks)
                float influence = regionTypes[regionIndex] == RegionType.Tutorial ? 0.7f : 1.0f;

                _regionSeeds.Add(new RegionSeed
                {
                    Center = (Vector2I)center,
                    Type = regionTypes[regionIndex],
                    Influence = influence
                });

                regionIndex++;
            }
        }

        GD.Print("=== Region Seeds Generated ===");
        foreach (var seed in _regionSeeds)
        {
            GD.Print($"{seed.Type}: Center at ({seed.Center.X:F0}, {seed.Center.Y:F0}), " +
                     $"Influence: {seed.Influence}");
        }
    }

    public RegionType GetRegionForChunk(Vector2I chunkCoord)
    {
        Vector2 chunkPos = new Vector2(chunkCoord.X, chunkCoord.Y);

        float minDistance = float.MaxValue;
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
        Vector2 chunkPos = new Vector2(chunkCoord.X, chunkCoord.Y);

        // Find closest and second-closest regions
        float closest = float.MaxValue;
        float secondClosest = float.MaxValue;

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
        Vector2 chunkPos = new Vector2(chunkCoord.X, chunkCoord.Y);

        float closest = float.MaxValue;
        float secondClosest = float.MaxValue;
        RegionType closestRegion = RegionType.Forest;
        RegionType secondClosestRegion = RegionType.Forest;

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

        for (int y = 0; y < _worldSize.Y; y++)
        {
            for (int x = 0; x < _worldSize.X; x++)
            {
                var region = GetRegionForChunk(new Vector2I(x, y));
                counts[region]++;
            }
        }

        GD.Print("\n=== Region Size Verification ===");
        foreach (var kvp in counts)
        {
            bool meetsRequirement = kvp.Key == RegionType.Volcano
                ? kvp.Value >= 500
                : kvp.Value >= 1000;

            string status = meetsRequirement ? "✓" : "✗";
            GD.Print($"{status} {kvp.Key}: {kvp.Value} chunks");
        }

        return counts;
    }

    private void GenerateProgressionLayout()
    {
        _regionSeeds = new List<RegionSeed>();
        Vector2I worldCenter = new Vector2I((int)(_worldSize.X / 2f), (int)(_worldSize.Y / 2f));

        var tutorial = new RegionSeed
        {
            Type = RegionType.Tutorial,
            Center = worldCenter,
            Radius = Mathf.Sqrt(500 / Mathf.Pi),
            MinLevel = 1,
            MaxLevel = 10,
            Influence = 0.7f // Smaller influence = smaller region
        };
        _regionSeeds.Add(tutorial);



        // Assign genres to early regions
        var earlyGenres = new[] { RegionType.Fantasy, RegionType.SciFi,
                                  RegionType.Prehistoric, RegionType.Mythological, RegionType.Horror };
        for (int i = 0; i < earlyRegions.Count; i++)
        {
            earlyRegions[i].Genre = earlyGenres[i];
            tutorial.NeighborRegions.Add(earlyRegions[i]);
            earlyRegions[i].NeighborRegions.Add(tutorial);
        }
        _regions.AddRange(earlyRegions);

        // STEP 3: Place "End Game" region at edge (far from Tutorial)
        // Level 40-50, only accessible after powering up
        var endGame = new ProgressionRegion
        {
            Genre = GenreRegion.Horror, // Horror as hardest region
            Center = worldCenter + new Vector2(_worldSize.X * 0.35f, _worldSize.Y * 0.35f),
            Radius = CalculateRadiusForChunkCount(1200),
            MinLevel = 40,
            MaxLevel = 50,
            Influence = 1.0f
        };
        _regions.Add(endGame);

        // Connect EndGame to one Early region (creates progression path)
        earlyRegions[0].NeighborRegions.Add(endGame);
        endGame.NeighborRegions.Add(earlyRegions[0]);

        GD.Print("=== Progression Layout Generated ===");
        foreach (var region in _regions)
        {
            GD.Print($"{region.Genre} (Lvl {region.MinLevel}-{region.MaxLevel}): " +
                     $"Center ({region.Center.X:F0}, {region.Center.Y:F0}), " +
                     $"Neighbors: {region.NeighborRegions.Count}");
        }
    }
}
