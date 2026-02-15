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

                                                                                private async void OnAddUserClicked(object sender, EventArgs e)
                                                                                    {
                                                                                            await Navigation.PushAsync(new AddEditUserPage(null));
                                                                                                }

                                                                                                    private async void OnEditClicked(object sender, EventArgs e)
                                                                                                        {
                                                                                                                var button = sender as Button;
                                                                                                                        var user = button?.CommandParameter as FoodApp.Models.User;
                                                                                                                                if (user != null)
                                                                                                                                        {
                                                                                                                                                    await Navigation.PushAsync(new AddEditUserPage(user));
                                                                                                                                                            }
                                                                                                                                                                }

                                                                                                                                                                    private async void OnDeleteClicked(object sender, EventArgs e)
                                                                                                                                                                        {
                                                                                                                                                                                var button = sender as Button;
                                                                                                                                                                                        var user = button?.CommandParameter as FoodApp.Models.User;
                                                                                                                                                                                                
                                                                                                                                                                                                        if (user != null)
                                                                                                                                                                                                                {
                                                                                                                                                                                                                            bool confirm = await DisplayAlert("تأیید", $"حذف {user.Name}؟", "بله", "خیر");
                                                                                                                                                                                                                                        if (confirm)
                                                                                                                                                                                                                                                    {
                                                                                                                                                                                                                                                                    await _database.DeleteUserAsync(user);
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
