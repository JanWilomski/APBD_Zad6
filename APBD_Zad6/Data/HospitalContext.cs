using System;
using System.Collections.Generic;
using APBD_Zad6.Models;
using Microsoft.EntityFrameworkCore;

namespace APBD_Zad6.Data;

public partial class HospitalContext : DbContext
{
    public HospitalContext(DbContextOptions<HospitalContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Admission> Admissions { get; set; } 

    public virtual DbSet<Bed> Beds { get; set; }

    public virtual DbSet<BedAssignment> BedAssignments { get; set; }

    public virtual DbSet<BedType> BedTypes { get; set; }

    public virtual DbSet<Patient> Patients { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<Ward> Wards { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Admission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Admissions_pk");

            entity.Property(e => e.AdmissionDate).HasColumnType("datetime");
            entity.Property(e => e.DischargeDate).HasColumnType("datetime");
            entity.Property(e => e.PatientPesel)
                .HasMaxLength(11)
                .IsUnicode(false)
                .IsFixedLength();

            entity.HasOne(d => d.PatientPeselNavigation).WithMany(p => p.Admissions)
                .HasForeignKey(d => d.PatientPesel)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Admissions_Patients");

            entity.HasOne(d => d.Ward).WithMany(p => p.Admissions)
                .HasForeignKey(d => d.WardId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Admissions_Wards");
        });

        modelBuilder.Entity<Bed>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Beds_pk");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.RoomId)
                .HasMaxLength(4)
                .IsUnicode(false);

            entity.HasOne(d => d.BedType).WithMany(p => p.Beds)
                .HasForeignKey(d => d.BedTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Beds_BedTypes");

            entity.HasOne(d => d.Room).WithMany(p => p.Beds)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Beds_Rooms");
        });

        modelBuilder.Entity<BedAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("BedAssignments_pk");

            entity.Property(e => e.From).HasColumnType("datetime");
            entity.Property(e => e.PatientPesel)
                .HasMaxLength(11)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.To).HasColumnType("datetime");

            entity.HasOne(d => d.Bed).WithMany(p => p.BedAssignments)
                .HasForeignKey(d => d.BedId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("BedAssignments_Beds");

            entity.HasOne(d => d.PatientPeselNavigation).WithMany(p => p.BedAssignments)
                .HasForeignKey(d => d.PatientPesel)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("BedAssignments_Patients");
        });

        modelBuilder.Entity<BedType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("BedTypes_pk");

            entity.Property(e => e.Name).HasMaxLength(300);
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(e => e.Pesel).HasName("Patients_pk");

            entity.Property(e => e.Pesel)
                .HasMaxLength(11)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(100);
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Rooms_pk");

            entity.Property(e => e.Id)
                .HasMaxLength(4)
                .IsUnicode(false);

            entity.HasOne(d => d.Ward).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.WardId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Room_Ward");
        });

        modelBuilder.Entity<Ward>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Wards_pk");

            entity.Property(e => e.Name).HasMaxLength(300);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
