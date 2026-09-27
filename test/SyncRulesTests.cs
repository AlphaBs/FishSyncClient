using System.Collections.Concurrent;
using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Syncer;

namespace FishSyncClientTest;

public sealed class SyncRulesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly PathOptions _pathOptions = new();
    private readonly RecordingComparer _comparer = new();
    private string SourceRoot => Path.Combine(_root, "source");
    private string TargetRoot => Path.Combine(_root, "target");

    public SyncRulesTests()
    {
        Directory.CreateDirectory(SourceRoot);
        Directory.CreateDirectory(TargetRoot);
    }

    [Theory]
    [InlineData(SyncAction.FullSync, false)]
    [InlineData(SyncAction.FullSync, true)]
    [InlineData(SyncAction.UpdateOnly, false)]
    [InlineData(SyncAction.UpdateOnly, true)]
    [InlineData(SyncAction.InstallOnly, false)]
    [InlineData(SyncAction.InstallOnly, true)]
    [InlineData(SyncAction.Exclude, false)]
    [InlineData(SyncAction.Exclude, true)]
    public async Task actions_control_all_file_states(SyncAction action, bool apply)
    {
        WriteSource("nested/added.txt", "new");
        WriteSource("changed.txt", "new");
        WriteTarget("changed.txt", "old"); // Same size: changes must be found by checksum.
        WriteSource("identical.txt", "same");
        WriteTarget("identical.txt", "same");
        WriteTarget("deleted.txt", "local");

        var result = await Run(new SyncerOptions
        {
            Rules = [Rule(action)],
            Context = new SyncContext { IsForced = true }
        }, apply);

        var updatesExisting = action is SyncAction.FullSync or SyncAction.UpdateOnly;
        AssertPaths(action != SyncAction.Exclude ? ["nested/added.txt"] : [], result.AddedFiles);
        AssertPaths(action == SyncAction.FullSync ? ["deleted.txt"] : [], result.DeletedFiles);
        AssertPaths(updatesExisting ? ["identical.txt"] : [],
            result.IdenticalFilePairs.Select(pair => pair.Source));
        var expectedUpdates = new List<string>();
        if (updatesExisting)
            expectedUpdates.Add("changed.txt");
        if (apply && action != SyncAction.Exclude)
            expectedUpdates.Add("nested/added.txt");
        AssertPaths(expectedUpdates, result.UpdatedFilePairs.Select(pair => pair.Source));

        Assert.Equal(apply && action != SyncAction.Exclude, TargetExists("nested/added.txt"));
        Assert.Equal(apply && updatesExisting ? "new" : "old", ReadTarget("changed.txt"));
        Assert.Equal("same", ReadTarget("identical.txt"));
        Assert.True(TargetExists("deleted.txt")); // Returning a deletion must not execute it.
        if (!updatesExisting)
        {
            Assert.DoesNotContain("changed.txt", _comparer.Paths);
            Assert.DoesNotContain("identical.txt", _comparer.Paths);
        }
        if (action == SyncAction.Exclude)
            Assert.Empty(_comparer.Paths);

        if (apply)
        {
            CreateSyncer().DeleteLocalFiles(result.DeletedFiles);
            Assert.Equal(action != SyncAction.FullSync, TargetExists("deleted.txt"));
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task version_conditions_fall_through_and_force_respects_protected_files(
        bool isNewVersion, bool isForced, bool apply)
    {
        foreach (var path in new[] { "mods/+changed.jar", "resourcepacks/nested/changed.zip", "config/options.json", "private/token.json" })
        {
            WriteSource(path, "new");
            WriteTarget(path, "old");
        }
        WriteSource("mods/+missing.jar", "new");
        WriteSource("private/missing.json", "new");
        WriteTarget("mods/+old.jar", "old");
        WriteTarget("resourcepacks/old.zip", "old");
        WriteTarget("config/local.json", "local");
        WriteTarget("private/local.json", "local");

        var options = new SyncerOptions
        {
            Rules =
            [
                Rule(SyncAction.FullSync, "mods/+*") with { Condition = SyncCondition.OnNewVersion },
                Rule(SyncAction.FullSync, "resourcepacks/**"),
                Rule(SyncAction.Exclude, "private/**"),
                Rule(SyncAction.InstallOnly)
            ],
            Context = new SyncContext { IsNewVersion = isNewVersion, IsForced = isForced }
        };
        var result = await Run(options, apply);
        var active = isNewVersion || isForced;

        AssertPaths(["mods/+missing.jar"], result.AddedFiles);
        AssertPaths(active ? ["mods/+old.jar", "resourcepacks/old.zip"] : ["resourcepacks/old.zip"], result.DeletedFiles);
        var updates = new List<string> { "resourcepacks/nested/changed.zip" };
        if (active)
            updates.Add("mods/+changed.jar");
        if (apply)
            updates.Add("mods/+missing.jar");
        AssertPaths(updates, result.UpdatedFilePairs.Select(pair => pair.Source));
        Assert.Equal(apply && active ? "new" : "old", ReadTarget("mods/+changed.jar"));
        Assert.Equal(apply ? "new" : "old", ReadTarget("resourcepacks/nested/changed.zip"));
        Assert.Equal("old", ReadTarget("config/options.json"));
        Assert.Equal("old", ReadTarget("private/token.json"));
        Assert.False(TargetExists("private/missing.json"));
        Assert.DoesNotContain("config/options.json", _comparer.Paths);
        Assert.DoesNotContain("private/token.json", _comparer.Paths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task first_matching_rule_wins_even_when_later_pattern_is_more_specific(bool excludeFirst)
    {
        WriteSource("mods/+example.jar", "new");
        var exclude = Rule(SyncAction.Exclude);
        var fullSync = Rule(SyncAction.FullSync, "mods/+*");
        var result = await Run(new SyncerOptions
        {
            Rules = excludeFirst ? [exclude, fullSync] : [fullSync, exclude]
        }, true);

        Assert.Equal(!excludeFirst, TargetExists("mods/+example.jar"));
        Assert.Equal(excludeFirst ? 0 : 1, result.AddedFiles.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task inactive_rule_without_explicit_fallback_throws(bool apply)
    {
        WriteSource("missing.txt", "new");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Run(new SyncerOptions
        {
            Rules = [Rule(SyncAction.FullSync) with { Condition = SyncCondition.OnNewVersion }]
        }, apply));

        Assert.Contains("missing.txt", error.Message);
        Assert.Equal("Rules", error.ParamName);
        Assert.Empty(_comparer.Paths);
        Assert.False(TargetExists("missing.txt"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task explicit_exclude_rule_preserves_all_files_without_a_comparer(bool apply)
    {
        WriteSource("added.txt", "new");
        WriteSource("changed.txt", "new");
        WriteTarget("changed.txt", "old");
        WriteTarget("local.txt", "old");
        var result = await Run(new SyncerOptions { Rules = [Rule(SyncAction.Exclude)] }, apply);

        Assert.Empty(result.AddedFiles);
        Assert.Empty(result.UpdatedFilePairs);
        Assert.Empty(result.IdenticalFilePairs);
        Assert.Empty(result.DeletedFiles);
        Assert.Empty(_comparer.Paths);
        Assert.False(TargetExists("added.txt"));
        Assert.Equal("old", ReadTarget("changed.txt"));
        Assert.True(TargetExists("local.txt"));
    }

    [Theory]
    [InlineData("added", false)]
    [InlineData("added", true)]
    [InlineData("existing", false)]
    [InlineData("existing", true)]
    [InlineData("target-only", false)]
    [InlineData("target-only", true)]
    public async Task unmatched_path_in_any_file_state_fails_before_comparison_or_transfer(string state, bool apply)
    {
        WriteSource("matched/added.txt", "new");
        WriteSource("matched/changed.txt", "new");
        WriteTarget("matched/changed.txt", "old");
        if (state != "target-only")
            WriteSource("other/file.txt", "new");
        if (state != "added")
            WriteTarget("other/file.txt", "old");

        var error = await Assert.ThrowsAsync<ArgumentException>(() => Run(new SyncerOptions
        {
            Rules = [Rule(SyncAction.FullSync, "matched/**")]
        }, apply));

        Assert.Contains("other/file.txt", error.Message);
        Assert.Equal("Rules", error.ParamName);
        Assert.Empty(_comparer.Paths);
        Assert.False(TargetExists("matched/added.txt"));
        Assert.Equal("old", ReadTarget("matched/changed.txt"));
        Assert.Equal(state != "added", TargetExists("other/file.txt"));
        if (state != "added")
            Assert.Equal("old", ReadTarget("other/file.txt"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task empty_rules_are_rejected_even_without_files(bool apply)
    {
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Run(new SyncerOptions { Rules = [] }, apply));
        Assert.Contains("Rules", error.Message);
    }

    public static IEnumerable<object?[]> InvalidOptions()
    {
        var valid = new SyncRule(SyncAction.FullSync, SyncCondition.Always, "**", new LocalFileChecksumComparer());
        yield return [null, "options"];
        yield return [new SyncerOptions(), "Rules"];
        yield return [new SyncerOptions { Rules = null }, "Rules"];
        yield return [new SyncerOptions { Rules = [] }, "Rules"];
        yield return [new SyncerOptions { Rules = [valid], Context = null! }, "Context"];
        foreach (var (rule, field) in new (SyncRule?, string)[]
        {
            (null, "rule"),
            (valid with { Action = default }, "action"),
            (valid with { Action = (SyncAction)99 }, "action"),
            (valid with { Condition = default }, "condition"),
            (valid with { Condition = (SyncCondition)99 }, "condition"),
            (valid with { Pattern = "" }, "pattern"),
            (valid with { Pattern = null! }, "pattern"),
            (valid with { Pattern = "[a-]" }, "pattern"),
            (valid with { Comparer = null }, "comparer"),
            (valid with { Action = SyncAction.UpdateOnly, Comparer = null }, "comparer"),
            (valid with { Action = SyncAction.InstallOnly, Comparer = null }, "comparer")
        })
        {
            // Even an unreachable or inactive rule must be validated.
            var invalid = rule == null ? null! : rule with
            {
                Condition = field == "condition" ? rule.Condition : SyncCondition.OnNewVersion
            };
            yield return [new SyncerOptions { Rules = [valid, invalid] }, $"Rules[1].{field}"];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public async Task invalid_configuration_fails_before_enumeration_or_file_changes(SyncerOptions? options, string errorField)
    {
        WriteSource("added.txt", "new");
        WriteSource("changed.txt", "new");
        WriteTarget("changed.txt", "old");
        WriteTarget("local.txt", "old");

        foreach (var apply in new[] { false, true })
        {
            var error = await Assert.ThrowsAnyAsync<ArgumentException>(() => Run(options, apply));
            Assert.Contains(errorField, error.Message);
            var syncer = new SyncFileCollectionSyncer(new ParallelSyncFilePairSyncer(1), _pathOptions);
            await Assert.ThrowsAnyAsync<ArgumentException>(() => apply
                ? syncer.CompareAndSyncFiles(ThrowOnEnumeration(), ThrowOnEnumeration(), options)
                : syncer.CompareFiles(ThrowOnEnumeration(), ThrowOnEnumeration(), options));
        }

        Assert.Empty(_comparer.Paths);
        Assert.False(TargetExists("added.txt"));
        Assert.Equal("old", ReadTarget("changed.txt"));
        Assert.True(TargetExists("local.txt"));
    }

    [Theory]
    [InlineData("mods/+*", "mods/+example.jar", true, true)]
    [InlineData("mods/+*", "mods/example.jar", true, false)]
    [InlineData("mods/+*", "mods/+nested/file.jar", true, false)]
    [InlineData("resourcepacks/**", "resourcepacks/nested/file.zip", true, true)]
    [InlineData("MODS/+*", "mods/+example.jar", true, true)]
    [InlineData("MODS/+*", "mods/+example.jar", false, false)]
    [InlineData("mods\\+*", "mods/+example.jar", false, true)]
    [InlineData("mods//./+*", "mods\\+example.jar", false, false)]
    [InlineData("mods/[a-z].jar", "mods/a.jar", false, true)]
    [InlineData("mods/[!a].jar", "mods/b.jar", false, true)]
    [InlineData("[abc", "a", false, true)]
    [InlineData("  ", "  ", false, true)]
    [InlineData("/absolute/**", "absolute/file", false, false)]
    [InlineData("C:\\absolute\\**", "absolute/file", false, false)]
    [InlineData("../outside/**", "outside/file", false, false)]
    public async Task glob_matching_respects_path_options(string pattern, string path, bool ignoreCase, bool matches)
    {
        // Exercise either primary separator without relying on the host filesystem.
        foreach (var separator in new[] { '/', '\\' })
        {
            var options = new PathOptions
            {
                PathSeparator = separator,
                AltPathSeparator = separator == '/' ? '\\' : '/',
                CaseInsensitive = ignoreCase
            };
            var source = new VirtualSyncFile(RootedPath.FromSubPath(path, options));
            var syncer = new SyncFileCollectionSyncer(new ParallelSyncFilePairSyncer(1), options);
            var result = await syncer.CompareFiles([source], [], new SyncerOptions
            {
                Rules = [Rule(SyncAction.InstallOnly, pattern), Rule(SyncAction.Exclude)]
            });
            Assert.Equal(matches ? 1 : 0, result.AddedFiles.Count);
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task legacy_launcher_rules_select_comparers_and_deletions(
        bool isNewVersion, bool isForced, bool apply)
    {
        WriteSource("mods/+changed.jar", "new");
        WriteTarget("mods/+changed.jar", "old");
        WriteSource("mods/+protected.cfg", "new contents");
        WriteTarget("mods/+protected.cfg", "old");
        WriteSource("mods/+protected-missing.cfg", "new");
        WriteSource("config/changed.txt", "new contents");
        WriteTarget("config/changed.txt", "old");
        WriteSource("config/same-size.txt", "new");
        WriteTarget("config/same-size.txt", "old");
        WriteSource("config/missing.txt", "new");
        WriteTarget("mods/+old.jar", "old");
        WriteTarget("mods/+protected-local.cfg", "local");
        WriteTarget("config/local.txt", "local");

        var checksum = new RecordingComparer();
        var size = new RecordingComparer(new LocalFileSizeComparer());
        var result = await Run(new SyncerOptions
        {
            Rules =
            [
                Rule(SyncAction.InstallOnly, "mods/+protected*"),
                Rule(SyncAction.FullSync, "mods/+*") with { Comparer = checksum },
                Rule(SyncAction.FullSync) with { Condition = SyncCondition.OnNewVersion, Comparer = checksum },
                Rule(SyncAction.UpdateOnly) with { Comparer = size }
            ],
            Context = new SyncContext { IsNewVersion = isNewVersion, IsForced = isForced }
        }, apply);

        var active = isNewVersion || isForced;
        AssertPaths(["config/missing.txt", "mods/+protected-missing.cfg"], result.AddedFiles);
        AssertPaths(active ? ["config/local.txt", "mods/+old.jar"] : ["mods/+old.jar"], result.DeletedFiles);
        AssertPaths(active ? [] : ["config/same-size.txt"], result.IdenticalFilePairs.Select(pair => pair.Source));
        var updated = new List<string> { "mods/+changed.jar", "config/changed.txt" };
        if (active)
            updated.Add("config/same-size.txt");
        if (apply)
            updated.AddRange(["config/missing.txt", "mods/+protected-missing.cfg"]);
        AssertPaths(updated, result.UpdatedFilePairs.Select(pair => pair.Source));
        Assert.Equal(apply ? "new" : "old", ReadTarget("mods/+changed.jar"));
        Assert.Equal(apply ? "new contents" : "old", ReadTarget("config/changed.txt"));
        Assert.Equal(apply && active ? "new" : "old", ReadTarget("config/same-size.txt"));
        Assert.Equal("old", ReadTarget("mods/+protected.cfg"));
        Assert.Contains("mods/+changed.jar", checksum.Paths);
        if (active)
        {
            Assert.Contains("config/same-size.txt", checksum.Paths);
            Assert.Empty(size.Paths);
        }
        else
        {
            Assert.Contains("config/same-size.txt", size.Paths);
            Assert.DoesNotContain("config/same-size.txt", checksum.Paths);
        }
        Assert.Equal(apply ? 2 : 0, _comparer.Paths.Count);
        Assert.All(_comparer.Paths, path => Assert.Equal("mods/+protected-missing.cfg", path));

        if (apply)
        {
            CreateSyncer().DeleteLocalFiles(result.DeletedFiles);
            Assert.False(TargetExists("mods/+old.jar"));
            Assert.Equal(!active, TargetExists("config/local.txt"));
            Assert.True(TargetExists("mods/+protected-local.cfg"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task first_matching_rule_uses_its_own_comparer(bool apply)
    {
        WriteSource("size.txt", "new");
        WriteTarget("size.txt", "old");
        WriteSource("checksum.txt", "new");
        WriteTarget("checksum.txt", "old");
        var size = new RecordingComparer(new LocalFileSizeComparer());
        var unused = new RecordingComparer();

        var result = await Run(new SyncerOptions
        {
            Rules =
            [
                Rule(SyncAction.UpdateOnly, "size.txt") with { Comparer = size },
                Rule(SyncAction.UpdateOnly, "checksum.txt"),
                Rule(SyncAction.FullSync) with { Comparer = unused }
            ]
        }, apply);

        AssertPaths(["size.txt"], result.IdenticalFilePairs.Select(pair => pair.Source));
        AssertPaths(["checksum.txt"], result.UpdatedFilePairs.Select(pair => pair.Source));
        Assert.Equal("old", ReadTarget("size.txt"));
        Assert.Equal(apply ? "new" : "old", ReadTarget("checksum.txt"));
        Assert.Empty(unused.Paths);
        Assert.Equal(["size.txt"], size.Paths);
        Assert.All(_comparer.Paths, path => Assert.Equal("checksum.txt", path));
    }

    [Theory]
    [InlineData(SyncAction.FullSync)]
    [InlineData(SyncAction.UpdateOnly)]
    [InlineData(SyncAction.InstallOnly)]
    public async Task rule_comparer_verifies_new_files_after_transfer(SyncAction action)
    {
        WriteSource("added.txt", "new");
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var rejectingComparer = new CallbackComparer((pair, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            Assert.Equal("added.txt", pair.Source.Path.SubPath);
            calls++;
            return new ValueTask<bool>(false);
        });

        await Assert.ThrowsAsync<FileIntegrityException>(() => Run(new SyncerOptions
        {
            Rules = [Rule(action) with { Comparer = rejectingComparer }],
            CancellationToken = cancellation.Token
        }, true));

        Assert.Equal(2, calls);
        Assert.Equal("new", ReadTarget("added.txt"));
        Assert.Empty(_comparer.Paths);
    }

    [Fact]
    public async Task selected_rule_and_comparer_remain_fixed_during_transfers()
    {
        WriteSource("changed.txt", "new");
        WriteTarget("changed.txt", "old");
        var checksum = new RecordingComparer();
        var rules = new List<SyncRule>();
        var comparer = new CallbackComparer((pair, token) =>
        {
            rules[0] = Rule(SyncAction.Exclude);
            return checksum.AreEqual(pair, token);
        });
        rules.Add(Rule(SyncAction.UpdateOnly) with { Comparer = comparer });

        var result = await Run(new SyncerOptions { Rules = rules }, true);

        Assert.Equal("new", ReadTarget("changed.txt"));
        AssertPaths(["changed.txt"], result.UpdatedFilePairs.Select(pair => pair.Source));
        Assert.Equal(2, checksum.Paths.Count);
        Assert.Empty(_comparer.Paths);
    }

    private sealed class CallbackComparer(Func<SyncFilePair, CancellationToken, ValueTask<bool>> compare) : IFileComparer
    {
        public ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken) =>
            compare(pair, cancellationToken);
    }

    private Task<SyncFileCollectionComparerResult> Run(SyncerOptions? options, bool apply)
    {
        var syncer = CreateSyncer();
        var sources = LocalSyncer.EnumerateLocalSyncFiles(SourceRoot, _pathOptions);
        return apply
            ? syncer.CompareAndSyncFiles(sources, options)
            : syncer.CompareFiles(sources, options);
    }

    private LocalSyncer CreateSyncer() => new(TargetRoot, _pathOptions, new ParallelSyncFilePairSyncer(1));
    private SyncRule Rule(SyncAction action, string pattern = "**") =>
        new(action, SyncCondition.Always, pattern, action == SyncAction.Exclude ? null : _comparer);
    private bool TargetExists(string path) => File.Exists(Path.Combine(TargetRoot, path));
    private string ReadTarget(string path) => File.ReadAllText(Path.Combine(TargetRoot, path));
    private void WriteTarget(string path, string content) => Write(TargetRoot, path, content);
    private void WriteSource(string path, string content) => Write(SourceRoot, path, content);

    private static void Write(string root, string path, string content)
    {
        var fullPath = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private static void AssertPaths(IEnumerable<string> expected, IEnumerable<SyncFile> actual) =>
        Assert.Equal(expected.OrderBy(path => path), actual.Select(file => file.Path.SubPath).OrderBy(path => path));

    private static IEnumerable<SyncFile> ThrowOnEnumeration() =>
        Enumerable.Range(0, 1).Select<int, SyncFile>(_ =>
            throw new InvalidOperationException("Files must not be enumerated before rule validation."));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class RecordingComparer(IFileComparer? inner = null) : IFileComparer
    {
        public ConcurrentBag<string> Paths { get; } = new();

        public async ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
        {
            Paths.Add(pair.Source.Path.SubPath);
            using var stream = await pair.Source.OpenReadStream(cancellationToken);
            var source = new VirtualSyncFile(pair.Source.Path)
            {
                Metadata = new SyncFileMetadata
                {
                    Size = stream.Length,
                    Checksum = new SyncFileChecksum(ChecksumAlgorithmNames.SHA1,
                        ChecksumAlgorithms.ComputeHash(ChecksumAlgorithmNames.SHA1, stream))
                }
            };
            return await (inner ?? new LocalFileChecksumComparer()).AreEqual(new(source, pair.Target), cancellationToken);
        }
    }
}
