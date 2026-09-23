using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using MyApp.Licensing;

internal static class DesktopHost
{
    public static int Run(string publicKey, string issuer, string product, string buildDate, string[] examples)
    {
        var lifetime = new ClassicDesktopStyleApplicationLifetime { ShutdownMode = ShutdownMode.OnLastWindowClose };
        AppBuilder.Configure<Application>().UsePlatformDetect().AfterSetup(b => b.Instance!.Styles.Add(new FluentTheme())).SetupWithLifetime(lifetime);
        var body = new StackPanel { Spacing = 18, Margin = new Thickness(32) };
        TextBlock Text(string s, int size = 16) => new() { Text = s, FontSize = size, TextWrapping = TextWrapping.Wrap };
        body.Children.Add(Text("Acme Studio · simple offline licensing", 28));
        body.Children.Add(Text($"Build released {buildDate}. Free editing is always available."));
        var editor = new TextBox { Text = "My next big idea", AcceptsReturn = true, MinHeight = 120 };
        var input = new TextBox { PlaceholderText = "Paste your JWT license key", AcceptsReturn = true, Height = 90, MaxLength = 16000 };
        body.Children.Add(editor); body.Children.Add(input);
        var status = Text("Free · no license loaded"); body.Children.Add(status);
        var verify = new Button { Content = "Verify offline" }; body.Children.Add(verify);
        var export = new Button { Content = "Pro export", IsEnabled = false };
        var batch = new Button { Content = "Pro uppercase", IsEnabled = false };
        body.Children.Add(export); body.Children.Add(batch);
        LicenseCheck Check() => LicenseJwt.Verify(input.Text, publicKey, issuer, product, buildDate);
        void Update() {
            var result = Check(); export.IsEnabled = batch.IsEnabled = result.Valid;
            status.Text = result.Valid ? "Pro · this build is covered" : result.Status == "buildNotCovered"
                ? "Renew for this build, or keep using a covered version." : "Free · import a valid license to unlock Pro";
            if (result.License is { } c) status.Text += $"\nRegistered to {c.Name} · {c.Organization} · {c.Seats} seats";
        }
        verify.Click += (_, _) => Update();
        input.TextChanged += (_, _) => { export.IsEnabled = batch.IsEnabled = false; };
        export.Click += async (_, _) => {
            if (!Check().Valid) { Update(); return; }
            var window = lifetime.MainWindow!;
            var file = await window.StorageProvider.SaveFilePickerAsync(new() { Title = "Export", SuggestedFileName = "acme.txt" });
            if (file == null) return;
            try { await using var output = await file.OpenWriteAsync(); using var writer = new StreamWriter(output); await writer.WriteAsync(editor.Text); }
            catch (Exception) { status.Text = "Could not write the export."; }
        };
        batch.Click += (_, _) => { if (Check().Valid) editor.Text = editor.Text?.ToUpperInvariant(); else Update(); };
        foreach (var (token, index) in examples.Select((token, index) => (token, index))) {
            var button = new Button { Content = new[] { "Demo: covered", "Demo: newer build needs renewal", "Demo: lifetime" }[index] };
            button.Click += (_, _) => { input.Text = token; Update(); }; body.Children.Add(button);
        }
        body.Children.Add(Text("No network, activation, feature catalog or helper process is required. Paste the renewed key after upgrading.", 12));
        lifetime.MainWindow = new Window { Title = "Acme Studio", Width = 900, Height = 800, Content = new ScrollViewer { Content = body } };
        return lifetime.Start([]);
    }
}
