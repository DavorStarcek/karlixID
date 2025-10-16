using System;

namespace KarlixID.Web.Models.ViewModels
{
    public class TenantUserRow
    {
        public string Id { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? UserName { get; set; }
        public Guid? TenantId { get; set; }
        public string TenantName { get; set; } = "";
        public bool EmailConfirmed { get; set; }
        public bool LockedOut { get; set; }
        public string RolesCsv { get; set; } = "";
    }
}
