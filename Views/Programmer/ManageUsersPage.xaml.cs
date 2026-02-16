using FoodApp.Database;
using FoodApp.Models;

namespace FoodApp.Views.Programmer;

public partial class ManageUsersPage : ContentPage
{
    private readonly AppDatabase _database;

    public ManageUsersPage()
    {
        InitializeComponent();
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodapp.db");
        _database = new AppDatabase(dbPath);
        LoadUsers();
    }

    private async void LoadUsers()
    {
        var users = await _database.GetUsersAsync();
        UsersCollection.ItemsSource = users;
    }

    private async void OnAddUserClicked(object? sender, EventArgs e)  // ✅ object? شد
    {
        await Navigation.PushAsync(new AddEditUserPage(null!));  // ✅ null! برای رفع warning
    }

    private async void OnEditClicked(object? sender, EventArgs e)  // ✅ object? شد
    {
        var button = sender as Button;
        var user = button?.CommandParameter as FoodApp.Models.User;
        if (user != null)
        {
            await Navigation.PushAsync(new AddEditUserPage(user));
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)  // ✅ object? شد
    {
        var button = sender as Button;
        var user = button?.CommandParameter as FoodApp.Models.User;
        
        if (user != null)
        {
            bool confirm = await DisplayAlertAsync("تأیید", $"حذف {user.Name}؟", "بله", "خیر");  // ✅ DisplayAlertAsync شد
            if (confirm)
            {
                await _database.DeleteUserAsync(user);
                
                // ✅ Broadcast حذف کاربر (با User خالی برای اطلاع‌رسانی)
                await App.SyncService.BroadcastUserAsync(new FoodApp.Models.User 
                { 
                    Id = user.Id, 
                    Username = user.Username,
                    Name = "[DELETED]"
                });
                
                LoadUsers();
            }
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadUsers();
    }
}
