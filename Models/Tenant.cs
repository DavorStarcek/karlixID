using System;
using System.Collections.Generic;

namespace KarlixID.Web.Models;

public partial class Tenant
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Hostname { get; set; } = null!;

    public bool IsActive { get; set; }
}
