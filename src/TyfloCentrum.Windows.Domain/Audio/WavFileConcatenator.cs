using System.Buffers.Binary;
using System.Text;

namespace TyfloCentrum.Windows.Domain.Audio;

public static class WavFileConcatenator
{
    public static void Concatenate(IReadOnlyList<string> inputPaths, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(inputPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (inputPaths.Count == 0)
        {
            throw new InvalidDataException("Brak plików WAV do połączenia.");
        }

        using var output = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        var descriptors = new List<WavDescriptor>(inputPaths.Count);

        foreach (var inputPath in inputPaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

            using var probeStream = new FileStream(
                inputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );
            descriptors.Add(ReadDescriptor(probeStream));
        }

        var reference = descriptors[0];
        for (var index = 1; index < descriptors.Count; index++)
        {
            if (!reference.Format.Equals(descriptors[index].Format))
            {
                throw new InvalidDataException("Nie można połączyć plików WAV o różnych parametrach audio.");
            }
        }

        using (var headerStream = new FileStream(
            inputPaths[0],
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        ))
        {
            var headerBytes = new byte[reference.DataOffset];
            ReadExactly(headerStream, headerBytes, 0, headerBytes.Length);
            output.Write(headerBytes, 0, headerBytes.Length);
        }

        long totalDataLength = 0;
        foreach (var (inputPath, descriptor) in inputPaths.Zip(descriptors))
        {
            using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            input.Position = descriptor.DataOffset;
            CopyExactly(input, output, descriptor.DataLength);
            totalDataLength += descriptor.DataLength;
        }

        if (totalDataLength > uint.MaxValue)
        {
            throw new InvalidDataException("Połączony plik WAV jest zbyt duży.");
        }

        output.Flush();
        WriteUInt32(output, 4, checked((uint)(output.Length - 8)));
        WriteUInt32(output, reference.DataSizeOffset, checked((uint)totalDataLength));
        output.Flush();
    }

    private static WavDescriptor ReadDescriptor(Stream stream)
    {
        Span<byte> header = stackalloc byte[12];
        ReadExactly(stream, header);

        if (!header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("Plik nie jest poprawnym kontenerem WAV.");
        }

        WavFormat? format = null;
        long dataOffset = -1;
        long dataLength = -1;
        long dataSizeOffset = -1;
        Span<byte> chunkHeader = stackalloc byte[8];

        while (stream.Position + 8 <= stream.Length)
        {
            var chunkStart = stream.Position;
            ReadExactly(stream, chunkHeader);

            var chunkId = Encoding.ASCII.GetString(chunkHeader[..4]);
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..8]);

            if (chunkId == "fmt ")
            {
                if (chunkSize < 16)
                {
                    throw new InvalidDataException("Sekcja fmt WAV jest niepoprawna.");
                }

                var formatBytes = new byte[chunkSize];
                ReadExactly(stream, formatBytes, 0, formatBytes.Length);
                format = new WavFormat(
                    AudioFormat: BinaryPrimitives.ReadUInt16LittleEndian(formatBytes.AsSpan(0, 2)),
                    ChannelCount: BinaryPrimitives.ReadUInt16LittleEndian(formatBytes.AsSpan(2, 2)),
                    SampleRate: BinaryPrimitives.ReadUInt32LittleEndian(formatBytes.AsSpan(4, 4)),
                    ByteRate: BinaryPrimitives.ReadUInt32LittleEndian(formatBytes.AsSpan(8, 4)),
                    BlockAlign: BinaryPrimitives.ReadUInt16LittleEndian(formatBytes.AsSpan(12, 2)),
                    BitsPerSample: BinaryPrimitives.ReadUInt16LittleEndian(formatBytes.AsSpan(14, 2))
                );
            }
            else if (chunkId == "data")
            {
                dataSizeOffset = chunkStart + 4;
                dataOffset = stream.Position;
                dataLength = chunkSize;
                break;
            }
            else
            {
                stream.Seek(chunkSize, SeekOrigin.Current);
            }

            if ((chunkSize & 1) == 1)
            {
                stream.Seek(1, SeekOrigin.Current);
            }
        }

        if (format is null || dataOffset < 0 || dataLength < 0 || dataSizeOffset < 0)
        {
            throw new InvalidDataException("Plik WAV nie zawiera wymaganych sekcji fmt/data.");
        }

        return new WavDescriptor(format.Value, dataOffset, dataLength, dataSizeOffset);
    }

    private static void WriteUInt32(Stream stream, long offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        stream.Position = offset;
        stream.Write(bytes);
    }

    private static void CopyExactly(Stream input, Stream output, long byteCount)
    {
        var buffer = new byte[81920];
        long remaining = byteCount;

        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = input.Read(buffer, 0, toRead);
            if (read <= 0)
            {
                throw new EndOfStreamException("Plik WAV zakończył się przedwcześnie podczas łączenia.");
            }

            output.Write(buffer, 0, read);
            remaining -= read;
        }
    }

    private static void ReadExactly(Stream stream, Span<byte> destination)
    {
        var totalRead = 0;
        while (totalRead < destination.Length)
        {
            var read = stream.Read(destination[totalRead..]);
            if (read <= 0)
            {
                throw new EndOfStreamException("Nie udało się odczytać pełnego nagłówka WAV.");
            }

            totalRead += read;
        }
    }

    private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read <= 0)
            {
                throw new EndOfStreamException("Nie udało się odczytać pełnych danych WAV.");
            }

            totalRead += read;
        }
    }

    private readonly record struct WavDescriptor(
        WavFormat Format,
        long DataOffset,
        long DataLength,
        long DataSizeOffset
    );

    private readonly record struct WavFormat(
        ushort AudioFormat,
        ushort ChannelCount,
        uint SampleRate,
        uint ByteRate,
        ushort BlockAlign,
        ushort BitsPerSample
    );
}
