using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class MacroTerrain
{
    public Vector2I WorldSize;
    public int Resolution; // Samples per axis (e.g., 100 = 100x100 height samples)

    // Height map at macro scale (low resolution)
    public float[,] MacroHeightMap;

    // Feature masks (0-1 values indicating feature strength)
    public float[,] MountainMask;
    public float[,] ValleyMask;
    public float[,] WaterMask; // Lakes and rivers
    public float[,] OceanMask; // Coastal areas
    public float[,] PlainsMask;

    // Feature lists for POI placement
    public List<Vector2> MountainPeaks = new();
    public List<Vector2> LakeBeds = new();
    public List<Vector2> RiverBanks = new();
    public List<Vector2> FlatAreas = new();
    public List<Vector2> Valleys = new();
    public List<Vector2> Volcanoes = new();

    public MacroTerrain(Vector2I worldSize, int resolution)
    {
        WorldSize = worldSize;
        Resolution = resolution;

        MacroHeightMap = new float[resolution, resolution];
        MountainMask = new float[resolution, resolution];
        ValleyMask = new float[resolution, resolution];
        WaterMask = new float[resolution, resolution];
        OceanMask = new float[resolution, resolution];
        PlainsMask = new float[resolution, resolution];
    }

    /// <summary>
    /// Get interpolated height at any world position
    /// using bilinear interpolation to smoothly blend between
    /// macro terrain samples. This prevents visible "steps" between samples.
    /// </summary>
    public float GetHeightAtWorldPos(float worldX, float worldZ)
    {
        // Convert world position to macro terrain coordinates
        float macroX = (worldX / WorldSize.X) * (Resolution - 1);
        float macroZ = (worldZ / WorldSize.Y) * (Resolution - 1);

        return BilinearInterpolate(MacroHeightMap, macroX, macroZ);
    }

    public float GetMountainMaskAt(float worldX, float worldZ)
    {
        float macroX = (worldX / WorldSize.X) * (Resolution - 1);
        float macroZ = (worldZ / WorldSize.Y) * (Resolution - 1);
        return BilinearInterpolate(MountainMask, macroX, macroZ);
    }

    public float GetWaterMaskAt(float worldX, float worldZ)
    {
        float macroX = (worldX / WorldSize.X) * (Resolution - 1);
        float macroZ = (worldZ / WorldSize.Y) * (Resolution - 1);
        return BilinearInterpolate(WaterMask, macroX, macroZ);
    }

    private float BilinearInterpolate(float[,] data, float x, float z)
    {
        int x0 = Mathf.FloorToInt(x);
        int z0 = Mathf.FloorToInt(z);
        int x1 = Mathf.Min(x0 + 1, Resolution - 1);
        int z1 = Mathf.Min(z0 + 1, Resolution - 1);

        float fx = x - x0;
        float fz = z - z0;

        // Bilinear interpolation
        float v00 = data[x0, z0];
        float v10 = data[x1, z0];
        float v01 = data[x0, z1];
        float v11 = data[x1, z1];

        float v0 = Mathf.Lerp(v00, v10, fx);
        float v1 = Mathf.Lerp(v01, v11, fx);

        return Mathf.Lerp(v0, v1, fz);
    }

    public Vector2 FindClosestMountainPeak(Vector2 location)
    {
        Vector2 match = Vector2.Zero;
        foreach (Vector2 peak in MountainPeaks)
        {
            if (match == Vector2.Zero)
            {
                match = peak;
                continue;
            }

            var compOld = CoordinateConversion.DistanceBetween(match, peak);
            var compNew = CoordinateConversion.DistanceBetween(location, peak);
            if (compNew > compOld)
                match = peak;
        }
        return match;
    }

    public Vector2 FindClosestLakeBed(Vector2 location)
    {
        Vector2 match = Vector2.Zero;
        foreach (Vector2 peak in LakeBeds)
        {
            if (match == Vector2.Zero)
            {
                match = peak;
                continue;
            }

            var compOld = CoordinateConversion.DistanceBetween(match, peak);
            var compNew = CoordinateConversion.DistanceBetween(location, peak);
            if (compNew > compOld)
                match = peak;
        }
        return match;
    }

    public Vector2 FindClosestRiverBank(Vector2 location)
    {
        Vector2 match = Vector2.Zero;
        foreach (Vector2 peak in RiverBanks)
        {
            if (match == Vector2.Zero)
            {
                match = peak;
                continue;
            }

            var compOld = CoordinateConversion.DistanceBetween(match, peak);
            var compNew = CoordinateConversion.DistanceBetween(location, peak);
            if (compNew > compOld)
                match = peak;
        }
        return match;
    }

    public Vector2 FindClosestFlatArea(Vector2 location)
    {
        Vector2 match = Vector2.Zero;
        foreach (Vector2 peak in FlatAreas)
        {
            if (match == Vector2.Zero)
            {
                match = peak;
                continue;
            }

            var compOld = CoordinateConversion.DistanceBetween(match, peak);
            var compNew = CoordinateConversion.DistanceBetween(location, peak);
            if (compNew > compOld)
                match = peak;
        }
        return match;
    }

    public Vector2 FindClosestValley(Vector2 location)
    {
        Vector2 match = Vector2.Zero;
        foreach (Vector2 peak in Valleys)
        {
            if (match == Vector2.Zero)
            {
                match = peak;
                continue;
            }

            var compOld = CoordinateConversion.DistanceBetween(match, peak);
            var compNew = CoordinateConversion.DistanceBetween(location, peak);
            if (compNew > compOld)
                match = peak;
        }
        return match;
    }
}

