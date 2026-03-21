using System.Buffers.Binary;
using System.Text;

namespace Tyflocentrum.Windows.Domain.Audio;

public static class WavVoiceLimiter
{
    private const double ThresholdDb = -22.0;
    private const double KneeDb = 8.0;
    private const double Ratio = 2.8;
    private const double BaseMakeupGainDb = 3.5;
    private const double TargetPeakDb = -4.5;
    private const double TargetRmsDb = -23.0;
    private const double MaxAdaptiveMakeupGainDb = 10.0;
    private const double CeilingDb = -1.2;
    private const double AttackSeconds = 0.005;
    private const double ReleaseSeconds = 0.220;
    private const double SilenceFloor = 1e-6;
    private const double SoftLimitCurve = 3.0;

    public static void Process(string inputPath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var descriptor = ReadDescriptor(input);
        ValidateFormat(descriptor.Format);
        var analysis = Analyze(input, descriptor);

        input.Position = 0;
        using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var headerBytes = new byte[descriptor.DataOffset];
        ReadExactly(input, headerBytes, 0, headerBytes.Length);
        output.Write(headerBytes, 0, headerBytes.Length);

        var channelCount = descriptor.Format.ChannelCount;
        var frameSize = descriptor.Format.BlockAlign;
        var sampleRate = descriptor.Format.SampleRate;
        var attackCoefficient = Math.Exp(-1.0 / (sampleRate * AttackSeconds));
        var releaseCoefficient = Math.Exp(-1.0 / (sampleRate * ReleaseSeconds));
        var ceilingLinear = DbToLinear(CeilingDb);
        var adaptiveMakeupGainDb = ComputeAdaptiveMakeupGainDb(analysis);
        var makeupLinear = DbToLinear(BaseMakeupGainDb + adaptiveMakeupGainDb);
        var currentGainDb = 0.0;

        var bufferSize = Math.Max(frameSize * 256, 81920);
        bufferSize -= bufferSize % frameSize;
        if (bufferSize <= 0)
        {
            bufferSize = frameSize;
        }

        var buffer = new byte[bufferSize];
        long remainingDataBytes = descriptor.DataLength;

        while (remainingDataBytes > 0)
        {
            var bytesToRead = (int)Math.Min(buffer.Length, remainingDataBytes);
            bytesToRead -= bytesToRead % frameSize;
            if (bytesToRead <= 0)
            {
                bytesToRead = frameSize;
            }

            ReadExactly(input, buffer, 0, bytesToRead);
            ProcessBlock(
                buffer.AsSpan(0, bytesToRead),
                channelCount,
                frameSize,
                attackCoefficient,
                releaseCoefficient,
                makeupLinear,
                ceilingLinear,
                ref currentGainDb
            );
            output.Write(buffer, 0, bytesToRead);
            remainingDataBytes -= bytesToRead;
        }

        input.CopyTo(output);
        output.Flush();
    }

    private static WavAnalysis Analyze(Stream input, WavDescriptor descriptor)
    {
        input.Position = descriptor.DataOffset;

        var frameSize = descriptor.Format.BlockAlign;
        var bufferSize = Math.Max(frameSize * 256, 81920);
        bufferSize -= bufferSize % frameSize;
        if (bufferSize <= 0)
        {
            bufferSize = frameSize;
        }

        var buffer = new byte[bufferSize];
        long remainingDataBytes = descriptor.DataLength;
        double peakLinear = SilenceFloor;
        double sumSquares = 0.0;
        long sampleCount = 0;

        while (remainingDataBytes > 0)
        {
            var bytesToRead = (int)Math.Min(buffer.Length, remainingDataBytes);
            bytesToRead -= bytesToRead % frameSize;
            if (bytesToRead <= 0)
            {
                bytesToRead = frameSize;
            }

            ReadExactly(input, buffer, 0, bytesToRead);
            for (var sampleOffset = 0; sampleOffset < bytesToRead; sampleOffset += 2)
            {
                var sample = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(sampleOffset, 2));
                var normalized = sample / 32768.0;
                var abs = Math.Abs(normalized);
                if (abs > peakLinear)
                {
                    peakLinear = abs;
                }

                sumSquares += normalized * normalized;
                sampleCount++;
            }

            remainingDataBytes -= bytesToRead;
        }

