using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdiConsole.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngestedFiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Sha256 = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SnipLevelApplied = table.Column<byte>(type: "tinyint", nullable: false),
                    EdiFabricVersion = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    InterchangeControlNumber = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    UsageIndicator = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    ClaimCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestedFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngestedFileId = table.Column<long>(type: "bigint", nullable: false),
                    TransactionSetControlNumber = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    PatientControlNumber = table.Column<string>(type: "nvarchar(38)", maxLength: 38, nullable: false),
                    TotalCharge = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PlaceOfServiceCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    FrequencyCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    BillingProvider_Npi = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BillingProvider_TaxId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BillingProvider_Address_City = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    BillingProvider_Address_Line1 = table.Column<string>(type: "nvarchar(55)", maxLength: 55, nullable: true),
                    BillingProvider_Address_Line2 = table.Column<string>(type: "nvarchar(55)", maxLength: 55, nullable: true),
                    BillingProvider_Address_PostalCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    BillingProvider_Address_State = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    BillingProvider_Name_FirstName = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: true),
                    BillingProvider_Name_LastOrOrganizationName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    BillingProvider_Name_MiddleName = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    Patient_DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Patient_Gender = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    Patient_RelationshipToSubscriber = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Patient_Name_FirstName = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: true),
                    Patient_Name_LastOrOrganizationName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Patient_Name_MiddleName = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    Payer_Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Payer_PayerId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Subscriber_ClaimFilingIndicator = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Subscriber_DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Subscriber_Gender = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    Subscriber_GroupNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Subscriber_MemberId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Subscriber_PayerResponsibility = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Subscriber_Address_City = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Subscriber_Address_Line1 = table.Column<string>(type: "nvarchar(55)", maxLength: 55, nullable: true),
                    Subscriber_Address_Line2 = table.Column<string>(type: "nvarchar(55)", maxLength: 55, nullable: true),
                    Subscriber_Address_PostalCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Subscriber_Address_State = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Subscriber_Name_FirstName = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: true),
                    Subscriber_Name_LastOrOrganizationName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Subscriber_Name_MiddleName = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Claims_IngestedFiles_IngestedFileId",
                        column: x => x.IngestedFileId,
                        principalTable: "IngestedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationIssues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngestedFileId = table.Column<long>(type: "bigint", nullable: false),
                    TransactionSetControlNumber = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    Segment = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    SegmentPosition = table.Column<int>(type: "int", nullable: true),
                    LoopId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ElementPosition = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationIssues_IngestedFiles_IngestedFileId",
                        column: x => x.IngestedFileId,
                        principalTable: "IngestedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimDiagnoses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<byte>(type: "tinyint", nullable: false),
                    Qualifier = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimDiagnoses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimDiagnoses_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    ProcedureCode = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    Modifier1 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Modifier2 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Modifier3 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Modifier4 = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Charge = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitBasis = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Units = table.Column<decimal>(type: "decimal(15,3)", precision: 15, scale: 3, nullable: false),
                    PlaceOfServiceCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    DiagnosisPointer1 = table.Column<byte>(type: "tinyint", nullable: false),
                    DiagnosisPointer2 = table.Column<byte>(type: "tinyint", nullable: true),
                    DiagnosisPointer3 = table.Column<byte>(type: "tinyint", nullable: true),
                    DiagnosisPointer4 = table.Column<byte>(type: "tinyint", nullable: true),
                    ServiceDateFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ServiceDateTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceLines_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDiagnoses_ClaimId_Sequence",
                table: "ClaimDiagnoses",
                columns: new[] { "ClaimId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_IngestedFileId",
                table: "Claims",
                column: "IngestedFileId");

            migrationBuilder.CreateIndex(
                name: "IX_IngestedFiles_Sha256",
                table: "IngestedFiles",
                column: "Sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLines_ClaimId_LineNumber",
                table: "ServiceLines",
                columns: new[] { "ClaimId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValidationIssues_IngestedFileId",
                table: "ValidationIssues",
                column: "IngestedFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimDiagnoses");

            migrationBuilder.DropTable(
                name: "ServiceLines");

            migrationBuilder.DropTable(
                name: "ValidationIssues");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "IngestedFiles");
        }
    }
}
