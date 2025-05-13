using Nest;

namespace SignalRChat.Models
{
    public class UserConnection
    {
        [PropertyName("username")]
        public string Username { get; set; }
        [PropertyName("room")]
        public string Room { get; set; }
    }
}
