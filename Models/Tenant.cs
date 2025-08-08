namespace KarlixID.Web.Models
{
    public class Tenant
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } // Naziv firme
        public string Hostname { get; set; } // firma1.karlix.eu
        public bool IsActive { get; set; } = true;
    }
}
