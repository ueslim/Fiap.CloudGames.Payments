﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FIAP.CloudGames.Payment.Infra.Data.Migrations.EventStore
{
    /// <inheritdoc />
    public partial class InitialEventStoreMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payment");

            migrationBuilder.CreateTable(
                name: "StoredEvents",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<string>(type: "varchar(100)", nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredEvents", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoredEvents",
                schema: "payment");
        }
    }
}
