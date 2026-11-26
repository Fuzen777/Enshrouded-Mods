using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Kiln;

namespace BetterSpells;

/// <summary>
/// The Skills tab's icons (1.2.2): an active skill's icon is a keen::UiTextureResource (its SkillTreeResource node's
/// iconActive). Its pixels are the game's own, read at run time (nothing of the game's is shipped): the resource names a
/// content blob (data.hash) with its format and mip levels, Kiln.Assets reads the blob, GameTexture decodes the level of at
/// least 64 pixels, and it is handed to Kiln's overlay as a PNG. Runs on the game client's main thread (Client.Post).
/// </summary>
internal static class SkillIcons
{
    private const int WantedSide = 64;

    public static UiImage? Load(string textureGuid)
    {
        ReflectedResource? resource = ReflectedResources.Get(Guid.Parse(textureGuid), "keen::UiTextureResource", 0);
        if (resource == null)
        {
            throw new InvalidOperationException("no UiTextureResource " + textureGuid);
        }
        uint size = Convert.ToUInt32(resource["data.hash.size"]);
        uint h0 = Convert.ToUInt32(resource["data.hash.hash0"]);
        uint h1 = Convert.ToUInt32(resource["data.hash.hash1"]);
        uint h2 = Convert.ToUInt32(resource["data.hash.hash2"]);
        int width = Convert.ToInt32(resource["size.x"]);
        int height = Convert.ToInt32(resource["size.y"]);
        var format = (GamePixelFormat)Convert.ToInt32(resource["format"]);
        int levels = Math.Max(1, Convert.ToInt32(resource["levelCount"]));
        // keen::ContentHash is four words (size, hash0..2), as Kiln's ContentHash keeps them.
        Span<uint> words = stackalloc uint[] { size, h0, h1, h2 };
        ContentHash hash = MemoryMarshal.Read<ContentHash>(MemoryMarshal.AsBytes(words));
        byte[] blob = Assets.ReadBlob(hash) ?? throw new InvalidOperationException($"no blob {hash} for {textureGuid}");
        int mip = 0;
        while (mip + 1 < levels && (width >> (mip + 1)) >= WantedSide && (height >> (mip + 1)) >= WantedSide)
        {
            mip++;
        }
        int w = Math.Max(1, width >> mip);
        int h = Math.Max(1, height >> mip);
        byte[] rgba = levels > 1 ? GameTexture.DecodeMip(blob, format, width, height, mip) : GameTexture.Decode(blob, format, width, height);
        while (w >= 2 * WantedSide && h >= 2 * WantedSide)
        {
            (rgba, w, h) = Half(rgba, w, h);
        }
        return Ui.LoadImage(Png(rgba, w, h));
    }

    // A box filter to half the size.
    private static (byte[] Rgba, int W, int H) Half(byte[] rgba, int w, int h)
    {
        int nw = w / 2, nh = h / 2;
        var output = new byte[nw * nh * 4];
        for (int y = 0; y < nh; y++)
        {
            for (int x = 0; x < nw; x++)
            {
                for (int c = 0; c < 4; c++)
                {
                    int sum = rgba[((2 * y) * w + 2 * x) * 4 + c] + rgba[((2 * y) * w + 2 * x + 1) * 4 + c]
                        + rgba[((2 * y + 1) * w + 2 * x) * 4 + c] + rgba[((2 * y + 1) * w + 2 * x + 1) * 4 + c];
                    output[(y * nw + x) * 4 + c] = (byte)(sum / 4);
                }
            }
        }
        return (output, nw, nh);
    }

    // An 8-bit RGBA PNG.
    private static byte[] Png(byte[] rgba, int w, int h)
    {
        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (int y = 0; y < h; y++)
            {
                z.WriteByte(0);
                z.Write(rgba, y * w * 4, w * 4);
            }
        }
        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), w);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), h);
        header[8] = 8;
        header[9] = 6;
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", raw.ToArray());
        Chunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        output.Write(word);
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        uint crc = Crc(Crc(0xffffffffu, typeBytes), data) ^ 0xffffffffu;
        BinaryPrimitives.WriteUInt32BigEndian(word, crc);
        output.Write(word);
    }

    private static readonly uint[] CrcTable = MakeCrcTable();

    private static uint[] MakeCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xedb88320u ^ (c >> 1) : c >> 1;
            }
            table[n] = c;
        }
        return table;
    }

    private static uint Crc(uint crc, byte[] data)
    {
        foreach (byte b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xff] ^ (crc >> 8);
        }
        return crc;
    }
}
