using LearnPlayAI.Core.Hashing;

namespace LearnPlayAI.Core.Tests.Hashing;

public class VideoHasherTests
{
    [Fact]
    public void ComputeHash_SameContent_ProducesSameHash()
    {
        var hasher = new VideoHasher();
        var path1 = WriteTempFile(new byte[] { 1, 2, 3, 4, 5 });
        var path2 = WriteTempFile(new byte[] { 1, 2, 3, 4, 5 });

        try
        {
            Assert.Equal(hasher.ComputeHash(path1), hasher.ComputeHash(path2));
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public void ComputeHash_DifferentContent_ProducesDifferentHash()
    {
        var hasher = new VideoHasher();
        var path1 = WriteTempFile(new byte[] { 1, 2, 3, 4, 5 });
        var path2 = WriteTempFile(new byte[] { 1, 2, 3, 4, 6 });

        try
        {
            Assert.NotEqual(hasher.ComputeHash(path1), hasher.ComputeHash(path2));
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public void ComputeHash_LargeFile_IgnoresChangesOutsideSampledRegion()
    {
        // The hasher samples the first and last 1 MB plus file length, to avoid
        // hashing entire large video files. A 3 MB file has an un-sampled 1 MB
        // region in the middle; changes there are intentionally not detected.
        var hasher = new VideoHasher();
        const int size = 3 * 1024 * 1024;
        var bytes = new byte[size];
        new Random(42).NextBytes(bytes);

        var path1 = WriteTempFile(bytes);
        var modified = (byte[])bytes.Clone();
        modified[size / 2] = (byte)(modified[size / 2] + 1);
        var path2 = WriteTempFile(modified);

        try
        {
            Assert.Equal(hasher.ComputeHash(path1), hasher.ComputeHash(path2));
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    [Fact]
    public void ComputeHash_LargeFile_DetectsChangeInTailSample()
    {
        // Files over 1 MB have their last 1 MB sampled. A change to the final byte
        // must therefore alter the hash; this guards against the tail read being
        // skipped or read from the wrong offset.
        var hasher = new VideoHasher();
        const int size = 2 * 1024 * 1024;
        var bytes = new byte[size];
        new Random(42).NextBytes(bytes);

        var path1 = WriteTempFile(bytes);
        var modified = (byte[])bytes.Clone();
        modified[size - 1] = (byte)(modified[size - 1] + 1);
        var path2 = WriteTempFile(modified);

        try
        {
            Assert.NotEqual(hasher.ComputeHash(path1), hasher.ComputeHash(path2));
        }
        finally
        {
            File.Delete(path1);
            File.Delete(path2);
        }
    }

    private static string WriteTempFile(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".bin");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
