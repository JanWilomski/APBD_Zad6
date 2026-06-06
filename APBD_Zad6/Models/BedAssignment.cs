using System;
using System.Collections.Generic;

namespace APBD_Zad6.Models;

public partial class BedAssignment
{
    public int Id { get; set; }

    public string PatientPesel { get; set; } = null!;

    public int BedId { get; set; }

    public DateTime From { get; set; }

    public DateTime? To { get; set; }

    public virtual Bed Bed { get; set; } = null!;

    public virtual Patient PatientPeselNavigation { get; set; } = null!;
}
