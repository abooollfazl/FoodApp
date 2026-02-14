using FoodApp.Database;
using FoodApp.Models;

namespace FoodApp.Services
{
    public class AuthService
        {
                private readonly AppDatabase _database;
                        private static User _currentUser;

                                public AuthService(AppDatabase database)
                                        {
                                                    _database = database;
                                                            }

                                                                    public async Task<bool> LoginAsync(string username, string password)
                                                                            {
                                                                                        var user = await _database.GetUserByUsernameAsync(username);
                                                                                                    if (user != null && user.Password == password)
                                                                                                                {
                                                                                                                                _currentUser = user;
                                                                                                                                                return true;
                                                                                                                                                            }
                                                                                                                                                                        return false;
                                                                                                                                                                                }

                                                                                                                                                                                        public void Logout() => _currentUser = null;
                                                                                                                                                                                                public static User GetCurrentUser() => _currentUser;
                                                                                                                                                                                                        public static bool IsLoggedIn() => _currentUser != null;
                                                                                                                                                                                                                public static bool IsProgrammer() => _currentUser?.Role == UserRole.Programmer;
                                                                                                                                                                                                                        public static bool IsManager() => _currentUser?.Role == UserRole.Manager;
                                                                                                                                                                                                                                public static bool IsNormalUser() => _currentUser?.Role == UserRole.Normal;
                                                                                                                                                                                                                                    }
                                                                                                                                                                                                                                    }