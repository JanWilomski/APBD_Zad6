using System;
using System.Collections.Generic;

namespace APBD_Zad6.Models;

public partial class Ward
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public virtual ICollection<Admission> Admissions { get; set; } = new List<Admission>();

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
}
