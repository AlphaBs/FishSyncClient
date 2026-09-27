using FishSyncClient.FileComparers;

namespace FishSyncClient.Syncer;

/// <summary>The first rule matching both the relative path and the condition wins.</summary>
/// <param name="Action">The operation to perform for matching files.</param>
/// <param name="Condition">The execution condition required to select this rule.</param>
/// <param name="Pattern">A glob relative to the sync root.</param>
/// <param name="Comparer">The comparer for matching files and transfer verification. Required unless Action is Exclude.</param>
public sealed record SyncRule(
    SyncAction Action,
    SyncCondition Condition,
    string Pattern,
    IFileComparer? Comparer);