public class MacroTerrainGenerator
{
    private int _seed;
    private Vector2I _worldSize;
    private int _resolution;
    private Random _random;

    // Noise generators for different features
    private FastNoiseLite _continentNoise;
    private FastNoiseLite _mountainNoise;
    private FastNoiseLite _valleyNoise;
    private FastNoiseLite _detailNoise;
    private FastNoiseLite _erosionNoise;

    public MacroTerrainGenerator(int seed, Vector2I worldSize, int resolution = 200)
    {
        _seed = seed;
        _worldSize = worldSize;
        _resolution = resolution;
        _random = new Random(seed);

        InitializeNoiseGenerators();
    }

    private void InitializeNoiseGenerators()
    {
        // Continental scale - defines major land/water areas
        _continentNoise = new FastNoiseLite();
        _continentNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _continentNoise.Seed = _seed;
        _continentNoise.Frequency = 0.003f; // Very large features
        _continentNoise.FractalOctaves = 3;

        // Mountain range scale
        _mountainNoise = new FastNoiseLite();
        _mountainNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _mountainNoise.Seed = _seed + 1000;
        _mountainNoise.Frequency = 0.015f; // Medium features
        _mountainNoise.FractalOctaves = 5;
        _mountainNoise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;

        // Valley carving
        _valleyNoise = new FastNoiseLite();
        _valleyNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
        _valleyNoise.Seed = _seed + 2000;
        _valleyNoise.Frequency = 0.02f;
        _valleyNoise.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance2Sub;

        // Fine detail
        _detailNoise = new FastNoiseLite();
        _detailNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
        _detailNoise.Seed = _seed + 3000;
        _detailNoise.Frequency = 0.05f;
        _detailNoise.FractalOctaves = 3;

        // Erosion patterns
        _erosionNoise = new FastNoiseLite();
        _erosionNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
        _erosionNoise.Seed = _seed + 4000;
        _erosionNoise.Frequency = 0.03f;
        _erosionNoise.CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance;
    }

    /// <summary>
    /// The main terrain generation function. It builds terrain
    /// in layers, each adding different features.
    /// </summary>
    public MacroTerrain GenerateMacroTerrain(RegionSeed region = null, bool includeVolcano = false)
    {
        var terrain = new MacroTerrain(_worldSize, _resolution);

        GenerateBaseHeight(terrain);
        GenerateMountains(terrain);
        GenerateValleys(terrain);
        GenerateWaterFeatures(terrain);
        ApplyOceanFade(terrain);

        if (region != null && includeVolcano) // 10% chance
        {
            // Volcano is the only region special feature
            GenerateVolcano(terrain, region);
        }

        ApplyErosion(terrain);
        IdentifyFeatureLocations(terrain);

        GD.Print($"Macro terrain generated: {terrain.MountainPeaks.Count} peaks, " +
                 $"{terrain.LakeBeds.Count} lakes, {terrain.Valleys.Count} valleys");

        return terrain;
    }

