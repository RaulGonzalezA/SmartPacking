using Android.App;
using Android.Runtime;

namespace SmartPacking.Mobile;

#if DEBUG
[Application(UsesCleartextTraffic = true)]
#else
[Application]
#endif
public sealed class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
