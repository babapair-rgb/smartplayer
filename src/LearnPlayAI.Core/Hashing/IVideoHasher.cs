namespace LearnPlayAI.Core.Hashing;

public interface IVideoHasher
{
    string ComputeHash(string filePath);
}
