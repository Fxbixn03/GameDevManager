using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class EntityVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "WorldStates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "StoryEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "SoundEffects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "Skills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "Recipes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "Rarities",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "Quests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "Npcs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "Maps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "LootTables",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "Items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "GameEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "GameEffects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "Factions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "DiplomaticRelations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "Dialogues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "Cutscenes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "Currencies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "Collectibles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "CharacterClasses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnId",
                table: "Achievements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorldStates_BasedOnId",
                table: "WorldStates",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_BasedOnId",
                table: "StoryEntries",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_SoundEffects_BasedOnId",
                table: "SoundEffects",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_BasedOnId",
                table: "Skills",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_BasedOnId",
                table: "Recipes",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Rarities_BasedOnId",
                table: "Rarities",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_BasedOnId",
                table: "Quests",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_BasedOnId",
                table: "Npcs",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_BasedOnId",
                table: "Maps",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_LootTables_BasedOnId",
                table: "LootTables",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_BasedOnId",
                table: "Items",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_BasedOnId",
                table: "GameEvents",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEffects_BasedOnId",
                table: "GameEffects",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Factions_BasedOnId",
                table: "Factions",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_BasedOnId",
                table: "DiplomaticRelations",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_BasedOnId",
                table: "Dialogues",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_BasedOnId",
                table: "Cutscenes",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_BasedOnId",
                table: "Currencies",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Collectibles_BasedOnId",
                table: "Collectibles",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClasses_BasedOnId",
                table: "CharacterClasses",
                column: "BasedOnId");

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_BasedOnId",
                table: "Achievements",
                column: "BasedOnId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorldStates_BasedOnId",
                table: "WorldStates");

            migrationBuilder.DropIndex(
                name: "IX_StoryEntries_BasedOnId",
                table: "StoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_SoundEffects_BasedOnId",
                table: "SoundEffects");

            migrationBuilder.DropIndex(
                name: "IX_Skills_BasedOnId",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_BasedOnId",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Rarities_BasedOnId",
                table: "Rarities");

            migrationBuilder.DropIndex(
                name: "IX_Quests_BasedOnId",
                table: "Quests");

            migrationBuilder.DropIndex(
                name: "IX_Npcs_BasedOnId",
                table: "Npcs");

            migrationBuilder.DropIndex(
                name: "IX_Maps_BasedOnId",
                table: "Maps");

            migrationBuilder.DropIndex(
                name: "IX_LootTables_BasedOnId",
                table: "LootTables");

            migrationBuilder.DropIndex(
                name: "IX_Items_BasedOnId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_GameEvents_BasedOnId",
                table: "GameEvents");

            migrationBuilder.DropIndex(
                name: "IX_GameEffects_BasedOnId",
                table: "GameEffects");

            migrationBuilder.DropIndex(
                name: "IX_Factions_BasedOnId",
                table: "Factions");

            migrationBuilder.DropIndex(
                name: "IX_DiplomaticRelations_BasedOnId",
                table: "DiplomaticRelations");

            migrationBuilder.DropIndex(
                name: "IX_Dialogues_BasedOnId",
                table: "Dialogues");

            migrationBuilder.DropIndex(
                name: "IX_Cutscenes_BasedOnId",
                table: "Cutscenes");

            migrationBuilder.DropIndex(
                name: "IX_Currencies_BasedOnId",
                table: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_Collectibles_BasedOnId",
                table: "Collectibles");

            migrationBuilder.DropIndex(
                name: "IX_CharacterClasses_BasedOnId",
                table: "CharacterClasses");

            migrationBuilder.DropIndex(
                name: "IX_Achievements_BasedOnId",
                table: "Achievements");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "WorldStates");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "StoryEntries");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "SoundEffects");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "Rarities");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "Quests");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "Maps");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "LootTables");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "GameEvents");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "GameEffects");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "Factions");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "DiplomaticRelations");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "Dialogues");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "Cutscenes");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "Collectibles");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "CharacterClasses");

            migrationBuilder.DropColumn(
                name: "BasedOnId",
                table: "Achievements");
        }
    }
}
