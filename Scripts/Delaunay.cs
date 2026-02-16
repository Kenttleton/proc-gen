using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// LEARNING: Delaunay triangulation creates triangles from points such that
/// no point is inside the circumcircle of any triangle. This maximizes the
/// minimum angle of triangles, creating well-distributed points.
/// 
/// Why this matters for POI placement: We want POIs spread out evenly, not
/// clustered. Delaunay triangulation helps us measure and maximize spacing.
/// </summary>

public class DelaunayTriangle
{
    public Vector2 A, B, C;

    public DelaunayTriangle(Vector2 a, Vector2 b, Vector2 c)
    {
        A = a;
        B = b;
        C = c;
    }

    /// <summary>
    /// Calculate circumcircle (circle passing through all 3 vertices)
    /// LEARNING: The circumcircle is key to Delaunay - no other points
    /// should be inside it.
    /// </summary>
    public (Vector2 center, float radius) GetCircumcircle()
    {
        float ax = A.X, ay = A.Y;
        float bx = B.X, by = B.Y;
        float cx = C.X, cy = C.Y;

        float d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));

        if (Mathf.Abs(d) < 0.0001f)
        {
            // Degenerate triangle (points are collinear)
            return (Vector2.Zero, float.MaxValue);
        }

        float aSq = ax * ax + ay * ay;
        float bSq = bx * bx + by * by;
        float cSq = cx * cx + cy * cy;

        float ux = (aSq * (by - cy) + bSq * (cy - ay) + cSq * (ay - by)) / d;
        float uy = (aSq * (cx - bx) + bSq * (ax - cx) + cSq * (bx - ax)) / d;

        Vector2 center = new Vector2(ux, uy);
        float radius = center.DistanceTo(A);

        return (center, radius);
    }

    /// <summary>
    /// Check if a point is inside this triangle's circumcircle
    /// </summary>
    public bool IsPointInCircumcircle(Vector2 point)
    {
        var (center, radius) = GetCircumcircle();
        return center.DistanceTo(point) < radius;
    }

    /// <summary>
    /// Check if this triangle contains a given edge
    /// </summary>
    public bool HasEdge(Vector2 p1, Vector2 p2)
    {
        return (A.IsEqualApprox(p1) && B.IsEqualApprox(p2)) ||
               (B.IsEqualApprox(p1) && C.IsEqualApprox(p2)) ||
               (C.IsEqualApprox(p1) && A.IsEqualApprox(p2)) ||
               (A.IsEqualApprox(p2) && B.IsEqualApprox(p1)) ||
               (B.IsEqualApprox(p2) && C.IsEqualApprox(p1)) ||
               (C.IsEqualApprox(p2) && A.IsEqualApprox(p1));
    }

    /// <summary>
    /// Check if triangle contains a vertex
    /// </summary>
    public bool HasVertex(Vector2 point)
    {
        return A.IsEqualApprox(point) || B.IsEqualApprox(point) || C.IsEqualApprox(point);
    }

    /// <summary>
    /// Calculate area of triangle (for quality scoring)
    /// </summary>
    public float GetArea()
    {
        return Mathf.Abs((B.X - A.X) * (C.Y - A.Y) - (C.X - A.X) * (B.Y - A.Y)) * 0.5f;
    }

    /// <summary>
    /// Get minimum angle in triangle (for quality scoring)
    /// LEARNING: Triangles with larger minimum angles are "better quality"
    /// </summary>
    public float GetMinAngle()
    {
        float a = B.DistanceTo(C); // Side opposite to A
        float b = C.DistanceTo(A); // Side opposite to B
        float c = A.DistanceTo(B); // Side opposite to C

        if (a < 0.0001f || b < 0.0001f || c < 0.0001f)
            return 0f; // Degenerate

        // Law of cosines to find angles
        float angleA = Mathf.Acos(Mathf.Clamp((b * b + c * c - a * a) / (2 * b * c), -1f, 1f));
        float angleB = Mathf.Acos(Mathf.Clamp((a * a + c * c - b * b) / (2 * a * c), -1f, 1f));
        float angleC = Mathf.Acos(Mathf.Clamp((a * a + b * b - c * c) / (2 * a * b), -1f, 1f));

        return Mathf.Min(Mathf.Min(angleA, angleB), angleC);
    }
}

public class DelaunayEdge
{
    public Vector2 A, B;

