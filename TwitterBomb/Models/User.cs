namespace TwitterBobomb.Models
{
    public class User
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Username { get; set; }

        public override string ToString()
        {
            return $"Id: {Id}\nName: {Name}\nUsername: {Username}";
        }
    }
}
