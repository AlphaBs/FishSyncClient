using FishBucket.ApiClient;
using FishBucket.SyncClient;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using gui;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace FishSyncClient.Gui;

public sealed record Comparison(List<LocalEntry> Local, List<RemoteEntry> Remote, List<FileChange> Changes)
{
    // Deterministic manifest signature, also used to validate the source of a PULL.
    public string Signature => string.Join('\n', Local.Where(x => !x.IsDirectory).OrderBy(x => x.Path, WorkspaceFiles.Comparer)
        .Select(x => $"L:{x.Path}:{x.Size}:{x.Checksum}")) + "\n" +
        string.Join('\n', Remote.OrderBy(x => x.Path, WorkspaceFiles.Comparer)
            .Select(x => $"R:{x.Path}:{x.Metadata.Size}:{x.Metadata.Checksum}"));
}

public sealed record UploadResult(string? VerificationWarning = null);

public sealed class BucketSession
{
    private readonly HttpClient _http;
    private readonly FishApiClient _api;
    private readonly string _host;
    private readonly string _id;
    private string _token = "";
    public WorkspaceFiles Workspace { get; }
    public List<RemoteEntry> Remote { get; private set; } = [];

    public BucketSession(string host, string bucketId, string applicationDirectory, HttpClient http)
    {
        _host = host.TrimEnd('/');
        if (!Uri.TryCreate(_host, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new ArgumentException("올바른 서버 API 주소를 입력해 주세요.");
        Workspace = new(applicationDirectory, bucketId);
        _id = Uri.EscapeDataString(bucketId);
        _http = http;
        _api = new(_host, http);
    }

    public async Task Login(string username, string password, CancellationToken ct) =>
        _token = await _api.Login(username, password, ct);

    private async Task<T> Get<T>(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _host + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct) ?? throw new IOException("서버 응답이 비어 있습니다.");
    }

    public async Task<List<RemoteEntry>> ReadRemote(CancellationToken ct)
    {
        var listing = await Get<RemoteListing>($"/buckets/common/{_id}/files", ct);
        if (!string.Equals(listing.Id, Uri.UnescapeDataString(_id), StringComparison.Ordinal))
            throw new IOException("서버가 요청한 버킷과 다른 파일 목록을 반환했습니다.");
        return Workspace.ValidateRemote(listing);
    }

    public async Task<BucketUsage> ReadUsage(CancellationToken ct)
    {
        var limits = Get<BucketLimits>($"/buckets/common/{_id}/limitations", ct);
        var count = Get<int>($"/v2/bucket-items/{_id}/monthly-sync-count", ct);
        var files = ReadRemote(ct);
        await Task.WhenAll(limits, count, files);
        Remote = await files;
        return new(await limits, await count, Remote.Sum(x => x.Metadata.Size), Remote.Count);
    }

    public async Task<Comparison> Compare(CancellationToken ct)
    {
        var localTask = Task.Run(() => Workspace.Scan(ct), ct);
        var remoteTask = ReadRemote(ct);
        await Task.WhenAll(localTask, remoteTask);
        var local = await localTask;
        Remote = await remoteTask;
        return new(local, Remote, WorkspaceFiles.Compare(local, Remote));
    }

    public async Task Pull(IProgress<string> status, IProgress<ByteProgress> progress, CancellationToken ct)
    {
        // Authenticate and validate the entire remote list before touching the workspace.
        Remote = await ReadRemote(ct);
        Workspace.Prepare();
        // Stage on the bucket's volume, using short independent filenames. Installation
        // then needs no additional data copy or long temporary filename at the destination.
        using var staging = new StagingDirectory(Path.GetDirectoryName(Workspace.Root));
        var staged = new Dictionary<string, string>(WorkspaceFiles.Comparer);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var local = await Task.Run(() => Workspace.Scan(ct), ct);
            var changes = WorkspaceFiles.Compare(local, Remote);
            var needed = changes.Where(x => x.Kind != "추가").Select(x => x.Path).ToHashSet(WorkspaceFiles.Comparer);
            var downloads = Remote.Where(x => needed.Contains(x.Path) && !staged.ContainsKey(x.Path)).ToList();
            progress.Report(new(downloads.Sum(x => x.Metadata.Size), 0));
            var completed = 0;
            foreach (var file in downloads)
            {
                ct.ThrowIfCancellationRequested();
                status.Report($"다운로드 {++completed}/{downloads.Count} · {file.Path}");
                var temp = Path.Combine(staging.Path, Guid.NewGuid().ToString("N"));
                using var response = await _http.GetAsync(file.Location, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                await using (var source = await response.Content.ReadAsStreamAsync(ct))
                await using (var target = File.Create(temp))
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await source.ReadAsync(buffer, ct)) > 0)
                    {
                        await target.WriteAsync(buffer.AsMemory(0, read), ct);
                        progress.Report(new(0, read));
                    }
                }
                using var stream = File.OpenRead(temp);
                var checksum = Convert.ToHexString(await MD5.HashDataAsync(stream, ct));
                if (stream.Length != file.Metadata.Size || !checksum.Equals(file.Metadata.Checksum, StringComparison.OrdinalIgnoreCase))
                    throw new IOException($"다운로드 검증 실패: {file.Path}");
                staged.Add(file.Path, temp);
            }

            var latestRemote = await ReadRemote(ct);
            if (new Comparison([], Remote, []).Signature != new Comparison([], latestRemote, []).Signature)
                throw new IOException("다운로드 중 서버 파일이 변경되었습니다. 초기화를 다시 시도해 주세요.");

