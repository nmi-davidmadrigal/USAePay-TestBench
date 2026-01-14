using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using UsaepaySupportTestbench.Models;
using UsaepaySupportTestbench.Data;

#nullable disable

namespace UsaepaySupportTestbench.Migrations;

[DbContext(typeof(ApplicationDbContext))]
partial class ApplicationDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        #pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.12");

        modelBuilder.Entity("UsaepaySupportTestbench.Models.Preset", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("TEXT");

            b.Property<ApiType>("ApiType")
                .HasColumnType("TEXT")
                .HasConversion<string>();

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<EnvironmentType>("Environment")
                .HasColumnType("TEXT")
                .HasConversion<string>();

            b.Property<string>("HeadersJson")
                .HasColumnType("TEXT");

            b.Property<bool>("IsQuickPreset")
                .HasColumnType("INTEGER");

            b.Property<bool>("IsSystemPreset")
                .HasColumnType("INTEGER");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("TEXT");

            b.Property<string>("Notes")
                .HasColumnType("TEXT");

            b.Property<string>("RestMethod")
                .HasMaxLength(20)
                .HasColumnType("TEXT");

            b.Property<string>("RestPathOrEndpoint")
                .HasMaxLength(400)
                .HasColumnType("TEXT");

            b.Property<string>("SoapAction")
                .HasMaxLength(200)
                .HasColumnType("TEXT");

            b.Property<string>("BodyTemplate")
                .HasColumnType("TEXT");

            b.Property<string>("TagsJson")
                .HasColumnType("TEXT");

            b.Property<DateTime>("UpdatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("VariablesJson")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("Presets");
        });

        modelBuilder.Entity("UsaepaySupportTestbench.Models.ScenarioRun", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("TEXT");

            b.Property<ApiType>("ApiType")
                .HasColumnType("TEXT")
                .HasConversion<string>();

            b.Property<string>("CorrelationId")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<EnvironmentType>("Environment")
                .HasColumnType("TEXT")
                .HasConversion<string>();

            b.Property<int?>("HttpStatus")
                .HasColumnType("INTEGER");

            b.Property<long>("LatencyMs")
                .HasColumnType("INTEGER");

            b.Property<Guid?>("PresetId")
                .HasColumnType("TEXT");

            b.Property<string>("RequestRedacted")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<string>("ResponseRedacted")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<bool?>("SoapFault")
                .HasColumnType("INTEGER");

            b.Property<string>("TicketNumber")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("CreatedAt");

            b.HasIndex("PresetId");

            b.ToTable("ScenarioRuns");
        });

        modelBuilder.Entity("UsaepaySupportTestbench.Models.ScenarioRun", b =>
        {
            b.HasOne("UsaepaySupportTestbench.Models.Preset", "Preset")
                .WithMany()
                .HasForeignKey("PresetId")
                .OnDelete(DeleteBehavior.SetNull);

            b.Navigation("Preset");
        });
        #pragma warning restore 612, 618
    }
}
