using Android.App;
using Android.OS;
using Android.Runtime;
using AndroidX.AppCompat.App;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace AndroidNtlm
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            SetContentView(Resource.Layout.activity_main);
            
            // This is only needed if you have invalid SSL certs during testing:
            {
                System.Environment.SetEnvironmentVariable("XA_HTTP_CLIENT_HANDLER_TYPE", "System.Net.Http.MonoWebRequestHandler, System.Net.Http");
                System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicy) => true;
            }

            _ = TestRuntimeWithNtlm();
        }

        const string url = "https://path/to/iwa/secured/service";
        const string username = "username";
        const string password = "password";

        private async Task TestRuntimeWithNtlm()
        {
            var handler = new NtlmHttp.NtlmHttpMessageHandler();
            HttpClient client = new HttpClient(handler);
            try
            {
                var result = await client.GetAsync(url);
                if (result.StatusCode == HttpStatusCode.Unauthorized)
                {
                    System.Diagnostics.Debug.WriteLine("Access denied - retrying with password");
                    handler.NetworkCredential = new NetworkCredential(username, password);
                    result = await client.GetAsync(url);
                }
                result.EnsureSuccessStatusCode();
                System.Diagnostics.Debug.WriteLine("Request successful");
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERR: " + ex.Message);
            }
        }
    }
}