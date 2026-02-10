using System;
using System.Text;
using Godot;
public class BinaryStreamReader
{
	private byte[] _data;
	private int _position = 0;

	public BinaryStreamReader(byte[] data)
	{
		_data = data;
	}

	public bool HasData => _position < _data.Length;
	public int Position => _position;
	public int Remaining => _data.Length - _position;

	private byte[] ReadBytes(int count)
	{
		if (_position + count > _data.Length)
			throw new Exception($"Not enough data. Need {count}, have {Remaining}");

		byte[] result = new byte[count];
		Buffer.BlockCopy(_data, _position, result, 0, count);
		_position += count;
		return result;
	}

	public object ReadNext()
	{
		if (!HasData)
			throw new Exception("No more data to read");

		var type = (BinarySerializationHelper.SerializationType)_data[_position++];

		switch (type)
		{
			case BinarySerializationHelper.SerializationType.Byte:
				{
					return ReadBytes(1);
				}
			case BinarySerializationHelper.SerializationType.Int:
				{
					byte length = _data[_position++];
					var bytes = ReadBytes(length);
					return BinarySerializationHelper.BytesToInt(bytes);
				}

			case BinarySerializationHelper.SerializationType.Float:
				{
					byte length = _data[_position++];
					var bytes = ReadBytes(length);
					return BinarySerializationHelper.BytesToFloat(bytes);
				}

			case BinarySerializationHelper.SerializationType.Vector3:
				{
					byte length = _data[_position++];
					var bytes = ReadBytes(length);
					return BinarySerializationHelper.BytesToVector3(bytes);
				}

			case BinarySerializationHelper.SerializationType.Vector2:
				{
					byte length = _data[_position++];
					var bytes = ReadBytes(length);
					return BinarySerializationHelper.BytesToVector2(bytes);
				}

			case BinarySerializationHelper.SerializationType.Vector2I:
				{
					byte length = _data[_position++];
					var bytes = ReadBytes(length);
					return BinarySerializationHelper.BytesToVector2I(bytes);
				}

			case BinarySerializationHelper.SerializationType.String:
				{
					var lengthBytes = ReadBytes(4);
					int length = BinarySerializationHelper.BytesToInt(lengthBytes);
					var stringBytes = ReadBytes(length);
					return Encoding.UTF8.GetString(stringBytes);
				}

			case BinarySerializationHelper.SerializationType.ByteArray:
				{
					var lengthBytes = ReadBytes(4);
					int length = BinarySerializationHelper.BytesToInt(lengthBytes);
					return ReadBytes(length);
				}
			case BinarySerializationHelper.SerializationType.FloatArray:
			case BinarySerializationHelper.SerializationType.CompressedFloatArray:
				{
					var countBytes = ReadBytes(4);
					int count = BinarySerializationHelper.BytesToInt(countBytes);

					var lengthBytes = ReadBytes(4);
					int dataLength = BinarySerializationHelper.BytesToInt(lengthBytes);

					var bytes = ReadBytes(dataLength);

					if (type == BinarySerializationHelper.SerializationType.CompressedFloatArray)
						bytes = BinarySerializationHelper.Decompress(bytes);

					return BinarySerializationHelper.BytesToFloatArray(bytes, count);
				}

			case BinarySerializationHelper.SerializationType.IntArray:
			case BinarySerializationHelper.SerializationType.CompressedIntArray:
				{
					var countBytes = ReadBytes(4);
					int count = BinarySerializationHelper.BytesToInt(countBytes);

					var lengthBytes = ReadBytes(4);
					int dataLength = BinarySerializationHelper.BytesToInt(lengthBytes);

					var bytes = ReadBytes(dataLength);

					if (type == BinarySerializationHelper.SerializationType.CompressedIntArray)
						bytes = BinarySerializationHelper.Decompress(bytes);

					return BinarySerializationHelper.BytesToIntArray(bytes, count);
				}

			case BinarySerializationHelper.SerializationType.Vector3Array:
			case BinarySerializationHelper.SerializationType.CompressedVector3Array:
				{
					var countBytes = ReadBytes(4);
					int count = BinarySerializationHelper.BytesToInt(countBytes);

					var lengthBytes = ReadBytes(4);
					int dataLength = BinarySerializationHelper.BytesToInt(lengthBytes);

					var bytes = ReadBytes(dataLength);

					if (type == BinarySerializationHelper.SerializationType.CompressedVector3Array)
						bytes = BinarySerializationHelper.Decompress(bytes);

					return BinarySerializationHelper.BytesToVector3Array(bytes, count);
				}

			case BinarySerializationHelper.SerializationType.Vector2Array:
				{
					var countBytes = ReadBytes(4);
					int count = BinarySerializationHelper.BytesToInt(countBytes);

					var lengthBytes = ReadBytes(4);
					int dataLength = BinarySerializationHelper.BytesToInt(lengthBytes);

					var bytes = ReadBytes(dataLength);

					return BinarySerializationHelper.BytesToVector2Array(bytes, count);
				}

			default:
				throw new Exception($"Unknown serialization type: {type}");
		}
	}

	// Typed read methods for convenience
	public byte ReadSingleByte() => ((byte[])ReadNext())[0];
	public byte[] ReadByteArray() => (byte[])ReadNext();
	public int ReadInt() => (int)ReadNext();
	public float ReadFloat() => (float)ReadNext();
	public Vector3 ReadVector3() => (Vector3)ReadNext();
	public Vector2 ReadVector2() => (Vector2)ReadNext();
	public Vector2I ReadVector2I() => (Vector2I)ReadNext();
	public string ReadString() => (string)ReadNext();
	public float[] ReadFloatArray() => (float[])ReadNext();
	public int[] ReadIntArray() => (int[])ReadNext();
	public Vector3[] ReadVector3Array() => (Vector3[])ReadNext();
	public Vector2[] ReadVector2Array() => (Vector2[])ReadNext();
}