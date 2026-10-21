using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CreateAColony.Import;

/// <summary>
/// Minimal reader for Minecraft's NBT format (gzip, big-endian), enough to read MineColonies
/// .blueprint files. Compounds become dictionaries, lists become List&lt;object&gt;, numbers keep their type.
/// </summary>
public static class NbtReader
{
    public static Dictionary<string, object> ReadGzipFile(string path)
    {
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new BinaryReader(gzip);
        byte type = reader.ReadByte();
        if (type != 10) throw new InvalidDataException("Not an NBT compound");
        ReadString(reader); // root name
        return ReadCompound(reader);
    }

    static Dictionary<string, object> ReadCompound(BinaryReader r)
    {
        var result = new Dictionary<string, object>();
        while (true)
        {
            byte type = r.ReadByte();
            if (type == 0) return result;
            string name = ReadString(r);
            result[name] = ReadPayload(r, type);
        }
    }

    static object ReadPayload(BinaryReader r, byte type) => type switch
    {
        1 => r.ReadSByte(),
        2 => ReadShort(r),
        3 => ReadInt(r),
        4 => ReadLong(r),
        5 => BitConverter.Int32BitsToSingle(ReadInt(r)),
        6 => BitConverter.Int64BitsToDouble(ReadLong(r)),
        7 => r.ReadBytes(ReadInt(r)),
        8 => ReadString(r),
        9 => ReadList(r),
        10 => ReadCompound(r),
        11 => ReadArray(r, ReadInt),
        12 => ReadArray(r, ReadLong),
        _ => throw new InvalidDataException("Unknown NBT tag " + type)
    };

    static List<object> ReadList(BinaryReader r)
    {
        byte type = r.ReadByte();
        int count = ReadInt(r);
        var list = new List<object>(Math.Max(0, count));
        for (int i = 0; i < count; i++) list.Add(ReadPayload(r, type));
        return list;
    }

    static T[] ReadArray<T>(BinaryReader r, Func<BinaryReader, T> read)
    {
        var array = new T[ReadInt(r)];
        for (int i = 0; i < array.Length; i++) array[i] = read(r);
        return array;
    }

    static short ReadShort(BinaryReader r) => (short)((r.ReadByte() << 8) | r.ReadByte());

    static int ReadInt(BinaryReader r) => (r.ReadByte() << 24) | (r.ReadByte() << 16) | (r.ReadByte() << 8) | r.ReadByte();

    static long ReadLong(BinaryReader r) => ((long)ReadInt(r) << 32) | (uint)ReadInt(r);

    static string ReadString(BinaryReader r)
    {
        int length = (ushort)ReadShort(r);
        return Encoding.UTF8.GetString(r.ReadBytes(length));
    }
}
