using System.Buffers.Binary;

namespace GameDevManager.Data.Assets;

/// <summary>
/// Liest Breite und Höhe aus dem Kopf gängiger Bildformate, ohne das Bild zu dekodieren.
/// <para>
/// Bewusst ohne Bildbibliothek: Gebraucht werden zwei Zahlen zur Anzeige, und das Tool wird
/// self-hosted betrieben — eine Abhängigkeit, die Bilder tatsächlich dekodiert, wäre dafür
/// unverhältnismäßig. Für nicht erkannte Formate (etwa SVG) liefert der Leser <c>null</c>;
/// die Maße bleiben dann leer.
/// </para>
/// </summary>
public static class ImageDimensionReader
{
    /// <summary>So viele Bytes reichen für alle unterstützten Formate, auch für JPEG mit EXIF-Vorschau.</summary>
    public const int HeaderSize = 256 * 1024;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static (int Width, int Height)? TryRead(ReadOnlySpan<byte> header) =>
        TryReadPng(header)
        ?? TryReadGif(header)
        ?? TryReadBmp(header)
        ?? TryReadWebp(header)
        ?? TryReadJpeg(header);

    /// <summary>Liest den Anfang eines Stroms und wertet ihn aus. Der Strom muss lesbar sein.</summary>
    public static async Task<(int Width, int Height)?> TryReadAsync(Stream stream, CancellationToken ct = default)
    {
        var buffer = new byte[HeaderSize];
        var read = await stream.ReadAtLeastAsync(buffer, HeaderSize, throwOnEndOfStream: false, ct);

        return TryRead(buffer.AsSpan(0, read));
    }

    // PNG: Signatur, dann der IHDR-Block mit Breite und Höhe als 32-Bit-Werte in Big Endian.
    private static (int, int)? TryReadPng(ReadOnlySpan<byte> header)
    {
        if (header.Length < 24 || !header[..8].SequenceEqual(PngSignature))
        {
            return null;
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
        var height = BinaryPrimitives.ReadInt32BigEndian(header[20..24]);

        return Validate(width, height);
    }

    // GIF: "GIF87a"/"GIF89a", danach die logische Bildschirmgröße als 16-Bit-Werte in Little Endian.
    private static (int, int)? TryReadGif(ReadOnlySpan<byte> header)
    {
        if (header.Length < 10 || header[0] != (byte)'G' || header[1] != (byte)'I' || header[2] != (byte)'F')
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt16LittleEndian(header[6..8]);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(header[8..10]);

        return Validate(width, height);
    }

    // BMP: "BM", danach der Info-Header. Eine negative Höhe heißt nur, dass das Bild von oben
    // nach unten gespeichert ist.
    private static (int, int)? TryReadBmp(ReadOnlySpan<byte> header)
    {
        if (header.Length < 26 || header[0] != (byte)'B' || header[1] != (byte)'M')
        {
            return null;
        }

        var width = BinaryPrimitives.ReadInt32LittleEndian(header[18..22]);
        var height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(header[22..26]));

        return Validate(width, height);
    }

    // WebP kennt drei Varianten, die ihre Maße jeweils woanders ablegen.
    private static (int, int)? TryReadWebp(ReadOnlySpan<byte> header)
    {
        if (header.Length < 30
            || !header[..4].SequenceEqual("RIFF"u8)
            || !header[8..12].SequenceEqual("WEBP"u8))
        {
            return null;
        }

        // VP8X (erweitert): die Leinwandgröße steht als zwei 24-Bit-Werte, jeweils um eins vermindert.
        if (header[12..16].SequenceEqual("VP8X"u8))
        {
            var width = ReadUInt24LittleEndian(header[24..27]) + 1;
            var height = ReadUInt24LittleEndian(header[27..30]) + 1;

            return Validate(width, height);
        }

        // VP8 (verlustbehaftet): nach der Startsequenz 9D 01 2A zwei 14-Bit-Werte.
        if (header[12..16].SequenceEqual("VP8 "u8)
            && header[23] == 0x9D && header[24] == 0x01 && header[25] == 0x2A)
        {
            var width = BinaryPrimitives.ReadUInt16LittleEndian(header[26..28]) & 0x3FFF;
            var height = BinaryPrimitives.ReadUInt16LittleEndian(header[28..30]) & 0x3FFF;

            return Validate(width, height);
        }

        // VP8L (verlustfrei): 14 Bit Breite und 14 Bit Höhe, beide um eins vermindert.
        if (header[12..16].SequenceEqual("VP8L"u8) && header[20] == 0x2F)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(header[21..25]);
            var width = (int)(bits & 0x3FFF) + 1;
            var height = (int)((bits >> 14) & 0x3FFF) + 1;

            return Validate(width, height);
        }

        return null;
    }

    // JPEG: die Maße stehen in einem SOF-Segment, das erst hinter Metadaten wie EXIF kommt.
    // Deshalb muss die Segmentkette durchlaufen werden.
    private static (int, int)? TryReadJpeg(ReadOnlySpan<byte> header)
    {
        if (header.Length < 4 || header[0] != 0xFF || header[1] != 0xD8)
        {
            return null;
        }

        var position = 2;

        while (position + 3 < header.Length)
        {
            if (header[position] != 0xFF)
            {
                position++;
                continue;
            }

            var marker = header[position + 1];
            position += 2;

            // 0xFF wiederholt sich als Füllbyte vor dem eigentlichen Marker.
            if (marker == 0xFF)
            {
                position--;
                continue;
            }

            // Diese Marker stehen für sich allein und haben kein Längenfeld.
            if (marker is 0x01 or 0xD8 or 0xD9 || marker is >= 0xD0 and <= 0xD7)
            {
                continue;
            }

            if (position + 2 > header.Length)
            {
                break;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(header[position..(position + 2)]);
            if (length < 2)
            {
                break;
            }

            if (IsStartOfFrame(marker))
            {
                if (position + 7 > header.Length)
                {
                    break;
                }

                // Segmentinhalt: Genauigkeit (1 Byte), Höhe (2), Breite (2).
                var height = BinaryPrimitives.ReadUInt16BigEndian(header[(position + 3)..(position + 5)]);
                var width = BinaryPrimitives.ReadUInt16BigEndian(header[(position + 5)..(position + 7)]);

                return Validate(width, height);
            }

            position += length;
        }

        return null;
    }

    /// <summary>Alle SOF-Marker außer DHT (C4), JPG (C8) und DAC (CC), die keine Rahmen sind.</summary>
    private static bool IsStartOfFrame(byte marker) =>
        marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC);

    private static int ReadUInt24LittleEndian(ReadOnlySpan<byte> value) =>
        value[0] | (value[1] << 8) | (value[2] << 16);

    private static (int, int)? Validate(int width, int height) =>
        width > 0 && height > 0 ? (width, height) : null;
}
