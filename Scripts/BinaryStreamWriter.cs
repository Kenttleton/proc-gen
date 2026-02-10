using Godot;
using System.Collections.Generic;
using System.Text;

public class BinaryStreamWriter
{
	private List<byte> _buffer = new List<byte>();

	public byte[] GetBytes() => _buffer.ToArray();
	public int Length => _buffer.Count;

	public void WriteByte(byte value)
	{
		_buffer.Add((byte)BinarySerializationHelper.SerializationType.Byte);
		_buffer.Add(value);
	}

	public void WriteByteArray(byte[] bytes)
	{
		_buffer.Add((byte)BinarySerializationHelper.SerializationType.ByteArray);
		var lengthBytes = BinarySerializationHelper.IntToBytes(bytes.Length);
		_buffer.AddRange(lengthBytes);
		_buffer.AddRange(bytes);
	}

	// Write with type tag
	public void WriteInt(int value)
	{
		var bytes = BinarySerializationHelper.IntToBytes(value);
		_buffer.Add((byte)BinarySerializationHelper.SerializationType.Int);
		_buffer.Add((byte)bytes.Length);
		_buffer.AddRange(bytes);
	}

	public void WriteFloat(float value)
	{
		_buffer.Add((byte)BinarySerializationHelper.SerializationType.Float);
		var bytes = BinarySerializationHelper.FloatToBytes(value);
		_buffer.Add((byte)bytes.Length);
		_buffer.AddRange(bytes);
	}

	public void WriteVector3(Vector3 value)
	{
		_buffer.Add((byte)BinarySerializationHelper.SerializationType.Vector3);
		var bytes = BinarySerializationHelper.Vector3ToBytes(value);
		_buffer.Add((byte)bytes.Length);
		_buffer.AddRange(bytes);
	}

	public void WriteVector2(Vector2 value)
	{
		_buffer.Add((byte)BinarySerializationHelper.SerializationType.Vector2);
		var bytes = BinarySerializationHelper.Vector2ToBytes(value);
		_buffer.Add((byte)bytes.Length);
		_buffer.AddRange(bytes);
	}

	public void WriteVector2I(Vector2I value)
	{
		_buffer.Add((byte)BinarySerializationHelper.SerializationType.Vector2I);
		var bytes = BinarySerializationHelper.Vector2IToBytes(value);
		_buffer.Add((byte)bytes.Length);
		_buffer.AddRange(bytes);
	}

	public void WriteString(string value)
	{
		_buffer.Add((byte)BinarySerializationHelper.SerializationType.String);
		var bytes = Encoding.UTF8.GetBytes(value);

		// Write length as 4 bytes for strings (can be long)
		var lengthBytes = BinarySerializationHelper.IntToBytes(bytes.Length);
		_buffer.AddRange(lengthBytes);
		_buffer.AddRange(bytes);
	}

	// Arrays with optional compression
	public void WriteFloatArray(float[] array, bool compress = true)
	{
		var bytes = BinarySerializationHelper.FloatArrayToBytes(array);

		if (compress && bytes.Length > 1024) // Only compress if > 1KB
		{
			bytes = BinarySerializationHelper.Compress(bytes);
			_buffer.Add((byte)BinarySerializationHelper.SerializationType.CompressedFloatArray);
		}
		else
		{
			_buffer.Add((byte)BinarySerializationHelper.SerializationType.FloatArray);
		}

		// Write count
		var countBytes = BinarySerializationHelper.IntToBytes(array.Length);
		_buffer.AddRange(countBytes);

		// Write data length
		var lengthBytes = BinarySerializationHelper.IntToBytes(bytes.Length);
		_buffer.AddRange(lengthBytes);

		// Write data
		_buffer.AddRange(bytes);
	}

	public void WriteIntArray(int[] array, bool compress = true)
	{
		var bytes = BinarySerializationHelper.IntArrayToBytes(array);

		if (compress && bytes.Length > 1024)
		{
			bytes = BinarySerializationHelper.Compress(bytes);
			_buffer.Add((byte)BinarySerializationHelper.SerializationType.CompressedIntArray);
		}
		else
		{
			_buffer.Add((byte)BinarySerializationHelper.SerializationType.IntArray);
		}

		var countBytes = BinarySerializationHelper.IntToBytes(array.Length);
		_buffer.AddRange(countBytes);

		var lengthBytes = BinarySerializationHelper.IntToBytes(bytes.Length);
		_buffer.AddRange(lengthBytes);

		_buffer.AddRange(bytes);
	}

	public void WriteVector3Array(Vector3[] array, bool compress = true)
	{
		var bytes = BinarySerializationHelper.Vector3ArrayToBytes(array);

		if (compress && bytes.Length > 1024)
		{
			bytes = BinarySerializationHelper.Compress(bytes);
			_buffer.Add((byte)BinarySerializationHelper.SerializationType.CompressedVector3Array);
		}
		else
		{
			_buffer.Add((byte)BinarySerializationHelper.SerializationType.Vector3Array);
		}

		var countBytes = BinarySerializationHelper.IntToBytes(array.Length);
		_buffer.AddRange(countBytes);

		var lengthBytes = BinarySerializationHelper.IntToBytes(bytes.Length);
		_buffer.AddRange(lengthBytes);

		_buffer.AddRange(bytes);
	}

	public void WriteVector2Array(Vector2[] array, bool compress = true)
	{
		var bytes = BinarySerializationHelper.Vector2ArrayToBytes(array);

		if (compress && bytes.Length > 1024)
		{
			bytes = BinarySerializationHelper.Compress(bytes);
			_buffer.Add((byte)BinarySerializationHelper.SerializationType.Vector2Array);
		}
		else
		{
			_buffer.Add((byte)BinarySerializationHelper.SerializationType.Vector2Array);
		}

		var countBytes = BinarySerializationHelper.IntToBytes(array.Length);
		_buffer.AddRange(countBytes);

		var lengthBytes = BinarySerializationHelper.IntToBytes(bytes.Length);
		_buffer.AddRange(lengthBytes);

		_buffer.AddRange(bytes);
	}
}