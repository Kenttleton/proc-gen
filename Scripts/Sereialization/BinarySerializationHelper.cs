using Godot;
using System;
using System.IO;
using System.IO.Compression;

public static class BinarySerializationHelper
{
    public enum SerializationType : byte
    {
        Bool = 0,
        Byte = 1,
        Int = 2,
        Float = 3,
        Double = 4,
        ULong = 5,
        UShort = 6,
        Vector3 = 7,
        Vector2I = 8,
        Vector2 = 9,
        String = 10,
        ByteArray = 11,
        IntArray = 12,
        FloatArray = 13,
        Vector3Array = 14,
        Vector2Array = 15,
        // Compressed versions for large arrays
        CompressedIntArray = 16,
        CompressedFloatArray = 17,
        CompressedVector2Array = 18,
        CompressedVector3Array = 19,
        Guid = 20
    }

    // ============= LOW-LEVEL BYTE ARRAY CONVERSIONS =============

    public static byte BoolToByte(bool value)
    {
        return value ? (byte)1 : (byte)0;
    }

    public static bool ByteToBool(byte value)
    {
        return value != 0;
    }

    public static byte[] IntToBytes(int value)
    {
        return BitConverter.GetBytes(value);
    }

    public static int BytesToInt(byte[] bytes, int offset = 0)
    {
        return BitConverter.ToInt32(bytes, offset);
    }

    public static byte[] FloatToBytes(float value)
    {
        return BitConverter.GetBytes(value);
    }

    public static float BytesToFloat(byte[] bytes, int offset = 0)
    {
        return BitConverter.ToSingle(bytes, offset);
    }

    public static byte[] DoubleToBytes(double value)
    {
        return BitConverter.GetBytes(value);
    }

    public static double BytesToDouble(byte[] bytes, int offset = 0)
    {
        return BitConverter.ToDouble(bytes, offset);
    }

    public static byte[] ULongToBytes(ulong value)
    {
        return BitConverter.GetBytes(value);
    }

    public static ulong BytesToULong(byte[] bytes, int offset = 0)
    {
        return BitConverter.ToUInt64(bytes, offset);
    }

    public static byte[] Vector3ToBytes(Vector3 v)
    {
        byte[] bytes = new byte[12];
        Buffer.BlockCopy(BitConverter.GetBytes(v.X), 0, bytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(v.Y), 0, bytes, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(v.Z), 0, bytes, 8, 4);
        return bytes;
    }

    public static Vector3 BytesToVector3(byte[] bytes, int offset = 0)
    {
        return new Vector3(
            BitConverter.ToSingle(bytes, offset),
            BitConverter.ToSingle(bytes, offset + 4),
            BitConverter.ToSingle(bytes, offset + 8)
        );
    }

    public static byte[] Vector2ToBytes(Vector2 v)
    {
        byte[] bytes = new byte[8];
        Buffer.BlockCopy(BitConverter.GetBytes(v.X), 0, bytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(v.Y), 0, bytes, 4, 4);
        return bytes;
    }

    public static Vector2 BytesToVector2(byte[] bytes, int offset = 0)
    {
        return new Vector2(
            BitConverter.ToSingle(bytes, offset),
            BitConverter.ToSingle(bytes, offset + 4)
        );
    }

    public static byte[] Vector2IToBytes(Vector2I v)
    {
        byte[] bytes = new byte[8];
        Buffer.BlockCopy(BitConverter.GetBytes(v.X), 0, bytes, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(v.Y), 0, bytes, 4, 4);
        return bytes;
    }

    public static Vector2I BytesToVector2I(byte[] bytes, int offset = 0)
    {
        return new Vector2I(
            BitConverter.ToInt32(bytes, offset),
            BitConverter.ToInt32(bytes, offset + 4)
        );
    }

    // ============= ARRAY CONVERSIONS (BULK) =============

    public static byte[] FloatArrayToBytes(float[] array)
    {
        byte[] bytes = new byte[array.Length * 4];
        Buffer.BlockCopy(array, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public static float[] BytesToFloatArray(byte[] bytes, int count, int offset = 0)
    {
        float[] array = new float[count];
        Buffer.BlockCopy(bytes, offset, array, 0, count * 4);
        return array;
    }

    public static byte[] IntArrayToBytes(int[] array)
    {
        byte[] bytes = new byte[array.Length * 4];
        Buffer.BlockCopy(array, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public static int[] BytesToIntArray(byte[] bytes, int count, int offset = 0)
    {
        int[] array = new int[count];
        Buffer.BlockCopy(bytes, offset, array, 0, count * 4);
        return array;
    }

    public static byte[] Vector3ArrayToBytes(Vector3[] array)
    {
        byte[] bytes = new byte[array.Length * 12];
        int offset = 0;
        foreach (var v in array)
        {
            var vBytes = Vector3ToBytes(v);
            Buffer.BlockCopy(vBytes, 0, bytes, offset, 12);
            offset += 12;
        }
        return bytes;
    }

    public static Vector3[] BytesToVector3Array(byte[] bytes, int count, int offset = 0)
    {
        Vector3[] array = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            array[i] = BytesToVector3(bytes, offset + (i * 12));
        }
        return array;
    }

    public static byte[] Vector2ArrayToBytes(Vector2[] array)
    {
        byte[] bytes = new byte[array.Length * 8];
        int offset = 0;
        foreach (var v in array)
        {
            var vBytes = Vector2ToBytes(v);
            Buffer.BlockCopy(vBytes, 0, bytes, offset, 8);
            offset += 8;
        }
        return bytes;
    }

    public static Vector2[] BytesToVector2Array(byte[] bytes, int count, int offset = 0)
    {
        Vector2[] array = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            array[i] = BytesToVector2(bytes, offset + (i * 8));
        }
        return array;
    }

    // ============= COMPRESSION HELPERS =============

    public static byte[] Compress(byte[] data)
    {
        using (var memoryStream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
            {
                gzipStream.Write(data, 0, data.Length);
            }
            return memoryStream.ToArray();
        }
    }

    public static byte[] Decompress(byte[] data)
    {
        using (var compressedStream = new MemoryStream(data))
        using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
        using (var resultStream = new MemoryStream())
        {
            gzipStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }
    }

    // ============= UTILITY CONVERSIONS =============

    public static byte[] GuidToBytes(Guid id)
    {
        return id.ToByteArray();
    }

    public static Guid BytesToGuid(byte[] bytes)
    {
        return new Guid(bytes);
    }
}