    public DelaunayEdge(Vector2 a, Vector2 b)
    {
        // Normalize edge direction (A is always "less than" B)
        if (a.X < b.X || (Mathf.Abs(a.X - b.X) < 0.0001f && a.Y < b.Y))
        {
            A = a;
            B = b;
        }
        else
        {
            A = b;
            B = a;
        }
    }

    public float Length()
    {
        return A.DistanceTo(B);
    }

    public override bool Equals(object obj)
    {
        if (obj is DelaunayEdge other)
        {
            return A.IsEqualApprox(other.A) && B.IsEqualApprox(other.B);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return A.GetHashCode() ^ B.GetHashCode();
    }
}

/// <summary>
/// Bowyer-Watson algorithm for Delaunay triangulation
/// LEARNING: This is the standard algorithm. It works by:
/// 1. Create super-triangle containing all points
/// 2. Add points one by one
/// 3. For each point, find triangles whose circumcircle contains it
/// 4. Remove those triangles and re-triangulate the hole
/// </summary>
public class DelaunayTriangulator
{
    /// <summary>
    /// Main triangulation method
    /// </summary>
    public List<DelaunayTriangle> DelaunayTriangulation(List<Vector2> points)
    {
        if (points.Count < 3)
        {
            GD.PrintErr("Delaunay triangulation requires at least 3 points");
            return new List<DelaunayTriangle>();
        }

        // Remove duplicate points
        points = RemoveDuplicates(points);

        if (points.Count < 3)
        {
            GD.PrintErr("After removing duplicates, less than 3 points remain");
            return new List<DelaunayTriangle>();
        }

        // Step 1: Create super-triangle that contains all points
        var (superA, superB, superC) = CreateSuperTriangle(points);

        // Initialize triangulation with super-triangle
        var triangles = new List<DelaunayTriangle>
        {
            new DelaunayTriangle(superA, superB, superC)
        };

        // Step 2: Add each point one by one
        foreach (var point in points)
        {
            AddPointToTriangulation(triangles, point);
        }

        // Step 3: Remove triangles that share vertices with super-triangle
        triangles.RemoveAll(t =>
            t.HasVertex(superA) || t.HasVertex(superB) || t.HasVertex(superC)
        );

        return triangles;
    }

    /// <summary>
    /// Create a super-triangle that contains all points
    /// LEARNING: We make this VERY large to ensure it contains everything
    /// </summary>
    private (Vector2, Vector2, Vector2) CreateSuperTriangle(List<Vector2> points)
    {
        // Find bounding box
        float minX = points.Min(p => p.X);
        float minY = points.Min(p => p.Y);
        float maxX = points.Max(p => p.X);
        float maxY = points.Max(p => p.Y);

        float dx = maxX - minX;
        float dy = maxY - minY;
        float deltaMax = Mathf.Max(dx, dy);
        float midX = (minX + maxX) * 0.5f;
        float midY = (minY + maxY) * 0.5f;

        // Create super-triangle (equilateral, very large)
        Vector2 p1 = new Vector2(midX - 20 * deltaMax, midY - deltaMax);
        Vector2 p2 = new Vector2(midX, midY + 20 * deltaMax);
        Vector2 p3 = new Vector2(midX + 20 * deltaMax, midY - deltaMax);

        return (p1, p2, p3);
    }

    /// <summary>
    /// Add a single point to existing triangulation
    /// LEARNING: This is the core of Bowyer-Watson algorithm
    /// </summary>
    private void AddPointToTriangulation(List<DelaunayTriangle> triangles, Vector2 point)
    {
        // Find all triangles whose circumcircle contains this point
        var badTriangles = new List<DelaunayTriangle>();

        foreach (var triangle in triangles)
        {
            if (triangle.IsPointInCircumcircle(point))
            {
                badTriangles.Add(triangle);
            }
        }

        if (badTriangles.Count == 0)
        {
            // Point is outside all existing triangles (shouldn't happen with super-triangle)
            GD.PrintErr("Warning: Point outside all triangles");
            return;
        }

        // Find the boundary of the polygonal hole
        var polygon = new List<DelaunayEdge>();

        foreach (var triangle in badTriangles)
        {
            // Add each edge of bad triangle
            var edges = new[]
            {
                new DelaunayEdge(triangle.A, triangle.B),
                new DelaunayEdge(triangle.B, triangle.C),
                new DelaunayEdge(triangle.C, triangle.A)
            };

            foreach (var edge in edges)
            {
                // Check if this edge is shared with another bad triangle
                bool isShared = false;

                foreach (var otherTriangle in badTriangles)
                {
                    if (otherTriangle == triangle)
                        continue;

                    if (otherTriangle.HasEdge(edge.A, edge.B))
                    {
                        isShared = true;
                        break;
                    }
                }

                // Only keep edges that are NOT shared (boundary edges)
                if (!isShared)
                {
                    polygon.Add(edge);
                }
            }
        }

        // Remove bad triangles
        triangles.RemoveAll(t => badTriangles.Contains(t));

        // Re-triangulate the polygonal hole
        foreach (var edge in polygon)
        {
            triangles.Add(new DelaunayTriangle(edge.A, edge.B, point));
        }
    }

