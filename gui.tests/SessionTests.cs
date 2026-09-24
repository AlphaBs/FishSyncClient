using FishSyncClient.Gui;
using FishSyncClient.Progress;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FishSyncClient.Gui.Tests;

internal sealed class FakeBucketServer : HttpMessageHandler
{
    public List<RemoteEntry> Files { get; set; } = [];
    public Dictionary<string, string> Content { get; } = [];
    public List<string> AuthorizedReads { get; } = [];
    public bool BrokenDownload { get; set; }
    public int SyncCalls { get; private set; }
    public string? SubmittedManifest { get; private set; }
    public bool FailUsage { get; set; }
    public bool FailFilesAfterCommit { get; set; }
    public BucketLimits Limits { get; set; } = new(false, 10000, 100, 100000, DateTimeOffset.UtcNow.AddDays(30), 20);
    public bool RequireUpload { get; set; }
    public Dictionary<string, string> Uploads { get; } = [];
    public Action? DuringUpload { get; set; }
    public Action? DuringDownload { get; set; }
    public TaskCompletionSource? SyncGate { get; set; }
    public TaskCompletionSource? UploadHalfwayGate { get; set; }
    public bool FailUpload { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/auth/login")) return Json(new { token = "test-token" });
        if (request.RequestUri.Host == "uploads.test")
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Null(request.Headers.Authorization);
            DuringUpload?.Invoke();
            if (UploadHalfwayGate != null)
            {
                using var stream = await request.Content!.ReadAsStreamAsync(ct);
                using var output = new MemoryStream();
                var buffer = new byte[3];
                var read = await stream.ReadAsync(buffer, ct);
                output.Write(buffer, 0, read);
                await UploadHalfwayGate.Task.WaitAsync(ct);
                await stream.CopyToAsync(output, ct);
                if (FailUpload) return new(HttpStatusCode.ServiceUnavailable);
                lock (Uploads) Uploads[path.TrimStart('/')] = System.Text.Encoding.UTF8.GetString(output.ToArray());
                return Json(new { success = true });
            }
            var uploaded = await request.Content!.ReadAsStringAsync(ct);
            lock (Uploads) Uploads[path.TrimStart('/')] = uploaded;
            return Json(new { success = true });
        }
        if (request.RequestUri.Host == "files.test")
        {
            Assert.Null(request.Headers.Authorization);
            DuringDownload?.Invoke();
            if (BrokenDownload) return new(HttpStatusCode.ServiceUnavailable);
            return new(HttpStatusCode.OK) { Content = new StringContent(Content[path.TrimStart('/')]) };
        }
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
        AuthorizedReads.Add(path);
        if (path.EndsWith("/files")) return FailFilesAfterCommit && SyncCalls > 0 ? new(HttpStatusCode.ServiceUnavailable) : Json(new RemoteListing("bucket", WorkspaceTests.Updated, Files));
        if (path.EndsWith("/limitations")) return FailUsage ? new(HttpStatusCode.Forbidden) : Json(Limits);
        if (path.EndsWith("/monthly-sync-count")) return Json(3);
        if (path.EndsWith("/sync"))
        {
            if (SyncGate != null) await SyncGate.Task.WaitAsync(ct);
            SyncCalls++;
            SubmittedManifest = await request.Content!.ReadAsStringAsync(ct);
            using var manifest = JsonDocument.Parse(SubmittedManifest);
            if (RequireUpload)
            {
                var actions = manifest.RootElement.GetProperty("files").EnumerateArray()
                    .Where(x => !Uploads.ContainsKey(x.GetProperty("path").GetString()!))
                    .Select(x => new { path = x.GetProperty("path").GetString(), action = new
                    {
                        type = "Http", parameters = new Dictionary<string, string>
                        { ["method"] = "PUT", ["url"] = "https://uploads.test/" + x.GetProperty("path").GetString() }
                    }}).ToArray();
                if (actions.Length > 0) return Json(new { isSuccess = false, requiredActions = actions, updatedAt = WorkspaceTests.Updated });
            }
            Files = manifest.RootElement.GetProperty("files").EnumerateArray().Select(x => new RemoteEntry(
                x.GetProperty("path").GetString()!, "https://files.test/file",
                new(x.GetProperty("size").GetInt64(), WorkspaceTests.Updated, x.GetProperty("checksum").GetString()))).ToList();
            return Json(new { isSuccess = true, requiredActions = Array.Empty<object>(), updatedAt = WorkspaceTests.Updated });
        }
        throw new InvalidOperationException(path);
    }

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
}

