using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Data;

public partial class GsbcDbContext
{
    public void BuildPeopleModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbPerson>()
            .Property(x => x.MediaConsent)
            .HasDefaultValue(nameof(MediaConsent.NotRequested));
        
        //  auto includes \\
        
        modelBuilder.Entity<DbPerson>()
            .Navigation(x => x.SchoolGrade)
            .AutoInclude();
        
        modelBuilder.Entity<DbPerson>()
            .Navigation(x => x.Allergies)
            .AutoInclude();
        
        modelBuilder.Entity<DbAllergy>()
            .Navigation(x => x.Allergen)
            .AutoInclude();
        
        modelBuilder.Entity<DbPerson>()
            .Navigation(x => x.MedicalNotes)
            .AutoInclude();
        
        modelBuilder.Entity<DbMedicalNote>()
            .Navigation(x => x.MedicalType)
            .AutoInclude();
        
        // person indexes \\
        modelBuilder.Entity<DbPerson>()
            .HasIndex(x => x.ElvantoId)
            .IsUnique();
        
        // relationships \\
        modelBuilder.Entity<DbPerson>()
            .HasOne(x => x.SchoolGrade)
            .WithMany()
            .HasForeignKey(x => x.SchoolGradeId);
        
        modelBuilder.Entity<DbPerson>()
            .HasMany(x => x.Allergies)
            .WithOne(x => x.Person)
            .HasForeignKey(x => x.PersonId);

        modelBuilder.Entity<DbAllergy>()
            .HasOne(x => x.Allergen)
            .WithMany()
            .HasForeignKey(x => x.AllergenId);
        
        modelBuilder.Entity<DbPerson>()
            .HasMany(x => x.MedicalNotes)
            .WithOne(x => x.Person)
            .HasForeignKey(x => x.PersonId);

        modelBuilder.Entity<DbMedicalNote>()
            .HasOne(x => x.MedicalType)
            .WithMany()
            .HasForeignKey(x => x.MedicalTypeId);
    }
}