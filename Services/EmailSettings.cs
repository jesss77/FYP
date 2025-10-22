﻿namespace FYP.Services
{
    public class EmailSettings
    {
        public string SenderName { get; set; }
        public string SenderEmail { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool EnableSSL { get; set; }
        public string ConfirmationUrl { get; set; } 
        public string From => SenderEmail;
    }
}
