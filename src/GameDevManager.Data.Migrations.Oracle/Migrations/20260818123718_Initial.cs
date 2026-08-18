using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    IsArchived = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    CanWrite = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    CanExport = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    CanImport = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    AllowedModuleKeys = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    OwnerModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    OwnerEntityId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    FileName = table.Column<string>(type: "NVARCHAR2(260)", maxLength: 260, nullable: false),
                    MimeType = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    SizeBytes = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    Width = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    Height = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    Description = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    LanguageCode = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    VoiceActor = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    IsPrimary = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assets_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetTags_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UserId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    UserName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    EntityName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Action = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Details = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeLogEntries_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CombatMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    HealthFieldId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    DamageFieldId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    DefenseFieldId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    SpeedFieldId = table.Column<Guid>(type: "RAW(16)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombatMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CombatMappings_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConditionSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    OwnerId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    OwnerModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Slot = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Logic = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConditionSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConditionSets_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    OwnerEntityId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    OwnerModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Text = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: false),
                    AuthorName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ResolvedBy = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentComments_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentLanguages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Code = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    IsSource = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentLanguages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentLanguages_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Check = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    TagId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Slot = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    Severity = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentRules_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    Color = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentTags_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    OwnerEntityId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    OwnerModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Slot = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    LanguageCode = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    SourceText = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentTranslations_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    Icon = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ParentId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentTypes_ContentTypes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContentTypes_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DashboardCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CardKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    IsHidden = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DashboardCards_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExportProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Target = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    IncludeAssets = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    Layout = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    MinimumStatus = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ModuleKeys = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportProfiles_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HealthCheckMutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CheckKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    EntityName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthCheckMutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HealthCheckMutes_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KanbanBoards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanbanBoards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KanbanBoards_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModuleSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModuleSettings_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcRelationTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    InverseName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcRelationTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcRelationTypes_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerCharacters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    CharacterClassId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerCharacters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerCharacters_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecycleBinEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    EntityName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DeletedBy = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecycleBinEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecycleBinEntries_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillTrees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillTrees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillTrees_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Webhooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    Secret = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    ModuleKeys = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    LastDeliveryAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    LastStatusCode = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    LastError = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Webhooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Webhooks_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Whiteboards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Whiteboards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Whiteboards_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    UserName = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    TotpSecret = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    TotpConfirmedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    TotpRecoveryCodes = table.Column<string>(type: "NCLOB", maxLength: 5000, nullable: true),
                    ExternalId = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    IsAdministrator = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    IsDisabled = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    CanWrite = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    CanExport = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    CanImport = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    AllowedModuleKeys = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    RoleId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    OverridesRole = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    FeedReadAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    Email = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: true),
                    NotifyOnAssignment = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    NotifyOnComment = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    NotifyOnReview = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUsers_UserRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "UserRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AssetRegions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    AssetId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    X = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Y = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Width = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Height = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetRegions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetRegions_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    AssetId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    StorageKey = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: false),
                    FileName = table.Column<string>(type: "NVARCHAR2(300)", maxLength: 300, nullable: false),
                    MimeType = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    Width = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    Height = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ReplacedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetVersions_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetTagAssignments",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    AssetTagId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTagAssignments", x => new { x.AssetId, x.AssetTagId });
                    table.ForeignKey(
                        name: "FK_AssetTagAssignments_AssetTags_AssetTagId",
                        column: x => x.AssetTagId,
                        principalTable: "AssetTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetTagAssignments_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Conditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ConditionSetId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Kind = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TargetModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Operator = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    NumberValue = table.Column<double>(type: "BINARY_DOUBLE", nullable: true),
                    BooleanValue = table.Column<bool>(type: "BOOLEAN", nullable: true),
                    TextValue = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conditions_ConditionSets_ConditionSetId",
                        column: x => x.ConditionSetId,
                        principalTable: "ConditionSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentTagAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTagId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    TargetModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "RAW(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTagAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentTagAssignments_ContentTags_ContentTagId",
                        column: x => x.ContentTagId,
                        principalTable: "ContentTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentTagScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTagId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTagScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentTagScopes_ContentTags_ContentTagId",
                        column: x => x.ContentTagId,
                        principalTable: "ContentTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Achievements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    IsSecret = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Achievements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Achievements_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Achievements_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterClasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterClasses_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CharacterClasses_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Collectibles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collectibles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Collectibles_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Collectibles_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Symbol = table.Column<string>(type: "NVARCHAR2(10)", maxLength: 10, nullable: true),
                    ExchangeRate = table.Column<double>(type: "BINARY_DOUBLE", nullable: false, defaultValue: 1.0),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Currencies_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Currencies_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cutscenes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    StoryEntryId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    DialogueId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cutscenes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cutscenes_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cutscenes_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Dialogues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Kind = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IncludesPlayer = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dialogues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dialogues_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dialogues_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiplomaticRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    FactionAId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    FactionBId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Stance = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiplomaticRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiplomaticRelations_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiplomaticRelations_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnginePresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Engine = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    ModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    TypeName = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnginePresets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnginePresets_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EnginePresets_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Factions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Factions_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Factions_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    OwnerEntityId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IsRequired = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    IsTagList = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    IsMultiValue = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    Unit = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: true),
                    MinValue = table.Column<double>(type: "BINARY_DOUBLE", nullable: true),
                    MaxValue = table.Column<double>(type: "BINARY_DOUBLE", nullable: true),
                    Pattern = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: true),
                    ReferenceModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    GroupName = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldDefinitions_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameEffects_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameEffects_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Chance = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    RewardLootTableId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameEvents_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameEvents_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Items_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LootTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    RollMode = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LootTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LootTables_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LootTables_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Maps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Maps_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Maps_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Npcs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Kind = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IsUnique = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    IsTrader = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    IsQuestGiver = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    LootTableId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    CharacterClassId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Preferences = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    Personality = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    Traits = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: true),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Npcs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Npcs_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Npcs_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Quests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Kind = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    GiverNpcId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    StoryEntryId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    DialogueId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quests_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quests_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rarities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Color = table.Column<string>(type: "NVARCHAR2(9)", maxLength: 9, nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rarities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rarities_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rarities_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recipes_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Recipes_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    SkillTreeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    ParentSkillId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    CostPoints = table.Column<double>(type: "BINARY_DOUBLE", nullable: true),
                    CostItemId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    CostItemAmount = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skills_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Skills_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SoundEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoundEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoundEffects_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoundEffects_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Body = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Mood = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    GameDate = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    Duration = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    Location = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: true),
                    TargetMapId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    TargetMapMarkerId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryEntries_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoryEntries_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorldStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Kind = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Color = table.Column<string>(type: "NVARCHAR2(9)", maxLength: 9, nullable: true),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    BasedOnId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Status = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorldStates_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorldStates_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KanbanColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    BoardId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanbanColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KanbanColumns_KanbanBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "KanbanBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WhiteboardNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    WhiteboardId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    X = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    Y = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    Text = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    Color = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhiteboardNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhiteboardNotes_Whiteboards_WhiteboardId",
                        column: x => x.WhiteboardId,
                        principalTable: "Whiteboards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WhiteboardStrokes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    WhiteboardId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Points = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    Color = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    Width = table.Column<double>(type: "BINARY_DOUBLE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhiteboardStrokes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhiteboardStrokes_Whiteboards_WhiteboardId",
                        column: x => x.WhiteboardId,
                        principalTable: "Whiteboards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Prefix = table.Column<string>(type: "NVARCHAR2(16)", maxLength: 16, nullable: false),
                    KeyHash = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    IsDisabled = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    CanWrite = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    AppUserId = table.Column<Guid>(type: "RAW(16)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeys_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ApiKeys_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReviewRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    OwnerEntityId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    OwnerModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    RequestedBy = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    RequestedById = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    AssignedUserId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Note = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    Decision = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DecisionNote = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    DecidedBy = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewRequests_AppUsers_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReviewRequests_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    AppUserId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    FilterJson = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    ColumnFieldIds = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedViews_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedViews_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    AppUserId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPins_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPins_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CutsceneShots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CutsceneId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Text = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    DurationSeconds = table.Column<double>(type: "BINARY_DOUBLE", nullable: true),
                    CameraNote = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CutsceneShots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CutsceneShots_Cutscenes_CutsceneId",
                        column: x => x.CutsceneId,
                        principalTable: "Cutscenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DialogueLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    DialogueId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    SpeakerNpcId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Text = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DialogueLines_Dialogues_DialogueId",
                        column: x => x.DialogueId,
                        principalTable: "Dialogues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DialogueParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    DialogueId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    NpcId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DialogueParticipants_Dialogues_DialogueId",
                        column: x => x.DialogueId,
                        principalTable: "Dialogues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnginePresetMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    EnginePresetId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Target = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    Source = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    ConstantValue = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnginePresetMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnginePresetMappings_EnginePresets_EnginePresetId",
                        column: x => x.EnginePresetId,
                        principalTable: "EnginePresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FactionMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    FactionId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    NpcId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Role = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactionMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FactionMembers_Factions_FactionId",
                        column: x => x.FactionId,
                        principalTable: "Factions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Label = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldOptions_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    OwnerEntityId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    OwnerModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    TextValue = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    NumberValue = table.Column<double>(type: "BINARY_DOUBLE", nullable: true),
                    BooleanValue = table.Column<bool>(type: "BOOLEAN", nullable: true),
                    DateValue = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ReferenceValue = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    OptionId = table.Column<Guid>(type: "RAW(16)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldValues_FieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "FieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EffectAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameEffectId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ItemId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EffectAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EffectAssignments_GameEffects_GameEffectId",
                        column: x => x.GameEffectId,
                        principalTable: "GameEffects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventSpawns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    GameEventId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    NpcId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Count = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSpawns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventSpawns_GameEvents_GameEventId",
                        column: x => x.GameEventId,
                        principalTable: "GameEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LootEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    LootTableId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ItemId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Chance = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    MinQuantity = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MaxQuantity = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LootEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LootEntries_LootTables_LootTableId",
                        column: x => x.LootTableId,
                        principalTable: "LootTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MapLayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    MapId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Name = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    IsVisible = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapLayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapLayers_Maps_MapId",
                        column: x => x.MapId,
                        principalTable: "Maps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MapMarkers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    MapId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    X = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    Y = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    Radius = table.Column<double>(type: "BINARY_DOUBLE", nullable: true),
                    Points = table.Column<string>(type: "NVARCHAR2(2000)", nullable: true),
                    Label = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    TargetModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    IconAssetId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Color = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    LayerId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapMarkers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapMarkers_Maps_MapId",
                        column: x => x.MapId,
                        principalTable: "Maps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    NpcId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    OtherNpcId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    RelationTypeId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Stance = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcRelations_NpcRelationTypes_RelationTypeId",
                        column: x => x.RelationTypeId,
                        principalTable: "NpcRelationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpcRelations_Npcs_NpcId",
                        column: x => x.NpcId,
                        principalTable: "Npcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpawnRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    NpcId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    TargetMapId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    TargetMarkerId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    MinCount = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MaxCount = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    RespawnSeconds = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpawnRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpawnRules_Npcs_NpcId",
                        column: x => x.NpcId,
                        principalTable: "Npcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TraderOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    NpcId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ItemId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CurrencyId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    SellPrice = table.Column<double>(type: "BINARY_DOUBLE", nullable: true),
                    BuyPrice = table.Column<double>(type: "BINARY_DOUBLE", nullable: true),
                    Stock = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    RestockSeconds = table.Column<double>(type: "BINARY_DOUBLE", nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraderOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TraderOffers_Npcs_NpcId",
                        column: x => x.NpcId,
                        principalTable: "Npcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestObjectives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    QuestId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Text = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    IsOptional = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestObjectives_Quests_QuestId",
                        column: x => x.QuestId,
                        principalTable: "Quests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    RecipeId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ItemId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Quantity = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeOutputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    RecipeId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ItemId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Quantity = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeOutputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeOutputs_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    StoryEntryId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    TargetEntryId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Label = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryLinks_StoryEntries_StoryEntryId",
                        column: x => x.StoryEntryId,
                        principalTable: "StoryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    StoryEntryId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    TargetModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryParticipants_StoryEntries_StoryEntryId",
                        column: x => x.StoryEntryId,
                        principalTable: "StoryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KanbanCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    ColumnId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Title = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    Notes = table.Column<string>(type: "NCLOB", maxLength: 4000, nullable: true),
                    AssignedUserId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    DueDate = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    Color = table.Column<string>(type: "NVARCHAR2(9)", maxLength: 9, nullable: true),
                    Label = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    TargetModuleKey = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanbanCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KanbanCards_AppUsers_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_KanbanCards_KanbanColumns_ColumnId",
                        column: x => x.ColumnId,
                        principalTable: "KanbanColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DialogueChoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    DialogueLineId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Text = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    NextLineId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    SortOrder = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueChoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DialogueChoices_DialogueLines_DialogueLineId",
                        column: x => x.DialogueLineId,
                        principalTable: "DialogueLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_BasedOnId",
                table: "Achievements",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_ContentTypeId",
                table: "Achievements",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_GameProjectId",
                table: "Achievements",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_GameProjectId_Status",
                table: "Achievements",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_Name",
                table: "Achievements",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_AppUserId",
                table: "ApiKeys",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_GameProjectId",
                table: "ApiKeys",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_Prefix",
                table: "ApiKeys",
                column: "Prefix");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_ExternalId",
                table: "AppUsers",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_RoleId",
                table: "AppUsers",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_UserName",
                table: "AppUsers",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetRegions_AssetId",
                table: "AssetRegions",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_GameProjectId",
                table: "Assets",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_OwnerEntityId_IsPrimary",
                table: "Assets",
                columns: new[] { "OwnerEntityId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetTagAssignments_AssetTagId",
                table: "AssetTagAssignments",
                column: "AssetTagId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTags_GameProjectId_Name",
                table: "AssetTags",
                columns: new[] { "GameProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetVersions_AssetId",
                table: "AssetVersions",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogEntries_EntityId",
                table: "ChangeLogEntries",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogEntries_GameProjectId_AtUtc",
                table: "ChangeLogEntries",
                columns: new[] { "GameProjectId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClasses_BasedOnId",
                table: "CharacterClasses",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClasses_ContentTypeId",
                table: "CharacterClasses",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClasses_GameProjectId",
                table: "CharacterClasses",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClasses_GameProjectId_Status",
                table: "CharacterClasses",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClasses_Name",
                table: "CharacterClasses",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Collectibles_BasedOnId",
                table: "Collectibles",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Collectibles_ContentTypeId",
                table: "Collectibles",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Collectibles_GameProjectId",
                table: "Collectibles",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Collectibles_GameProjectId_Status",
                table: "Collectibles",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Collectibles_Name",
                table: "Collectibles",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CombatMappings_GameProjectId",
                table: "CombatMappings",
                column: "GameProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conditions_ConditionSetId",
                table: "Conditions",
                column: "ConditionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_Conditions_TargetEntityId",
                table: "Conditions",
                column: "TargetEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ConditionSets_GameProjectId",
                table: "ConditionSets",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ConditionSets_OwnerId_Slot",
                table: "ConditionSets",
                columns: new[] { "OwnerId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentComments_GameProjectId_ResolvedAtUtc",
                table: "ContentComments",
                columns: new[] { "GameProjectId", "ResolvedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentComments_OwnerEntityId",
                table: "ContentComments",
                column: "OwnerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLanguages_GameProjectId_Code",
                table: "ContentLanguages",
                columns: new[] { "GameProjectId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentRules_GameProjectId",
                table: "ContentRules",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTagAssignments_ContentTagId",
                table: "ContentTagAssignments",
                column: "ContentTagId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTagAssignments_TargetEntityId_ContentTagId",
                table: "ContentTagAssignments",
                columns: new[] { "TargetEntityId", "ContentTagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTags_GameProjectId_Name",
                table: "ContentTags",
                columns: new[] { "GameProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTagScopes_ContentTagId",
                table: "ContentTagScopes",
                column: "ContentTagId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTranslations_GameProjectId_LanguageCode",
                table: "ContentTranslations",
                columns: new[] { "GameProjectId", "LanguageCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentTranslations_OwnerEntityId_Slot_LanguageCode",
                table: "ContentTranslations",
                columns: new[] { "OwnerEntityId", "Slot", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypes_GameProjectId_ModuleKey",
                table: "ContentTypes",
                columns: new[] { "GameProjectId", "ModuleKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypes_ParentId",
                table: "ContentTypes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_BasedOnId",
                table: "Currencies",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_ContentTypeId",
                table: "Currencies",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_GameProjectId",
                table: "Currencies",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_GameProjectId_Status",
                table: "Currencies",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Name",
                table: "Currencies",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_BasedOnId",
                table: "Cutscenes",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_ContentTypeId",
                table: "Cutscenes",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_DialogueId",
                table: "Cutscenes",
                column: "DialogueId");

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_GameProjectId",
                table: "Cutscenes",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_GameProjectId_Status",
                table: "Cutscenes",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_Name",
                table: "Cutscenes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_StoryEntryId",
                table: "Cutscenes",
                column: "StoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CutsceneShots_CutsceneId",
                table: "CutsceneShots",
                column: "CutsceneId");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardCards_GameProjectId_CardKey",
                table: "DashboardCards",
                columns: new[] { "GameProjectId", "CardKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DialogueChoices_DialogueLineId",
                table: "DialogueChoices",
                column: "DialogueLineId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueChoices_NextLineId",
                table: "DialogueChoices",
                column: "NextLineId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueLines_DialogueId",
                table: "DialogueLines",
                column: "DialogueId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueLines_SpeakerNpcId",
                table: "DialogueLines",
                column: "SpeakerNpcId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueParticipants_DialogueId",
                table: "DialogueParticipants",
                column: "DialogueId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueParticipants_NpcId",
                table: "DialogueParticipants",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_BasedOnId",
                table: "Dialogues",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_ContentTypeId",
                table: "Dialogues",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_GameProjectId",
                table: "Dialogues",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_GameProjectId_Status",
                table: "Dialogues",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_Name",
                table: "Dialogues",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_BasedOnId",
                table: "DiplomaticRelations",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_ContentTypeId",
                table: "DiplomaticRelations",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_FactionAId",
                table: "DiplomaticRelations",
                column: "FactionAId");

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_FactionBId",
                table: "DiplomaticRelations",
                column: "FactionBId");

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_GameProjectId",
                table: "DiplomaticRelations",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_GameProjectId_Status",
                table: "DiplomaticRelations",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_Name",
                table: "DiplomaticRelations",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_EffectAssignments_GameEffectId",
                table: "EffectAssignments",
                column: "GameEffectId");

            migrationBuilder.CreateIndex(
                name: "IX_EffectAssignments_ItemId",
                table: "EffectAssignments",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EnginePresetMappings_EnginePresetId",
                table: "EnginePresetMappings",
                column: "EnginePresetId");

            migrationBuilder.CreateIndex(
                name: "IX_EnginePresets_ContentTypeId",
                table: "EnginePresets",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EnginePresets_GameProjectId",
                table: "EnginePresets",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSpawns_GameEventId",
                table: "EventSpawns",
                column: "GameEventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSpawns_NpcId",
                table: "EventSpawns",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportProfiles_GameProjectId",
                table: "ExportProfiles",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_FactionMembers_FactionId",
                table: "FactionMembers",
                column: "FactionId");

            migrationBuilder.CreateIndex(
                name: "IX_FactionMembers_NpcId",
                table: "FactionMembers",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_Factions_BasedOnId",
                table: "Factions",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Factions_ContentTypeId",
                table: "Factions",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Factions_GameProjectId",
                table: "Factions",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Factions_GameProjectId_Status",
                table: "Factions",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Factions_Name",
                table: "Factions",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_FieldDefinitions_ContentTypeId",
                table: "FieldDefinitions",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldDefinitions_OwnerEntityId",
                table: "FieldDefinitions",
                column: "OwnerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldOptions_FieldDefinitionId",
                table: "FieldOptions",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldValues_FieldDefinitionId",
                table: "FieldValues",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldValues_OwnerEntityId_FieldDefinitionId",
                table: "FieldValues",
                columns: new[] { "OwnerEntityId", "FieldDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FieldValues_ReferenceValue",
                table: "FieldValues",
                column: "ReferenceValue");

            migrationBuilder.CreateIndex(
                name: "IX_GameEffects_BasedOnId",
                table: "GameEffects",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEffects_ContentTypeId",
                table: "GameEffects",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEffects_GameProjectId",
                table: "GameEffects",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEffects_GameProjectId_Status",
                table: "GameEffects",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GameEffects_Name",
                table: "GameEffects",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_BasedOnId",
                table: "GameEvents",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_ContentTypeId",
                table: "GameEvents",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_GameProjectId",
                table: "GameEvents",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_GameProjectId_Status",
                table: "GameEvents",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_Name",
                table: "GameEvents",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_RewardLootTableId",
                table: "GameEvents",
                column: "RewardLootTableId");

            migrationBuilder.CreateIndex(
                name: "IX_HealthCheckMutes_GameProjectId_CheckKey_EntityId",
                table: "HealthCheckMutes",
                columns: new[] { "GameProjectId", "CheckKey", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_BasedOnId",
                table: "Items",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ContentTypeId",
                table: "Items",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_GameProjectId",
                table: "Items",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_GameProjectId_Status",
                table: "Items",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_Name",
                table: "Items",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_KanbanBoards_GameProjectId",
                table: "KanbanBoards",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_KanbanCards_AssignedUserId",
                table: "KanbanCards",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_KanbanCards_ColumnId",
                table: "KanbanCards",
                column: "ColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_KanbanCards_TargetEntityId",
                table: "KanbanCards",
                column: "TargetEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_KanbanColumns_BoardId",
                table: "KanbanColumns",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_LootEntries_ItemId",
                table: "LootEntries",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LootEntries_LootTableId",
                table: "LootEntries",
                column: "LootTableId");

            migrationBuilder.CreateIndex(
                name: "IX_LootTables_BasedOnId",
                table: "LootTables",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_LootTables_ContentTypeId",
                table: "LootTables",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LootTables_GameProjectId",
                table: "LootTables",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_LootTables_GameProjectId_Status",
                table: "LootTables",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LootTables_Name",
                table: "LootTables",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_MapLayers_MapId",
                table: "MapLayers",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_MapMarkers_MapId",
                table: "MapMarkers",
                column: "MapId");

            migrationBuilder.CreateIndex(
                name: "IX_MapMarkers_TargetEntityId",
                table: "MapMarkers",
                column: "TargetEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_BasedOnId",
                table: "Maps",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_ContentTypeId",
                table: "Maps",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_GameProjectId",
                table: "Maps",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_GameProjectId_Status",
                table: "Maps",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Maps_Name",
                table: "Maps",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleSettings_GameProjectId_ModuleKey",
                table: "ModuleSettings",
                columns: new[] { "GameProjectId", "ModuleKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpcRelations_NpcId",
                table: "NpcRelations",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcRelations_OtherNpcId",
                table: "NpcRelations",
                column: "OtherNpcId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcRelations_RelationTypeId",
                table: "NpcRelations",
                column: "RelationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcRelationTypes_GameProjectId",
                table: "NpcRelationTypes",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_BasedOnId",
                table: "Npcs",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_CharacterClassId",
                table: "Npcs",
                column: "CharacterClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_ContentTypeId",
                table: "Npcs",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_GameProjectId",
                table: "Npcs",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_GameProjectId_Kind",
                table: "Npcs",
                columns: new[] { "GameProjectId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_GameProjectId_Status",
                table: "Npcs",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_IsTrader",
                table: "Npcs",
                column: "IsTrader");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_LootTableId",
                table: "Npcs",
                column: "LootTableId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_Name",
                table: "Npcs",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCharacters_CharacterClassId",
                table: "PlayerCharacters",
                column: "CharacterClassId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCharacters_GameProjectId",
                table: "PlayerCharacters",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestObjectives_QuestId",
                table: "QuestObjectives",
                column: "QuestId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_BasedOnId",
                table: "Quests",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_ContentTypeId",
                table: "Quests",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_DialogueId",
                table: "Quests",
                column: "DialogueId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_GameProjectId",
                table: "Quests",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_GameProjectId_Kind",
                table: "Quests",
                columns: new[] { "GameProjectId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_Quests_GameProjectId_Status",
                table: "Quests",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Quests_GiverNpcId",
                table: "Quests",
                column: "GiverNpcId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_Name",
                table: "Quests",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_StoryEntryId",
                table: "Quests",
                column: "StoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Rarities_BasedOnId",
                table: "Rarities",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Rarities_ContentTypeId",
                table: "Rarities",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Rarities_GameProjectId",
                table: "Rarities",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Rarities_GameProjectId_Status",
                table: "Rarities",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Rarities_Name",
                table: "Rarities",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_ItemId",
                table: "RecipeIngredients",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_RecipeId",
                table: "RecipeIngredients",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeOutputs_ItemId",
                table: "RecipeOutputs",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeOutputs_RecipeId",
                table: "RecipeOutputs",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_BasedOnId",
                table: "Recipes",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ContentTypeId",
                table: "Recipes",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_GameProjectId",
                table: "Recipes",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_GameProjectId_Status",
                table: "Recipes",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_Name",
                table: "Recipes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RecycleBinEntries_EntityId",
                table: "RecycleBinEntries",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_RecycleBinEntries_GameProjectId_DeletedAtUtc",
                table: "RecycleBinEntries",
                columns: new[] { "GameProjectId", "DeletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRequests_AssignedUserId",
                table: "ReviewRequests",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRequests_GameProjectId_AssignedUserId_Decision",
                table: "ReviewRequests",
                columns: new[] { "GameProjectId", "AssignedUserId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRequests_OwnerEntityId",
                table: "ReviewRequests",
                column: "OwnerEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_AppUserId",
                table: "SavedViews",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_GameProjectId_AppUserId_ModuleKey_Name",
                table: "SavedViews",
                columns: new[] { "GameProjectId", "AppUserId", "ModuleKey", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_BasedOnId",
                table: "Skills",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_ContentTypeId",
                table: "Skills",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_CostItemId",
                table: "Skills",
                column: "CostItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_GameProjectId",
                table: "Skills",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_GameProjectId_Status",
                table: "Skills",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name",
                table: "Skills",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_ParentSkillId",
                table: "Skills",
                column: "ParentSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_SkillTreeId",
                table: "Skills",
                column: "SkillTreeId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillTrees_GameProjectId",
                table: "SkillTrees",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SoundEffects_BasedOnId",
                table: "SoundEffects",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_SoundEffects_ContentTypeId",
                table: "SoundEffects",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SoundEffects_GameProjectId",
                table: "SoundEffects",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SoundEffects_GameProjectId_Status",
                table: "SoundEffects",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SoundEffects_Name",
                table: "SoundEffects",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SpawnRules_NpcId",
                table: "SpawnRules",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_SpawnRules_TargetMapId",
                table: "SpawnRules",
                column: "TargetMapId");

            migrationBuilder.CreateIndex(
                name: "IX_SpawnRules_TargetMarkerId",
                table: "SpawnRules",
                column: "TargetMarkerId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_BasedOnId",
                table: "StoryEntries",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_ContentTypeId",
                table: "StoryEntries",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_GameProjectId",
                table: "StoryEntries",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_GameProjectId_SortOrder",
                table: "StoryEntries",
                columns: new[] { "GameProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_GameProjectId_Status",
                table: "StoryEntries",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_Name",
                table: "StoryEntries",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_TargetMapId",
                table: "StoryEntries",
                column: "TargetMapId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryLinks_StoryEntryId",
                table: "StoryLinks",
                column: "StoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryLinks_TargetEntryId",
                table: "StoryLinks",
                column: "TargetEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryParticipants_StoryEntryId",
                table: "StoryParticipants",
                column: "StoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryParticipants_TargetEntityId",
                table: "StoryParticipants",
                column: "TargetEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_TraderOffers_CurrencyId",
                table: "TraderOffers",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_TraderOffers_ItemId",
                table: "TraderOffers",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TraderOffers_NpcId",
                table: "TraderOffers",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPins_AppUserId_EntityId",
                table: "UserPins",
                columns: new[] { "AppUserId", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPins_AppUserId_GameProjectId",
                table: "UserPins",
                columns: new[] { "AppUserId", "GameProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPins_GameProjectId",
                table: "UserPins",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_Name",
                table: "UserRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Webhooks_GameProjectId",
                table: "Webhooks",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WhiteboardNotes_WhiteboardId",
                table: "WhiteboardNotes",
                column: "WhiteboardId");

            migrationBuilder.CreateIndex(
                name: "IX_Whiteboards_GameProjectId",
                table: "Whiteboards",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WhiteboardStrokes_WhiteboardId",
                table: "WhiteboardStrokes",
                column: "WhiteboardId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldStates_BasedOnId",
                table: "WorldStates",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldStates_ContentTypeId",
                table: "WorldStates",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldStates_GameProjectId",
                table: "WorldStates",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldStates_GameProjectId_Kind_SortOrder",
                table: "WorldStates",
                columns: new[] { "GameProjectId", "Kind", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WorldStates_GameProjectId_Status",
                table: "WorldStates",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorldStates_Name",
                table: "WorldStates",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Achievements");

            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "AssetRegions");

            migrationBuilder.DropTable(
                name: "AssetTagAssignments");

            migrationBuilder.DropTable(
                name: "AssetVersions");

            migrationBuilder.DropTable(
                name: "ChangeLogEntries");

            migrationBuilder.DropTable(
                name: "CharacterClasses");

            migrationBuilder.DropTable(
                name: "Collectibles");

            migrationBuilder.DropTable(
                name: "CombatMappings");

            migrationBuilder.DropTable(
                name: "Conditions");

            migrationBuilder.DropTable(
                name: "ContentComments");

            migrationBuilder.DropTable(
                name: "ContentLanguages");

            migrationBuilder.DropTable(
                name: "ContentRules");

            migrationBuilder.DropTable(
                name: "ContentTagAssignments");

            migrationBuilder.DropTable(
                name: "ContentTagScopes");

            migrationBuilder.DropTable(
                name: "ContentTranslations");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropTable(
                name: "CutsceneShots");

            migrationBuilder.DropTable(
                name: "DashboardCards");

            migrationBuilder.DropTable(
                name: "DialogueChoices");

            migrationBuilder.DropTable(
                name: "DialogueParticipants");

            migrationBuilder.DropTable(
                name: "DiplomaticRelations");

            migrationBuilder.DropTable(
                name: "EffectAssignments");

            migrationBuilder.DropTable(
                name: "EnginePresetMappings");

            migrationBuilder.DropTable(
                name: "EventSpawns");

            migrationBuilder.DropTable(
                name: "ExportProfiles");

            migrationBuilder.DropTable(
                name: "FactionMembers");

            migrationBuilder.DropTable(
                name: "FieldOptions");

            migrationBuilder.DropTable(
                name: "FieldValues");

            migrationBuilder.DropTable(
                name: "HealthCheckMutes");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "KanbanCards");

            migrationBuilder.DropTable(
                name: "LootEntries");

            migrationBuilder.DropTable(
                name: "MapLayers");

            migrationBuilder.DropTable(
                name: "MapMarkers");

            migrationBuilder.DropTable(
                name: "ModuleSettings");

            migrationBuilder.DropTable(
                name: "NpcRelations");

            migrationBuilder.DropTable(
                name: "PlayerCharacters");

            migrationBuilder.DropTable(
                name: "QuestObjectives");

            migrationBuilder.DropTable(
                name: "Rarities");

            migrationBuilder.DropTable(
                name: "RecipeIngredients");

            migrationBuilder.DropTable(
                name: "RecipeOutputs");

            migrationBuilder.DropTable(
                name: "RecycleBinEntries");

            migrationBuilder.DropTable(
                name: "ReviewRequests");

            migrationBuilder.DropTable(
                name: "SavedViews");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "SkillTrees");

            migrationBuilder.DropTable(
                name: "SoundEffects");

            migrationBuilder.DropTable(
                name: "SpawnRules");

            migrationBuilder.DropTable(
                name: "StoryLinks");

            migrationBuilder.DropTable(
                name: "StoryParticipants");

            migrationBuilder.DropTable(
                name: "TraderOffers");

            migrationBuilder.DropTable(
                name: "UserPins");

            migrationBuilder.DropTable(
                name: "Webhooks");

            migrationBuilder.DropTable(
                name: "WhiteboardNotes");

            migrationBuilder.DropTable(
                name: "WhiteboardStrokes");

            migrationBuilder.DropTable(
                name: "WorldStates");

            migrationBuilder.DropTable(
                name: "AssetTags");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "ConditionSets");

            migrationBuilder.DropTable(
                name: "ContentTags");

            migrationBuilder.DropTable(
                name: "Cutscenes");

            migrationBuilder.DropTable(
                name: "DialogueLines");

            migrationBuilder.DropTable(
                name: "GameEffects");

            migrationBuilder.DropTable(
                name: "EnginePresets");

            migrationBuilder.DropTable(
                name: "GameEvents");

            migrationBuilder.DropTable(
                name: "Factions");

            migrationBuilder.DropTable(
                name: "FieldDefinitions");

            migrationBuilder.DropTable(
                name: "KanbanColumns");

            migrationBuilder.DropTable(
                name: "LootTables");

            migrationBuilder.DropTable(
                name: "Maps");

            migrationBuilder.DropTable(
                name: "NpcRelationTypes");

            migrationBuilder.DropTable(
                name: "Quests");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "StoryEntries");

            migrationBuilder.DropTable(
                name: "Npcs");

            migrationBuilder.DropTable(
                name: "AppUsers");

            migrationBuilder.DropTable(
                name: "Whiteboards");

            migrationBuilder.DropTable(
                name: "Dialogues");

            migrationBuilder.DropTable(
                name: "KanbanBoards");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "ContentTypes");

            migrationBuilder.DropTable(
                name: "GameProjects");
        }
    }
}