    private void GenerateBaseHeight(MacroTerrain terrain)
    {
        for (int z = 0; z < _resolution; z++)
        {
            for (int x = 0; x < _resolution; x++)
            {
                // World space position
                float worldX = (x / (float)_resolution) * _worldSize.X;
                float worldZ = (z / (float)_resolution) * _worldSize.Y;

                // Get continental noise (-1 to 1)
                float continentValue = _continentNoise.GetNoise2D(worldX, worldZ);

                // Convert to height (sea level = 0)
                // LEARNING: We bias toward land by shifting the threshold
                float height = (continentValue + 0.3f) * 50f; // More land than water

                terrain.MacroHeightMap[x, z] = height;
            }
        }
    }

    private void GenerateMountains(MacroTerrain terrain)
    {
        // Mountains are placed using domain warping - we distort the
        // noise space to create realistic mountain ranges instead of random peaks

        for (int z = 0; z < _resolution; z++)
        {
            for (int x = 0; x < _resolution; x++)
            {
                float worldX = (x / (float)_resolution) * _worldSize.X;
                float worldZ = (z / (float)_resolution) * _worldSize.Y;

                // Get mountain noise
                float mountainValue = _mountainNoise.GetNoise2D(worldX, worldZ);

                // Only create mountains on land (positive height)
                if (terrain.MacroHeightMap[x, z] > 0)
                {
                    // Apply ridging - sharp peaks
                    mountainValue = Mathf.Abs(mountainValue); // Creates ridges
                    mountainValue = Mathf.Pow(mountainValue, 1.5f); // Sharpen peaks

                    // Add mountains to base height
                    float mountainHeight = mountainValue * 80f; // Up to 80m mountains
                    terrain.MacroHeightMap[x, z] += mountainHeight;

                    // Store mountain mask for later
                    terrain.MountainMask[x, z] = mountainValue;
                }
            }
        }
    }

    private void GenerateValleys(MacroTerrain terrain)
    {
        // Valleys use cellular noise, which creates natural-looking
        // drainage patterns. We invert it to create depressions.

        for (int z = 0; z < _resolution; z++)
        {
            for (int x = 0; x < _resolution; x++)
            {
                float worldX = (x / (float)_resolution) * _worldSize.X;
                float worldZ = (z / (float)_resolution) * _worldSize.Y;

                // Get valley carving noise
                float valleyValue = _valleyNoise.GetNoise2D(worldX, worldZ);

                // Normalize from [-1, 1] to [0, 1]
                valleyValue = (valleyValue + 1f) * 0.5f;

                // Only carve valleys on land above a certain height
                if (terrain.MacroHeightMap[x, z] > 10f)
                {
                    // Carve valleys
                    float valleyDepth = (1f - valleyValue) * 30f; // Up to 30m deep
                    terrain.MacroHeightMap[x, z] -= valleyDepth;

                    terrain.ValleyMask[x, z] = 1f - valleyValue;
                }
            }
        }
    }

    private void GenerateWaterFeatures(MacroTerrain terrain)
    {
        // Water accumulates in low areas. We do a simple "watershed"
        // simulation where we mark areas below a threshold as water.

        for (int z = 0; z < _resolution; z++)
        {
            for (int x = 0; x < _resolution; x++)
            {
                float height = terrain.MacroHeightMap[x, z];

                // Lakes form in depressions
                if (height < 5f && height > -5f)
                {
                    terrain.WaterMask[x, z] = 1f;
                    terrain.MacroHeightMap[x, z] = Mathf.Min(height, 0f); // Flatten to water level
                }

                // Rivers follow valleys
                if (terrain.ValleyMask[x, z] > 0.7f && height < 15f && height > 0f)
                {
                    terrain.WaterMask[x, z] = 0.5f; // Partial water (river)
                    terrain.MacroHeightMap[x, z] = Mathf.Max(height - 10f, 0f); // Lower river bed
                }
            }
        }
    }

