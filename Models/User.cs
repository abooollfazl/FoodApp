using SQLite;

namespace FoodApp.Models
{
    public enum UserRole
        {
                Programmer,
                        Manager,
                                Normal
                                    }

                                        public class User
                                            {
                                                    [PrimaryKey]
                                                            public string Id { get; set; } = Guid.NewGuid().ToString();
                                                                    
                                                                            public string Username { get; set; }
                                                                                    public string Password { get; set; }
                                                                                            public string Name { get; set; }
                                                                                                    public UserRole Role { get; set; }
                                                                                                            public DateTime CreatedAt { get; set; } = DateTime.Now;
                                                                                                                    public string CreatedBy { get; set; }
                                                                                                                        }
                                                                                                                        }