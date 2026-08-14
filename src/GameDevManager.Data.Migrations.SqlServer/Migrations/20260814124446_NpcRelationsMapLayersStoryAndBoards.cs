using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class NpcRelationsMapLayersStoryAndBoards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Duration",
                table: "StoryEntries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameDate",
                table: "StoryEntries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "StoryEntries",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mood",
                table: "StoryEntries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetMapId",
                table: "StoryEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetMapMarkerId",
                table: "StoryEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnique",
                table: "Npcs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Personality",
                table: "Npcs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Preferences",
                table: "Npcs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Traits",
                table: "Npcs",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LayerId",
                table: "MapMarkers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KanbanBoards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                name: "MapLayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MapId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
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
                name: "NpcRelationTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InverseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
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
                name: "StoryLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoryEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
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
                name: "Whiteboards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                name: "KanbanColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
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
                name: "NpcRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NpcId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OtherNpcId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stance = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
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
                name: "WhiteboardNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WhiteboardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    X = table.Column<double>(type: "float", nullable: false),
                    Y = table.Column<double>(type: "float", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WhiteboardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Points = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Width = table.Column<double>(type: "float", nullable: false)
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
                name: "KanbanCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColumnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanbanCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KanbanCards_KanbanColumns_ColumnId",
                        column: x => x.ColumnId,
                        principalTable: "KanbanColumns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_TargetMapId",
                table: "StoryEntries",
                column: "TargetMapId");

            migrationBuilder.CreateIndex(
                name: "IX_KanbanBoards_GameProjectId",
                table: "KanbanBoards",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_KanbanCards_ColumnId",
                table: "KanbanCards",
                column: "ColumnId");

            migrationBuilder.CreateIndex(
                name: "IX_KanbanColumns_BoardId",
                table: "KanbanColumns",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_MapLayers_MapId",
                table: "MapLayers",
                column: "MapId");

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
                name: "IX_StoryLinks_StoryEntryId",
                table: "StoryLinks",
                column: "StoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryLinks_TargetEntryId",
                table: "StoryLinks",
                column: "TargetEntryId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KanbanCards");

            migrationBuilder.DropTable(
                name: "MapLayers");

            migrationBuilder.DropTable(
                name: "NpcRelations");

            migrationBuilder.DropTable(
                name: "StoryLinks");

            migrationBuilder.DropTable(
                name: "WhiteboardNotes");

            migrationBuilder.DropTable(
                name: "WhiteboardStrokes");

            migrationBuilder.DropTable(
                name: "KanbanColumns");

            migrationBuilder.DropTable(
                name: "NpcRelationTypes");

            migrationBuilder.DropTable(
                name: "Whiteboards");

            migrationBuilder.DropTable(
                name: "KanbanBoards");

            migrationBuilder.DropIndex(
                name: "IX_StoryEntries_TargetMapId",
                table: "StoryEntries");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "StoryEntries");

            migrationBuilder.DropColumn(
                name: "GameDate",
                table: "StoryEntries");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "StoryEntries");

            migrationBuilder.DropColumn(
                name: "Mood",
                table: "StoryEntries");

            migrationBuilder.DropColumn(
                name: "TargetMapId",
                table: "StoryEntries");

            migrationBuilder.DropColumn(
                name: "TargetMapMarkerId",
                table: "StoryEntries");

            migrationBuilder.DropColumn(
                name: "IsUnique",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "Personality",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "Preferences",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "Traits",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "LayerId",
                table: "MapMarkers");
        }
    }
}
