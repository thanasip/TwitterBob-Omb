namespace TwitterBobomb.Models
{
    public class Tweet
    {
        public string? Id { get; set; }

        public string? Text { get; set; }

        public string[]? Edit_History_Tweet_Ids { get; set; }
    }
}
