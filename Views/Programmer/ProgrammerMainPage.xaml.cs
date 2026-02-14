using FoodApp.Services;

namespace FoodApp.Views.Programmer;

public partial class ProgrammerMainPage : ContentPage
{
    public ProgrammerMainPage()
        {
                InitializeComponent();
                        var user = AuthService.GetCurrentUser();
                                WelcomeLabel.Text = $"برنامه‌نویس: {user?.Name}";
                                    }

                                        private async void OnManageUsersClicked(object sender, EventArgs e)
                                            {
                                                    await Navigation.PushAsync(new ManageUsersPage());
                                                        }

                                                            private async void OnViewAllPlansClicked(object sender, EventArgs e)
                                                                {
                                                                        await Navigation.PushAsync(new Views.Manager.ManagerMainPage());
                                                                            }

                                                                                private async void OnMyPlanClicked(object sender, EventArgs e)
                                                                                    {
                                                                                            await Navigation.PushAsync(new Views.User.UserMainPage());
                                                                                                }

                                                                                                    private async void OnGoToChatClicked(object sender, EventArgs e)
                                                                                                        {
                                                                                                                await Navigation.PushAsync(new Views.Chat.ChatPage());
                                                                                                                    }
                                                                                                                    }