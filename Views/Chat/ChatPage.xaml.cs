using FoodApp.Database;
using FoodApp.Models;
using FoodApp.Services;
using System.Collections.ObjectModel;

namespace FoodApp.Views.Chat;

public partial class ChatPage : ContentPage
{
    private readonly AppDatabase _database;
    private readonly MeshNetworkService _networkService;
    private ObservableCollection<ChatMessageDisplay> _messages = new();

    public ChatPage()
    {
        InitializeComponent();
        
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodapp.db");
        _database = new AppDatabase(dbPath);
        _networkService = new MeshNetworkService();
        
        MessagesCollection.ItemsSource = _messages;
        
        _ = _networkService.StartAsync();
        _networkService.ChatMessageReceived += OnMessageReceivedFromNetwork;
        
        LoadMessages();
    }

    private async void LoadMessages()
    {
        var messages = await _database.GetChatMessagesAsync();
        var currentUser = AuthService.GetCurrentUser();
        
        _messages.Clear();
        foreach (var msg in messages)
        {
            _messages.Add(new ChatMessageDisplay
            {
                SenderName = msg.SenderName ?? "Unknown",  // ✅ null check
                Content = msg.Content ?? "",  // ✅ null check
                Timestamp = msg.Timestamp,
                Alignment = msg.SenderId == currentUser?.Id ? LayoutOptions.End : LayoutOptions.Start,
                BackgroundColor = msg.SenderId == currentUser?.Id ? Colors.LightBlue : Colors.LightGray
            });
        }
    }

    private async void OnSendClicked(object? sender, EventArgs e)  // ✅ object? شد
    {
        if (string.IsNullOrWhiteSpace(MessageEntry.Text)) return;
        
        var currentUser = AuthService.GetCurrentUser();
        if (currentUser == null) return;  // ✅ null check اضافه شد
        
        var message = new ChatMessage
        {
            SenderId = currentUser.Id,
            SenderName = currentUser.Name,
            Content = MessageEntry.Text,
            Timestamp = DateTime.Now
        };
        
        await _database.SaveChatMessageAsync(message);
        await _networkService.BroadcastChatMessageAsync(message);
        
        _messages.Add(new ChatMessageDisplay
        {
            SenderName = currentUser.Name,
            Content = message.Content,
            Timestamp = message.Timestamp,
            Alignment = LayoutOptions.End,
            BackgroundColor = Colors.LightBlue
        });
        
        MessageEntry.Text = string.Empty;
    }

    private void OnMessageReceivedFromNetwork(object? sender, ChatMessage message)  // ✅ object? شد
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _messages.Add(new ChatMessageDisplay
            {
                SenderName = message.SenderName ?? "Unknown",  // ✅ null check
                Content = message.Content ?? "",  // ✅ null check
                Timestamp = message.Timestamp,
                Alignment = LayoutOptions.Start,
                BackgroundColor = Colors.LightGray
            });
        });
    }
}

public class ChatMessageDisplay
{
    public string SenderName { get; set; } = string.Empty;  // ✅ مقداردهی اولیه
    public string Content { get; set; } = string.Empty;  // ✅ مقداردهی اولیه
    public DateTime Timestamp { get; set; }
    public LayoutOptions Alignment { get; set; }
    public Color BackgroundColor { get; set; } = Colors.LightGray;  // ✅ مقداردهی اولیه
}
