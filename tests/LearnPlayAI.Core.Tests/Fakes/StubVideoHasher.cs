using LearnPlayAI.Core.Hashing;

namespace LearnPlayAI.Core.Tests.Fakes;

public sealed class StubVideoHasher : IVideoHasher
{
    private readonly Dictionary<string, string> _hashesByPath;

    public StubVideoHasher(Dictionary<string, string>? hashesByPath = null) =>
        _hashesByPath = hashesByPath ?? new Dictionary<string, string>();

    public string ComputeHash(string filePath) =>
        _hashesByPath.TryGetValue(filePath, out var hash) ? hash : filePath;
}