    private List<Vector2> RemoveDuplicates(List<Vector2> points)
    {
        var unique = new List<Vector2>();

        foreach (var point in points)
        {
            bool isDuplicate = false;

            foreach (var existing in unique)
            {
                if (existing.IsEqualApprox(point))
                {
                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate)
            {
                unique.Add(point);
            }
        }

        return unique;
    }

    /// <summary>
    /// Score the quality of a triangulation
    /// LEARNING: Good POI placement = evenly distributed points.
    /// We measure this with multiple metrics.
    /// </summary>
    public float ScoreDistribution(List<Vector2> points, List<DelaunayTriangle> triangles)
    {
        if (triangles.Count == 0)
            return 0f;

        float score = 0f;

        // Metric 1: Triangle quality (prefer large minimum angles)
        // LEARNING: Skinny triangles = clustered points. We want "fat" triangles.
        float avgMinAngle = 0f;
        foreach (var triangle in triangles)
        {
            float minAngle = triangle.GetMinAngle();
            avgMinAngle += minAngle;
        }
        avgMinAngle /= triangles.Count;

        // Convert to score (60 degrees = perfect equilateral triangle)
        float angleScore = avgMinAngle / (Mathf.Pi / 3f); // Normalize to [0, 1]
        score += angleScore * 40f; // Weight: 40%

        // Metric 2: Area variance (prefer consistent triangle sizes)
        // LEARNING: Similar-sized triangles = even spacing
        float[] areas = triangles.Select(t => t.GetArea()).ToArray();
        float avgArea = areas.Average();
        float areaVariance = areas.Sum(a => Mathf.Pow(a - avgArea, 2)) / areas.Length;
        float areaStdDev = Mathf.Sqrt(areaVariance);

        // Lower variance = better (normalize inversely)
        float areaScore = 1f / (1f + areaStdDev / avgArea);
        score += areaScore * 30f; // Weight: 30%

        // Metric 3: Minimum distance between any two points
        // LEARNING: We want POIs to maintain minimum separation
        float minDistance = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                float dist = points[i].DistanceTo(points[j]);
                if (dist < minDistance)
                    minDistance = dist;
            }
        }

        // Normalize distance score (assume world is ~10000 units)
        float distanceScore = Mathf.Clamp(minDistance / 100f, 0f, 1f);
        score += distanceScore * 30f; // Weight: 30%

        return score;
    }

    /// <summary>
    /// Alternative scoring that emphasizes different aspects
    /// Use this if you want to prioritize maximum spacing over uniformity
    /// </summary>
    public float ScoreDistributionMaxSpacing(List<Vector2> points, List<DelaunayTriangle> triangles)
    {
        if (triangles.Count == 0)
            return 0f;

        // Extract all unique edges
        var edges = new HashSet<DelaunayEdge>();
        foreach (var triangle in triangles)
        {
            edges.Add(new DelaunayEdge(triangle.A, triangle.B));
            edges.Add(new DelaunayEdge(triangle.B, triangle.C));
            edges.Add(new DelaunayEdge(triangle.C, triangle.A));
        }

        // Find minimum edge length (closest neighbors)
        float minEdgeLength = edges.Min(e => e.Length());

        // Find average edge length
        float avgEdgeLength = edges.Average(e => e.Length());

        // Find variance in edge lengths
        float variance = edges.Sum(e => Mathf.Pow(e.Length() - avgEdgeLength, 2)) / edges.Count;
        float stdDev = Mathf.Sqrt(variance);

        // Score: prioritize large minimum distance and low variance
        float minDistanceScore = minEdgeLength / avgEdgeLength; // 0-1 range
        float uniformityScore = 1f / (1f + stdDev / avgEdgeLength);

        return minDistanceScore * 70f + uniformityScore * 30f; // Heavily weight minimum distance
    }
}