using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveLite.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccrualPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LeaveType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EmploymentType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AccrualPeriod = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    HoursPerPeriod = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AnnualCapHours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    CarryOverCapHours = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    MinTenureMonths = table.Column<int>(type: "INTEGER", nullable: false),
                    GrantsBalanceUpfront = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccrualPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    EmploymentType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TeamRole = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    HiredOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AccrualPolicyId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HolidayCalendars",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Holidays = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LeaveType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DecidedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DenialReason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccrualPolicies_Name",
                table: "AccrualPolicies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Email",
                table: "Employees",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TeamId",
                table: "Employees",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayCalendars_Year",
                table: "HolidayCalendars",
                column: "Year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId",
                table: "LeaveRequests",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_Status",
                table: "LeaveRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccrualPolicies");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "HolidayCalendars");

            migrationBuilder.DropTable(
                name: "LeaveRequests");
        }
    }
}
