using FishSyncClient.Server;

namespace FishSyncClientTest;

public class FishBucketDependencyResolverTests
{
    [Fact]
    public async Task resolve_single()
    {
        var client = new MockFishApiClient();
        client.Add(new FishBucketFiles
        {
            Id = "root",
            Dependencies = [],
            Files = [file("1"), file("2"), file("3")]
        });
        
        var result = await FishBucketDependencyResolver.Resolve(client, "root");
        Assert.Equal("root", result.Id);
        Assert.Equal([file("1"), file("2"), file("3")], result.Files);
    }

    [Fact]
    public async Task resolve_bucket_with_dependencies()
    {
        var client = new MockFishApiClient();
        client.Add(new FishBucketFiles
        {
            Id = "root",
            Dependencies = ["dep1", "dep2"],
            Files = [file("1"), file("2")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep1",
            Dependencies = ["dep3"],
            Files = [file("3"), file("4")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep2",
            Dependencies = [],
            Files = [file("5"), file("6")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep3",
            Dependencies = [],
            Files = [file("7"), file("8")]
        });
        
        var result = await FishBucketDependencyResolver.Resolve(client, "root");
        Assert.Equal("root", result.Id);
        Assert.Equal(
            ["1", "2", "3", "4", "5", "6", "7", "8"], 
            result.Files.Select(f => f.Path).ToHashSet());
    }

    [Fact]
    public async Task avoid_circular_dependencies()
    {
        var client = new MockFishApiClient();
        client.Add(new FishBucketFiles
        {
            Id = "root",
            Dependencies = ["root", "dep1"],
            Files = [file("1"), file("2")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep1",
            Dependencies = ["dep2"],
            Files = [file("3"), file("4")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep2",
            Dependencies = ["root", "dep1", "dep3"],
            Files = [file("5"), file("6")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep3",
            Dependencies = ["dep1", "dep3"],
            Files = [file("7"), file("8")]
        });
        
        var result = await FishBucketDependencyResolver.Resolve(client, "root");
        Assert.Equal("root", result.Id);
        Assert.Equal(
            ["1", "2", "3", "4", "5", "6", "7", "8"], 
            result.Files.Select(f => f.Path).ToHashSet());
    }

    [Fact]
    public async Task overwrite_root_file()
    {
        var client = new MockFishApiClient();
        client.Add(new FishBucketFiles
        {
            Id = "root",
            Dependencies = ["root", "dep1"],
            Files = [file("1", "root"), file("2", "root")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep1",
            Dependencies = ["dep2"],
            Files = [file("3", "dep1"), file("4", "dep1")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep2",
            Dependencies = ["root", "dep1", "dep3"],
            Files = [file("1", "dep2"), file("3", "dep2")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep3",
            Dependencies = ["dep1", "dep3"],
            Files = [file("2", "dep3"), file("4", "dep3")]
        });
        
        var result = await FishBucketDependencyResolver.Resolve(client, "root");
        Assert.Equal("root", result.Id);
        Assert.Equal([
                file("1", "root"), 
                file("2", "root"), 
                file("3", "dep1"), 
                file("4", "dep1")], 
            result.Files.ToHashSet());
    }

    [Fact]
    public async Task stop_too_deep_dependencies()
    {
        var client = new MockFishApiClient();
        client.Add(new FishBucketFiles
        {
            Id = "root",
            Dependencies = ["dep1"],
            Files = [file("1"), file("2")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep1",
            Dependencies = ["dep2"],
            Files = [file("3"), file("4")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep2",
            Dependencies = ["dep3"],
            Files = [file("5"), file("6")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep3",
            Dependencies = ["dep4"],
            Files = [file("7"), file("8")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep4",
            Dependencies = ["dep5"],
            Files = [file("9"), file("10")]
        });
        client.Add(new FishBucketFiles
        {
            Id = "dep5",
            Dependencies = [],
            Files = [file("11"), file("12")]
        });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await FishBucketDependencyResolver.Resolve(client, "root", 4);
        });
    }
    
    private static FishBucketFile file(string path) => file(path, path);
    private static FishBucketFile file(string path, string location)
    {
        return new FishBucketFile(
            path, 
            location, 
            new FishFileMetadata(
                0, 
                DateTimeOffset.MinValue, 
                string.Empty));
    }
}