    private void ApplyOceanFade(MacroTerrain terrain)
    {
        // We fade to ocean at world edges using a radial gradient.
        // This prevents harsh boundaries and creates natural coastlines.

        Vector2 center = new Vector2(_resolution / 2f, _resolution / 2f);
        float maxRadius = Mathf.Min(_resolution, _resolution) * 0.5f;
        float fadeStart = maxRadius * 0.8f; // Start fading at 80% of radius

        for (int z = 0; z < _resolution; z++)
        {
            for (int x = 0; x < _resolution; x++)
            {
                Vector2 pos = new Vector2(x, z);
                float distFromCenter = pos.DistanceTo(center);

                if (distFromCenter > fadeStart)
                {
                    // Calculate fade (0 = no fade, 1 = full ocean)
                    float fadeAmount = (distFromCenter - fadeStart) / (maxRadius - fadeStart);
                    fadeAmount = Mathf.Clamp(fadeAmount, 0f, 1f);

                    // Blend height toward ocean level (-20m)
                    float targetHeight = -20f;
                    terrain.MacroHeightMap[x, z] = Mathf.Lerp(
                        terrain.MacroHeightMap[x, z],
                        targetHeight,
                        fadeAmount
                    );

                    // Mark as ocean
                    terrain.OceanMask[x, z] = fadeAmount;

                    // Create lagoons and coastal features
                    if (fadeAmount > 0.3f && fadeAmount < 0.7f)
                    {
                        // Add noise to create interesting coastline
                        float coastalNoise = _detailNoise.GetNoise2D(x * 2f, z * 2f);

                        if (coastalNoise > 0.2f)
                        {
                            // Create small islands or peninsulas
                            terrain.MacroHeightMap[x, z] += 15f * (1f - fadeAmount);
                        }
                    }
                }
            }
        }
    }

    private void GenerateVolcano(MacroTerrain terrain, RegionSeed region)
    {
        // Place volcano near region center but not exactly at center
        float offsetX = ((float)_random.NextDouble() - 0.5f) * 0.3f;
        float offsetZ = ((float)_random.NextDouble() - 0.5f) * 0.3f;

        float volcanoX = (region.Center.X / (float)_worldSize.X) * _resolution;
        float volcanoZ = (region.Center.Y / (float)_worldSize.Y) * _resolution;

        volcanoX += offsetX * _resolution;
        volcanoZ += offsetZ * _resolution;

        Vector2 volcanoPos = new Vector2(volcanoX, volcanoZ);

        // LEARNING: Volcanoes are cone-shaped with a crater at top
        float volcanoRadius = _resolution * 0.15f; // 15% of map
        float craterRadius = volcanoRadius * 0.3f;

        for (int z = 0; z < _resolution; z++)
        {
            for (int x = 0; x < _resolution; x++)
            {
                Vector2 pos = new Vector2(x, z);
                float distFromVolcano = pos.DistanceTo(volcanoPos);

                if (distFromVolcano < volcanoRadius)
                {
                    // Create cone shape
                    float heightFactor = 1f - (distFromVolcano / volcanoRadius);
                    float volcanoHeight = heightFactor * 120f; // 120m tall volcano

                    // Create crater at top
                    if (distFromVolcano < craterRadius)
                    {
                        float craterDepth = (1f - distFromVolcano / craterRadius) * 40f;
                        volcanoHeight -= craterDepth;
                    }

                    // Blend with existing terrain
                    terrain.MacroHeightMap[x, z] = Mathf.Max(
                        terrain.MacroHeightMap[x, z],
                        terrain.MacroHeightMap[x, z] + volcanoHeight
                    );

                    // Add to mountain mask
                    terrain.MountainMask[x, z] = Mathf.Max(terrain.MountainMask[x, z], heightFactor);
                }
            }
        }

        // Store volcano position for POI placement
        float worldVolcanoX = (volcanoX / _resolution) * _worldSize.X;
        float worldVolcanoZ = (volcanoZ / _resolution) * _worldSize.Y;
        terrain.Volcanoes.Add(new Vector2(worldVolcanoX, worldVolcanoZ));

        GD.Print($"Volcano generated at ({worldVolcanoX:F0}, {worldVolcanoZ:F0})");
    }

