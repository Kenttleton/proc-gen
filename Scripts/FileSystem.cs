using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class FileSystem
{
    private string _worldDataLookupPath;
    private Dictionary<Vector2I, FileMap> _worldDataLookup = new();
    private Dictionary<string, int> _toClean = new();
    private const int _maxFragments = 300;
    private HashSet<string> _filesToCompact = new HashSet<string>();
    public FileSystem(string worldDataLookupPath)
    {
        _worldDataLookupPath = worldDataLookupPath;
        ReadMetadata();
    }
    public void WriteChunkData(ChunkData chunkData, Vector2I chunkCoord)
    {
        var filePath = _worldDataLookup[chunkCoord].FilePath;
        var pStart = _worldDataLookup[chunkCoord].FilePosition.Start;
        var pEnd = _worldDataLookup[chunkCoord].FilePosition.End;
        var file = FileAccess.Open(filePath, FileAccess.ModeFlags.ReadWrite);
        if (file == null || file.GetError() != Error.Ok)
        {
            GD.PrintErr($"Failed to open file for writing: {filePath}");
            return;
        }
        file.Seek(file.GetLength());
        var data = chunkData.Serialize();
        var newStart = (ulong)file.GetPosition();
        file.StoreBuffer(data);
        var newEnd = (ulong)file.GetPosition();
        file.Close();
        var tc = _toClean.GetValueOrDefault(_worldDataLookup[chunkCoord].FilePath, 0);
        _toClean[_worldDataLookup[chunkCoord].FilePath] = tc + 1;
        _worldDataLookup[chunkCoord].FilePosition.Start = newStart;
        _worldDataLookup[chunkCoord].FilePosition.End = newEnd;
    }


    public ChunkData ReadChunkData(Vector2I chunkCoord)
    {
        var filePath = _worldDataLookup[chunkCoord].FilePath;
        var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
        if (file == null || file.GetError() != Error.Ok)
        {
            GD.PrintErr($"Failed to open file for reading: {filePath}");
            return null;
        }
        file.Seek(_worldDataLookup[chunkCoord].FilePosition.Start);
        var length = (int)(_worldDataLookup[chunkCoord].FilePosition.End - _worldDataLookup[chunkCoord].FilePosition.Start);
        var chunkBytes = file.GetBuffer(length);
        file.Close();
        return ChunkData.Deserialize(chunkBytes);
    }

    public async Task CompactFiles()
    {
        foreach (var kvp in _toClean)
        {
            var filePath = kvp.Key;
            var newFilePath = $"{filePath}.tmp";

            using var oldFile = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
            using var newFile = FileAccess.Open(newFilePath, FileAccess.ModeFlags.Write);

            if (oldFile == null || newFile == null)
            {
                GD.PrintErr($"Failed to open files for compaction: {filePath}");
                continue;
            }

            var chunksInFile = new List<(Vector2I coord, FileMap map)>();
            _worldDataLookup.Values.Where(m => m.FilePath == filePath).ToList().ForEach(m =>
            {
                chunksInFile.Add((m.ChunkCoord, m));
            });
            chunksInFile.Sort((a, b) => a.map.FilePosition.Start.CompareTo(b.map.FilePosition.Start));

            foreach (var (coord, map) in chunksInFile)
            {
                oldFile.Seek(map.FilePosition.Start);
                ulong chunkLength = map.FilePosition.End - map.FilePosition.Start;
                byte[] chunkData = oldFile.GetBuffer((int)chunkLength);

                // Write to new file
                ulong newStart = (ulong)newFile.GetPosition();
                newFile.StoreBuffer(chunkData);
                ulong newEnd = (ulong)newFile.GetPosition();

                // Update lookup with new position
                map.FilePosition.Start = newStart;
                map.FilePosition.End = newEnd;
                _worldDataLookup[coord] = map;

                GD.Print($"  Chunk {coord}: {chunkLength} bytes, position {newStart}");
            }

            oldFile.Close();
            newFile.Close();

            // Replace old file with compacted one
            DirAccess.RemoveAbsolute(filePath);
            DirAccess.RenameAbsolute(newFilePath, filePath);

            GD.Print($"Compacted {filePath}: kept {chunksInFile.Count} chunks");
        }

        // Clear the cleanup list after compaction
        _toClean.Clear();

        // Save updated metadata with new positions
        WriteMetadata();
        if (!VerifyIntegrity())
        {
            GD.PrintErr("Compaction resulted in integrity errors!");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Verify that all chunks in the lookup can actually be read from their files.
    /// Call this after compaction to ensure integrity.
    /// </summary>
    public bool VerifyIntegrity()
    {
        GD.Print("Verifying file system integrity...");

        int errors = 0;
        int verified = 0;

        foreach (var kvp in _worldDataLookup)
        {
            var coord = kvp.Key;
            var fileMap = kvp.Value;

            try
            {
                // Try to read the chunk
                var chunkData = ReadChunkData(coord);

                if (chunkData == null)
                {
                    GD.PrintErr($"  ERROR: Chunk {coord} returned null");
                    errors++;
                }
                else if (chunkData.ChunkCoord != coord)
                {
                    GD.PrintErr($"  ERROR: Chunk {coord} has wrong coord in data: {chunkData.ChunkCoord}");
                    errors++;
                }
                else
                {
                    verified++;
                }
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"  ERROR: Failed to read chunk {coord}: {ex.Message}");
                errors++;
            }
        }

        if (errors == 0)
        {
            GD.Print($"Integrity check PASSED: {verified} chunks verified");
            return true;
        }
        else
        {
            GD.PrintErr($"Integrity check FAILED: {errors} errors, {verified} ok");
            return false;
        }
    }

    public bool ShouldCompact()
    {
        _filesToCompact = new HashSet<string>();
        foreach (var entry in _toClean)
        {
            if (entry.Value > _maxFragments)
                _filesToCompact.Add(entry.Key);
        }
        return _filesToCompact.Count > 0;
    }

    public void ReadMetadata()
    {
        var file = FileAccess.Open(_worldDataLookupPath, FileAccess.ModeFlags.Read);
        if (file == null || file.GetError() != Error.Ok)
        {
            GD.PrintErr($"Failed to open world data lookup file: {_worldDataLookupPath}");
            return;
        }
        var reader = new BinaryStreamReader(file.GetBuffer((int)file.GetLength()));
        int entryCount = reader.ReadInt();
        for (int i = 0; i < entryCount; i++)
        {
            var chunkCoord = reader.ReadVector2I();
            var fileMap = FileMap.Deserialize(reader);
            _worldDataLookup[chunkCoord] = fileMap;
        }
        file.Close();
    }

    public void WriteMetadata()
    {
        var file = FileAccess.Open(_worldDataLookupPath, FileAccess.ModeFlags.Write);
        if (file == null || file.GetError() != Error.Ok)
        {
            GD.PrintErr($"Failed to open world data lookup file for writing: {_worldDataLookupPath}");
            return;
        }
        var writer = new BinaryStreamWriter();
        writer.WriteInt(_worldDataLookup.Count);
        foreach (var entry in _worldDataLookup)
        {
            writer.WriteVector2I(entry.Key);
            entry.Value.Serialize(entry.Value, writer);
        }
        file.StoreBuffer(writer.GetBytes());
        file.Close();
    }
}

public class FilePointer
{
    public ulong Start;
    public ulong End;
}

public class FileMap
{
    public string FilePath;
    public Vector2I ChunkCoord;
    public FilePointer FilePosition;
    public bool IsCompressed = true;

    public void Serialize(FileMap fileMap, BinaryStreamWriter writer)
    {
        writer.WriteString(fileMap.FilePath);
        writer.WriteVector2I(fileMap.ChunkCoord);
        writer.WriteULong(fileMap.FilePosition.Start);
        writer.WriteULong(fileMap.FilePosition.End);
        writer.WriteBool(fileMap.IsCompressed);
    }

    public static FileMap Deserialize(BinaryStreamReader reader)
    {
        var fileMap = new FileMap
        {
            FilePath = reader.ReadString(),
            ChunkCoord = reader.ReadVector2I(),
            FilePosition = new FilePointer
            {
                Start = reader.ReadULong(),
                End = reader.ReadULong()
            },
            IsCompressed = reader.ReadBool()
        };
        return fileMap;
    }
}