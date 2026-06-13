namespace GlassBilling;

public partial class App : Application
{
    public App(IServiceProvider services)
    {
        InitializeComponent();

        try
        {
            MainPage = services.GetRequiredService<AppShell>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"STARTUP CRASH: {ex}");
            MainPage = new ContentPage
            {
                BackgroundColor = Colors.White,
                Content = new ScrollView
                {
                    Content = new Label
                    {
                        Margin = new Thickness(16),
                        TextColor = Colors.Black,
                        FontSize = 13,
                        Text = $"Startup error:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                        LineBreakMode = LineBreakMode.WordWrap
                    }
                }
            };
        }
    }
}
