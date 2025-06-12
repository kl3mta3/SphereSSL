namespace SphereSSLv2.Data
{

    public class DeviceConfig
    {
        public string ServerURL { get; set; }
        public int ServerPort { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public bool UsePassword { get; set; }

    }
}
