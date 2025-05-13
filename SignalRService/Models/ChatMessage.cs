using Nest;
using SignalRService.Enums;

namespace SignalRService.Models
{
    public class ChatMessage
    {
        [PropertyName("id")]
        public string Id { get; set; }

        [PropertyName("username")]
        public string Username { get; set; }

        [PropertyName("room")]
        public string Room { get; set; }

        [PropertyName("content")]
        public string Content { get; set; }

        [PropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [PropertyName("messageType")]
        public MessageType MessageType { get; set; }
    }
}
