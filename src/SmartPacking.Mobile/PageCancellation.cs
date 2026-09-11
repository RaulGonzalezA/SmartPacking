using System.Diagnostics.CodeAnalysis;

namespace SmartPacking.Mobile;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The CTS is explicitly cancelled and disposed when the page leaves and before it is replaced.")]
internal sealed class PageCancellation
{
    private CancellationTokenSource? source;

    public CancellationToken Token => source?.Token ?? CancellationToken.None;

    public void Reset()
    {
        Release();
        source = new CancellationTokenSource();
    }

    public void Release()
    {
        source?.Cancel();
        source?.Dispose();
        source = null;
    }
}
