using System;
using System.Collections.Generic;

namespace APBD_Zad6.Models;

public partial class Patient
{
    public string Pesel { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public int Age { get; set; }

    public bool Sex { get; set; }

    public virtual ICollection<Admission> Admissions { get; set; } = new List<Admission>();

    public virtual ICollection<BedAssignment> BedAssignments { get; set; } = new List<BedAssignment>();
}
