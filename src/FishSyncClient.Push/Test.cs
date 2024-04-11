namespace FishSyncClient.Push;

public class Test
{
    async Task Main()
    {
        var client = new PushClient();
        var result = await client.Push();
    }
}