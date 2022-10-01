namespace TwitterBobomb.Models
{
    public class TweetDeletedEventArgs : EventArgs
    {
        public string? Id { get; set; }

        public TweetDeletedEventArgs(string? id)
        {
            Id = id;
        }
    }
}
