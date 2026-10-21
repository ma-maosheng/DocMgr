using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AerialPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SurveyArea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Scale = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhotographyDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BoxContents = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhotoCount = table.Column<int>(type: "int", nullable: false),
                    Registrant = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegistrationDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Modifier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastStorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AerialPhotos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalWorkflowRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ConditionLogic = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Condition1FieldKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Condition1Value = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Condition2FieldKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Condition2Value = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EnableDeptHead = table.Column<bool>(type: "bit", nullable: false),
                    EnableArchiveRoomHead = table.Column<bool>(type: "bit", nullable: false),
                    EnableProductionHead = table.Column<bool>(type: "bit", nullable: false),
                    EnableArchiveDeputyPresident = table.Column<bool>(type: "bit", nullable: false),
                    EnableProductionVicePresident = table.Column<bool>(type: "bit", nullable: false),
                    DefaultDeptHeadUserId = table.Column<int>(type: "int", nullable: true),
                    DefaultArchiveRoomHeadUserId = table.Column<int>(type: "int", nullable: true),
                    DefaultProductionHeadUserId = table.Column<int>(type: "int", nullable: true),
                    DefaultArchiveDeputyPresidentUserId = table.Column<int>(type: "int", nullable: true),
                    DefaultProductionVicePresidentUserId = table.Column<int>(type: "int", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalWorkflowRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchiveBoxSpecifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WidthCm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HeightCm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ThicknessCm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveBoxSpecifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessLogicSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationOverdueSetting = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessLogicSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cabinets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Width = table.Column<double>(type: "float", nullable: false),
                    Height = table.Column<double>(type: "float", nullable: false),
                    Depth = table.Column<double>(type: "float", nullable: false),
                    CanvasLeft = table.Column<double>(type: "float", nullable: false),
                    CanvasTop = table.Column<double>(type: "float", nullable: false),
                    FaceCount = table.Column<int>(type: "int", nullable: false),
                    LayerCount = table.Column<int>(type: "int", nullable: false),
                    ColumnCount = table.Column<int>(type: "int", nullable: false),
                    RotationAngle = table.Column<double>(type: "float", nullable: false),
                    HardDiskSlotCapacity = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    OpticalDiscSlotCapacity = table.Column<int>(type: "int", nullable: false, defaultValue: 20)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cabinets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CabinetSlotSpecialRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CabinetName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OpenFaceCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SlotCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiredBoxSpecification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequiredArchiveFaceCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LayoutModeOverride = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SpecialRuleText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabinetSlotSpecialRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CabinetSlotSpecifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CabinetTypeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WidthCm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    HeightCm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DepthCm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabinetSlotSpecifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DbOperationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourcePage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceButton = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EntityKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChangedColumns = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbOperationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FieldDomainDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDomainEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldDomainDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HardDiskDisposalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisposalNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DisposalReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DispositionMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherRemark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantUserId = table.Column<int>(type: "int", nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantDept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveRoomHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveDeputyPresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveDeputyPresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionVicePresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionVicePresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "bit", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DiskPhotoUploaded = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardDiskDisposalRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HardDiskInventoryRegisterRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegisterNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RegisterKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantUserId = table.Column<int>(type: "int", nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantDept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveRoomHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveDeputyPresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveDeputyPresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionVicePresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionVicePresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "bit", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DamagedDiskRelocationConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    DamagedDiskRelocationConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DamagedDiskRelocationConfirmedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardDiskInventoryRegisterRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HardDiskMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiskCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DiskType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Capacity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InterfaceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegisterPerson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FactoryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegistrationMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardDiskMedia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoryArchiveBoxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BoxCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BoxSpecification = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PlacementMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastStorageLocation = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArchivedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchivedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryArchiveBoxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoryArchiveDisposalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisposalNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MaterialKind = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DispositionMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransferTarget = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherRemark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantUserId = table.Column<int>(type: "int", nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantDept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveRoomHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveDeputyPresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveDeputyPresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionVicePresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionVicePresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "bit", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScenePhotoUploaded = table.Column<bool>(type: "bit", nullable: false),
                    PhysicalRemovalConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PhysicalRemovalConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PhysicalRemovalConfirmedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryArchiveDisposalRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NetworkArchiveBusinessChains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChainNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ScenarioKind = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PrimaryBusinessType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PrimaryBusinessId = table.Column<int>(type: "int", nullable: false),
                    StatusSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkArchiveBusinessChains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NetworkOnNetAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AssetKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Year = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServerPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfidentialLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataSizeText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VersionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginKind = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OriginInboundItemId = table.Column<int>(type: "int", nullable: true),
                    OriginOutboundItemId = table.Column<int>(type: "int", nullable: true),
                    ParentAssetId = table.Column<int>(type: "int", nullable: true),
                    SourceFilingFactId = table.Column<int>(type: "int", nullable: true),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegisteredBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkOnNetAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NetworkOnNetDisposalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisposalNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DisposalReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DispositionMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantUserId = table.Column<int>(type: "int", nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantDept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveRoomHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveDeputyPresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveDeputyPresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionVicePresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionVicePresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "bit", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkOnNetDisposalRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpticalDiscMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiscCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DiscType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Capacity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegisterPerson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegistrationMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceRecordKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpticalDiscMedia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OtherMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SequenceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaterialCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartYear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EndYear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Scale = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MapName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Registrant = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegistrationDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Modifier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastStorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoryMiscMaterials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MajorCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SubCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContainerKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContainerCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StorageLocation = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Registrant = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Modifier = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LastStorageLocation = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryMiscMaterials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoryMiscMaterialLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    LineText = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryMiscMaterialLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoryMiscMaterialLines_HistoryMiscMaterials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "HistoryMiscMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProjectCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ImplementYear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CapitalMgrDept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerPathSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PathName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PhysicalPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CapacityTb = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerPathSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BusinessNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BusinessId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    FileCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploaderName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToDoReadStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ToDoId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToDoReadStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TopoMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Scale = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MapNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentMapNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MapName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SheetCount = table.Column<int>(type: "int", nullable: false),
                    CreationDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SurveyDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CoordinateSystem = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ElevationDatum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Region = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Registrant = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegistrationDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Modifier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificationDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastStorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopoMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EnableToDoPopup = table.Column<bool>(type: "bit", nullable: false),
                    EnableToDoBadge = table.Column<bool>(type: "bit", nullable: false),
                    ToDoRefreshSeconds = table.Column<int>(type: "int", nullable: false),
                    ToDoTopN = table.Column<int>(type: "int", nullable: false),
                    MarkAllAsReadOnAcknowledge = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoginName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RealName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockoutUntil = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveBoxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArchiveSequenceNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BoxLocationCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Year = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Specs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlacementMode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchivedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchivedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContainerLifecycleStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastStorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveBoxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveDisposalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisposalNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MediaKind = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DisposalReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DispositionMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantUserId = table.Column<int>(type: "int", nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantDept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveRoomHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveDeputyPresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveDeputyPresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionVicePresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionVicePresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "bit", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScenePhotoUploaded = table.Column<bool>(type: "bit", nullable: false),
                    PhysicalRemovalConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PhysicalRemovalConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PhysicalRemovalConfirmedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FormatRetainedConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    FormatRetainedConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FormatRetainedConfirmedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveDisposalRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveFilingFacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FilingFactNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MediaKind = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    RegisterRecordId = table.Column<int>(type: "int", nullable: false),
                    RegisterMediaId = table.Column<int>(type: "int", nullable: false),
                    MediaItemId = table.Column<int>(type: "int", nullable: false),
                    FormNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ProvideUnit = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ConfidentialLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentCount = table.Column<int>(type: "int", nullable: false),
                    InventoryLostCopyCount = table.Column<int>(type: "int", nullable: false),
                    InventoryScrapCopyCount = table.Column<int>(type: "int", nullable: false),
                    ContainerKind = table.Column<int>(type: "int", nullable: false),
                    ContainerId = table.Column<int>(type: "int", nullable: false),
                    ContainerCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StorageLocation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CabinetName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BoxLocationCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BoxSpecs = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StorageCarrierType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Disposition = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MediumCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FilingStoragePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DataSizeMb = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FiledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FiledBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceLinkType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceLinkId = table.Column<int>(type: "int", nullable: false),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CurrentContainerCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CurrentStorageLocation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LifecycleUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LifecycleRemark = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    BorrowHintLevel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BorrowHintText = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    BorrowHintUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PrimaryFilingFactId = table.Column<int>(type: "int", nullable: true),
                    ArchiveCopyRole = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveFilingFacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveInventoryRegisterRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegisterNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MediaKind = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RegisterKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantUserId = table.Column<int>(type: "int", nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantDept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveRoomHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveDeputyPresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveDeputyPresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionVicePresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionVicePresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "bit", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveInventoryRegisterRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveMaterialTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FilingFactId = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BusinessNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    DedupKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BeforeLifecycleStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AfterLifecycleStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BeforeContainerCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AfterContainerCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BeforeStorageLocation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AfterStorageLocation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    OperatorName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OperatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveMaterialTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveOutboundRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OutboundNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ArchiveYear = table.Column<int>(type: "int", nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ApplicantUserId = table.Column<int>(type: "int", nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApplicantDept = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ApplyDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DestinationKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalUnit = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SelfRetainDisposition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProofMaterialNote = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaterialSummary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceResultSetId = table.Column<int>(type: "int", nullable: true),
                    SourceResultSetNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedUploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ForceVoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ForceVoidReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ForceVoidKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ApprovalDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OverdueRemindedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandoverRemark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhysicallyCompletedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHeadOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveRoomHeadOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveRoomHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionHeadOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VicePresidentOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveDeputyPresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveDeputyPresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionVicePresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionVicePresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveOutboundRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRelocationRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RelocationNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MediaKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RelocationMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceContainerId = table.Column<int>(type: "int", nullable: false),
                    SourceContainerCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceStorageLocation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TargetContainerId = table.Column<int>(type: "int", nullable: true),
                    TargetContainerCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetStorageLocation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceMediumDisposition = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OperatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OperatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PreviewReport = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRelocationRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveReturnRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceOutboundRecordId = table.Column<int>(type: "int", nullable: false),
                    SourceOutboundNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArchiveYear = table.Column<int>(type: "int", nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    BorrowerName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BorrowerDept = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "int", nullable: false),
                    RegisteredByName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RegisteredByDept = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    LossDescription = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    HandlerName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DeptHead = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DeptHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveRoomHead = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalOpinion = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ProductionHead = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProductionHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveDeputyPresident = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArchiveDeputyPresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionVicePresident = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProductionVicePresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandoverApplicant = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HandoverAdmin = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    HandoverDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "bit", nullable: false),
                    SignedAttachmentUploader = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedUploadedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ForceVoidReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ForceVoidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveReturnRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveSearchResultSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResultSetNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MediaKind = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SearchCriteriaJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveSearchResultSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyElectronicArchiveUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ElectronicArchiveNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Year = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StorageCarrierType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinkedMediumCodes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Disposition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MediaCount = table.Column<int>(type: "int", nullable: false),
                    ContentSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchivedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchivedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceRecordKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitLifecycleStatus = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyElectronicArchiveUnits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CabinetArchiveSlotCategoryAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CabinetId = table.Column<int>(type: "int", nullable: false),
                    FaceCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SlotCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabinetArchiveSlotCategoryAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CabinetArchiveSlotCategoryAssignments_Cabinets_CabinetId",
                        column: x => x.CabinetId,
                        principalTable: "Cabinets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CabinetHardDiskSlotCategoryAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CabinetId = table.Column<int>(type: "int", nullable: false),
                    FaceCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SlotCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabinetHardDiskSlotCategoryAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CabinetHardDiskSlotCategoryAssignments_Cabinets_CabinetId",
                        column: x => x.CabinetId,
                        principalTable: "Cabinets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldDomainOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FieldDomainDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OptionValue = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OptionLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldDomainOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldDomainOptions_FieldDomainDefinitions_FieldDomainDefinitionId",
                        column: x => x.FieldDomainDefinitionId,
                        principalTable: "FieldDomainDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HardDiskDisposalItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisposalRecordId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    MediumId = table.Column<int>(type: "int", nullable: false),
                    DiskCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeMediaStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeStorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeMediaNature = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisposalReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DispositionMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardDiskDisposalItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HardDiskDisposalItems_HardDiskDisposalRecords_DisposalRecordId",
                        column: x => x.DisposalRecordId,
                        principalTable: "HardDiskDisposalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HardDiskDisposalItems_HardDiskMedia_MediumId",
                        column: x => x.MediumId,
                        principalTable: "HardDiskMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HardDiskInventoryRegisterItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegisterRecordId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    MediumId = table.Column<int>(type: "int", nullable: false),
                    DiskCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeMediaStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeStorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeMediaNature = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetStorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardDiskInventoryRegisterItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HardDiskInventoryRegisterItems_HardDiskInventoryRegisterRecords_RegisterRecordId",
                        column: x => x.RegisterRecordId,
                        principalTable: "HardDiskInventoryRegisterRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HardDiskInventoryRegisterItems_HardDiskMedia_MediumId",
                        column: x => x.MediumId,
                        principalTable: "HardDiskMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HardDiskLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MediumId = table.Column<int>(type: "int", nullable: false),
                    DiskCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MediaStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MediaNature = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HolderOrOrganization = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NeedReturn = table.Column<bool>(type: "bit", nullable: false),
                    RegisterPerson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardDiskLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HardDiskLedgers_HardDiskMedia_MediumId",
                        column: x => x.MediumId,
                        principalTable: "HardDiskMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HardDiskMediaApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MediumId = table.Column<int>(type: "int", nullable: false),
                    SourceApplicationId = table.Column<int>(type: "int", nullable: true),
                    SourceOutboundRecordId = table.Column<int>(type: "int", nullable: true),
                    SourceNetworkOutboundRecordId = table.Column<int>(type: "int", nullable: true),
                    ApplicationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicationStatus = table.Column<int>(type: "int", nullable: false),
                    ApplicantUserId = table.Column<int>(type: "int", nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantDept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProofMaterialNote = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetPersonOrUnit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DestinationKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RelatedBatch = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RelatedArchiveTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    PrintedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "bit", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveRoomHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveDeputyPresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveDeputyPresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionVicePresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionVicePresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InspectionResult = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FormatConfirmation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardDiskMediaApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HardDiskMediaApplications_HardDiskMedia_MediumId",
                        column: x => x.MediumId,
                        principalTable: "HardDiskMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HardDiskRegisterLocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MediumId = table.Column<int>(type: "int", nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BusinessRecordId = table.Column<int>(type: "int", nullable: true),
                    BusinessNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LockedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardDiskRegisterLocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HardDiskRegisterLocks_HardDiskMedia_MediumId",
                        column: x => x.MediumId,
                        principalTable: "HardDiskMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistoryArchiveBoxLedgerLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HistoryArchiveBoxId = table.Column<int>(type: "int", nullable: false),
                    MaterialKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RecordId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryArchiveBoxLedgerLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoryArchiveBoxLedgerLinks_HistoryArchiveBoxes_HistoryArchiveBoxId",
                        column: x => x.HistoryArchiveBoxId,
                        principalTable: "HistoryArchiveBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistoryArchiveDisposalItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisposalRecordId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    BoxCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BoxSpecification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CabinetName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FaceCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SlotCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LedgerRecordCount = table.Column<int>(type: "int", nullable: false),
                    SourceRecordKeys = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsMixedPlacement = table.Column<bool>(type: "bit", nullable: false),
                    RelatedBoxCodes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryArchiveDisposalItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoryArchiveDisposalItems_HistoryArchiveDisposalRecords_DisposalRecordId",
                        column: x => x.DisposalRecordId,
                        principalTable: "HistoryArchiveDisposalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkArchiveBusinessTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessChainId = table.Column<int>(type: "int", nullable: false),
                    TaskKind = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BusinessId = table.Column<int>(type: "int", nullable: true),
                    BusinessNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DedupKey = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    ResultMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkArchiveBusinessTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkArchiveBusinessTasks_NetworkArchiveBusinessChains_BusinessChainId",
                        column: x => x.BusinessChainId,
                        principalTable: "NetworkArchiveBusinessChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkInboundRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InboundNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProvideUnit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetServerPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaterialPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Year = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherRequests = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProofMaterialNote = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReturnBorrowedHardDiskWithInbound = table.Column<bool>(type: "bit", nullable: false),
                    SourceResultSetId = table.Column<int>(type: "int", nullable: true),
                    SourceResultSetNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BusinessChainId = table.Column<int>(type: "int", nullable: true),
                    ApplicantUserId = table.Column<int>(type: "int", nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantDept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProductionHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveRoomHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveDeputyPresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveDeputyPresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionVicePresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionVicePresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Deliverer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliverDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Administrator = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdminDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeptHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "bit", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandoverConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkInboundRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkInboundRecords_NetworkArchiveBusinessChains_BusinessChainId",
                        column: x => x.BusinessChainId,
                        principalTable: "NetworkArchiveBusinessChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NetworkOutboundRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OutboundNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DestinationKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DestinationUnit = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServerPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaterialPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchivePurpose = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Year = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherRequests = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProofMaterialNote = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetRegisterRecordId = table.Column<int>(type: "int", nullable: true),
                    TargetRegisterFormNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BusinessChainId = table.Column<int>(type: "int", nullable: true),
                    ApplicantUserId = table.Column<int>(type: "int", nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantDept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProductionHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveRoomHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchiveDeputyPresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveDeputyPresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionVicePresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionVicePresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Deliverer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliverDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Administrator = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdminDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeptHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "bit", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandoverConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkOutboundRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkOutboundRecords_NetworkArchiveBusinessChains_BusinessChainId",
                        column: x => x.BusinessChainId,
                        principalTable: "NetworkArchiveBusinessChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRegisterRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceNetworkOutboundRecordId = table.Column<int>(type: "int", nullable: true),
                    SourceNetworkOutboundNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BusinessChainId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchivedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SimulatedArchiveStatus = table.Column<int>(type: "int", nullable: false),
                    ElectronicArchiveStatus = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    ProjectName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaterialName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchivePurpose = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProofMaterialNote = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OtherRequests = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantUserId = table.Column<int>(type: "int", nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantDept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProdDeptOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RndDeptOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveRoomHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeputyOpinion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveDeputyPresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArchiveDeputyPresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProductionVicePresident = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionVicePresidentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Deliverer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliverDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Administrator = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdminDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeptHead = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeptHeadDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PrintCount = table.Column<int>(type: "int", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstPrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRegisterRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterRecords_NetworkArchiveBusinessChains_BusinessChainId",
                        column: x => x.BusinessChainId,
                        principalTable: "NetworkArchiveBusinessChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NetworkOnNetDisposalItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisposalRecordId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    OnNetAssetId = table.Column<int>(type: "int", nullable: false),
                    AssetNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServerPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeLifecycleStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisposalReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DispositionMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkOnNetDisposalItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkOnNetDisposalItems_NetworkOnNetAssets_OnNetAssetId",
                        column: x => x.OnNetAssetId,
                        principalTable: "NetworkOnNetAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NetworkOnNetDisposalItems_NetworkOnNetDisposalRecords_DisposalRecordId",
                        column: x => x.DisposalRecordId,
                        principalTable: "NetworkOnNetDisposalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpticalDiscLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MediumId = table.Column<int>(type: "int", nullable: false),
                    DiscCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MediaStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HolderOrOrganization = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NeedReturn = table.Column<bool>(type: "bit", nullable: false),
                    RegisterPerson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpticalDiscLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpticalDiscLedgers_OpticalDiscMedia_MediumId",
                        column: x => x.MediumId,
                        principalTable: "OpticalDiscMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpticalDiscMediaTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MediumId = table.Column<int>(type: "int", nullable: false),
                    ApplicationId = table.Column<int>(type: "int", nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BusinessNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperatorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RelatedPerson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetOrganization = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NeedReturn = table.Column<bool>(type: "bit", nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RelatedBatch = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RelatedArchiveTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpticalDiscMediaTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpticalDiscMediaTransactions_OpticalDiscMedia_MediumId",
                        column: x => x.MediumId,
                        principalTable: "OpticalDiscMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TerminalName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LoginTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastHeartbeatTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LogoutTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveDisposalItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisposalRecordId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    FilingFactId = table.Column<int>(type: "int", nullable: false),
                    ContainerId = table.Column<int>(type: "int", nullable: false),
                    ContainerCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeStorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceRegisterKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisposalReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DispositionMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FormNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InventoryLostCopyCount = table.Column<int>(type: "int", nullable: false),
                    InventoryScrapCopyCount = table.Column<int>(type: "int", nullable: false),
                    BeforeLifecycleStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MediumKind = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MediumId = table.Column<int>(type: "int", nullable: false),
                    MediumCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ElectronicArchiveUnitId = table.Column<int>(type: "int", nullable: false),
                    ElectronicArchiveNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeMediaStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetBlankSlotLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveDisposalItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveDisposalItems_YearlyArchiveDisposalRecords_DisposalRecordId",
                        column: x => x.DisposalRecordId,
                        principalTable: "YearlyArchiveDisposalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveInventoryRegisterItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegisterRecordId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    FilingFactId = table.Column<int>(type: "int", nullable: false),
                    MediaItemId = table.Column<int>(type: "int", nullable: false),
                    ContainerId = table.Column<int>(type: "int", nullable: false),
                    ContainerCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LostCopyCount = table.Column<int>(type: "int", nullable: false),
                    BeforeAvailableCopyCount = table.Column<int>(type: "int", nullable: false),
                    MediumKind = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MediumId = table.Column<int>(type: "int", nullable: false),
                    MediumCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ElectronicArchiveUnitId = table.Column<int>(type: "int", nullable: false),
                    ElectronicArchiveNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeMediaStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeStorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Year = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveInventoryRegisterItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveInventoryRegisterItems_YearlyArchiveInventoryRegisterRecords_RegisterRecordId",
                        column: x => x.RegisterRecordId,
                        principalTable: "YearlyArchiveInventoryRegisterRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveOutboundItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OutboundRecordId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    FilingFactId = table.Column<int>(type: "int", nullable: false),
                    PrimaryFilingFactId = table.Column<int>(type: "int", nullable: true),
                    ArchiveCopyRole = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SourceResultSetItemId = table.Column<int>(type: "int", nullable: true),
                    SourceResultSetId = table.Column<int>(type: "int", nullable: true),
                    ItemArchiveYear = table.Column<int>(type: "int", nullable: true),
                    ItemProjectName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SelectionScopeKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContentEntryId = table.Column<int>(type: "int", nullable: true),
                    ContentEntryKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentEntryName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FormNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContainerCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StorageLocation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CurrentStorageLocation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ContainerStatusHint = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConfidentialLevel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ArchivePurpose = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MediaKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StorageCarrierType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StockCopyCount = table.Column<int>(type: "int", nullable: false),
                    UsageMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NeedReturn = table.Column<bool>(type: "bit", nullable: false),
                    CopyCount = table.Column<int>(type: "int", nullable: true),
                    DataSizeMb = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ElectronicMediaSource = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsSelfDiskRegistered = table.Column<bool>(type: "bit", nullable: true),
                    ElectronicMediumType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequisitionedMediumId = table.Column<int>(type: "int", nullable: true),
                    RequisitionedDiskCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequisitionedDiskNeedReturn = table.Column<bool>(type: "bit", nullable: false),
                    SelfDiskSerialNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelfDiskCapacity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelfDiskCodesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelfDiskSerialNumbersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContainerDisposition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReservationStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveOutboundItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveOutboundItems_YearlyArchiveOutboundRecords_OutboundRecordId",
                        column: x => x.OutboundRecordId,
                        principalTable: "YearlyArchiveOutboundRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveOutboundSyncEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OutboundRecordId = table.Column<int>(type: "int", nullable: false),
                    OutboundItemId = table.Column<int>(type: "int", nullable: false),
                    FilingFactId = table.Column<int>(type: "int", nullable: false),
                    EntryKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OperatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveOutboundSyncEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveOutboundSyncEntries_YearlyArchiveOutboundRecords_OutboundRecordId",
                        column: x => x.OutboundRecordId,
                        principalTable: "YearlyArchiveOutboundRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRelocationItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RelocationRecordId = table.Column<int>(type: "int", nullable: false),
                    FilingFactId = table.Column<int>(type: "int", nullable: false),
                    SourceLinkId = table.Column<int>(type: "int", nullable: false),
                    SourceLinkType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BeforeContainerCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BeforeStorageLocation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AfterContainerCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AfterStorageLocation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRelocationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRelocationItems_YearlyArchiveRelocationRecords_RelocationRecordId",
                        column: x => x.RelocationRecordId,
                        principalTable: "YearlyArchiveRelocationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveReturnItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnRecordId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    SourceOutboundItemId = table.Column<int>(type: "int", nullable: false),
                    FilingFactId = table.Column<int>(type: "int", nullable: false),
                    RegisterMediaId = table.Column<int>(type: "int", nullable: false),
                    MediaKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UsageMode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReturnCopyCount = table.Column<int>(type: "int", nullable: false),
                    IntactReturnCopyCount = table.Column<int>(type: "int", nullable: false),
                    LossCopyCount = table.Column<int>(type: "int", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContainerCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RehomeTargetBoxId = table.Column<int>(type: "int", nullable: true),
                    ItemCondition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveReturnItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveReturnItems_YearlyArchiveReturnRecords_ReturnRecordId",
                        column: x => x.ReturnRecordId,
                        principalTable: "YearlyArchiveReturnRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveSearchResultSetItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResultSetId = table.Column<int>(type: "int", nullable: false),
                    FilingFactId = table.Column<int>(type: "int", nullable: false),
                    SelectionScopeKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContentEntryId = table.Column<int>(type: "int", nullable: true),
                    ContentEntryKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ContentEntryName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    FormNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContainerCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StorageLocation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BorrowHintLevel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BorrowHintText = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestedCopyCount = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveSearchResultSetItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveSearchResultSetItems_YearlyArchiveSearchResultSets_ResultSetId",
                        column: x => x.ResultSetId,
                        principalTable: "YearlyArchiveSearchResultSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyElectronicArchiveUnitDiscLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    YearlyElectronicArchiveUnitId = table.Column<int>(type: "int", nullable: false),
                    OpticalDiscMediumId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyElectronicArchiveUnitDiscLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyElectronicArchiveUnitDiscLinks_OpticalDiscMedia_OpticalDiscMediumId",
                        column: x => x.OpticalDiscMediumId,
                        principalTable: "OpticalDiscMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YearlyElectronicArchiveUnitDiscLinks_YearlyElectronicArchiveUnits_YearlyElectronicArchiveUnitId",
                        column: x => x.YearlyElectronicArchiveUnitId,
                        principalTable: "YearlyElectronicArchiveUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyElectronicArchiveUnitMediumLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    YearlyElectronicArchiveUnitId = table.Column<int>(type: "int", nullable: false),
                    HardDiskMediumId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyElectronicArchiveUnitMediumLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyElectronicArchiveUnitMediumLinks_HardDiskMedia_HardDiskMediumId",
                        column: x => x.HardDiskMediumId,
                        principalTable: "HardDiskMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YearlyElectronicArchiveUnitMediumLinks_YearlyElectronicArchiveUnits_YearlyElectronicArchiveUnitId",
                        column: x => x.YearlyElectronicArchiveUnitId,
                        principalTable: "YearlyElectronicArchiveUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HardDiskMediaTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MediumId = table.Column<int>(type: "int", nullable: false),
                    ApplicationId = table.Column<int>(type: "int", nullable: true),
                    TransactionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BusinessNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperatorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RelatedPerson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetOrganization = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NeedReturn = table.Column<bool>(type: "bit", nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RelatedBatch = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RelatedArchiveTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardDiskMediaTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HardDiskMediaTransactions_HardDiskMediaApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "HardDiskMediaApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HardDiskMediaTransactions_HardDiskMedia_MediumId",
                        column: x => x.MediumId,
                        principalTable: "HardDiskMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkInboundItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InboundRecordId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    AssetKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfidentialLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataSizeText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetServerPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceResultSetItemId = table.Column<int>(type: "int", nullable: true),
                    SourceFilingFactId = table.Column<int>(type: "int", nullable: true),
                    FormNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContainerCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StorageLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OnNetAssetId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkInboundItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkInboundItems_NetworkInboundRecords_InboundRecordId",
                        column: x => x.InboundRecordId,
                        principalTable: "NetworkInboundRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkInboundReturnHardDiskItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InboundRecordId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    MediumId = table.Column<int>(type: "int", nullable: false),
                    DiskCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceApplicationId = table.Column<int>(type: "int", nullable: true),
                    SourceOutboundRecordId = table.Column<int>(type: "int", nullable: true),
                    SourceNetworkOutboundRecordId = table.Column<int>(type: "int", nullable: true),
                    TargetBlankSlotLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkInboundReturnHardDiskItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkInboundReturnHardDiskItems_NetworkInboundRecords_InboundRecordId",
                        column: x => x.InboundRecordId,
                        principalTable: "NetworkInboundRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkOutboundItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OutboundRecordId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    OnNetAssetId = table.Column<int>(type: "int", nullable: true),
                    AssetNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServerPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfidentialLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataSizeText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Year = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkOutboundItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkOutboundItems_NetworkOnNetAssets_OnNetAssetId",
                        column: x => x.OnNetAssetId,
                        principalTable: "NetworkOnNetAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NetworkOutboundItems_NetworkOutboundRecords_OutboundRecordId",
                        column: x => x.OutboundRecordId,
                        principalTable: "NetworkOutboundRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRegisterMedias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    YearlyArchiveRegisterRecordId = table.Column<int>(type: "int", nullable: true),
                    NetworkInboundRecordId = table.Column<int>(type: "int", nullable: true),
                    NetworkOutboundRecordId = table.Column<int>(type: "int", nullable: true),
                    MediaKind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MediaCount = table.Column<int>(type: "int", nullable: false),
                    Disposition = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsBorrowedHardDisk = table.Column<bool>(type: "bit", nullable: false),
                    BorrowedHardDiskCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    UseInStockBlankHardDisk = table.Column<bool>(type: "bit", nullable: false),
                    RequisitionedMediumId = table.Column<int>(type: "int", nullable: true),
                    RequisitionedHardDiskCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValue: ""),
                    RequisitionedDiskNeedReturn = table.Column<bool>(type: "bit", nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppendTargetElectronicUnitId = table.Column<int>(type: "int", nullable: true),
                    AppendTargetHardDiskCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRegisterMedias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterMedias_NetworkInboundRecords_NetworkInboundRecordId",
                        column: x => x.NetworkInboundRecordId,
                        principalTable: "NetworkInboundRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterMedias_NetworkOutboundRecords_NetworkOutboundRecordId",
                        column: x => x.NetworkOutboundRecordId,
                        principalTable: "NetworkOutboundRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterMedias_YearlyArchiveRegisterRecords_YearlyArchiveRegisterRecordId",
                        column: x => x.YearlyArchiveRegisterRecordId,
                        principalTable: "YearlyArchiveRegisterRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRegisterMediaItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    YearlyArchiveRegisterMediaId = table.Column<int>(type: "int", nullable: false),
                    ContentDesc = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentCount = table.Column<int>(type: "int", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProvideUnit = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ConfidentialLevel = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRegisterMediaItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterMediaItems_YearlyArchiveRegisterMedias_YearlyArchiveRegisterMediaId",
                        column: x => x.YearlyArchiveRegisterMediaId,
                        principalTable: "YearlyArchiveRegisterMedias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveBoxMediaItemLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    YearlyArchiveBoxId = table.Column<int>(type: "int", nullable: false),
                    YearlyArchiveRegisterMediaItemId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveBoxMediaItemLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveBoxMediaItemLinks_YearlyArchiveBoxes_YearlyArchiveBoxId",
                        column: x => x.YearlyArchiveBoxId,
                        principalTable: "YearlyArchiveBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveBoxMediaItemLinks_YearlyArchiveRegisterMediaItems_YearlyArchiveRegisterMediaItemId",
                        column: x => x.YearlyArchiveRegisterMediaItemId,
                        principalTable: "YearlyArchiveRegisterMediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRegisterElectronicMediaItemDetails",
                columns: table => new
                {
                    MediaItemId = table.Column<int>(type: "int", nullable: false),
                    MaterialCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    SubCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    DataOrganizationForm = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    DataSizeMb = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRegisterElectronicMediaItemDetails", x => x.MediaItemId);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterElectronicMediaItemDetails_YearlyArchiveRegisterMediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "YearlyArchiveRegisterMediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRegisterSimulatedMediaItemDetails",
                columns: table => new
                {
                    MediaItemId = table.Column<int>(type: "int", nullable: false),
                    MaterialCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    SubCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    OrganizationForm = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRegisterSimulatedMediaItemDetails", x => x.MediaItemId);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterSimulatedMediaItemDetails_YearlyArchiveRegisterMediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "YearlyArchiveRegisterMediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyElectronicArchiveUnitMediaItemLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    YearlyElectronicArchiveUnitId = table.Column<int>(type: "int", nullable: false),
                    YearlyArchiveRegisterMediaItemId = table.Column<int>(type: "int", nullable: false),
                    FilingStoragePath = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MediumCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FormNo = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MaterialName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DataSizeMb = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyElectronicArchiveUnitMediaItemLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyElectronicArchiveUnitMediaItemLinks_YearlyArchiveRegisterMediaItems_YearlyArchiveRegisterMediaItemId",
                        column: x => x.YearlyArchiveRegisterMediaItemId,
                        principalTable: "YearlyArchiveRegisterMediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YearlyElectronicArchiveUnitMediaItemLinks_YearlyElectronicArchiveUnits_YearlyElectronicArchiveUnitId",
                        column: x => x.YearlyElectronicArchiveUnitId,
                        principalTable: "YearlyElectronicArchiveUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRegisterElectronicMediaItemEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ElectronicMediaItemDetailId = table.Column<int>(type: "int", nullable: false),
                    EntryKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    EntryName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    SizeMb = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRegisterElectronicMediaItemEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterElectronicMediaItemEntries_YearlyArchiveRegisterElectronicMediaItemDetails_ElectronicMediaItemDetailId",
                        column: x => x.ElectronicMediaItemDetailId,
                        principalTable: "YearlyArchiveRegisterElectronicMediaItemDetails",
                        principalColumn: "MediaItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AerialPhotos_Category",
                table: "AerialPhotos",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowRules_BusinessType_Priority_IsEnabled",
                table: "ApprovalWorkflowRules",
                columns: new[] { "BusinessType", "Priority", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_CabinetArchiveSlotCategoryAssignments_CabinetId_CategoryName",
                table: "CabinetArchiveSlotCategoryAssignments",
                columns: new[] { "CabinetId", "CategoryName" });

            migrationBuilder.CreateIndex(
                name: "IX_CabinetArchiveSlotCategoryAssignments_CabinetId_FaceCode_SlotCode",
                table: "CabinetArchiveSlotCategoryAssignments",
                columns: new[] { "CabinetId", "FaceCode", "SlotCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CabinetHardDiskSlotCategoryAssignments_CabinetId_CategoryName",
                table: "CabinetHardDiskSlotCategoryAssignments",
                columns: new[] { "CabinetId", "CategoryName" });

            migrationBuilder.CreateIndex(
                name: "IX_CabinetHardDiskSlotCategoryAssignments_CabinetId_FaceCode_SlotCode",
                table: "CabinetHardDiskSlotCategoryAssignments",
                columns: new[] { "CabinetId", "FaceCode", "SlotCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DbOperationLogs_EntityType",
                table: "DbOperationLogs",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_DbOperationLogs_Operation",
                table: "DbOperationLogs",
                column: "Operation");

            migrationBuilder.CreateIndex(
                name: "IX_DbOperationLogs_OperationTime",
                table: "DbOperationLogs",
                column: "OperationTime");

            migrationBuilder.CreateIndex(
                name: "IX_DbOperationLogs_TableName",
                table: "DbOperationLogs",
                column: "TableName");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldDomainDefinitions_EntityName_FieldName",
                table: "FieldDomainDefinitions",
                columns: new[] { "EntityName", "FieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldDomainOptions_FieldDomainDefinitionId_Scope_OptionValue",
                table: "FieldDomainOptions",
                columns: new[] { "FieldDomainDefinitionId", "Scope", "OptionValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskDisposalItems_DisposalRecordId",
                table: "HardDiskDisposalItems",
                column: "DisposalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskDisposalItems_DisposalRecordId_MediumId",
                table: "HardDiskDisposalItems",
                columns: new[] { "DisposalRecordId", "MediumId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskDisposalItems_MediumId",
                table: "HardDiskDisposalItems",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskDisposalRecords_ApplyTime",
                table: "HardDiskDisposalRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskDisposalRecords_DisposalNo",
                table: "HardDiskDisposalRecords",
                column: "DisposalNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskDisposalRecords_Status",
                table: "HardDiskDisposalRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskInventoryRegisterItems_MediumId",
                table: "HardDiskInventoryRegisterItems",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskInventoryRegisterItems_RegisterRecordId",
                table: "HardDiskInventoryRegisterItems",
                column: "RegisterRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskInventoryRegisterItems_RegisterRecordId_MediumId",
                table: "HardDiskInventoryRegisterItems",
                columns: new[] { "RegisterRecordId", "MediumId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskInventoryRegisterRecords_ApplyTime",
                table: "HardDiskInventoryRegisterRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskInventoryRegisterRecords_RegisterNo",
                table: "HardDiskInventoryRegisterRecords",
                column: "RegisterNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskInventoryRegisterRecords_Status",
                table: "HardDiskInventoryRegisterRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskLedgers_DiskCode",
                table: "HardDiskLedgers",
                column: "DiskCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskLedgers_MediumId",
                table: "HardDiskLedgers",
                column: "MediumId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskMedia_DiskCode",
                table: "HardDiskMedia",
                column: "DiskCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskMedia_SerialNumber",
                table: "HardDiskMedia",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskMediaApplications_ApplicationNo",
                table: "HardDiskMediaApplications",
                column: "ApplicationNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskMediaApplications_MediumId",
                table: "HardDiskMediaApplications",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskMediaApplications_SourceApplicationId",
                table: "HardDiskMediaApplications",
                column: "SourceApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskMediaApplications_SourceOutboundRecordId",
                table: "HardDiskMediaApplications",
                column: "SourceOutboundRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskMediaTransactions_ApplicationId",
                table: "HardDiskMediaTransactions",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskMediaTransactions_MediumId_OperateTime",
                table: "HardDiskMediaTransactions",
                columns: new[] { "MediumId", "OperateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskRegisterLocks_BusinessNo",
                table: "HardDiskRegisterLocks",
                column: "BusinessNo");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskRegisterLocks_BusinessType_BusinessRecordId",
                table: "HardDiskRegisterLocks",
                columns: new[] { "BusinessType", "BusinessRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskRegisterLocks_MediumId",
                table: "HardDiskRegisterLocks",
                column: "MediumId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveBoxes_BoxCode",
                table: "HistoryArchiveBoxes",
                column: "BoxCode",
                unique: true,
                filter: "[BoxCode] <> N''");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryMiscMaterials_ContainerKind_ContainerCode",
                table: "HistoryMiscMaterials",
                columns: new[] { "ContainerKind", "ContainerCode" },
                unique: true,
                filter: "[LifecycleStatus] <> N'已离库'");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryMiscMaterials_StorageLocation",
                table: "HistoryMiscMaterials",
                column: "StorageLocation",
                unique: true,
                filter: "[StorageLocation] <> N''");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryMiscMaterialLines_MaterialId_SortOrder",
                table: "HistoryMiscMaterialLines",
                columns: new[] { "MaterialId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveBoxLedgerLinks_HistoryArchiveBoxId_MaterialKind_RecordId",
                table: "HistoryArchiveBoxLedgerLinks",
                columns: new[] { "HistoryArchiveBoxId", "MaterialKind", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveBoxLedgerLinks_MaterialKind_RecordId",
                table: "HistoryArchiveBoxLedgerLinks",
                columns: new[] { "MaterialKind", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalItems_BoxCode",
                table: "HistoryArchiveDisposalItems",
                column: "BoxCode");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalItems_DisposalRecordId",
                table: "HistoryArchiveDisposalItems",
                column: "DisposalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalItems_DisposalRecordId_BoxCode",
                table: "HistoryArchiveDisposalItems",
                columns: new[] { "DisposalRecordId", "BoxCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalRecords_ApplyTime",
                table: "HistoryArchiveDisposalRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalRecords_DisposalNo",
                table: "HistoryArchiveDisposalRecords",
                column: "DisposalNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalRecords_MaterialKind",
                table: "HistoryArchiveDisposalRecords",
                column: "MaterialKind");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalRecords_Status",
                table: "HistoryArchiveDisposalRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkArchiveBusinessChains_ChainNo",
                table: "NetworkArchiveBusinessChains",
                column: "ChainNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkArchiveBusinessChains_PrimaryBusinessType_PrimaryBusinessId",
                table: "NetworkArchiveBusinessChains",
                columns: new[] { "PrimaryBusinessType", "PrimaryBusinessId" });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkArchiveBusinessChains_ScenarioKind",
                table: "NetworkArchiveBusinessChains",
                column: "ScenarioKind");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkArchiveBusinessTasks_BusinessChainId",
                table: "NetworkArchiveBusinessTasks",
                column: "BusinessChainId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkArchiveBusinessTasks_BusinessType_BusinessId",
                table: "NetworkArchiveBusinessTasks",
                columns: new[] { "BusinessType", "BusinessId" });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkArchiveBusinessTasks_DedupKey",
                table: "NetworkArchiveBusinessTasks",
                column: "DedupKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundItems_InboundRecordId",
                table: "NetworkInboundItems",
                column: "InboundRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundItems_OnNetAssetId",
                table: "NetworkInboundItems",
                column: "OnNetAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundItems_SourceFilingFactId",
                table: "NetworkInboundItems",
                column: "SourceFilingFactId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundRecords_ApplyTime",
                table: "NetworkInboundRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundRecords_BusinessChainId",
                table: "NetworkInboundRecords",
                column: "BusinessChainId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundRecords_InboundNo",
                table: "NetworkInboundRecords",
                column: "InboundNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundRecords_SourceResultSetId",
                table: "NetworkInboundRecords",
                column: "SourceResultSetId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundRecords_Status",
                table: "NetworkInboundRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundReturnHardDiskItems_InboundRecordId",
                table: "NetworkInboundReturnHardDiskItems",
                column: "InboundRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundReturnHardDiskItems_InboundRecordId_MediumId",
                table: "NetworkInboundReturnHardDiskItems",
                columns: new[] { "InboundRecordId", "MediumId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundReturnHardDiskItems_MediumId",
                table: "NetworkInboundReturnHardDiskItems",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_AssetNo",
                table: "NetworkOnNetAssets",
                column: "AssetNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_LifecycleStatus",
                table: "NetworkOnNetAssets",
                column: "LifecycleStatus");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_OriginInboundItemId",
                table: "NetworkOnNetAssets",
                column: "OriginInboundItemId",
                unique: true,
                filter: "[OriginInboundItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_OriginKind",
                table: "NetworkOnNetAssets",
                column: "OriginKind");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_OriginOutboundItemId",
                table: "NetworkOnNetAssets",
                column: "OriginOutboundItemId",
                unique: true,
                filter: "[OriginOutboundItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_ParentAssetId",
                table: "NetworkOnNetAssets",
                column: "ParentAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetDisposalItems_DisposalRecordId",
                table: "NetworkOnNetDisposalItems",
                column: "DisposalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetDisposalItems_DisposalRecordId_OnNetAssetId",
                table: "NetworkOnNetDisposalItems",
                columns: new[] { "DisposalRecordId", "OnNetAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetDisposalItems_OnNetAssetId",
                table: "NetworkOnNetDisposalItems",
                column: "OnNetAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetDisposalRecords_ApplyTime",
                table: "NetworkOnNetDisposalRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetDisposalRecords_DisposalNo",
                table: "NetworkOnNetDisposalRecords",
                column: "DisposalNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetDisposalRecords_Status",
                table: "NetworkOnNetDisposalRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundItems_OnNetAssetId",
                table: "NetworkOutboundItems",
                column: "OnNetAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundItems_OutboundRecordId",
                table: "NetworkOutboundItems",
                column: "OutboundRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundItems_OutboundRecordId_OnNetAssetId",
                table: "NetworkOutboundItems",
                columns: new[] { "OutboundRecordId", "OnNetAssetId" },
                unique: true,
                filter: "[OnNetAssetId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundRecords_ApplyTime",
                table: "NetworkOutboundRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundRecords_BusinessChainId",
                table: "NetworkOutboundRecords",
                column: "BusinessChainId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundRecords_OutboundNo",
                table: "NetworkOutboundRecords",
                column: "OutboundNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundRecords_Status",
                table: "NetworkOutboundRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundRecords_TargetRegisterRecordId",
                table: "NetworkOutboundRecords",
                column: "TargetRegisterRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_OpticalDiscLedgers_DiscCode",
                table: "OpticalDiscLedgers",
                column: "DiscCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpticalDiscLedgers_MediumId",
                table: "OpticalDiscLedgers",
                column: "MediumId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpticalDiscMedia_DiscCode",
                table: "OpticalDiscMedia",
                column: "DiscCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpticalDiscMediaTransactions_MediumId_OperateTime",
                table: "OpticalDiscMediaTransactions",
                columns: new[] { "MediumId", "OperateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInfos_ProjectCode",
                table: "ProjectInfos",
                column: "ProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerPathSettings_DepartmentName_PathName",
                table: "ServerPathSettings",
                columns: new[] { "DepartmentName", "PathName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ToDoReadStates_UserId_ToDoId",
                table: "ToDoReadStates",
                columns: new[] { "UserId", "ToDoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopoMaps_Category",
                table: "TopoMaps",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_TopoMaps_Scale",
                table: "TopoMaps",
                column: "Scale");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UserId",
                table: "UserPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_LoginName",
                table: "Users",
                column: "LoginName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_SessionId",
                table: "UserSessions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId",
                table: "UserSessions",
                column: "UserId",
                unique: true,
                filter: "\"IsActive\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveBoxes_ArchiveSequenceNo",
                table: "YearlyArchiveBoxes",
                column: "ArchiveSequenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveBoxMediaItemLinks_YearlyArchiveBoxId_YearlyArchiveRegisterMediaItemId",
                table: "YearlyArchiveBoxMediaItemLinks",
                columns: new[] { "YearlyArchiveBoxId", "YearlyArchiveRegisterMediaItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveBoxMediaItemLinks_YearlyArchiveRegisterMediaItemId",
                table: "YearlyArchiveBoxMediaItemLinks",
                column: "YearlyArchiveRegisterMediaItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalItems_ContainerId",
                table: "YearlyArchiveDisposalItems",
                column: "ContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalItems_DisposalRecordId",
                table: "YearlyArchiveDisposalItems",
                column: "DisposalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalItems_DisposalRecordId_FilingFactId",
                table: "YearlyArchiveDisposalItems",
                columns: new[] { "DisposalRecordId", "FilingFactId" });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalItems_DisposalRecordId_MediumKind_MediumId",
                table: "YearlyArchiveDisposalItems",
                columns: new[] { "DisposalRecordId", "MediumKind", "MediumId" });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalItems_FilingFactId",
                table: "YearlyArchiveDisposalItems",
                column: "FilingFactId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalItems_MediumId",
                table: "YearlyArchiveDisposalItems",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalRecords_ApplyTime",
                table: "YearlyArchiveDisposalRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalRecords_DisposalNo",
                table: "YearlyArchiveDisposalRecords",
                column: "DisposalNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalRecords_MediaKind",
                table: "YearlyArchiveDisposalRecords",
                column: "MediaKind");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalRecords_Status",
                table: "YearlyArchiveDisposalRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveFilingFacts_BoxLocationCode",
                table: "YearlyArchiveFilingFacts",
                column: "BoxLocationCode");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveFilingFacts_ContainerCode",
                table: "YearlyArchiveFilingFacts",
                column: "ContainerCode");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveFilingFacts_FormNo",
                table: "YearlyArchiveFilingFacts",
                column: "FormNo");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveFilingFacts_LifecycleStatus",
                table: "YearlyArchiveFilingFacts",
                column: "LifecycleStatus");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveFilingFacts_MediaItemId",
                table: "YearlyArchiveFilingFacts",
                column: "MediaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveFilingFacts_MediaKind_FiledAt",
                table: "YearlyArchiveFilingFacts",
                columns: new[] { "MediaKind", "FiledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveFilingFacts_MediumCode",
                table: "YearlyArchiveFilingFacts",
                column: "MediumCode");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveFilingFacts_PrimaryFilingFactId",
                table: "YearlyArchiveFilingFacts",
                column: "PrimaryFilingFactId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveFilingFacts_ProjectName",
                table: "YearlyArchiveFilingFacts",
                column: "ProjectName");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveFilingFacts_SourceLinkType_SourceLinkId",
                table: "YearlyArchiveFilingFacts",
                columns: new[] { "SourceLinkType", "SourceLinkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveFilingFacts_StorageLocation",
                table: "YearlyArchiveFilingFacts",
                column: "StorageLocation");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterItems_FilingFactId",
                table: "YearlyArchiveInventoryRegisterItems",
                column: "FilingFactId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterItems_MediumId",
                table: "YearlyArchiveInventoryRegisterItems",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterItems_RegisterRecordId",
                table: "YearlyArchiveInventoryRegisterItems",
                column: "RegisterRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterItems_RegisterRecordId_FilingFactId",
                table: "YearlyArchiveInventoryRegisterItems",
                columns: new[] { "RegisterRecordId", "FilingFactId" });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterItems_RegisterRecordId_MediumKind_MediumId",
                table: "YearlyArchiveInventoryRegisterItems",
                columns: new[] { "RegisterRecordId", "MediumKind", "MediumId" });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterRecords_ApplyTime",
                table: "YearlyArchiveInventoryRegisterRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterRecords_MediaKind",
                table: "YearlyArchiveInventoryRegisterRecords",
                column: "MediaKind");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterRecords_RegisterNo",
                table: "YearlyArchiveInventoryRegisterRecords",
                column: "RegisterNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterRecords_Status",
                table: "YearlyArchiveInventoryRegisterRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveMaterialTransactions_DedupKey",
                table: "YearlyArchiveMaterialTransactions",
                column: "DedupKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveMaterialTransactions_FilingFactId_OperatedAt",
                table: "YearlyArchiveMaterialTransactions",
                columns: new[] { "FilingFactId", "OperatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveOutboundItems_OutboundRecordId",
                table: "YearlyArchiveOutboundItems",
                column: "OutboundRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveOutboundRecords_OutboundNo",
                table: "YearlyArchiveOutboundRecords",
                column: "OutboundNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveOutboundSyncEntries_OutboundRecordId_OutboundItemId_EntryKind_Phase",
                table: "YearlyArchiveOutboundSyncEntries",
                columns: new[] { "OutboundRecordId", "OutboundItemId", "EntryKind", "Phase" });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterElectronicMediaItemDetails_DataOrganizationForm",
                table: "YearlyArchiveRegisterElectronicMediaItemDetails",
                column: "DataOrganizationForm");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterElectronicMediaItemDetails_MaterialCategory",
                table: "YearlyArchiveRegisterElectronicMediaItemDetails",
                column: "MaterialCategory");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterElectronicMediaItemDetails_SubCategory",
                table: "YearlyArchiveRegisterElectronicMediaItemDetails",
                column: "SubCategory");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterElectronicMediaItemEntries_ElectronicMediaItemDetailId_SortOrder",
                table: "YearlyArchiveRegisterElectronicMediaItemEntries",
                columns: new[] { "ElectronicMediaItemDetailId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterElectronicMediaItemEntries_EntryName",
                table: "YearlyArchiveRegisterElectronicMediaItemEntries",
                column: "EntryName");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterMediaItems_YearlyArchiveRegisterMediaId",
                table: "YearlyArchiveRegisterMediaItems",
                column: "YearlyArchiveRegisterMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterMedias_NetworkInboundRecordId",
                table: "YearlyArchiveRegisterMedias",
                column: "NetworkInboundRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterMedias_NetworkOutboundRecordId",
                table: "YearlyArchiveRegisterMedias",
                column: "NetworkOutboundRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterMedias_YearlyArchiveRegisterRecordId",
                table: "YearlyArchiveRegisterMedias",
                column: "YearlyArchiveRegisterRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterRecords_BusinessChainId",
                table: "YearlyArchiveRegisterRecords",
                column: "BusinessChainId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterRecords_FormNo",
                table: "YearlyArchiveRegisterRecords",
                column: "FormNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterRecords_SourceNetworkOutboundRecordId",
                table: "YearlyArchiveRegisterRecords",
                column: "SourceNetworkOutboundRecordId",
                unique: true,
                filter: "[SourceNetworkOutboundRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterSimulatedMediaItemDetails_MaterialCategory",
                table: "YearlyArchiveRegisterSimulatedMediaItemDetails",
                column: "MaterialCategory");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterSimulatedMediaItemDetails_OrganizationForm",
                table: "YearlyArchiveRegisterSimulatedMediaItemDetails",
                column: "OrganizationForm");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterSimulatedMediaItemDetails_SubCategory",
                table: "YearlyArchiveRegisterSimulatedMediaItemDetails",
                column: "SubCategory");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRelocationItems_RelocationRecordId",
                table: "YearlyArchiveRelocationItems",
                column: "RelocationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRelocationRecords_RelocationNo",
                table: "YearlyArchiveRelocationRecords",
                column: "RelocationNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveReturnItems_ReturnRecordId",
                table: "YearlyArchiveReturnItems",
                column: "ReturnRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveReturnRecords_ReturnNo",
                table: "YearlyArchiveReturnRecords",
                column: "ReturnNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveReturnRecords_SourceOutboundRecordId",
                table: "YearlyArchiveReturnRecords",
                column: "SourceOutboundRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveSearchResultSetItems_ResultSetId_FilingFactId_SelectionScopeKind_ContentEntryId",
                table: "YearlyArchiveSearchResultSetItems",
                columns: new[] { "ResultSetId", "FilingFactId", "SelectionScopeKind", "ContentEntryId" },
                unique: true,
                filter: "[ContentEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveSearchResultSets_CreatedByUserId",
                table: "YearlyArchiveSearchResultSets",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveSearchResultSets_MediaKind_CreatedAt",
                table: "YearlyArchiveSearchResultSets",
                columns: new[] { "MediaKind", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyElectronicArchiveUnitDiscLinks_OpticalDiscMediumId",
                table: "YearlyElectronicArchiveUnitDiscLinks",
                column: "OpticalDiscMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyElectronicArchiveUnitDiscLinks_YearlyElectronicArchiveUnitId_OpticalDiscMediumId",
                table: "YearlyElectronicArchiveUnitDiscLinks",
                columns: new[] { "YearlyElectronicArchiveUnitId", "OpticalDiscMediumId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyElectronicArchiveUnitMediaItemLinks_FormNo",
                table: "YearlyElectronicArchiveUnitMediaItemLinks",
                column: "FormNo");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyElectronicArchiveUnitMediaItemLinks_MediumCode",
                table: "YearlyElectronicArchiveUnitMediaItemLinks",
                column: "MediumCode");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyElectronicArchiveUnitMediaItemLinks_YearlyArchiveRegisterMediaItemId",
                table: "YearlyElectronicArchiveUnitMediaItemLinks",
                column: "YearlyArchiveRegisterMediaItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyElectronicArchiveUnitMediaItemLinks_YearlyElectronicArchiveUnitId_YearlyArchiveRegisterMediaItemId",
                table: "YearlyElectronicArchiveUnitMediaItemLinks",
                columns: new[] { "YearlyElectronicArchiveUnitId", "YearlyArchiveRegisterMediaItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyElectronicArchiveUnitMediumLinks_HardDiskMediumId",
                table: "YearlyElectronicArchiveUnitMediumLinks",
                column: "HardDiskMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyElectronicArchiveUnitMediumLinks_YearlyElectronicArchiveUnitId_HardDiskMediumId",
                table: "YearlyElectronicArchiveUnitMediumLinks",
                columns: new[] { "YearlyElectronicArchiveUnitId", "HardDiskMediumId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyElectronicArchiveUnits_ElectronicArchiveNo",
                table: "YearlyElectronicArchiveUnits",
                column: "ElectronicArchiveNo",
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE VIEW [vw_ArchiveContainerSummaries] AS
                SELECT
                    0 AS [Kind],
                    [ArchiveSequenceNo] AS [ContainerCode],
                    [ProjectName],
                    [Year],
                    [ArchivedBy],
                    [ArchivedDate],
                    [Remarks]
                FROM [YearlyArchiveBoxes]
                UNION ALL
                SELECT
                    1 AS [Kind],
                    [ElectronicArchiveNo] AS [ContainerCode],
                    [ProjectName],
                    [Year],
                    [ArchivedBy],
                    [ArchivedDate],
                    [Remarks]
                FROM [YearlyElectronicArchiveUnits];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS [vw_ArchiveContainerSummaries];");

            migrationBuilder.DropTable(
                name: "AerialPhotos");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflowRules");

            migrationBuilder.DropTable(
                name: "ArchiveBoxSpecifications");

            migrationBuilder.DropTable(
                name: "BusinessLogicSettings");

            migrationBuilder.DropTable(
                name: "CabinetArchiveSlotCategoryAssignments");

            migrationBuilder.DropTable(
                name: "CabinetHardDiskSlotCategoryAssignments");

            migrationBuilder.DropTable(
                name: "CabinetSlotSpecialRules");

            migrationBuilder.DropTable(
                name: "CabinetSlotSpecifications");

            migrationBuilder.DropTable(
                name: "DbOperationLogs");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "FieldDomainOptions");

            migrationBuilder.DropTable(
                name: "HardDiskDisposalItems");

            migrationBuilder.DropTable(
                name: "HardDiskInventoryRegisterItems");

            migrationBuilder.DropTable(
                name: "HardDiskLedgers");

            migrationBuilder.DropTable(
                name: "HardDiskMediaTransactions");

            migrationBuilder.DropTable(
                name: "HardDiskRegisterLocks");

            migrationBuilder.DropTable(
                name: "HistoryArchiveBoxLedgerLinks");

            migrationBuilder.DropTable(
                name: "HistoryMiscMaterialLines");

            migrationBuilder.DropTable(
                name: "HistoryArchiveDisposalItems");

            migrationBuilder.DropTable(
                name: "NetworkArchiveBusinessTasks");

            migrationBuilder.DropTable(
                name: "NetworkInboundItems");

            migrationBuilder.DropTable(
                name: "NetworkInboundReturnHardDiskItems");

            migrationBuilder.DropTable(
                name: "NetworkOnNetDisposalItems");

            migrationBuilder.DropTable(
                name: "NetworkOutboundItems");

            migrationBuilder.DropTable(
                name: "OpticalDiscLedgers");

            migrationBuilder.DropTable(
                name: "OpticalDiscMediaTransactions");

            migrationBuilder.DropTable(
                name: "HistoryMiscMaterials");

            migrationBuilder.DropTable(
                name: "OtherMaps");

            migrationBuilder.DropTable(
                name: "ProjectInfos");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "ServerPathSettings");

            migrationBuilder.DropTable(
                name: "SystemAttachments");

            migrationBuilder.DropTable(
                name: "ToDoReadStates");

            migrationBuilder.DropTable(
                name: "TopoMaps");

            migrationBuilder.DropTable(
                name: "UserPreferences");

            migrationBuilder.DropTable(
                name: "UserSessions");

            migrationBuilder.DropTable(
                name: "YearlyArchiveBoxMediaItemLinks");

            migrationBuilder.DropTable(
                name: "YearlyArchiveDisposalItems");

            migrationBuilder.DropTable(
                name: "YearlyArchiveFilingFacts");

            migrationBuilder.DropTable(
                name: "YearlyArchiveInventoryRegisterItems");

            migrationBuilder.DropTable(
                name: "YearlyArchiveMaterialTransactions");

            migrationBuilder.DropTable(
                name: "YearlyArchiveOutboundItems");

            migrationBuilder.DropTable(
                name: "YearlyArchiveOutboundSyncEntries");

            migrationBuilder.DropTable(
                name: "YearlyArchiveRegisterElectronicMediaItemEntries");

            migrationBuilder.DropTable(
                name: "YearlyArchiveRegisterSimulatedMediaItemDetails");

            migrationBuilder.DropTable(
                name: "YearlyArchiveRelocationItems");

            migrationBuilder.DropTable(
                name: "YearlyArchiveReturnItems");

            migrationBuilder.DropTable(
                name: "YearlyArchiveSearchResultSetItems");

            migrationBuilder.DropTable(
                name: "YearlyElectronicArchiveUnitDiscLinks");

            migrationBuilder.DropTable(
                name: "YearlyElectronicArchiveUnitMediaItemLinks");

            migrationBuilder.DropTable(
                name: "YearlyElectronicArchiveUnitMediumLinks");

            migrationBuilder.DropTable(
                name: "Cabinets");

            migrationBuilder.DropTable(
                name: "FieldDomainDefinitions");

            migrationBuilder.DropTable(
                name: "HardDiskDisposalRecords");

            migrationBuilder.DropTable(
                name: "HardDiskInventoryRegisterRecords");

            migrationBuilder.DropTable(
                name: "HardDiskMediaApplications");

            migrationBuilder.DropTable(
                name: "HistoryArchiveBoxes");

            migrationBuilder.DropTable(
                name: "HistoryArchiveDisposalRecords");

            migrationBuilder.DropTable(
                name: "NetworkOnNetDisposalRecords");

            migrationBuilder.DropTable(
                name: "NetworkOnNetAssets");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "YearlyArchiveBoxes");

            migrationBuilder.DropTable(
                name: "YearlyArchiveDisposalRecords");

            migrationBuilder.DropTable(
                name: "YearlyArchiveInventoryRegisterRecords");

            migrationBuilder.DropTable(
                name: "YearlyArchiveOutboundRecords");

            migrationBuilder.DropTable(
                name: "YearlyArchiveRegisterElectronicMediaItemDetails");

            migrationBuilder.DropTable(
                name: "YearlyArchiveRelocationRecords");

            migrationBuilder.DropTable(
                name: "YearlyArchiveReturnRecords");

            migrationBuilder.DropTable(
                name: "YearlyArchiveSearchResultSets");

            migrationBuilder.DropTable(
                name: "OpticalDiscMedia");

            migrationBuilder.DropTable(
                name: "YearlyElectronicArchiveUnits");

            migrationBuilder.DropTable(
                name: "HardDiskMedia");

            migrationBuilder.DropTable(
                name: "YearlyArchiveRegisterMediaItems");

            migrationBuilder.DropTable(
                name: "YearlyArchiveRegisterMedias");

            migrationBuilder.DropTable(
                name: "NetworkInboundRecords");

            migrationBuilder.DropTable(
                name: "NetworkOutboundRecords");

            migrationBuilder.DropTable(
                name: "YearlyArchiveRegisterRecords");

            migrationBuilder.DropTable(
                name: "NetworkArchiveBusinessChains");
        }
    }
}
