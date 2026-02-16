using FoodApp.Database;
using FoodApp.Models;

namespace FoodApp.Services
{
    public class MealPlanService
    {
        private readonly AppDatabase _database;

        public MealPlanService(AppDatabase database)
        {
            _database = database;
        }

        public async Task<MealPlan?> GetOrCreateMealPlanAsync(string userId, int week, int year)  // ✅ MealPlan? شد
        {
            var plan = await _database.GetMealPlanAsync(userId, week, year);
            if (plan == null)
            {
                plan = new MealPlan
                {
                    UserId = userId,
                    WeekNumber = week,
                    Year = year
                };
                await _database.SaveMealPlanAsync(plan);
            }
            return plan;
        }

        public async Task<bool> UpdateMealPlanAsync(MealPlan plan, string modifiedBy)
        {
            plan.LastModifiedBy = modifiedBy;
            await _database.UpdateMealPlanAsync(plan);
            return true;
        }

        public async Task<List<MealPlan>> GetAllMealPlansForWeekAsync(int week, int year)
        {
            var allPlans = await _database.GetMealPlansAsync();
            return allPlans.Where(m => m.WeekNumber == week && m.Year == year).ToList();
        }
    }
}
