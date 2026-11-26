using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectOrganizationLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_OrganizationId",
                table: "projects",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_projects_organizations_OrganizationId",
                table: "projects",
                column: "OrganizationId",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_projects_organizations_OrganizationId",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_projects_OrganizationId",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "projects");
        }
    }
}
