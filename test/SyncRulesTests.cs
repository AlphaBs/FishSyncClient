using System.Collections.Concurrent;
using System.Text.Json;
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

        AssertPaths(action != SyncAction.Exclude ? ["nested/added.txt"] : [], result.AddedFiles);
        AssertPaths(action == SyncAction.FullSync ? ["deleted.txt"] : [], result.DeletedFiles);
        AssertPaths(action == SyncAction.FullSync ? ["identical.txt"] : [],
            result.IdenticalFilePairs.Select(pair => pair.Source));
        var expectedUpdates = new List<string>();
        if (action == SyncAction.FullSync)
            expectedUpdates.Add("changed.txt");
        if (apply && action != SyncAction.Exclude)
            expectedUpdates.Add("nested/added.txt");
        AssertPaths(expectedUpdates, result.UpdatedFilePairs.Select(pair => pair.Source));

        Assert.Equal(apply && action != SyncAction.Exclude, TargetExists("nested/added.txt"));
        Assert.Equal(apply && action == SyncAction.FullSync ? "new" : "old", ReadTarget("changed.txt"));
        Assert.Equal("same", ReadTarget("identical.txt"));
        Assert.True(TargetExists("deleted.txt")); // Returning a deletion must not execute it.
        if (action != SyncAction.FullSync)
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

        var options = JsonSerializer.Deserialize<SyncerOptions>("""
            { "rules": [
                { "action": "fullSync", "condition": "onNewVersion", "pattern": "mods/+*" },
                { "action": "fullSync", "pattern": "resourcepacks/**" },
                { "action": "exclude", "pattern": "private/**" },
                { "action": "installOnly", "pattern": "**" }
            ] }
            """)!;
        options = options with
        {
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
    public async Task inactive_rule_does_not_install_without_an_installing_fallback(bool apply)
    {
        WriteSource("missing.txt", "new");
        var result = await Run(new SyncerOptions
        {
            Rules = [Rule(SyncAction.FullSync) with { Condition = SyncCondition.OnNewVersion }]
        }, apply);

        Assert.Empty(result.AddedFiles);
        Assert.Empty(_comparer.Paths);
        Assert.False(TargetExists("missing.txt"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task empty_or_unmatched_rules_exclude_all_files(bool emptyRules, bool apply)
    {
        WriteSource("added.txt", "new");
        WriteSource("changed.txt", "new");
        WriteTarget("changed.txt", "old");
        WriteTarget("local.txt", "old");
        var result = await Run(new SyncerOptions
        {
            Rules = emptyRules ? [] : [Rule(SyncAction.FullSync, "other/**")]
        }, apply);

        Assert.Empty(result.AddedFiles);
        Assert.Empty(result.UpdatedFilePairs);
        Assert.Empty(result.IdenticalFilePairs);
        Assert.Empty(result.DeletedFiles);
        Assert.Empty(_comparer.Paths);
        Assert.False(TargetExists("added.txt"));
        Assert.Equal("old", ReadTarget("changed.txt"));
        Assert.True(TargetExists("local.txt"));
    }

    public static IEnumerable<object?[]> InvalidOptions()
    {
        yield return [null, "options"];
        yield return [new SyncerOptions(), "Rules"];
        yield return [new SyncerOptions { Rules = null }, "Rules"];
        yield return [new SyncerOptions { Rules = [], Context = null! }, "Context"];
        foreach (var (rule, field) in new (SyncRule?, string)[]
        {
            (null, "rule"),
            (new SyncRule { Pattern = "**" }, "action"),
            (Rule((SyncAction)99), "action"),
            (Rule(SyncAction.FullSync) with { Condition = (SyncCondition)99 }, "condition"),
            (new SyncRule { Action = SyncAction.FullSync }, "pattern"),
            (Rule(SyncAction.FullSync, null!), "pattern"),
            (Rule(SyncAction.FullSync, "[a-]"), "pattern"),
        })
        {
            // Even an unreachable or inactive rule must be validated.
            var invalid = rule == null ? null! : rule with
            {
                Condition = field == "condition" ? rule.Condition : SyncCondition.OnNewVersion
            };
            yield return [new SyncerOptions { Rules = [Rule(SyncAction.FullSync), invalid] }, $"Rules[1].{field}"];
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
                ? syncer.CompareAndSyncFiles(ThrowOnEnumeration(), ThrowOnEnumeration(), _comparer, options)
                : syncer.CompareFiles(ThrowOnEnumeration(), ThrowOnEnumeration(), _comparer, options));
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
            var result = await syncer.CompareFiles([source], [], _comparer, new SyncerOptions
            {
                Rules = [Rule(SyncAction.InstallOnly, pattern)]
            });
            Assert.Equal(matches ? 1 : 0, result.AddedFiles.Count);
        }
    }

    [Fact]
    public void rules_round_trip_with_camel_case_string_values_and_default_condition()
    {
        var rules = JsonSerializer.Deserialize<SyncRule[]>("""
            [
                { "action": "fullSync", "condition": "onNewVersion", "pattern": "mods/+*" },
                { "action": "installOnly", "pattern": "**" },
                { "action": "exclude", "pattern": "private/**" }
            ]
            """)!;
        Assert.Equal(SyncCondition.Always, rules[1].Condition);
        var json = JsonSerializer.Serialize(rules);
        Assert.Contains("\"action\":\"fullSync\"", json);
        Assert.Contains("\"condition\":\"onNewVersion\"", json);
        Assert.Contains("\"action\":\"installOnly\"", json);
        Assert.Contains("\"action\":\"exclude\"", json);
        Assert.Equal(rules, JsonSerializer.Deserialize<SyncRule[]>(json));
    }

    [Theory]
    [InlineData("action", "\"sync\"")]
    [InlineData("action", "\"fullSync, installOnly\"")]
    [InlineData("action", "\"FullSync\"")]
    [InlineData("action", "1")]
    [InlineData("action", "null")]
    [InlineData("condition", "\"sometimes\"")]
    [InlineData("condition", "\"always, onNewVersion\"")]
    [InlineData("condition", "0")]
    [InlineData("condition", "null")]
    public void invalid_json_enum_values_report_rule_index_and_field(string field, string value)
    {
        var json = "{\"rules\":[{\"" + field + "\":" + value + ",\"pattern\":\"**\"}]}";
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SyncerOptions>(json));
        Assert.Equal($"$.rules[0].{field}", error.Path);
    }

    private Task<SyncFileCollectionComparerResult> Run(SyncerOptions? options, bool apply)
    {
        var syncer = CreateSyncer();
        var sources = LocalSyncer.EnumerateLocalSyncFiles(SourceRoot, _pathOptions);
        return apply
            ? syncer.CompareAndSyncFiles(sources, _comparer, options)
            : syncer.CompareFiles(sources, _comparer, options);
    }

    private LocalSyncer CreateSyncer() => new(TargetRoot, _pathOptions, new ParallelSyncFilePairSyncer(1));
    private static SyncRule Rule(SyncAction action, string pattern = "**") => new() { Action = action, Pattern = pattern };
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

    private sealed class RecordingComparer : IFileComparer
    {
        public ConcurrentBag<string> Paths { get; } = new();

        public async ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
        {
            Paths.Add(pair.Source.Path.SubPath);
            using var stream = await pair.Source.OpenReadStream(cancellationToken);
            pair.Source.Metadata = new SyncFileMetadata
            {
                Checksum = new SyncFileChecksum(ChecksumAlgorithmNames.SHA1,
                    ChecksumAlgorithms.ComputeHash(ChecksumAlgorithmNames.SHA1, stream))
            };
            return await new LocalFileChecksumComparer().AreEqual(pair, cancellationToken);
        }
    }
}
