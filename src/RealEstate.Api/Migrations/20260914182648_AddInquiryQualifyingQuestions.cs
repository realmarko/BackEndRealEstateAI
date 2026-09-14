using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInquiryQualifyingQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "funding_method",
                table: "inquiries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_agent",
                table: "inquiries",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "timeline",
                table: "inquiries",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "funding_method",
                table: "inquiries");

            migrationBuilder.DropColumn(
                name: "has_agent",
                table: "inquiries");

            migrationBuilder.DropColumn(
                name: "timeline",
                table: "inquiries");
        }
    }
}
