using System.IO;
using System.Security.Cryptography;

namespace FishSyncClient.Gui;

public record LocalEntry(string Path, long Size, DateTime LastUpdated, bool IsDirectory = false, string? Checksum = null);
public record RemoteEntry(string Path, string Location, RemoteMetadata Metadata);
public record RemoteMetadata(long Size, DateTimeOffset LastUpdated, string? Checksum);
public record RemoteListing(string Id, DateTimeOffset LastUpdated, List<RemoteEntry> Files);
public record BucketLimits(bool IsReadOnly, long MaxFileSize, long MaxNumberOfFiles,
    long MaxBucketSize, DateTimeOffset ExpiredAt, int MonthlyMaxSyncCount);
public record BucketUsage(BucketLimits Limits, int MonthlySyncCount, long UsedBytes, int FileCount);
public record FileChange(string Path, string Kind, long Size)
{
    public string SizeText => MainWindow.FormatSize(Size);
    public Avalonia.Media.IBrush Background => WorkspaceFileItem.BackgroundFor(Kind);
}

/// <summary>All filesystem access stays inside a dedicated, link-free bucket directory.</summary>
public sealed class WorkspaceFiles
{
    public static StringComparer Comparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public string Root { get; }

    public WorkspaceFiles(string applicationDirectory, string bucketId)
    {
        ValidateSegment(bucketId);
        Root = System.IO.Path.GetFullPath(System.IO.Path.Combine(applicationDirectory, "buckets", bucketId));
        EnsureNoLinks(Root);
    }

    private static void ValidateSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment) || segment is "." or ".." ||
            segment.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0 ||
            segment.Any(char.IsControl) || segment.EndsWith('.') || segment.EndsWith(' ') ||
            System.Text.RegularExpressions.Regex.IsMatch(segment,
                @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])($|\.)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new IOException($"사용할 수 없는 경로 이름입니다: {segment}");
    }

    public string Resolve(string relative)
    {
        var segments = relative.Replace('\\', '/').Split('/');
        foreach (var segment in segments) ValidateSegment(segment);
        var fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Root, System.IO.Path.Combine(segments)));
        var prefix = Root + System.IO.Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new IOException("버킷 폴더 밖의 경로는 사용할 수 없습니다.");
        EnsureNoLinks(fullPath);
        return fullPath;
    }

    public static void EnsureNoLinks(string fullPath)
    {
        for (string? current = System.IO.Path.GetFullPath(fullPath); current != null; current = System.IO.Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"연결된 폴더나 파일은 동기화할 수 없습니다: {current}");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    public void Prepare()
    {
        EnsureNoLinks(Root);
        Directory.CreateDirectory(Root);
        var probe = System.IO.Path.Combine(Root, $".fish-write-test-{Guid.NewGuid():N}");
        try { using var file = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose); }
        catch (UnauthorizedAccessException ex) { throw new IOException("실행 파일을 쓰기 가능한 폴더로 옮겨 주세요.", ex); }
    }

    public List<LocalEntry> Scan(CancellationToken ct)
    {
        EnsureNoLinks(Root);
        if (!Directory.Exists(Root)) throw new DirectoryNotFoundException("버킷 폴더가 없어졌습니다. 폴더를 복원한 뒤 다시 시도해 주세요.");
        var result = new List<LocalEntry>();
        Visit(Root);
        return result;

        void Visit(string directory)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                ct.ThrowIfCancellationRequested();
                EnsureNoLinks(path);
                var relative = System.IO.Path.GetRelativePath(Root, path).Replace('\\', '/');
                Resolve(relative);
                if (Directory.Exists(path))
                {
                    result.Add(new(relative, 0, Directory.GetLastWriteTimeUtc(path), true));
                    Visit(path);
                }
                else
                {
                    var info = new FileInfo(path);
                    var size = info.Length;
                    var updated = info.LastWriteTimeUtc;
                    string hash;
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
                        var buffer = new byte[81920];
                        int read;
                        while ((read = stream.Read(buffer)) > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            hasher.AppendData(buffer, 0, read);
                        }
                        ct.ThrowIfCancellationRequested();
                        hash = Convert.ToHexString(hasher.GetHashAndReset());
                        info.Refresh();
                        if (info.Length != size || info.LastWriteTimeUtc != updated)
                            throw new IOException($"비교 도중 파일이 변경되었습니다. 다시 시도해 주세요: {relative}");
                    }
                    result.Add(new(relative, size, updated, Checksum: hash));
                }
            }
        }
    }

    public List<RemoteEntry> ValidateRemote(RemoteListing listing)
    {
        if (listing.Files == null) throw new IOException("서버 파일 목록이 올바르지 않습니다.");
        var result = new List<RemoteEntry>();
        var names = new HashSet<string>(Comparer);
        foreach (var item in listing.Files)
        {
            if (item.Path == null || item.Metadata == null || item.Metadata.Size < 0)
                throw new IOException("서버 파일 정보가 올바르지 않습니다.");
            Resolve(item.Path);
            var path = item.Path.Replace('\\', '/');
            if (!names.Add(path)) throw new IOException($"중복된 서버 경로입니다: {path}");
            if (!Uri.TryCreate(item.Location, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                throw new IOException($"다운로드 주소가 올바르지 않습니다: {path}");
            if (item.Metadata.Checksum is not { Length: 32 } hash || !hash.All(Uri.IsHexDigit))
                throw new IOException($"파일 체크섬이 올바르지 않습니다: {path}");
            result.Add(item with { Path = path });
        }
        foreach (var path in names)
        {
            var parent = path;
            while (parent.Contains('/'))
            {
                parent = parent[..parent.LastIndexOf('/')];
                if (names.Contains(parent)) throw new IOException($"파일과 폴더 경로가 겹칩니다: {path}");
            }
        }
        return result;
    }

    public static List<FileChange> Compare(IEnumerable<LocalEntry> local, IEnumerable<RemoteEntry> remote)
    {
        var locals = local.Where(x => !x.IsDirectory).ToDictionary(x => x.Path, Comparer);
        var remotes = remote.ToDictionary(x => x.Path, Comparer);
        var changes = new List<FileChange>();
        foreach (var file in locals.Values)
        {
            if (!remotes.TryGetValue(file.Path, out var server)) changes.Add(new(file.Path, "추가", file.Size));
            else if (file.Size != server.Metadata.Size || string.IsNullOrEmpty(file.Checksum) ||
                     !string.Equals(file.Checksum, server.Metadata.Checksum, StringComparison.OrdinalIgnoreCase))
                changes.Add(new(file.Path, "갱신", file.Size));
        }
        foreach (var file in remotes.Values)
            if (!locals.ContainsKey(file.Path)) changes.Add(new(file.Path, "삭제", file.Metadata.Size));
        return changes.OrderBy(x => x.Path, Comparer).ToList();
    }
}