        var rmsLinear = sampleCount > 0 ? Math.Sqrt(sumSquares / sampleCount) : SilenceFloor;
        return new WavAnalysis(Math.Max(peakLinear, SilenceFloor), Math.Max(rmsLinear, SilenceFloor));
    }

    private static void ProcessBlock(
        Span<byte> pcmData,
        int channelCount,
        int frameSize,
        double attackCoefficient,
        double releaseCoefficient,
        double makeupLinear,
        double ceilingLinear,
        ref double currentGainDb
    )
    {
        for (var frameOffset = 0; frameOffset < pcmData.Length; frameOffset += frameSize)
        {
            var framePeak = SilenceFloor;
            for (var channel = 0; channel < channelCount; channel++)
            {
                var sampleOffset = frameOffset + (channel * 2);
                var sample = BinaryPrimitives.ReadInt16LittleEndian(
                    pcmData.Slice(sampleOffset, 2)
                );
                var normalized = Math.Abs(sample / 32768.0);
                if (normalized > framePeak)
                {
                    framePeak = normalized;
                }
            }

            var targetGainDb = ComputeTargetGainDb(framePeak);
            var coefficient = targetGainDb < currentGainDb
                ? attackCoefficient
                : releaseCoefficient;
            currentGainDb = targetGainDb + (coefficient * (currentGainDb - targetGainDb));
            var frameGain = DbToLinear(currentGainDb) * makeupLinear;

            for (var channel = 0; channel < channelCount; channel++)
            {
                var sampleOffset = frameOffset + (channel * 2);
                var sample = BinaryPrimitives.ReadInt16LittleEndian(
                    pcmData.Slice(sampleOffset, 2)
                );
                var normalized = sample / 32768.0;
                var processed = normalized * frameGain;
                processed = SoftLimit(processed, ceilingLinear);
                var limited = Math.Clamp(processed, -1.0, 1.0);
                var outputSample = (short)Math.Round(limited * short.MaxValue);
                BinaryPrimitives.WriteInt16LittleEndian(
                    pcmData.Slice(sampleOffset, 2),
                    outputSample
                );
            }
        }
    }

    private static double ComputeTargetGainDb(double peakLinear)
    {
        if (peakLinear <= SilenceFloor)
        {
            return 0.0;
        }

        var inputDb = 20.0 * Math.Log10(peakLinear);
        var halfKnee = KneeDb / 2.0;
        if (inputDb <= ThresholdDb - halfKnee)
        {
            return 0.0;
        }

        if (inputDb >= ThresholdDb + halfKnee)
        {
            var compressedDb = ThresholdDb + ((inputDb - ThresholdDb) / Ratio);
            return compressedDb - inputDb;
        }

        var kneePosition = inputDb - (ThresholdDb - halfKnee);
        return ((1.0 / Ratio) - 1.0) * kneePosition * kneePosition / (2.0 * KneeDb);
    }

    private static double ComputeAdaptiveMakeupGainDb(WavAnalysis analysis)
    {
        var peakDb = LinearToDb(analysis.PeakLinear);
        var rmsDb = LinearToDb(analysis.RmsLinear);
        var compressedPeakDb = peakDb + ComputeTargetGainDb(analysis.PeakLinear);

        var peakLiftDb = Math.Clamp(
            TargetPeakDb - compressedPeakDb,
            0.0,
            MaxAdaptiveMakeupGainDb
        );
        var rmsLiftDb = Math.Clamp(TargetRmsDb - rmsDb, 0.0, MaxAdaptiveMakeupGainDb);

        return Math.Min(MaxAdaptiveMakeupGainDb, Math.Min(peakLiftDb, rmsLiftDb));
    }

    private static double SoftLimit(double value, double ceilingLinear)
    {
        var magnitude = Math.Abs(value);
        if (magnitude <= ceilingLinear)
        {
            return value;
        }

        var normalizedOverflow = Math.Clamp(
            (magnitude - ceilingLinear) / Math.Max(1.0 - ceilingLinear, SilenceFloor),
            0.0,
            1.0
        );
        var shapedOverflow =
            (1.0 - Math.Exp(-SoftLimitCurve * normalizedOverflow))
            / (1.0 - Math.Exp(-SoftLimitCurve));
        var limited = ceilingLinear + ((1.0 - ceilingLinear) * shapedOverflow);
        return Math.Sign(value) * Math.Min(limited, 0.995);
    }

    private static double DbToLinear(double valueDb)
    {
        return Math.Pow(10.0, valueDb / 20.0);
    }

    private static double LinearToDb(double valueLinear)
    {
        return 20.0 * Math.Log10(Math.Max(valueLinear, SilenceFloor));
    }

    private static void ValidateFormat(WavFormat format)
    {
        if (format.AudioFormat != 1)
        {
            throw new InvalidDataException("Limiter obsługuje tylko liniowe pliki WAV PCM.");
        }

        if (format.BitsPerSample != 16)
        {
            throw new InvalidDataException("Limiter obsługuje tylko pliki WAV 16-bit PCM.");
        }

        if (format.ChannelCount is <= 0 or > 2)
        {
            throw new InvalidDataException("Limiter obsługuje tylko pliki mono albo stereo.");
        }

        if (format.BlockAlign != format.ChannelCount * (format.BitsPerSample / 8))
        {
            throw new InvalidDataException("Plik WAV ma nieprawidłowe wyrównanie ramek.");
        }
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
        Span<byte> chunkHeader = stackalloc byte[8];

        while (stream.Position + 8 <= stream.Length)
        {
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
                dataOffset = stream.Position;
                dataLength = chunkSize;
                stream.Seek(chunkSize, SeekOrigin.Current);
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

        if (format is null || dataOffset < 0 || dataLength < 0)
        {
            throw new InvalidDataException("Plik WAV nie zawiera wymaganych sekcji fmt/data.");
        }

        return new WavDescriptor(format.Value, dataOffset, dataLength);
    }

    private static void ReadExactly(Stream stream, Span<byte> destination)
    {
        var totalRead = 0;
        while (totalRead < destination.Length)
        {
            var read = stream.Read(destination[totalRead..]);
            if (read <= 0)
            {
                throw new EndOfStreamException("Nie udało się odczytać pełnych danych WAV.");
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

    private readonly record struct WavDescriptor(WavFormat Format, long DataOffset, long DataLength);

    private readonly record struct WavAnalysis(double PeakLinear, double RmsLinear);

    private readonly record struct WavFormat(
        ushort AudioFormat,
        ushort ChannelCount,
        uint SampleRate,
        uint ByteRate,
        ushort BlockAlign,
        ushort BitsPerSample
    );
}
