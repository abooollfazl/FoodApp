using SQLite;
using FoodApp.Models;

namespace FoodApp.Database
{
    public class AppDatabase
        {
                private readonly SQLiteAsyncConnection _database;

                        public AppDatabase(string dbPath)
                                {
                                            _database = new SQLiteAsyncConnection(dbPath);
                                                        _database.CreateTableAsync<User>().Wait();
                                                                    _database.CreateTableAsync<MealPlan>().Wait();
                                                                                _database.CreateTableAsync<ChatMessage>().Wait();
                                                                                        }

                                                                                                public Task<List<User>> GetUsersAsync() => _database.Table<User>().ToListAsync();
                                                                                                        public Task<User> GetUserAsync(string id) => _database.Table<User>().Where(u => u.Id == id).FirstOrDefaultAsync();
                                                                                                                public Task<User> GetUserByUsernameAsync(string username) => _database.Table<User>().Where(u => u.Username == username).FirstOrDefaultAsync();
                                                                                                                        public Task<int> SaveUserAsync(User user) => _database.InsertAsync(user);
                                                                                                                                public Task<int> UpdateUserAsync(User user) => _database.UpdateAsync(user);
                                                                                                                                        public Task<int> DeleteUserAsync(User user) => _database.DeleteAsync(user);

                                                                                                                                                public Task<List<MealPlan>> GetMealPlansAsync() => _database.Table<MealPlan>().ToListAsync();
                                                                                                                                                        public Task<MealPlan> GetMealPlanAsync(string userId, int week, int year) =>
                                                                                                                                                                    _database.Table<MealPlan>().Where(m => m.UserId == userId && m.WeekNumber == week && m.Year == year).FirstOrDefaultAsync();
        // اضافه کردن به AppDatabase.cs
public Task<MealPlan> GetMealPlanByIdAsync(string id) => 
    _database.Table<MealPlan>().Where(m => m.Id == id).FirstOrDefaultAsync();
        
                                                                                                                                                                            public Task<int> SaveMealPlanAsync(MealPlan plan) => _database.InsertAsync(plan);
                                                                                                                                                                                    public Task<int> UpdateMealPlanAsync(MealPlan plan)
                                                                                                                                                                                            {
                                                                                                                                                                                                        plan.LastModified = DateTime.Now;
                                                                                                                                                                                                                    plan.Version++;
                                                                                                                                                                                                                                return _database.UpdateAsync(plan);
                                                                                                                                                                                                                                        }

                                                                                                                                                                                                                                                public Task<List<ChatMessage>> GetChatMessagesAsync() => _database.Table<ChatMessage>().OrderBy(c => c.Timestamp).ToListAsync();
                                                                                                                                                                                                                                                        public Task<int> SaveChatMessageAsync(ChatMessage message) => _database.InsertAsync(message);
                                                                                                                                                                                                                                                                public Task<int> UpdateChatMessageAsync(ChatMessage message) => _database.UpdateAsync(message);
                                                                                                                                                                                                                                                                    }
                                                                                                                                                                                                                                                                    }
