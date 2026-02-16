using FoodApp.Database;
using FoodApp.Services;
using FoodApp.Views.Auth;

namespace FoodApp;

public partial class App : Application
{
    private static AppDatabase? _database;
    private static MeshNetworkService? _networkService;
    private static SyncService? _syncService;

    public static AppDatabase Database => _database ?? throw new InvalidOperationException("Database not initialized");
    public static MeshNetworkService NetworkService => _networkService ?? throw new InvalidOperationException("Network not initialized");
    public static SyncService SyncService => _syncService ?? throw new InvalidOperationException("Sync not initialized");

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Initialize database
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "FoodApp.db3");
        _database = new AppDatabase(dbPath);

        // Initialize network services
        _networkService = new MeshNetworkService();
        _syncService = new SyncService(_database, _networkService);
        _syncService.Initialize();

        // Start network
        _ = _networkService.StartAsync();

        // Subscribe to chat messages (global)
        _networkService.ChatMessageReceived += OnChatMessageReceived;

        return new Window(new NavigationPage(new LoginPage()));
    }

    private void OnChatMessageReceived(object? sender, Models.ChatMessage message)
    {
        // Don't show messages from manager (based on your requirement)
        var senderUser = Database.GetUserAsync(message.SenderId).Result;
        if (senderUser?.Role == Models.UserRole.Manager) return;

        // Show toast/notification for new message
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var currentPage = Application.Current?.Windows[0]?.Page;
            if (currentPage != null)
            {
                await currentPage.DisplayAlert(
                    $"پیام از {message.SenderName}", 
                    message.Content, 
                    "بستن");
            }
        });
    }

    protected override void OnSleep()
    {
        // App going to background
        _networkService?.Stop();
        base.OnSleep();
    }

    protected override void OnResume()
    {
        // App coming to foreground
        _ = _networkService?.StartAsync();
        base.OnResume();
    }
}
