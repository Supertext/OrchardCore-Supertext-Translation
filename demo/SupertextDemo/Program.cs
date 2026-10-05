using SupertextDemo.DemoSetup;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOrchardCms()
    // First boot: installs the site from Recipes/supertext-demo.recipe.json with the
    // DEMO_ADMIN_* account (mapped to OrchardCore_AutoSetup by demo/entrypoint.sh).
    .AddSetupFeatures("OrchardCore.AutoSetup")
    // Every start: languages, demo accounts, sample content.
    .ConfigureServices(services => services.AddDemoSetup());

var app = builder.Build();

// Warm-up request: triggers the first-boot AutoSetup (and the demo setup after it) right
// after deployment instead of on the first visitor's request.
app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
{
    // Kestrel reports e.g. "http://[::]:8080"; call it on the loopback address instead.
    var port = System.Text.RegularExpressions.Regex.Match(app.Urls.FirstOrDefault() ?? string.Empty, @":(\d+)/?$");
    if (!port.Success)
    {
        return;
    }
    var address = $"http://127.0.0.1:{port.Groups[1].Value}/";
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    for (var attempt = 0; attempt < 3; attempt++)
    {
        try
        {
            await http.GetAsync(address);
            await Task.Delay(TimeSpan.FromSeconds(5));
            await http.GetAsync(address);
            return;
        }
        catch (Exception)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}));

app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseOrchardCore();

await app.RunAsync();
