using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lotplapp.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlotOwnerForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Plots_AspNetUsers_OwnerId",
                table: "Plots",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Plots_AspNetUsers_OwnerId",
                table: "Plots");
        }
    }
}
