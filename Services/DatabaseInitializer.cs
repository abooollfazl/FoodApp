using FoodApp.Database;
using FoodApp.Models;

namespace FoodApp.Services
{
    public static class DatabaseInitializer
        {
                public static async Task InitializeAsync(AppDatabase database)
                        {
                                    var users = await database.GetUsersAsync();
                                                if (users.Count == 0)
                                                            {
                                                                            var programmer = new User
                                                                                            {
                                                                                                                Username = "admin",
                                                                                                                                    Password = "admin123",
                                                                                                                                                        Name = "برنامه‌نویس اصلی",
                                                                                                                                                                            Role = UserRole.Programmer
                                                                                                                                                                                            };
                                                                                                                                                                                                            
                                                                                                                                                                                                                            await database.SaveUserAsync(programmer);
                                                                                                                                                                                                                                        }
                                                                                                                                                                                                                                                }
                                                                                                                                                                                                                                                    }
                                                                                                                                                                                                                                                    }