# Technical Design Documentation

## World Generation
World generation is a bit complicated. We are generating the world by 64 x 64 meter chunks. These are split into 40 x 40 chunk regions and then 3 x 3 regions for the world creating a massive open world.

```
World
  |
Region
  |
Chunk
  |
Meter
```

This means we have a coordinate system with multiple layers. The game engine operates in mainly "World Coordinates" which are measured in meters from the point of origin. This means we have created a bunch of helper methods to convert between spaces as needed for calculations and interactions.

Why? Great question, hardware processing. By breaking down a large world into many parts we can offload a lot of data to disk and keep RAM usage low by only bringing in and rendering what we need for the immediate player.

### World Space
Measured in meters about the origin point in Euler coordinates (X, Y, Z).

### Region Space
Measured in chunks with the origin at the (0, 0) X, Z point. This makes regions measured pretty small <= 40 chunks in either X or Z direction.

### Chunk Space
Measured in meters like the World space but the origin point is the "top left" corner of the chunk. This makes chunk space very small <= 64 meters in either X or Z directions.

## Chunk Space to World Space and back
This one takes a bit of math but is not that difficult. We can even pre-generate a table to get chunks based on world position which will happen A LOT.

- Meters -> Chunk = Vector2I (Chunk Space)
- Chunk -> Region = Vector2I (Region Space)
- Region -> World = Vector2I (World Space)

```
WorldPosition.X = (Regions.X * RegionSize.X * ChunkSize.X) + ChunkSpace.X
WorldPosition.Z = (Regions.Z * RegionSize.Z * ChunkSize.Z) + ChunkSpace.Z
WorldPosition.Y = Height
```
Height is always in world space since we are only chunking in the X and Z directions.