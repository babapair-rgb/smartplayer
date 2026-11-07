using System.Security.Cryptography;

namespace LearnPlayAI.Core.Hashing;

public sealed class VideoHasher : IVideoHasher
{
    private const int SampleSizeBytes = 1 * 1024 * 1024;

    public string ComputeHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        long length = stream.Length;

        using var sha256 = SHA256.Create();
        using var hashStream = new CryptoStream(Stream.Null, sha256, CryptoStreamMode.Write);

        var lengthBytes = BitConverter.GetBytes(length);
        hashStream.Write(lengthBytes, 0, lengthBytes.Length);

        int headSize = (int)Math.Min(SampleSizeBytes, length);
        var headBuffer = new byte[headSize];
        ReadExact(stream, headBuffer, headSize);
        hashStream.Write(headBuffer, 0, headSize);

        if (length > SampleSizeBytes)
        {
            int tailSize = (int)Math.Min(SampleSizeBytes, length);
            stream.Seek(-tailSize, SeekOrigin.End);
            var tailBuffer = new byte[tailSize];
            ReadExact(stream, tailBuffer, tailSize);
            hashStream.Write(tailBuffer, 0, tailSize);
        }

        hashStream.FlushFinalBlock();
        return Convert.ToHexString(sha256.Hash!);
    }

    private static void ReadExact(Stream stream, byte[] buffer, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, totalRead, count - totalRead);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }
            totalRead += read;
        }
    }
}
