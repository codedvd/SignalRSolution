﻿namespace SignalRService.Models
{
    public class WhitelistedIP
    {
        public int Id { get; set; }
        public string IPAddress { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}
