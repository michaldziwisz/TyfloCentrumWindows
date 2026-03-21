using System.Buffers.Binary;
using TyfloCentrum.Windows.Domain.Audio;
using Xunit;

namespace TyfloCentrum.Windows.Tests.Domain;

public sealed class WavVoiceLimiterTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"wav-limiter-{Guid.NewGuid():N}"
    );

    public WavVoiceLimiterTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Process_limits_peaks_and_preserves_riff_layout()
    {
        var inputPath = Path.Combine(_tempDirectory, "input.wav");
        var outputPath = Path.Combine(_tempDirectory, "output.wav");
        var inputSamples = Enumerable.Repeat<short>(short.MaxValue, 4096).ToArray();
        File.WriteAllBytes(inputPath, CreatePcm16Wav(inputSamples, sampleRate: 44100));

        WavVoiceLimiter.Process(inputPath, outputPath);

        var outputBytes = File.ReadAllBytes(outputPath);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(outputBytes, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(outputBytes, 8, 4));
        Assert.Equal(
            (uint)(outputBytes.Length - 8),
            BinaryPrimitives.ReadUInt32LittleEndian(outputBytes.AsSpan(4, 4))
        );

        var outputSamples = ReadPcm16Samples(outputBytes);
        Assert.Equal(inputSamples.Length, outputSamples.Length);
        Assert.True(outputSamples.Max(sample => Math.Abs((int)sample)) < 32700);
        Assert.Contains(outputSamples, sample => sample != inputSamples[0]);
    }

    [Fact]
    public void Process_rejects_non_pcm16_wav()
    {
        var inputPath = Path.Combine(_tempDirectory, "input-8bit.wav");
        var outputPath = Path.Combine(_tempDirectory, "output.wav");
        File.WriteAllBytes(inputPath, CreatePcm8Wav([0, 127, 255], sampleRate: 8000));

        var exception = Assert.Throws<InvalidDataException>(() =>
            WavVoiceLimiter.Process(inputPath, outputPath)
        );

        Assert.Equal("Limiter obsługuje tylko pliki WAV 16-bit PCM.", exception.Message);
    }

    [Fact]
    public void Process_boosts_quiet_recordings()
    {
        var inputPath = Path.Combine(_tempDirectory, "quiet-input.wav");
        var outputPath = Path.Combine(_tempDirectory, "quiet-output.wav");
        var inputSamples = Enumerable.Repeat<short>(1600, 4096).ToArray();
        File.WriteAllBytes(inputPath, CreatePcm16Wav(inputSamples, sampleRate: 44100));

        WavVoiceLimiter.Process(inputPath, outputPath);

        var outputSamples = ReadPcm16Samples(File.ReadAllBytes(outputPath));
        Assert.True(outputSamples.Max(sample => Math.Abs((int)sample)) > (Math.Abs((int)inputSamples[0]) * 1.5));
        Assert.Contains(outputSamples, sample => Math.Abs((int)sample) > Math.Abs(inputSamples[0]));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Best effort only.
        }
    }

    private static short[] ReadPcm16Samples(byte[] wavBytes)
    {
        var dataLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(wavBytes.AsSpan(40, 4));
        var sampleCount = dataLength / 2;
        var samples = new short[sampleCount];

        for (var index = 0; index < sampleCount; index++)
        {
            samples[index] = BinaryPrimitives.ReadInt16LittleEndian(
                wavBytes.AsSpan(44 + (index * 2), 2)
            );
        }

        return samples;
    }

    private static byte[] CreatePcm16Wav(short[] samples, uint sampleRate)
    {
        const ushort audioFormat = 1;
        const ushort channelCount = 1;
        const ushort bitsPerSample = 16;
        var blockAlign = (ushort)(channelCount * (bitsPerSample / 8));
        var byteRate = sampleRate * blockAlign;
        var dataLength = samples.Length * 2;
        var buffer = new byte[44 + dataLength];

        WriteHeader(buffer, sampleRate, audioFormat, channelCount, bitsPerSample, byteRate, blockAlign, dataLength);

        for (var index = 0; index < samples.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(44 + (index * 2), 2), samples[index]);
        }

        return buffer;
    }

    private static byte[] CreatePcm8Wav(byte[] samples, uint sampleRate)
    {
        const ushort audioFormat = 1;
        const ushort channelCount = 1;
        const ushort bitsPerSample = 8;
        var blockAlign = (ushort)(channelCount * (bitsPerSample / 8));
        var byteRate = sampleRate * blockAlign;
        var dataLength = samples.Length;
        var buffer = new byte[44 + dataLength];

        WriteHeader(buffer, sampleRate, audioFormat, channelCount, bitsPerSample, byteRate, blockAlign, dataLength);
        samples.CopyTo(buffer.AsSpan(44, dataLength));
        return buffer;
    }

    private static void WriteHeader(
        byte[] buffer,
        uint sampleRate,
        ushort audioFormat,
        ushort channelCount,
        ushort bitsPerSample,
        uint byteRate,
        ushort blockAlign,
        int dataLength
    )
    {
        "RIFF"u8.CopyTo(buffer.AsSpan(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4, 4), (uint)(buffer.Length - 8));
        "WAVE"u8.CopyTo(buffer.AsSpan(8, 4));
        "fmt "u8.CopyTo(buffer.AsSpan(12, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(20, 2), audioFormat);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(22, 2), channelCount);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(24, 4), sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(28, 4), byteRate);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(32, 2), blockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(34, 2), bitsPerSample);
        "data"u8.CopyTo(buffer.AsSpan(36, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(40, 4), (uint)dataLength);
    }
}
