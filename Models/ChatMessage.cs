using SQLite;

namespace FoodApp.Models
{
    public class ChatMessage
        {
                [PrimaryKey]
                        public string Id { get; set; } = Guid.NewGuid().ToString();
                                
                                        public string SenderId { get; set; }
                                                public string SenderName { get; set; }
                                                        public string ReceiverId { get; set; }
                                                                public string Content { get; set; }
                                                                        public DateTime Timestamp { get; set; } = DateTime.Now;
                                                                                public bool IsRead { get; set; } = false;
                                                                                        public long Version { get; set; } = 1;
                                                                                                public bool IsSynced { get; set; } = false;
                                                                                                    }
                                                                                                    }