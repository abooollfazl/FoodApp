using FoodApp.Database;
using FoodApp.Services;

namespace FoodApp.Views.Auth;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _authService;

        public LoginPage()
            {
                    InitializeComponent();
                            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodapp.db");
                                    var database = new AppDatabase(dbPath);
                                            _authService = new AuthService(database);
                                                    _ = DatabaseInitializer.InitializeAsync(database);
                                                        }

                                                            private async void OnLoginClicked(object sender, EventArgs e)
                                                                {
                                                                        var success = await _authService.LoginAsync(UsernameEntry.Text, PasswordEntry.Text);
                                                                                
                                                                                        if (success)
                                                                                                {
                                                                                                            var user = AuthService.GetCurrentUser();
                                                                                                                        
                                                                                                                                    if (AuthService.IsProgrammer())
                                                                                                                                                {
                                                                                                                                                                await Navigation.PushAsync(new Views.Programmer.ProgrammerMainPage());
                                                                                                                                                                            }
                                                                                                                                                                                        else if (AuthService.IsManager())
                                                                                                                                                                                                    {
                                                                                                                                                                                                                    await Navigation.PushAsync(new Views.Manager.ManagerMainPage());
                                                                                                                                                                                                                                }
                                                                                                                                                                                                                                            else
                                                                                                                                                                                                                                                        {
                                                                                                                                                                                                                                                                        await Navigation.PushAsync(new Views.User.UserMainPage());
                                                                                                                                                                                                                                                                                    }
                                                                                                                                                                                                                                                                                            }
                                                                                                                                                                                                                                                                                                    else
                                                                                                                                                                                                                                                                                                            {
                                                                                                                                                                                                                                                                                                                        ErrorLabel.Text = "نام کاربری یا رمز عبور اشتباه است";
                                                                                                                                                                                                                                                                                                                                    ErrorLabel.IsVisible = true;
                                                                                                                                                                                                                                                                                                                                            }
                                                                                                                                                                                                                                                                                                                                                }
                                                                                                                                                                                                                                                                                                                                                }