            // Catch files edited/created while the network downloads were in progress.
            local = await Task.Run(() => Workspace.Scan(ct), ct);
            changes = WorkspaceFiles.Compare(local, Remote);
            if (changes.Any(x => x.Kind != "추가" && !staged.ContainsKey(x.Path)))
                continue; // Download these newly changed files before deleting anything.

            foreach (var file in changes.Where(x => x.Kind == "추가"))
            {
                ct.ThrowIfCancellationRequested();
                status.Report($"로컬 전용 파일 삭제 · {file.Path}");
                File.Delete(Workspace.Resolve(file.Path));
            }
            foreach (var directory in local.Where(x => x.IsDirectory).OrderByDescending(x => x.Path.Length))
            {
                ct.ThrowIfCancellationRequested();
                var path = Workspace.Resolve(directory.Path);
                if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path);
            }
            foreach (var file in changes.Where(x => x.Kind != "추가"))
            {
                ct.ThrowIfCancellationRequested();
                var destination = Workspace.Resolve(file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                Workspace.Resolve(file.Path);
                File.Move(staged[file.Path], destination, true);
                staged.Remove(file.Path);
            }
            var finalLocal = await Task.Run(() => Workspace.Scan(ct), ct);
            if (WorkspaceFiles.Compare(finalLocal, Remote).Count == 0) return;
        }
        throw new IOException("초기화 중 로컬 파일이 계속 변경되고 있습니다. 파일 편집을 마친 뒤 다시 시도해 주세요.");
    }

    public async Task<UploadResult> Push(Comparison preview, IProgress<string> status,
        IProgress<ByteProgress> progress, CancellationToken ct, IProgress<FileUploadProgress>? fileProgress = null)
    {
        // Upload immutable copies so editor saves during a transfer cannot corrupt the manifest.
        using var staging = new StagingDirectory();
        var files = new List<SyncFile>();
        foreach (var entry in preview.Local.Where(x => !x.IsDirectory))
        {
            ct.ThrowIfCancellationRequested();
            status.Report($"업로드 준비 · {entry.Path}");
            fileProgress?.Report(new(entry.Path, "준비 중"));
            var target = Path.Combine(staging.Path, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using (var source = File.OpenRead(Workspace.Resolve(entry.Path)))
            await using (var output = File.Create(target))
                await source.CopyToAsync(output, ct);
            using var stream = File.OpenRead(target);
            var hash = await MD5.HashDataAsync(stream, ct);
            if (stream.Length != entry.Size || !Convert.ToHexString(hash).Equals(entry.Checksum, StringComparison.OrdinalIgnoreCase))
                throw new IOException("준비 중 파일이 변경되었습니다. 다시 비교해 주세요.");
            files.Add(new LocalSyncFile(RootedPath.Create(staging.Path, entry.Path, new PathOptions()))
            {
                Metadata = new SyncFileMetadata { Size = entry.Size, Checksum = new SyncFileChecksum(ChecksumAlgorithmNames.MD5, Convert.ToHexString(hash)) }
            });
        }
        var actions = new SynchronousProgress<SyncActionProgress>(ev =>
        {
            var state = ev.EventType switch
            {
                FileProgressEventType.DoneSync => "전송 완료",
                FileProgressEventType.StartSync => "업로드 중",
                _ => "대기"
            };
            status.Report($"{state} · {ev.Action.Path}");
            fileProgress?.Report(new(ev.Action.Path, state));
        });
        var bytes = new SynchronousProgress<SyncActionByteProgress>(ev =>
        {
            progress.Report(ev.Progress);
            fileProgress?.Report(new(ev.Path, Bytes: ev.Progress));
        });
        var handler = new SimpleBucketSyncActionCollectionHandler(4, actions, bytes);
        handler.Add(new HttpBucketSyncActionHandler(_http));
        status.Report("서버에 변경사항 반영 중…");
        var result = await _api.Sync(_id, new SyncFileCollection(files), handler, ct);
        if (!result.IsSuccess) throw new IOException("서버 반영이 완료되지 않았습니다. 다시 비교해 주세요.");
        status.Report("서버 반영 확인 중…");
        // The server has committed. A refresh error must not turn that success into an
        // upload failure (or encourage another quota-consuming upload).
        try
        {
            Remote = await ReadRemote(ct);
            if (WorkspaceFiles.Compare(preview.Local, Remote).Count != 0)
                return new("업로드 후 서버 파일이 달라졌습니다. 새로고침으로 현재 상태를 확인해 주세요.");
            return new();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or System.Text.Json.JsonException or OperationCanceledException)
        {
            return new("업로드는 완료됐지만 서버 목록을 확인하지 못했습니다. 새로고침해 주세요.");
        }
    }
}

internal sealed class StagingDirectory : IDisposable
{
    public string Path { get; }
    public StagingDirectory(string? parentDirectory = null)
    {
        Path = System.IO.Path.Combine(parentDirectory ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FishSyncClient"), ".fish-" + Guid.NewGuid().ToString("N"));
        WorkspaceFiles.EnsureNoLinks(Path);
        Directory.CreateDirectory(Path);
    }
    public void Dispose()
    {
        try
        {
            WorkspaceFiles.EnsureNoLinks(Path);
            // This directory is uniquely allocated here, never supplied by the user or server.
            Directory.Delete(Path, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
