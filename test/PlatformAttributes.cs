using System.Runtime.InteropServices;

namespace FishSyncClientTest;

public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Skip = "Windows-only test";
    }
}

public sealed class WindowsOnlyTheoryAttribute : TheoryAttribute
{
    public WindowsOnlyTheoryAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Skip = "Windows-only test";
    }
}

public sealed class UnixOnlyFactAttribute : FactAttribute
{
    public UnixOnlyFactAttribute()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Skip = "Unix-like-only test";
    }
}

public sealed class UnixOnlyTheoryAttribute : TheoryAttribute
{
    public UnixOnlyTheoryAttribute()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Skip = "Unix-like-only test";
    }
}
