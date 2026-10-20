using System.Buffers.Binary;

namespace MkTeX.Fonts;

/// <summary>Bounds-checked big-endian reads over an OpenType file.</summary>
internal readonly struct FontReader(byte[] data)
{
    public int Length => data.Length;

    public ushort U16(int offset) => offset >= 0 && offset + 2 <= data.Length
        ? BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2))
        : throw new InvalidDataException("Font table out of range.");

    public short I16(int offset) => unchecked((short)U16(offset));

    public uint U32(int offset) => offset >= 0 && offset + 4 <= data.Length
        ? BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4))
        : throw new InvalidDataException("Font table out of range.");

    public byte U8(int offset) => offset >= 0 && offset < data.Length
        ? data[offset]
        : throw new InvalidDataException("Font table out of range.");

    public string Tag(int offset) => System.Text.Encoding.ASCII.GetString(data, offset, 4);
}
