using Nest;

namespace SignalRChat.Models
{
    public class ChatRoom
    {
        [PropertyName("id")]
        public string Id { get; set; }
        [PropertyName("roomName")]
        public string RoomName { get; set; }
    }
}
