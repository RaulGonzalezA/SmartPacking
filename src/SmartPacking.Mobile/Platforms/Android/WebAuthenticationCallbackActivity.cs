using Android.App;
using Android.Content;
using Microsoft.Maui.Authentication;

namespace SmartPacking.Mobile;

[Activity(NoHistory = true, LaunchMode = Android.Content.PM.LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "smartpacking",
    DataHost = "callback")]
public sealed class WebAuthenticationCallbackActivity : WebAuthenticatorCallbackActivity
{
}
