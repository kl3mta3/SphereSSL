namespace SphereSSLv2.Models.ConfigModels
{
    public class StoredConfig
    {
        public string ServerURL {get; set; }
        public int ServerPort { get; set; }
        public string AdminUsername { get; set; }
        public string AdminPassword { get; set; }
        public string DatabasePath { get; set; }
        public string UseLogOn { get; set; }
    }
}
