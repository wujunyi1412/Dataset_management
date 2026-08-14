namespace DatasetManager.Core.Services;

public interface IImageMetadataReader
{
    bool TryRead(string path, out ImageMetadata metadata);
}

public readonly record struct ImageMetadata(int Width, int Height, int BitDepth, int Channels);
