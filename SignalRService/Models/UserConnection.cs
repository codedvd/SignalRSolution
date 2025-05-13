using Nest;

namespace SignalRChat.Models
{
    public class UserConnection
    {
        [PropertyName("username")]
        public string Username { get; set; } = string.Empty;
        [PropertyName("room")]
        public string Room { get; set; } = string.Empty;
    }
}
