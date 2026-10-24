using Microsoft.Extensions.Logging;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Archives;

namespace SharpKit.Renderer;

public class CachedBSAFileProvider(ILogger<TriangleRenderer> _log, long maxCacheBytes = 2L * 1024 * 1024 * 1024)
{
    private readonly Dictionary<string, IArchiveReader> _readerCache = [];
    private readonly long _maxCacheBytes = maxCacheBytes;

    // In-memory file cache: key is the relative path, value is (byte[], lastAccessTime)
    private readonly Dictionary<string, (byte[] Data, DateTime LastAccess)> _fileCache = new(StringComparer.OrdinalIgnoreCase);
    private long _totalCacheBytes;

    public void OpenArchive(string archivePath)
    {
        if (!File.Exists(archivePath))
        {
            _log.LogError("Archive {arch} not found!", archivePath);
        }

        _readerCache[archivePath] = Archive.CreateReader(GameRelease.SkyrimSE, archivePath);
    }

    public Stream? GetFileFromAny(string relativePath)
    {
        string normalizedPath = NormalizePath(relativePath);
        if (_fileCache.TryGetValue(normalizedPath, out var cached))
        {
            _fileCache[normalizedPath] = (cached.Data, DateTime.UtcNow);
            return new MemoryStream(cached.Data, writable: false);
        }

        foreach (var (path, reader) in _readerCache)
        {
            var maybeFile = reader.Files.FirstOrDefault(f =>
                NormalizePath(f.Path).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
            if (maybeFile is not null)
            {
                return CacheAndReturnStream(maybeFile.AsStream(), normalizedPath);
            }
        }

        return null;
    }

    public Stream? GetFileFromAnyByFileName(string fileName)
    {
        string requestedName = Path.GetFileName(NormalizePath(fileName));
        if (string.IsNullOrWhiteSpace(requestedName))
            return null;

        foreach (var (archivePath, reader) in _readerCache)
        {
            var archiveFile = reader.Files.FirstOrDefault(file =>
                Path.GetFileName(NormalizePath(file.Path)).Equals(
                    requestedName, StringComparison.OrdinalIgnoreCase));
            if (archiveFile is null)
                continue;

            string actualPath = NormalizePath(archiveFile.Path);
            _log.LogDebug(
                "Resolved archive filename {FileName} to {ArchivePath} in {Archive}",
                requestedName, actualPath, archivePath);
            return CacheAndReturnStream(archiveFile.AsStream(), actualPath);
        }

        return null;
    }

    public Stream? GetFileFromArchive(string relativePath, string archivePath)
    {
        if (!_readerCache.TryGetValue(archivePath, out var reader))
        {
            if (!File.Exists(archivePath))
            {
                _log.LogError("Archive {arch} not found!", archivePath);
                return null;
            }

            reader = Archive.CreateReader(GameRelease.SkyrimSE, archivePath);
            _readerCache[archivePath] = reader;
        }

        string normalizedPath = NormalizePath(relativePath);
        var maybeFile = reader.Files.FirstOrDefault(f =>
            NormalizePath(f.Path).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
        if (maybeFile is not null)
        {
            return CacheAndReturnStream(maybeFile.AsStream(), normalizedPath);
        }

        _log.LogError("Did not find file in BSA");
        return null;
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private Stream? CacheAndReturnStream(Stream originalStream, string relativePath)
    {
        // Check if we already have this file cached
        if (_fileCache.TryGetValue(relativePath, out var cached))
        {
            originalStream.Dispose();
            _fileCache[relativePath] = (cached.Data, DateTime.UtcNow);
            return new MemoryStream(cached.Data, writable: false);
        }

        // Read the stream into a byte array
        byte[] data;
        using (originalStream)
        {
            using var ms = new MemoryStream();
            originalStream.CopyTo(ms);
            data = ms.ToArray();
        }

        // Evict files if necessary to make room
        EvictUntilFits(data.Length);

        // Add to cache
        _fileCache[relativePath] = (data, DateTime.UtcNow);
        _totalCacheBytes += data.Length;

        return new MemoryStream(data, writable: false);
    }

    private void EvictUntilFits(long requiredBytes)
    {
        while (_totalCacheBytes + requiredBytes > _maxCacheBytes && _fileCache.Count > 0)
        {
            // Find the least recently accessed file
            string? leastAccessedKey = null;
            DateTime leastAccessTime = DateTime.MaxValue;

            foreach (var (key, value) in _fileCache)
            {
                if (value.LastAccess < leastAccessTime)
                {
                    leastAccessTime = value.LastAccess;
                    leastAccessedKey = key;
                }
            }

            if (leastAccessedKey is not null)
            {
                var evicted = _fileCache[leastAccessedKey];
                _totalCacheBytes -= evicted.Data.Length;
                _fileCache.Remove(leastAccessedKey);
                _log.LogDebug("Evicted {Key} ({Size} bytes) from cache", leastAccessedKey, evicted.Data.Length);
            }
        }
    }

    public void ClearCache()
    {
        _fileCache.Clear();
        _totalCacheBytes = 0;
    }

    public long CacheSizeBytes => _totalCacheBytes;
    public int CacheEntryCount => _fileCache.Count;
}
