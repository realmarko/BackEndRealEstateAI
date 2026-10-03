using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSalePipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pipeline_stages",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    requires_legal_review = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pipeline_stages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pipeline_stage_document_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    stage_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pipeline_stage_document_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_pipeline_stage_document_templates_pipeline_stages_stage_id",
                        column: x => x.stage_id,
                        principalTable: "pipeline_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sale_processes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    client_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    property_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    estimated_price = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    current_stage_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_processes", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_processes_listings_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_sale_processes_pipeline_stages_current_stage_id",
                        column: x => x.current_stage_id,
                        principalTable: "pipeline_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_processes_users_agent_user_id",
                        column: x => x.agent_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sale_process_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_process_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_process_documents_pipeline_stages_stage_id",
                        column: x => x.stage_id,
                        principalTable: "pipeline_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_process_documents_sale_processes_sale_process_id",
                        column: x => x.sale_process_id,
                        principalTable: "sale_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sale_process_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_process_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_process_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_sale_process_tasks_sale_processes_sale_process_id",
                        column: x => x.sale_process_id,
                        principalTable: "sale_processes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "pipeline_stages",
                columns: new[] { "id", "description", "name", "requires_legal_review", "sort_order" },
                values: new object[,]
                {
                    { 1, "Captación del cliente", "Prospección", false, 1 },
                    { 2, "Legal / propiedad", "Revisión documental", false, 2 },
                    { 3, "Firma con el vendedor", "Contrato de exclusividad", false, 3 },
                    { 4, "Validación legal exclusividad", "Revisión de contrato (1)", true, 4 },
                    { 5, "Marketing activo", "Promoción y visitas", false, 5 },
                    { 6, "Oferta del comprador", "Negociación y oferta", false, 6 },
                    { 7, "Compraventa / notaría", "Revisión de contrato (2)", true, 7 },
                    { 8, "Firma final", "Cierre y escrituración", false, 8 },
                    { 9, "Entrega y seguimiento", "Post-venta", false, 9 }
                });

            migrationBuilder.InsertData(
                table: "pipeline_stage_document_templates",
                columns: new[] { "id", "name", "sort_order", "stage_id" },
                values: new object[,]
                {
                    { 1, "Identificación oficial del propietario", 1, 1 },
                    { 2, "Comprobante de domicilio", 2, 1 },
                    { 3, "Ficha de datos del inmueble", 3, 1 },
                    { 4, "Escritura pública inscrita", 1, 2 },
                    { 5, "Boleta predial al corriente", 2, 2 },
                    { 6, "Certificado de libertad de gravamen", 3, 2 },
                    { 7, "Comprobante de servicios (agua/luz)", 4, 2 },
                    { 8, "Contrato de exclusividad firmado", 1, 3 },
                    { 9, "Identificación de quien firma", 2, 3 },
                    { 10, "Formato KYC / prevención de lavado", 3, 3 },
                    { 11, "Dictamen del abogado sobre el contrato de exclusividad", 1, 4 },
                    { 12, "Acta de revisión de cláusulas", 2, 4 },
                    { 13, "Ficha técnica comercial", 1, 5 },
                    { 14, "Fotografías / video del inmueble", 2, 5 },
                    { 15, "Autorización de publicación", 3, 5 },
                    { 16, "Carta de oferta del comprador", 1, 6 },
                    { 17, "Comprobante de solvencia / precalificación crédito", 2, 6 },
                    { 18, "Minuta de compraventa revisada por notaría", 1, 7 },
                    { 19, "Dictamen del abogado sobre el contrato de compraventa", 2, 7 },
                    { 20, "Carta saldo / cancelación de hipoteca (si aplica)", 3, 7 },
                    { 21, "Avalúo comercial oficial", 1, 8 },
                    { 22, "Hoja de retención ISR", 2, 8 },
                    { 23, "Escritura de compraventa firmada", 3, 8 },
                    { 24, "Acta de entrega-recepción", 1, 9 },
                    { 25, "Carta garantía / finiquito", 2, 9 },
                    { 26, "Encuesta de satisfacción", 3, 9 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_stage_document_templates_stage_id_name",
                table: "pipeline_stage_document_templates",
                columns: new[] { "stage_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_stages_sort_order",
                table: "pipeline_stages",
                column: "sort_order",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sale_process_documents_sale_process_id",
                table: "sale_process_documents",
                column: "sale_process_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_process_documents_stage_id",
                table: "sale_process_documents",
                column: "stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_process_tasks_sale_process_id",
                table: "sale_process_tasks",
                column: "sale_process_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_processes_agent_user_id",
                table: "sale_processes",
                column: "agent_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_processes_current_stage_id",
                table: "sale_processes",
                column: "current_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_processes_listing_id",
                table: "sale_processes",
                column: "listing_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pipeline_stage_document_templates");

            migrationBuilder.DropTable(
                name: "sale_process_documents");

            migrationBuilder.DropTable(
                name: "sale_process_tasks");

            migrationBuilder.DropTable(
                name: "sale_processes");

            migrationBuilder.DropTable(
                name: "pipeline_stages");
        }
    }
}
