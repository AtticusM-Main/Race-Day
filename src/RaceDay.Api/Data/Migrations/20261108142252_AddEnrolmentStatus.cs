using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceDay.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrolmentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Enrolment",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: false,
                defaultValue: "Confirmed");

            migrationBuilder.UpdateData(
                table: "Enrolment",
                keyColumn: "EnrolmentId",
                keyValue: 1,
                column: "Status",
                value: "Confirmed");

            migrationBuilder.UpdateData(
                table: "Enrolment",
                keyColumn: "EnrolmentId",
                keyValue: 2,
                column: "Status",
                value: "Confirmed");

            migrationBuilder.UpdateData(
                table: "Enrolment",
                keyColumn: "EnrolmentId",
                keyValue: 3,
                column: "Status",
                value: "Confirmed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Enrolment");
        }
    }
}
