namespace SignalRService.Models
{
    public class WhitelistedIP
    {
        public int Id { get; set; }
        public string IPAddress { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}
