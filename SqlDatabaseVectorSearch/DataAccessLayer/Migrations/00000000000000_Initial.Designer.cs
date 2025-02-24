﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SqlDatabaseVectorSearch.DataAccessLayer;

#nullable disable

namespace SqlDatabaseVectorSearch.DataAccessLayer.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20250224102351_Initial")]
    partial class Initial
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("SqlDatabaseVectorSearch.DataAccessLayer.Entities.Document", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreationDate")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)");

                    b.HasKey("Id");

                    b.ToTable("Documents", (string)null);
                });

            modelBuilder.Entity("SqlDatabaseVectorSearch.DataAccessLayer.Entities.DocumentChunk", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("DocumentId")
                        .HasColumnType("uniqueidentifier");

                    b.PrimitiveCollection<string>("Embedding")
                        .IsRequired()
                        .HasColumnType("vector(1536)");

                    b.Property<int>("Index")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("DocumentId");

                    b.ToTable("DocumentChunks", (string)null);
                });

            modelBuilder.Entity("SqlDatabaseVectorSearch.DataAccessLayer.Entities.DocumentChunk", b =>
                {
                    b.HasOne("SqlDatabaseVectorSearch.DataAccessLayer.Entities.Document", "Document")
                        .WithMany("Chunks")
                        .HasForeignKey("DocumentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("FK_DocumentChunks_Documents");

                    b.Navigation("Document");
                });

            modelBuilder.Entity("SqlDatabaseVectorSearch.DataAccessLayer.Entities.Document", b =>
                {
                    b.Navigation("Chunks");
                });
#pragma warning restore 612, 618
        }
    }
}
