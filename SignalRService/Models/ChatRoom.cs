using Nest;

namespace SignalRChat.Models
{
    public class ChatRoom
    {
        [PropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [PropertyName("roomName")]
        public string RoomName { get; set; } = string.Empty;
    }
}
