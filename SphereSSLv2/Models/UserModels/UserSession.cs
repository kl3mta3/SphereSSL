namespace SphereSSLv2.Models.UserModels
{
    public class UserSession
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string Role { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsAdmin { get; set; }

    }
}
