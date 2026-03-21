using System.Buffers.Binary;
using Tyflocentrum.Windows.Domain.Audio;
using Xunit;

namespace Tyflocentrum.Windows.Tests.Domain;

public sealed class WavFileConcatenatorTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"wav-merge-{Guid.NewGuid():N}");

    public WavFileConcatenatorTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Concatenate_merges_pcm_payloads_in_order()
    {
        var firstPath = Path.Combine(_tempDirectory, "first.wav");
        var secondPath = Path.Combine(_tempDirectory, "second.wav");
        var outputPath = Path.Combine(_tempDirectory, "merged.wav");

        File.WriteAllBytes(firstPath, CreatePcmWav([1, 2, 3, 4]));
        File.WriteAllBytes(secondPath, CreatePcmWav([5, 6, 7, 8, 9, 10]));

        WavFileConcatenator.Concatenate([firstPath, secondPath], outputPath);

        var mergedBytes = File.ReadAllBytes(outputPath);
        Assert.Equal((uint)(mergedBytes.Length - 8), BinaryPrimitives.ReadUInt32LittleEndian(mergedBytes.AsSpan(4, 4)));
        Assert.Equal((uint)10, BinaryPrimitives.ReadUInt32LittleEndian(mergedBytes.AsSpan(40, 4)));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, mergedBytes.Skip(44).ToArray());
    }

    [Fact]
    public void Concatenate_rejects_files_with_different_audio_parameters()
    {
        var firstPath = Path.Combine(_tempDirectory, "first.wav");
        var secondPath = Path.Combine(_tempDirectory, "second.wav");
        var outputPath = Path.Combine(_tempDirectory, "merged.wav");

        File.WriteAllBytes(firstPath, CreatePcmWav([1, 2, 3, 4], sampleRate: 16000));
        File.WriteAllBytes(secondPath, CreatePcmWav([5, 6, 7, 8], sampleRate: 22050));

        var exception = Assert.Throws<InvalidDataException>(() =>
            WavFileConcatenator.Concatenate([firstPath, secondPath], outputPath)
        );

        Assert.Equal("Nie można połączyć plików WAV o różnych parametrach audio.", exception.Message);
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

    private static byte[] CreatePcmWav(byte[] pcmData, uint sampleRate = 8000)
    {
        const ushort audioFormat = 1;
        const ushort channelCount = 1;
        const ushort bitsPerSample = 16;
        var blockAlign = (ushort)(channelCount * (bitsPerSample / 8));
        var byteRate = sampleRate * blockAlign;
        var dataLength = pcmData.Length;
        var fileLength = 44 + dataLength;
        var buffer = new byte[fileLength];

        "RIFF"u8.CopyTo(buffer.AsSpan(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4, 4), (uint)(fileLength - 8));
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
        pcmData.CopyTo(buffer.AsSpan(44, dataLength));
        return buffer;
    }
}
