using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMexicanStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mexican_states",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mexican_states", x => x.code);
                });

            // The 32 official INEGI entidades federativas — real, static reference data.
            migrationBuilder.InsertData(
                table: "mexican_states",
                columns: new[] { "code", "name" },
                values: new object[,]
                {
                    { "01", "Aguascalientes" },
                    { "02", "Baja California" },
                    { "03", "Baja California Sur" },
                    { "04", "Campeche" },
                    { "05", "Coahuila" },
                    { "06", "Colima" },
                    { "07", "Chiapas" },
                    { "08", "Chihuahua" },
                    { "09", "Ciudad de México" },
                    { "10", "Durango" },
                    { "11", "Guanajuato" },
                    { "12", "Guerrero" },
                    { "13", "Hidalgo" },
                    { "14", "Jalisco" },
                    { "15", "México" },
                    { "16", "Michoacán" },
                    { "17", "Morelos" },
                    { "18", "Nayarit" },
                    { "19", "Nuevo León" },
                    { "20", "Oaxaca" },
                    { "21", "Puebla" },
                    { "22", "Querétaro" },
                    { "23", "Quintana Roo" },
                    { "24", "San Luis Potosí" },
                    { "25", "Sinaloa" },
                    { "26", "Sonora" },
                    { "27", "Tabasco" },
                    { "28", "Tamaulipas" },
                    { "29", "Tlaxcala" },
                    { "30", "Veracruz" },
                    { "31", "Yucatán" },
                    { "32", "Zacatecas" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mexican_states");
        }
    }
}
