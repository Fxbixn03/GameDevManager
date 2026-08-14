using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.Sqlite.Migrations
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
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameDate",
                table: "StoryEntries",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "StoryEntries",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mood",
                table: "StoryEntries",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetMapId",
                table: "StoryEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetMapMarkerId",
                table: "StoryEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnique",
                table: "Npcs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Personality",
                table: "Npcs",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Preferences",
                table: "Npcs",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Traits",
                table: "Npcs",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LayerId",
                table: "MapMarkers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KanbanBoards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MapId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    InverseName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StoryEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BoardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NpcId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OtherNpcId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RelationTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Stance = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WhiteboardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: false),
                    Y = table.Column<double>(type: "REAL", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true)
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WhiteboardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Points = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Width = table.Column<double>(type: "REAL", nullable: false)
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ColumnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
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
