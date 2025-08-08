using Microsoft.AspNetCore.Identity;
using System;

namespace KarlixID.Web.Models
{
    public class ApplicationUser : IdentityUser
    {
        public Guid TenantId { get; set; }
    }
}