public class SessionTests
{
    private static readonly IProgress<string> Status = new SynchronousProgress<string>(_ => { });
    private static readonly IProgress<ByteProgress> Bytes = new SynchronousProgress<ByteProgress>(_ => { });

    [Fact]
    public async Task PullMirrorsServerDeletesLocalOnlyAndMatchesChecksums()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer { Files = [WorkspaceTests.Remote("new.txt", "new"), WorkspaceTests.Remote("changed.txt", "updated")] };
        server.Content["new.txt"] = "new";
        server.Content["changed.txt"] = "updated";
        using var http = new HttpClient(server);
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        await File.WriteAllTextAsync(session.Workspace.Resolve("local.txt"), "remove");
        await File.WriteAllTextAsync(session.Workspace.Resolve("changed.txt"), "old");
        var outside = Path.Combine(directory.Path, "keep.txt");
        await File.WriteAllTextAsync(outside, "keep");
        await session.Login("user", "password", default);
        await session.Pull(Status, Bytes, default);
        Assert.False(File.Exists(session.Workspace.Resolve("local.txt")));
        Assert.Equal("new", await File.ReadAllTextAsync(session.Workspace.Resolve("new.txt")));
        Assert.Equal("updated", await File.ReadAllTextAsync(session.Workspace.Resolve("changed.txt")));
        Assert.Equal("keep", await File.ReadAllTextAsync(outside));
        Assert.Empty(WorkspaceFiles.Compare(session.Workspace.Scan(default), server.Files));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedOrCorruptDownloadDoesNotDeleteOrOverwriteLocalFiles(bool requestFailure)
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer { Files = [WorkspaceTests.Remote("file", "expected")], BrokenDownload = requestFailure };
        server.Content["file"] = "corrupt";
        using var http = new HttpClient(server);
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        await File.WriteAllTextAsync(session.Workspace.Resolve("keep"), "keep");
        await File.WriteAllTextAsync(session.Workspace.Resolve("file"), "local");
        await session.Login("user", "password", default);
        await Assert.ThrowsAnyAsync<Exception>(() => session.Pull(Status, Bytes, default));
        Assert.Equal("keep", await File.ReadAllTextAsync(session.Workspace.Resolve("keep")));
        Assert.Equal("local", await File.ReadAllTextAsync(session.Workspace.Resolve("file")));
    }

    [Fact]
    public async Task EmptyServerDeletesAllLocalFilesButNotOutsideBucket()
    {
        using var directory = new TestDirectory();
        using var http = new HttpClient(new FakeBucketServer());
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        await File.WriteAllTextAsync(session.Workspace.Resolve("local"), "remove");
        await session.Login("user", "password", default);
        await session.Pull(Status, Bytes, default);
        Assert.Empty(session.Workspace.Scan(default));
    }

    [Fact]
    public async Task UsageCombinesLimitsMonthlyCountAndServerSize()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer { Files = [WorkspaceTests.Remote("file", "abc")] };
        using var http = new HttpClient(server);
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        await session.Login("user", "password", default);
        var usage = await session.ReadUsage(default);
        Assert.Equal(3, usage.UsedBytes);
        Assert.Equal(17, usage.Limits.MonthlyMaxSyncCount - usage.MonthlySyncCount);
        Assert.Contains("/api/v2/bucket-items/bucket/monthly-sync-count", server.AuthorizedReads);
    }

    [Fact]
    public async Task PushRejectsChangedSnapshotContentBeforeAnyServerMutation()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer();
        using var http = new HttpClient(server);
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        await File.WriteAllTextAsync(session.Workspace.Resolve("file"), "before");
        await session.Login("user", "password", default);
        var preview = await session.Compare(default);
        await File.WriteAllTextAsync(session.Workspace.Resolve("file"), "after");
        await Assert.ThrowsAsync<IOException>(() => session.Push(preview, Status, Bytes, default));
        Assert.Equal(0, server.SyncCalls);
    }

    [Fact]
    public async Task PushSendsWholeManifestIncludingDeletionAndVerifiesServerResult()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer { Files = [WorkspaceTests.Remote("removed", "old")] };
        using var http = new HttpClient(server);
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        await File.WriteAllTextAsync(session.Workspace.Resolve("new"), "abc");
        await session.Login("user", "password", default);
        var preview = await session.Compare(default);
        Assert.Equal(2, preview.Changes.Count);
        await session.Push(preview, Status, Bytes, default);
        Assert.Equal(1, server.SyncCalls);
        Assert.DoesNotContain("removed", server.SubmittedManifest);
        Assert.Equal("new", Assert.Single(server.Files).Path);
        Assert.Empty((await session.Compare(default)).Changes);
    }

    [Fact]
    public async Task PushContinuesWhenLocalOrServerListingChangesAfterComparison()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer();
        using var http = new HttpClient(server);
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        await File.WriteAllTextAsync(session.Workspace.Resolve("upload.txt"), "snapshot");
        await session.Login("user", "password", default);
        var preview = await session.Compare(default);

        await File.WriteAllTextAsync(session.Workspace.Resolve("later.txt"), "next sync");
        server.Files = [WorkspaceTests.Remote("server-change.txt", "changed")];

        await session.Push(preview, Status, Bytes, default);

        Assert.Equal(1, server.SyncCalls);
        Assert.Equal("upload.txt", Assert.Single(server.Files).Path);
        Assert.Equal("next sync", await File.ReadAllTextAsync(session.Workspace.Resolve("later.txt")));
        Assert.Equal("later.txt", Assert.Single((await session.Compare(default)).Changes).Path);
    }

    [Fact]
    public async Task UploadUsesStableSnapshotWhenLocalFileIsEditedDuringTransfer()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer { RequireUpload = true };
        using var http = new HttpClient(server);
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        var path = session.Workspace.Resolve("file.txt");
        await File.WriteAllTextAsync(path, "snapshot");
        await session.Login("user", "password", default);
        var preview = await session.Compare(default);
        server.DuringUpload = () => File.WriteAllText(path, "edited during upload");
        await session.Push(preview, Status, Bytes, default);
        Assert.Equal("snapshot", server.Uploads["file.txt"]);
        Assert.Equal("edited during upload", await File.ReadAllTextAsync(path));
        Assert.Equal("갱신", Assert.Single((await session.Compare(default)).Changes).Kind);
        Assert.Equal(2, server.SyncCalls);
    }

    [Fact]
    public async Task CancelledPullPreservesLocalOnlyFiles()
    {
        using var directory = new TestDirectory();
        using var http = new HttpClient(new FakeBucketServer());
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        var path = session.Workspace.Resolve("keep");
        await File.WriteAllTextAsync(path, "keep");
        await session.Login("user", "password", default);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.Pull(Status, Bytes, cancelled.Token));
        Assert.Equal("keep", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task RemoteChangeDuringDownloadAbortsBeforeLocalDeletion()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer { Files = [WorkspaceTests.Remote("file", "original")] };
        server.Content["file"] = "original";
        server.DuringDownload = () => server.Files = [WorkspaceTests.Remote("file", "changed")];
        using var http = new HttpClient(server);
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        var path = session.Workspace.Resolve("keep");
        await File.WriteAllTextAsync(path, "keep");
        await session.Login("user", "password", default);
        await Assert.ThrowsAsync<IOException>(() => session.Pull(Status, Bytes, default));
        Assert.Equal("keep", await File.ReadAllTextAsync(path));
        Assert.False(File.Exists(session.Workspace.Resolve("file")));
    }

    [Fact]
    public async Task PullHandlesFileToDirectoryAndDirectoryToFileChanges()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer { Files = [WorkspaceTests.Remote("was-file/child", "child"), WorkspaceTests.Remote("was-dir", "file")] };
        server.Content["was-file/child"] = "child";
        server.Content["was-dir"] = "file";
        using var http = new HttpClient(server);
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        await File.WriteAllTextAsync(session.Workspace.Resolve("was-file"), "file");
        Directory.CreateDirectory(session.Workspace.Resolve("was-dir"));
        await File.WriteAllTextAsync(session.Workspace.Resolve("was-dir/child"), "child");
        await session.Login("user", "password", default);
        await session.Pull(Status, Bytes, default);
        Assert.Equal("child", await File.ReadAllTextAsync(session.Workspace.Resolve("was-file/child")));
        Assert.Equal("file", await File.ReadAllTextAsync(session.Workspace.Resolve("was-dir")));
    }

    [Fact]
    public async Task PullRechecksFilesChangedOrCreatedWhileDownloading()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer { Files = [WorkspaceTests.Remote("same.txt", "same"), WorkspaceTests.Remote("download.txt", "new")] };
        server.Content["same.txt"] = "same";
        server.Content["download.txt"] = "new";
        using var http = new HttpClient(server);
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        var file = session.Workspace.Resolve("same.txt");
        var extra = session.Workspace.Resolve("created-during-download.txt");
        await File.WriteAllTextAsync(file, "same");
        server.DuringDownload = () =>
        {
            File.WriteAllText(file, "edit");
            File.WriteAllText(extra, "extra");
            server.DuringDownload = null;
        };
        await session.Login("user", "password", default);
        await session.Pull(Status, Bytes, default);
        Assert.Equal("same", await File.ReadAllTextAsync(file));
        Assert.False(File.Exists(extra));
        Assert.Empty((await session.Compare(default)).Changes);
    }

    [Fact]
    public async Task PullInstallsLongFilenameWithoutExtendingItsName()
    {
        using var directory = new TestDirectory();
        var name = new string('a', 230) + ".txt";
        using var server = new FakeBucketServer { Files = [WorkspaceTests.Remote(name, "new")] };
        server.Content[name] = "new";
        using var http = new HttpClient(server);
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        var target = session.Workspace.Resolve(name);
        await File.WriteAllTextAsync(target, "old");
        var localOnly = session.Workspace.Resolve("local-only.txt");
        await File.WriteAllTextAsync(localOnly, "local");
        await session.Login("user", "password", default);
        await session.Pull(Status, Bytes, default);
        Assert.Equal("new", await File.ReadAllTextAsync(target));
        Assert.False(File.Exists(localOnly));
        Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(session.Workspace.Root)!, ".fish-*"));
    }

    [Fact]
    public async Task SuccessfulUploadDoesNotReadLockedLocalFileAfterCommit()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer { RequireUpload = true };
        using var http = new HttpClient(server);
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        var path = session.Workspace.Resolve("file.txt");
        await File.WriteAllTextAsync(path, "snapshot");
        await session.Login("user", "password", default);
        var preview = await session.Compare(default);
        FileStream? fileLock = null;
        server.DuringUpload = () => fileLock = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var result = await session.Push(preview, Status, Bytes, default);
            Assert.Null(result.VerificationWarning);
            Assert.Equal("snapshot", server.Uploads["file.txt"]);
            Assert.Equal(2, server.SyncCalls);
        }
        finally { fileLock?.Dispose(); }
    }

    [Fact]
    public async Task PostCommitServerRefreshFailureReturnsNoticeInsteadOfUploadFailure()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer { FailFilesAfterCommit = true };
        using var http = new HttpClient(server);
        var session = new BucketSession("https://api.test/api", "bucket", directory.Path, http);
        session.Workspace.Prepare();
        await File.WriteAllTextAsync(session.Workspace.Resolve("file.txt"), "snapshot");
        await session.Login("user", "password", default);
        var preview = await session.Compare(default);
        var result = await session.Push(preview, Status, Bytes, default);
        Assert.NotNull(result.VerificationWarning);
        Assert.Equal(1, server.SyncCalls);
        Assert.Empty(WorkspaceFiles.Compare(preview.Local, server.Files));
    }
}
