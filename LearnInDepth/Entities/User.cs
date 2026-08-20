namespace LearnInDepth.Models
{
    public class User
    {
        public string id { get; set; } = string.Empty; // email, also the partition key
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastLoginAtUtc { get; set; } = DateTime.UtcNow;
    }
}