    private void ApplyErosion(MacroTerrain terrain)
    {
        // Erosion smooths sharp features and adds realism
        // We simulate it by blending each point with its neighbors

        float[,] smoothed = new float[_resolution, _resolution];

        for (int z = 1; z < _resolution - 1; z++)
        {
            for (int x = 1; x < _resolution - 1; x++)
            {
                // Get neighboring heights
                float sum = 0f;
                int count = 0;

                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        sum += terrain.MacroHeightMap[x + dx, z + dz];
                        count++;
                    }
                }

                float avg = sum / count;

                // Blend original with smoothed (80% original, 20% smoothed)
                smoothed[x, z] = Mathf.Lerp(terrain.MacroHeightMap[x, z], avg, 0.2f);

                // Add detail noise for texture
                float worldX = (x / (float)_resolution) * _worldSize.X;
                float worldZ = (z / (float)_resolution) * _worldSize.Y;
                float detail = _detailNoise.GetNoise2D(worldX, worldZ) * 3f;

                smoothed[x, z] += detail;
            }
        }

        // Copy smoothed back to terrain (skip edges)
        for (int z = 1; z < _resolution - 1; z++)
        {
            for (int x = 1; x < _resolution - 1; x++)
            {
                terrain.MacroHeightMap[x, z] = smoothed[x, z];
            }
        }
    }

    private void IdentifyFeatureLocations(MacroTerrain terrain)
    {
        // We scan the macro terrain to find interesting locations
        // for POI placement. This is much faster than checking every chunk.

        for (int z = 0; z < _resolution; z++)
        {
            for (int x = 0; x < _resolution; x++)
            {
                float height = terrain.MacroHeightMap[x, z];
                float mountainMask = terrain.MountainMask[x, z];
                float valleyMask = terrain.ValleyMask[x, z];
                float waterMask = terrain.WaterMask[x, z];

                // Convert to world position
                float worldX = (x / (float)_resolution) * _worldSize.X;
                float worldZ = (z / (float)_resolution) * _worldSize.Y;
                Vector2 worldPos = new Vector2(worldX, worldZ);

                // Mountain peaks (high, steep)
                if (height > 50f && mountainMask > 0.7f)
                {
                    // Check if this is a local maximum
                    if (IsLocalMaximum(terrain.MacroHeightMap, x, z, 3))
                    {
                        terrain.MountainPeaks.Add(worldPos);
                    }
                }

                // Lake beds (low, flat)
                if (waterMask > 0.9f && height < 5f)
                {
                    if (IsLocalMinimum(terrain.MacroHeightMap, x, z, 2))
                    {
                        terrain.LakeBeds.Add(worldPos);
                    }
                }

                // River banks (partial water, follows valleys)
                if (waterMask > 0.3f && waterMask < 0.9f)
                {
                    terrain.RiverBanks.Add(worldPos);
                }

                // Valleys (low mountain mask, high valley mask)
                if (valleyMask > 0.6f && height > 5f && height < 30f)
                {
                    terrain.Valleys.Add(worldPos);
                }

                // Flat plains (low slope)
                float slope = CalculateSlope(terrain.MacroHeightMap, x, z);
                if (slope < 0.1f && height > 5f && height < 40f && mountainMask < 0.3f)
                {
                    terrain.FlatAreas.Add(worldPos);
                }
            }
        }

        // Thin out features if we have too many (keep every Nth one)
        terrain.FlatAreas = ThinOutList(terrain.FlatAreas, 500);
        terrain.RiverBanks = ThinOutList(terrain.RiverBanks, 200);
    }

    private bool IsLocalMaximum(float[,] heightMap, int x, int z, int radius)
    {
        float centerHeight = heightMap[x, z];

        for (int dz = -radius; dz <= radius; dz++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx == 0 && dz == 0) continue;

                int nx = x + dx;
                int nz = z + dz;

                if (nx < 0 || nx >= _resolution || nz < 0 || nz >= _resolution)
                    continue;

                if (heightMap[nx, nz] > centerHeight)
                    return false;
            }
        }

        return true;
    }

    private bool IsLocalMinimum(float[,] heightMap, int x, int z, int radius)
    {
        float centerHeight = heightMap[x, z];

        for (int dz = -radius; dz <= radius; dz++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx == 0 && dz == 0) continue;

                int nx = x + dx;
                int nz = z + dz;

                if (nx < 0 || nx >= _resolution || nz < 0 || nz >= _resolution)
                    continue;

                if (heightMap[nx, nz] < centerHeight)
                    return false;
            }
        }

        return true;
    }

    private float CalculateSlope(float[,] heightMap, int x, int z)
    {
        if (x <= 0 || x >= _resolution - 1 || z <= 0 || z >= _resolution - 1)
            return 0f;

        float heightL = heightMap[x - 1, z];
        float heightR = heightMap[x + 1, z];
        float heightD = heightMap[x, z - 1];
        float heightU = heightMap[x, z + 1];

        float slopeX = Mathf.Abs(heightR - heightL) / 2f;
        float slopeZ = Mathf.Abs(heightU - heightD) / 2f;

        return Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ);
    }

    private List<Vector2> ThinOutList(List<Vector2> points, int maxCount)
    {
        if (points.Count <= maxCount)
            return points;

        // Keep every Nth point
        int step = points.Count / maxCount;
        var result = new List<Vector2>();

        for (int i = 0; i < points.Count; i += step)
        {
            result.Add(points[i]);
            if (result.Count >= maxCount)
                break;
        }

        return result;
    }
}