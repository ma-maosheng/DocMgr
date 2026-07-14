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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    BoxNumber = table.Column<string>(type: "TEXT", nullable: false),
                    BoxSpecification = table.Column<string>(type: "TEXT", nullable: false),
                    SurveyArea = table.Column<string>(type: "TEXT", nullable: false),
                    Scale = table.Column<string>(type: "TEXT", nullable: false),
                    PhotographyDate = table.Column<string>(type: "TEXT", nullable: false),
                    BoxContents = table.Column<string>(type: "TEXT", nullable: false),
                    PhotoCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Registrant = table.Column<string>(type: "TEXT", nullable: false),
                    RegistrationDate = table.Column<string>(type: "TEXT", nullable: false),
                    Modifier = table.Column<string>(type: "TEXT", nullable: false),
                    ModificationDate = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AerialPhotos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchiveBoxSpecifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    WidthCm = table.Column<decimal>(type: "TEXT", nullable: false),
                    HeightCm = table.Column<decimal>(type: "TEXT", nullable: false),
                    ThicknessCm = table.Column<decimal>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveBoxSpecifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessLogicSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationOverdueSetting = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessLogicSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CabinetArchiveBoxPlacements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoxCode = table.Column<string>(type: "TEXT", nullable: false),
                    BoxSpecification = table.Column<string>(type: "TEXT", nullable: false),
                    CabinetName = table.Column<string>(type: "TEXT", nullable: false),
                    FaceCode = table.Column<string>(type: "TEXT", nullable: false),
                    SlotCode = table.Column<string>(type: "TEXT", nullable: false),
                    PlacementMode = table.Column<string>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRecordKey = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabinetArchiveBoxPlacements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cabinets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Width = table.Column<double>(type: "REAL", nullable: false),
                    Height = table.Column<double>(type: "REAL", nullable: false),
                    Depth = table.Column<double>(type: "REAL", nullable: false),
                    CanvasLeft = table.Column<double>(type: "REAL", nullable: false),
                    CanvasTop = table.Column<double>(type: "REAL", nullable: false),
                    FaceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LayerCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ColumnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RotationAngle = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cabinets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CabinetSlotSpecialRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleKey = table.Column<string>(type: "TEXT", nullable: false),
                    CabinetName = table.Column<string>(type: "TEXT", nullable: false),
                    OpenFaceCode = table.Column<string>(type: "TEXT", nullable: false),
                    SlotCode = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredBoxSpecification = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredArchiveFaceCode = table.Column<string>(type: "TEXT", nullable: false),
                    LayoutModeOverride = table.Column<string>(type: "TEXT", nullable: false),
                    SpecialRuleText = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabinetSlotSpecialRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CabinetSlotSpecifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CabinetTypeCode = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    WidthCm = table.Column<decimal>(type: "TEXT", nullable: false),
                    HeightCm = table.Column<decimal>(type: "TEXT", nullable: false),
                    DepthCm = table.Column<decimal>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabinetSlotSpecifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DbOperationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OperationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", nullable: true),
                    SourcePage = table.Column<string>(type: "TEXT", nullable: false),
                    SourceButton = table.Column<string>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    TableName = table.Column<string>(type: "TEXT", nullable: false),
                    EntityKey = table.Column<string>(type: "TEXT", nullable: false),
                    Operation = table.Column<string>(type: "TEXT", nullable: false),
                    ChangedColumns = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbOperationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FieldDomainDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntityName = table.Column<string>(type: "TEXT", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsDomainEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldDomainDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HardDiskMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiskCode = table.Column<string>(type: "TEXT", nullable: false),
                    SerialNumber = table.Column<string>(type: "TEXT", nullable: false),
                    DiskType = table.Column<string>(type: "TEXT", nullable: false),
                    Brand = table.Column<string>(type: "TEXT", nullable: false),
                    Capacity = table.Column<string>(type: "TEXT", nullable: false),
                    InterfaceType = table.Column<string>(type: "TEXT", nullable: false),
                    RegisterPerson = table.Column<string>(type: "TEXT", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FactoryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RegistrationMethod = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardDiskMedia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpticalDiscMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiscCode = table.Column<string>(type: "TEXT", nullable: false),
                    DiscType = table.Column<string>(type: "TEXT", nullable: false),
                    Capacity = table.Column<string>(type: "TEXT", nullable: false),
                    RegisterPerson = table.Column<string>(type: "TEXT", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RegistrationMethod = table.Column<string>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRecordKey = table.Column<string>(type: "TEXT", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpticalDiscMedia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OtherMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    SequenceNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Scale = table.Column<string>(type: "TEXT", nullable: false),
                    BoxNumber = table.Column<string>(type: "TEXT", nullable: false),
                    BoxSpecification = table.Column<string>(type: "TEXT", nullable: false),
                    MapName = table.Column<string>(type: "TEXT", nullable: false),
                    SheetCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Registrant = table.Column<string>(type: "TEXT", nullable: false),
                    RegistrationDate = table.Column<string>(type: "TEXT", nullable: false),
                    Modifier = table.Column<string>(type: "TEXT", nullable: false),
                    ModificationDate = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectName = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectCode = table.Column<string>(type: "TEXT", nullable: false),
                    ImplementYear = table.Column<string>(type: "TEXT", nullable: false),
                    CapitalMgrDept = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BusinessType = table.Column<string>(type: "TEXT", nullable: false),
                    BusinessNo = table.Column<string>(type: "TEXT", nullable: false),
                    BusinessId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    Extension = table.Column<string>(type: "TEXT", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    FileContent = table.Column<byte[]>(type: "BLOB", nullable: true),
                    FileCategory = table.Column<string>(type: "TEXT", nullable: false),
                    UploadTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UploaderName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAttachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToDoReadStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToDoId = table.Column<string>(type: "TEXT", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToDoReadStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TopoMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Scale = table.Column<string>(type: "TEXT", nullable: false),
                    BoxNumber = table.Column<string>(type: "TEXT", nullable: false),
                    BoxSpecification = table.Column<string>(type: "TEXT", nullable: false),
                    MapNumber = table.Column<string>(type: "TEXT", nullable: false),
                    MapName = table.Column<string>(type: "TEXT", nullable: false),
                    SheetCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreationDate = table.Column<string>(type: "TEXT", nullable: false),
                    SurveyDate = table.Column<string>(type: "TEXT", nullable: false),
                    CoordinateSystem = table.Column<string>(type: "TEXT", nullable: false),
                    ElevationDatum = table.Column<string>(type: "TEXT", nullable: false),
                    Region = table.Column<string>(type: "TEXT", nullable: false),
                    Registrant = table.Column<string>(type: "TEXT", nullable: false),
                    RegistrationDate = table.Column<string>(type: "TEXT", nullable: false),
                    Modifier = table.Column<string>(type: "TEXT", nullable: false),
                    ModificationDate = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopoMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    EnableToDoPopup = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableToDoBadge = table.Column<bool>(type: "INTEGER", nullable: false),
                    ToDoRefreshSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    ToDoTopN = table.Column<int>(type: "INTEGER", nullable: false),
                    MarkAllAsReadOnAcknowledge = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LoginName = table.Column<string>(type: "TEXT", nullable: false),
                    RealName = table.Column<string>(type: "TEXT", nullable: false),
                    Department = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveBoxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArchiveSequenceNo = table.Column<string>(type: "TEXT", nullable: false),
                    BoxLocationCode = table.Column<string>(type: "TEXT", nullable: false),
                    CabinetName = table.Column<string>(type: "TEXT", nullable: false),
                    Side = table.Column<string>(type: "TEXT", nullable: false),
                    Row = table.Column<int>(type: "INTEGER", nullable: false),
                    Column = table.Column<int>(type: "INTEGER", nullable: false),
                    BoxIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<string>(type: "TEXT", nullable: false),
                    Specs = table.Column<string>(type: "TEXT", nullable: false),
                    PlacementMode = table.Column<string>(type: "TEXT", nullable: false),
                    ArchivedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ArchivedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", nullable: false),
                    ContainerLifecycleStatus = table.Column<string>(type: "TEXT", nullable: false),
                    LastStorageLocation = table.Column<string>(type: "TEXT", nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RetiredBy = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveBoxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveFilingFacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FilingFactNo = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    MediaKind = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    RegisterRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisterMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    FormNo = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    MaterialName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ProvideUnit = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ApplicantName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ConfidentialLevel = table.Column<string>(type: "TEXT", nullable: false),
                    ContentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StorageLocation = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CabinetName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BoxLocationCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BoxSpecs = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    StorageCarrierType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Disposition = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    MediumCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FilingStoragePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    DataSizeMb = table.Column<decimal>(type: "TEXT", nullable: false),
                    FiledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FiledBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceLinkType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SourceLinkId = table.Column<int>(type: "INTEGER", nullable: false),
                    LifecycleStatus = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CurrentContainerCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CurrentStorageLocation = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    LifecycleUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LifecycleRemark = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    BorrowHintLevel = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BorrowHintText = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    BorrowHintUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PrimaryFilingFactId = table.Column<int>(type: "INTEGER", nullable: true),
                    ArchiveCopyRole = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveFilingFacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveOutboundRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OutboundNo = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ArchiveYear = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ApplicantUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicantName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ApplicantDept = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ApplyDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    DestinationKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ExternalUnit = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SelfRetainDisposition = table.Column<string>(type: "TEXT", nullable: false),
                    ProofMaterialNote = table.Column<string>(type: "TEXT", nullable: false),
                    MaterialSummary = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SourceResultSetId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceResultSetNo = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedUploadedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WithdrawReason = table.Column<string>(type: "TEXT", nullable: false),
                    ForceVoidedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ForceVoidReason = table.Column<string>(type: "TEXT", nullable: false),
                    ForceVoidKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ApprovalDeadline = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OverdueRemindedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PrintCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HandoverRemark = table.Column<string>(type: "TEXT", nullable: false),
                    PhysicallyCompletedBy = table.Column<string>(type: "TEXT", nullable: false),
                    DeptAuditOpinion = table.Column<string>(type: "TEXT", nullable: false),
                    DeptAuditor = table.Column<string>(type: "TEXT", nullable: false),
                    DeptAuditDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ArchiveRoomHeadOpinion = table.Column<string>(type: "TEXT", nullable: false),
                    ArchiveRoomHead = table.Column<string>(type: "TEXT", nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProductionHeadOpinion = table.Column<string>(type: "TEXT", nullable: false),
                    ProductionHead = table.Column<string>(type: "TEXT", nullable: false),
                    ProductionHeadDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VicePresidentOpinion = table.Column<string>(type: "TEXT", nullable: false),
                    VicePresident = table.Column<string>(type: "TEXT", nullable: false),
                    VicePresidentDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveOutboundRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRegisterRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FormNo = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArchivedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SimulatedArchiveStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ElectronicArchiveStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectName = table.Column<string>(type: "TEXT", nullable: true),
                    MaterialName = table.Column<string>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    ProvideUnit = table.Column<string>(type: "TEXT", nullable: false),
                    ArchivePurpose = table.Column<string>(type: "TEXT", nullable: false),
                    OtherRequests = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantName = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantDept = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProdDeptOpinion = table.Column<string>(type: "TEXT", nullable: false),
                    ProdLeader = table.Column<string>(type: "TEXT", nullable: false),
                    ProdDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RndDeptOpinion = table.Column<string>(type: "TEXT", nullable: false),
                    RndLeader = table.Column<string>(type: "TEXT", nullable: false),
                    RndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeputyOpinion = table.Column<string>(type: "TEXT", nullable: false),
                    DeputyLeader = table.Column<string>(type: "TEXT", nullable: false),
                    DeputyDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Deliverer = table.Column<string>(type: "TEXT", nullable: false),
                    DeliverDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Administrator = table.Column<string>(type: "TEXT", nullable: false),
                    AdminDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeptLeader = table.Column<string>(type: "TEXT", nullable: false),
                    DeptDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRegisterRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRelocationRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RelocationNo = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    MediaKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    RelocationMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SourceContainerId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceContainerCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceStorageLocation = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    TargetContainerId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetContainerCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TargetStorageLocation = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SourceMediumDisposition = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OperatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OperatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    PreviewReport = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRelocationRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveReturnRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReturnNo = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceOutboundRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceOutboundNo = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ArchiveYear = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    BorrowerName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BorrowerDept = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisteredByName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RegisteredByDept = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    HandlerName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VoidReason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    PrintCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveReturnRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveSearchResultSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResultSetNo = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    MediaKind = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SearchCriteriaJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveSearchResultSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyElectronicArchiveUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ElectronicArchiveNo = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<string>(type: "TEXT", nullable: false),
                    StorageCarrierType = table.Column<string>(type: "TEXT", nullable: false),
                    StoragePath = table.Column<string>(type: "TEXT", nullable: false),
                    StorageLocation = table.Column<string>(type: "TEXT", nullable: false),
                    LinkedMediumCodes = table.Column<string>(type: "TEXT", nullable: false),
                    Disposition = table.Column<string>(type: "TEXT", nullable: false),
                    MediaCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ArchivedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ArchivedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRecordKey = table.Column<string>(type: "TEXT", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", nullable: false),
                    UnitLifecycleStatus = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyElectronicArchiveUnits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CabinetHardDiskSlotCategoryAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CabinetId = table.Column<int>(type: "INTEGER", nullable: false),
                    FaceCode = table.Column<string>(type: "TEXT", nullable: false),
                    SlotCode = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FieldDomainDefinitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", nullable: false),
                    OptionValue = table.Column<string>(type: "TEXT", nullable: false),
                    OptionLabel = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "HardDiskLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediumId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiskCode = table.Column<string>(type: "TEXT", nullable: false),
                    MediaStatus = table.Column<string>(type: "TEXT", nullable: false),
                    MediaNature = table.Column<string>(type: "TEXT", nullable: false),
                    StorageLocation = table.Column<string>(type: "TEXT", nullable: false),
                    HolderOrOrganization = table.Column<string>(type: "TEXT", nullable: false),
                    NeedReturn = table.Column<bool>(type: "INTEGER", nullable: false),
                    RegisterPerson = table.Column<string>(type: "TEXT", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationNo = table.Column<string>(type: "TEXT", nullable: false),
                    MediumId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceApplicationId = table.Column<int>(type: "INTEGER", nullable: true),
                    ApplicationType = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicationStatus = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantName = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantDept = table.Column<string>(type: "TEXT", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    TargetPersonOrUnit = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentLocation = table.Column<string>(type: "TEXT", nullable: false),
                    TargetLocation = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RelatedBatch = table.Column<string>(type: "TEXT", nullable: false),
                    RelatedArchiveTitle = table.Column<string>(type: "TEXT", nullable: false),
                    PrintCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PrintedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewerName = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewerDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ApprovedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovalOpinion = table.Column<string>(type: "TEXT", nullable: false),
                    InspectionResult = table.Column<string>(type: "TEXT", nullable: false),
                    FormatConfirmation = table.Column<string>(type: "TEXT", nullable: false),
                    ExecutedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ExecutedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Remark = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediumId = table.Column<int>(type: "INTEGER", nullable: false),
                    BusinessType = table.Column<string>(type: "TEXT", nullable: false),
                    BusinessRecordId = table.Column<int>(type: "INTEGER", nullable: true),
                    BusinessNo = table.Column<string>(type: "TEXT", nullable: false),
                    PreviousStatus = table.Column<string>(type: "TEXT", nullable: false),
                    LockedTime = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                name: "OpticalDiscLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediumId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscCode = table.Column<string>(type: "TEXT", nullable: false),
                    MediaStatus = table.Column<string>(type: "TEXT", nullable: false),
                    StorageLocation = table.Column<string>(type: "TEXT", nullable: false),
                    HolderOrOrganization = table.Column<string>(type: "TEXT", nullable: false),
                    NeedReturn = table.Column<bool>(type: "INTEGER", nullable: false),
                    RegisterPerson = table.Column<string>(type: "TEXT", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediumId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: true),
                    TransactionType = table.Column<string>(type: "TEXT", nullable: false),
                    BusinessNo = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeStatus = table.Column<string>(type: "TEXT", nullable: false),
                    AfterStatus = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeLocation = table.Column<string>(type: "TEXT", nullable: false),
                    AfterLocation = table.Column<string>(type: "TEXT", nullable: false),
                    OperatorName = table.Column<string>(type: "TEXT", nullable: false),
                    OperateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RelatedPerson = table.Column<string>(type: "TEXT", nullable: false),
                    TargetOrganization = table.Column<string>(type: "TEXT", nullable: false),
                    NeedReturn = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualReturnDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RelatedBatch = table.Column<string>(type: "TEXT", nullable: false),
                    RelatedArchiveTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TerminalName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    LoginTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastHeartbeatTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LogoutTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "YearlyArchiveOutboundItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OutboundRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    FilingFactId = table.Column<int>(type: "INTEGER", nullable: false),
                    PrimaryFilingFactId = table.Column<int>(type: "INTEGER", nullable: true),
                    ArchiveCopyRole = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SourceResultSetItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceResultSetId = table.Column<int>(type: "INTEGER", nullable: true),
                    ItemArchiveYear = table.Column<int>(type: "INTEGER", nullable: true),
                    ItemProjectName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SelectionScopeKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ContentEntryId = table.Column<int>(type: "INTEGER", nullable: true),
                    ContentEntryKind = table.Column<string>(type: "TEXT", nullable: false),
                    ContentEntryName = table.Column<string>(type: "TEXT", nullable: false),
                    ContentEntryRelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    FormNo = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    MaterialName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ContainerCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StorageLocation = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CurrentStorageLocation = table.Column<string>(type: "TEXT", nullable: false),
                    ConfidentialLevel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    MediaKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StorageCarrierType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StockCopyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UsageMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    NeedReturn = table.Column<bool>(type: "INTEGER", nullable: false),
                    CopyCount = table.Column<int>(type: "INTEGER", nullable: true),
                    DataSizeMb = table.Column<decimal>(type: "TEXT", nullable: true),
                    ElectronicMediaSource = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsSelfDiskRegistered = table.Column<bool>(type: "INTEGER", nullable: true),
                    ElectronicMediumType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RequisitionedMediumId = table.Column<int>(type: "INTEGER", nullable: true),
                    RequisitionedDiskCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RequisitionedDiskNeedReturn = table.Column<bool>(type: "INTEGER", nullable: false),
                    SelfDiskSerialNo = table.Column<string>(type: "TEXT", nullable: false),
                    SelfDiskCapacity = table.Column<string>(type: "TEXT", nullable: false),
                    SelfDiskCodesJson = table.Column<string>(type: "TEXT", nullable: false),
                    SelfDiskSerialNumbersJson = table.Column<string>(type: "TEXT", nullable: false),
                    ContainerDisposition = table.Column<string>(type: "TEXT", nullable: false),
                    ReservationStatus = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OutboundRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    OutboundItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilingFactId = table.Column<int>(type: "INTEGER", nullable: false),
                    EntryKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Phase = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OperatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
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
                name: "YearlyArchiveBoxYearlyArchiveRegisterRecord",
                columns: table => new
                {
                    ArchiveBoxesId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisterRecordsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveBoxYearlyArchiveRegisterRecord", x => new { x.ArchiveBoxesId, x.RegisterRecordsId });
                    table.ForeignKey(
                        name: "FK_YearlyArchiveBoxYearlyArchiveRegisterRecord_YearlyArchiveBoxes_ArchiveBoxesId",
                        column: x => x.ArchiveBoxesId,
                        principalTable: "YearlyArchiveBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveBoxYearlyArchiveRegisterRecord_YearlyArchiveRegisterRecords_RegisterRecordsId",
                        column: x => x.RegisterRecordsId,
                        principalTable: "YearlyArchiveRegisterRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRegisterMedias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YearlyArchiveRegisterRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaKind = table.Column<string>(type: "TEXT", nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", nullable: false),
                    MediaCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Disposition = table.Column<string>(type: "TEXT", nullable: false),
                    IsBorrowedHardDisk = table.Column<bool>(type: "INTEGER", nullable: false),
                    BorrowedHardDiskCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRegisterMedias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterMedias_YearlyArchiveRegisterRecords_YearlyArchiveRegisterRecordId",
                        column: x => x.YearlyArchiveRegisterRecordId,
                        principalTable: "YearlyArchiveRegisterRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRelocationItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RelocationRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilingFactId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceLinkId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceLinkType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BeforeContainerCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BeforeStorageLocation = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AfterContainerCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AfterStorageLocation = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReturnRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceOutboundItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilingFactId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisterMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaKind = table.Column<string>(type: "TEXT", nullable: false),
                    UsageMode = table.Column<string>(type: "TEXT", nullable: false),
                    ReturnCopyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialName = table.Column<string>(type: "TEXT", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", nullable: false),
                    ContainerCode = table.Column<string>(type: "TEXT", nullable: false),
                    StorageLocation = table.Column<string>(type: "TEXT", nullable: false),
                    ItemCondition = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResultSetId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilingFactId = table.Column<int>(type: "INTEGER", nullable: false),
                    SelectionScopeKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ContentEntryId = table.Column<int>(type: "INTEGER", nullable: true),
                    ContentEntryKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ContentEntryName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ContentEntryRelativePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    FormNo = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    MaterialName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ContainerCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StorageLocation = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BorrowHintLevel = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BorrowHintText = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    RequestedCopyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                name: "YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit",
                columns: table => new
                {
                    ElectronicArchiveUnitsId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisterRecordsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit", x => new { x.ElectronicArchiveUnitsId, x.RegisterRecordsId });
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit_YearlyArchiveRegisterRecords_RegisterRecordsId",
                        column: x => x.RegisterRecordsId,
                        principalTable: "YearlyArchiveRegisterRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit_YearlyElectronicArchiveUnits_ElectronicArchiveUnitsId",
                        column: x => x.ElectronicArchiveUnitsId,
                        principalTable: "YearlyElectronicArchiveUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyElectronicArchiveUnitDiscLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YearlyElectronicArchiveUnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    OpticalDiscMediumId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YearlyElectronicArchiveUnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    HardDiskMediumId = table.Column<int>(type: "INTEGER", nullable: false)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediumId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: true),
                    TransactionType = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeStatus = table.Column<string>(type: "TEXT", nullable: false),
                    AfterStatus = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeLocation = table.Column<string>(type: "TEXT", nullable: false),
                    AfterLocation = table.Column<string>(type: "TEXT", nullable: false),
                    OperatorName = table.Column<string>(type: "TEXT", nullable: false),
                    OperateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RelatedPerson = table.Column<string>(type: "TEXT", nullable: false),
                    TargetOrganization = table.Column<string>(type: "TEXT", nullable: false),
                    NeedReturn = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualReturnDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RelatedBatch = table.Column<string>(type: "TEXT", nullable: false),
                    RelatedArchiveTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardDiskMediaTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HardDiskMediaTransactions_HardDiskMediaApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "HardDiskMediaApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HardDiskMediaTransactions_HardDiskMedia_MediumId",
                        column: x => x.MediumId,
                        principalTable: "HardDiskMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRegisterMediaItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YearlyArchiveRegisterMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", nullable: false),
                    ContentDesc = table.Column<string>(type: "TEXT", nullable: false),
                    ContentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StoragePath = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false),
                    ConfidentialLevel = table.Column<string>(type: "TEXT", nullable: false)
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
                name: "YearlyElectronicArchiveUnitMediaLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YearlyElectronicArchiveUnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    YearlyArchiveRegisterMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyElectronicArchiveUnitMediaLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyElectronicArchiveUnitMediaLinks_YearlyArchiveRegisterMedias_YearlyArchiveRegisterMediaId",
                        column: x => x.YearlyArchiveRegisterMediaId,
                        principalTable: "YearlyArchiveRegisterMedias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YearlyElectronicArchiveUnitMediaLinks_YearlyElectronicArchiveUnits_YearlyElectronicArchiveUnitId",
                        column: x => x.YearlyElectronicArchiveUnitId,
                        principalTable: "YearlyElectronicArchiveUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveBoxMediaItemLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YearlyArchiveBoxId = table.Column<int>(type: "INTEGER", nullable: false),
                    YearlyArchiveRegisterMediaItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    MediaItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialCategory = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: ""),
                    SubCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: ""),
                    DataOrganizationForm = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: ""),
                    DataSizeMb = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
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
                name: "YearlyElectronicArchiveUnitMediaItemLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YearlyElectronicArchiveUnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    YearlyArchiveRegisterMediaItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilingStoragePath = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    MediumCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    FormNo = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    MaterialName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ContentSummary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    DataSizeMb = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ElectronicMediaItemDetailId = table.Column<int>(type: "INTEGER", nullable: false),
                    EntryKind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: ""),
                    EntryName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, defaultValue: ""),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false, defaultValue: ""),
                    SizeMb = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "IX_CabinetArchiveBoxPlacements_BoxCode",
                table: "CabinetArchiveBoxPlacements",
                column: "BoxCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CabinetArchiveBoxPlacements_CabinetName_FaceCode_SlotCode",
                table: "CabinetArchiveBoxPlacements",
                columns: new[] { "CabinetName", "FaceCode", "SlotCode" });

            migrationBuilder.CreateIndex(
                name: "IX_CabinetArchiveBoxPlacements_SourceType_SourceRecordKey",
                table: "CabinetArchiveBoxPlacements",
                columns: new[] { "SourceType", "SourceRecordKey" });

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
                name: "IX_ToDoReadStates_UserId_ToDoId",
                table: "ToDoReadStates",
                columns: new[] { "UserId", "ToDoId" },
                unique: true);

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
                name: "IX_YearlyArchiveBoxYearlyArchiveRegisterRecord_RegisterRecordsId",
                table: "YearlyArchiveBoxYearlyArchiveRegisterRecord",
                column: "RegisterRecordsId");

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
                name: "IX_YearlyArchiveRegisterMedias_YearlyArchiveRegisterRecordId",
                table: "YearlyArchiveRegisterMedias",
                column: "YearlyArchiveRegisterRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterRecords_FormNo",
                table: "YearlyArchiveRegisterRecords",
                column: "FormNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit_RegisterRecordsId",
                table: "YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit",
                column: "RegisterRecordsId");

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
                unique: true);

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
                name: "IX_YearlyElectronicArchiveUnitMediaLinks_YearlyArchiveRegisterMediaId",
                table: "YearlyElectronicArchiveUnitMediaLinks",
                column: "YearlyArchiveRegisterMediaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyElectronicArchiveUnitMediaLinks_YearlyElectronicArchiveUnitId_YearlyArchiveRegisterMediaId",
                table: "YearlyElectronicArchiveUnitMediaLinks",
                columns: new[] { "YearlyElectronicArchiveUnitId", "YearlyArchiveRegisterMediaId" },
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
                @"CREATE VIEW IF NOT EXISTS vw_ArchiveContainerSummaries AS
SELECT
    0 AS Kind,
    ArchiveSequenceNo AS ContainerCode,
    ProjectName,
    Year,
    ArchivedBy,
    ArchivedDate,
    Remarks
FROM YearlyArchiveBoxes
UNION ALL
SELECT
    1 AS Kind,
    ElectronicArchiveNo AS ContainerCode,
    ProjectName,
    Year,
    ArchivedBy,
    ArchivedDate,
    Remarks
FROM YearlyElectronicArchiveUnits;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ArchiveContainerSummaries;");

            migrationBuilder.DropTable(
                name: "AerialPhotos");

            migrationBuilder.DropTable(
                name: "ArchiveBoxSpecifications");

            migrationBuilder.DropTable(
                name: "BusinessLogicSettings");

            migrationBuilder.DropTable(
                name: "CabinetArchiveBoxPlacements");

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
                name: "HardDiskLedgers");

            migrationBuilder.DropTable(
                name: "HardDiskMediaTransactions");

            migrationBuilder.DropTable(
                name: "HardDiskRegisterLocks");

            migrationBuilder.DropTable(
                name: "OpticalDiscLedgers");

            migrationBuilder.DropTable(
                name: "OpticalDiscMediaTransactions");

            migrationBuilder.DropTable(
                name: "OtherMaps");

            migrationBuilder.DropTable(
                name: "ProjectInfos");

            migrationBuilder.DropTable(
                name: "Roles");

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
                name: "YearlyArchiveBoxYearlyArchiveRegisterRecord");

            migrationBuilder.DropTable(
                name: "YearlyArchiveFilingFacts");

            migrationBuilder.DropTable(
                name: "YearlyArchiveOutboundItems");

            migrationBuilder.DropTable(
                name: "YearlyArchiveOutboundSyncEntries");

            migrationBuilder.DropTable(
                name: "YearlyArchiveRegisterElectronicMediaItemEntries");

            migrationBuilder.DropTable(
                name: "YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit");

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
                name: "YearlyElectronicArchiveUnitMediaLinks");

            migrationBuilder.DropTable(
                name: "YearlyElectronicArchiveUnitMediumLinks");

            migrationBuilder.DropTable(
                name: "Cabinets");

            migrationBuilder.DropTable(
                name: "FieldDomainDefinitions");

            migrationBuilder.DropTable(
                name: "HardDiskMediaApplications");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "YearlyArchiveBoxes");

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
                name: "YearlyArchiveRegisterRecords");
        }
    }
}
