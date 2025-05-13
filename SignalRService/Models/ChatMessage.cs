using Nest;
using SignalRService.Enums;

namespace SignalRService.Models
{
    public class ChatMessage
    {
        [PropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [PropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [PropertyName("room")]
        public string Room { get; set; } = string.Empty;

        [PropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [PropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [PropertyName("messageType")]
        public MessageType MessageType { get; set; }
    }
}
