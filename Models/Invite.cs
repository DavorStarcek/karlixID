using System;
using System.Collections.Generic;

namespace KarlixID.Web.Models;

public partial class Invite
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public Guid? TenantId { get; set; }

    public string? RoleName { get; set; }

    public string Token { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
}
