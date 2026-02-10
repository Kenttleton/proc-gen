using Godot;
using System;
using System.Collections.Generic;

public partial class CollisionBitDebug : Node
{
	public override void _Ready()
    {
        GetTree().CreateTimer(1.0).Timeout += () => {
            DebugCollisionBits();
        };
    }
    
    private void DebugCollisionBits()
    {
        GD.Print("=== COLLISION BIT DEBUG ===");
        
        // Find player
        var player = FindPlayer();
        if (player != null)
        {
            GD.Print($"Player ({player.Name}):");
            GD.Print($"  CollisionLayer: {player.CollisionLayer} (binary: {Convert.ToString(player.CollisionLayer, 2).PadLeft(32, '0')})");
            GD.Print($"  CollisionMask: {player.CollisionMask} (binary: {Convert.ToString(player.CollisionMask, 2).PadLeft(32, '0')})");
            GD.Print($"  Position: {player.GlobalPosition}");
            GD.Print($"  Is on floor: {player.IsOnFloor()}");
            
            // Check bits individually
            for (int i = 0; i < 32; i++)
            {
                bool layerBit = (player.CollisionLayer & (1 << i)) != 0;
                bool maskBit = (player.CollisionMask & (1 << i)) != 0;
                if (layerBit || maskBit)
                {
                    GD.Print($"    Bit {i}: Layer={layerBit}, Mask={maskBit}");
                }
            }
        }
        
        // Find terrain chunks
        var chunks = FindTerrainChunks();
        GD.Print($"\nFound {chunks.Count} terrain chunks:");
        
        foreach (var chunk in chunks)
        {
            GD.Print($"\nChunk ({chunk.Name}):");
            GD.Print($"  CollisionLayer: {chunk.CollisionLayer} (binary: {Convert.ToString(chunk.CollisionLayer, 2).PadLeft(32, '0')})");
            GD.Print($"  CollisionMask: {chunk.CollisionMask} (binary: {Convert.ToString(chunk.CollisionMask, 2).PadLeft(32, '0')})");
            GD.Print($"  Global Position: {chunk.GlobalPosition}");
            GD.Print($"  Local Position: {chunk.Position}");
            
            // Check bits individually
            for (int i = 0; i < 32; i++)
            {
                bool layerBit = (chunk.CollisionLayer & (1 << i)) != 0;
                bool maskBit = (chunk.CollisionMask & (1 << i)) != 0;
                if (layerBit || maskBit)
                {
                    GD.Print($"    Bit {i}: Layer={layerBit}, Mask={maskBit}");
                }
            }
            
            // Check collision shape
            foreach (Node child in chunk.GetChildren())
            {
                if (child is CollisionShape3D shape)
                {
                    GD.Print($"  CollisionShape3D found:");
                    GD.Print($"    Disabled: {shape.Disabled}");
                    GD.Print($"    Shape: {shape.Shape?.GetType().Name ?? "null"}");
                    GD.Print($"    Global Position: {shape.GlobalPosition}");
                    
                    if (shape.Shape is ConcavePolygonShape3D concave)
                    {
                        var faces = concave.GetFaces();
                        GD.Print($"    Concave faces count: {faces.Length}");
                        if (faces.Length > 0)
                        {
                            GD.Print($"    First face vertex: {faces[0]}");
                            GD.Print($"    First face vertex (global): {chunk.ToGlobal(faces[0])}");
                        }
                    }
                }
            }
        }
        
        // Check if player and terrain overlap bits
        if (player != null && chunks.Count > 0)
        {
            GD.Print("\n=== COLLISION DETECTION CHECK ===");
            var chunk = chunks[0];
            
            // For collision to work: Player's mask must overlap with Terrain's layer
            bool willCollide = (player.CollisionMask & chunk.CollisionLayer) != 0;
            
            GD.Print($"Player mask ({player.CollisionMask}) & Terrain layer ({chunk.CollisionLayer}) = {player.CollisionMask & chunk.CollisionLayer}");
            GD.Print($"Will collide: {willCollide}");
            
            if (!willCollide)
            {
                GD.PrintErr("PROBLEM: Player's collision mask does not overlap with terrain's collision layer!");
                GD.PrintErr($"Player needs to have bit 0 set in mask (terrain is on layer 1 = bit 0)");
            }
        }
        
        GD.Print("=== END DEBUG ===");
    }
    
    private CharacterBody3D FindPlayer()
    {
        return FindNodeRecursive<CharacterBody3D>(GetTree().Root);
    }
    
    private List<StaticBody3D> FindTerrainChunks()
    {
        var chunks = new List<StaticBody3D>();
        FindTerrainChunksRecursive(GetTree().Root, chunks);
        return chunks;
    }
    
    private T FindNodeRecursive<T>(Node node) where T : Node
    {
        if (node is T result)
            return result;
        
        foreach (Node child in node.GetChildren())
        {
            var found = FindNodeRecursive<T>(child);
            if (found != null)
                return found;
        }
        
        return null;
    }
    
    private void FindTerrainChunksRecursive(Node node, List<StaticBody3D> chunks)
    {
        if (node is StaticBody3D body && node.Name.ToString().StartsWith("Chunk_"))
        {
            chunks.Add(body);
        }
        
        foreach (Node child in node.GetChildren())
        {
            FindTerrainChunksRecursive(child, chunks);
        }
    }
}
