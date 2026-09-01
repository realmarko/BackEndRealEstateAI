using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations.RealEstate
{
    /// <inheritdoc />
    public partial class SnakeCaseNaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Properties_Agents_AgentId",
                table: "Properties");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyAmenities_Amenities_AmenitiesId",
                table: "PropertyAmenities");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyAmenities_Properties_PropertiesId",
                table: "PropertyAmenities");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyImages_Properties_PropertyId",
                table: "PropertyImages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PropertyAmenities",
                table: "PropertyAmenities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Properties",
                table: "Properties");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Amenities",
                table: "Amenities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Agents",
                table: "Agents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PropertyImages",
                table: "PropertyImages");

            migrationBuilder.RenameTable(
                name: "Properties",
                newName: "properties");

            migrationBuilder.RenameTable(
                name: "Amenities",
                newName: "amenities");

            migrationBuilder.RenameTable(
                name: "Agents",
                newName: "agents");

            migrationBuilder.RenameTable(
                name: "PropertyImages",
                newName: "property_images");

            migrationBuilder.RenameColumn(
                name: "PropertiesId",
                table: "PropertyAmenities",
                newName: "properties_id");

            migrationBuilder.RenameColumn(
                name: "AmenitiesId",
                table: "PropertyAmenities",
                newName: "amenities_id");

            migrationBuilder.RenameIndex(
                name: "IX_PropertyAmenities_PropertiesId",
                table: "PropertyAmenities",
                newName: "ix_property_amenities_properties_id");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "properties",
                newName: "type");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "properties",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "properties",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "State",
                table: "properties",
                newName: "state");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "properties",
                newName: "price");

            migrationBuilder.RenameColumn(
                name: "Operation",
                table: "properties",
                newName: "operation");

            migrationBuilder.RenameColumn(
                name: "Neighborhood",
                table: "properties",
                newName: "neighborhood");

            migrationBuilder.RenameColumn(
                name: "Longitude",
                table: "properties",
                newName: "longitude");

            migrationBuilder.RenameColumn(
                name: "Latitude",
                table: "properties",
                newName: "latitude");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "properties",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Currency",
                table: "properties",
                newName: "currency");

            migrationBuilder.RenameColumn(
                name: "City",
                table: "properties",
                newName: "city");

            migrationBuilder.RenameColumn(
                name: "Bedrooms",
                table: "properties",
                newName: "bedrooms");

            migrationBuilder.RenameColumn(
                name: "Bathrooms",
                table: "properties",
                newName: "bathrooms");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "properties",
                newName: "address");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "properties",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "YearBuilt",
                table: "properties",
                newName: "year_built");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "properties",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "SourceUrl",
                table: "properties",
                newName: "source_url");

            migrationBuilder.RenameColumn(
                name: "ParkingSpots",
                table: "properties",
                newName: "parking_spots");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "properties",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ConstructionM2",
                table: "properties",
                newName: "construction_m2");

            migrationBuilder.RenameColumn(
                name: "AreaM2",
                table: "properties",
                newName: "area_m2");

            migrationBuilder.RenameColumn(
                name: "AgentId",
                table: "properties",
                newName: "agent_id");

            migrationBuilder.RenameIndex(
                name: "IX_Properties_Latitude_Longitude",
                table: "properties",
                newName: "ix_properties_latitude_longitude");

            migrationBuilder.RenameIndex(
                name: "IX_Properties_City",
                table: "properties",
                newName: "ix_properties_city");

            migrationBuilder.RenameIndex(
                name: "IX_Properties_AgentId",
                table: "properties",
                newName: "ix_properties_agent_id");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "amenities",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "amenities",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "agents",
                newName: "phone");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "agents",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "agents",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "agents",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "Url",
                table: "property_images",
                newName: "url");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "property_images",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "SortOrder",
                table: "property_images",
                newName: "sort_order");

            migrationBuilder.RenameColumn(
                name: "PropertyId",
                table: "property_images",
                newName: "property_id");

            migrationBuilder.RenameColumn(
                name: "IsPrimary",
                table: "property_images",
                newName: "is_primary");

            migrationBuilder.RenameIndex(
                name: "IX_PropertyImages_PropertyId",
                table: "property_images",
                newName: "ix_property_images_property_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_property_amenities",
                table: "PropertyAmenities",
                columns: new[] { "amenities_id", "properties_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_properties",
                table: "properties",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_amenities",
                table: "amenities",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_agents",
                table: "agents",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_property_images",
                table: "property_images",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_properties_agents_agent_id",
                table: "properties",
                column: "agent_id",
                principalTable: "agents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_property_images_properties_property_id",
                table: "property_images",
                column: "property_id",
                principalTable: "properties",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_property_amenities_amenities_amenities_id",
                table: "PropertyAmenities",
                column: "amenities_id",
                principalTable: "amenities",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_property_amenities_properties_properties_id",
                table: "PropertyAmenities",
                column: "properties_id",
                principalTable: "properties",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_properties_agents_agent_id",
                table: "properties");

            migrationBuilder.DropForeignKey(
                name: "fk_property_images_properties_property_id",
                table: "property_images");

            migrationBuilder.DropForeignKey(
                name: "fk_property_amenities_amenities_amenities_id",
                table: "PropertyAmenities");

            migrationBuilder.DropForeignKey(
                name: "fk_property_amenities_properties_properties_id",
                table: "PropertyAmenities");

            migrationBuilder.DropPrimaryKey(
                name: "pk_property_amenities",
                table: "PropertyAmenities");

            migrationBuilder.DropPrimaryKey(
                name: "pk_properties",
                table: "properties");

            migrationBuilder.DropPrimaryKey(
                name: "pk_amenities",
                table: "amenities");

            migrationBuilder.DropPrimaryKey(
                name: "pk_agents",
                table: "agents");

            migrationBuilder.DropPrimaryKey(
                name: "pk_property_images",
                table: "property_images");

            migrationBuilder.RenameTable(
                name: "properties",
                newName: "Properties");

            migrationBuilder.RenameTable(
                name: "amenities",
                newName: "Amenities");

            migrationBuilder.RenameTable(
                name: "agents",
                newName: "Agents");

            migrationBuilder.RenameTable(
                name: "property_images",
                newName: "PropertyImages");

            migrationBuilder.RenameColumn(
                name: "properties_id",
                table: "PropertyAmenities",
                newName: "PropertiesId");

            migrationBuilder.RenameColumn(
                name: "amenities_id",
                table: "PropertyAmenities",
                newName: "AmenitiesId");

            migrationBuilder.RenameIndex(
                name: "ix_property_amenities_properties_id",
                table: "PropertyAmenities",
                newName: "IX_PropertyAmenities_PropertiesId");

            migrationBuilder.RenameColumn(
                name: "type",
                table: "Properties",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "Properties",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "Properties",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "state",
                table: "Properties",
                newName: "State");

            migrationBuilder.RenameColumn(
                name: "price",
                table: "Properties",
                newName: "Price");

            migrationBuilder.RenameColumn(
                name: "operation",
                table: "Properties",
                newName: "Operation");

            migrationBuilder.RenameColumn(
                name: "neighborhood",
                table: "Properties",
                newName: "Neighborhood");

            migrationBuilder.RenameColumn(
                name: "longitude",
                table: "Properties",
                newName: "Longitude");

            migrationBuilder.RenameColumn(
                name: "latitude",
                table: "Properties",
                newName: "Latitude");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "Properties",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "currency",
                table: "Properties",
                newName: "Currency");

            migrationBuilder.RenameColumn(
                name: "city",
                table: "Properties",
                newName: "City");

            migrationBuilder.RenameColumn(
                name: "bedrooms",
                table: "Properties",
                newName: "Bedrooms");

            migrationBuilder.RenameColumn(
                name: "bathrooms",
                table: "Properties",
                newName: "Bathrooms");

            migrationBuilder.RenameColumn(
                name: "address",
                table: "Properties",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Properties",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "year_built",
                table: "Properties",
                newName: "YearBuilt");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Properties",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "source_url",
                table: "Properties",
                newName: "SourceUrl");

            migrationBuilder.RenameColumn(
                name: "parking_spots",
                table: "Properties",
                newName: "ParkingSpots");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Properties",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "construction_m2",
                table: "Properties",
                newName: "ConstructionM2");

            migrationBuilder.RenameColumn(
                name: "area_m2",
                table: "Properties",
                newName: "AreaM2");

            migrationBuilder.RenameColumn(
                name: "agent_id",
                table: "Properties",
                newName: "AgentId");

            migrationBuilder.RenameIndex(
                name: "ix_properties_latitude_longitude",
                table: "Properties",
                newName: "IX_Properties_Latitude_Longitude");

            migrationBuilder.RenameIndex(
                name: "ix_properties_city",
                table: "Properties",
                newName: "IX_Properties_City");

            migrationBuilder.RenameIndex(
                name: "ix_properties_agent_id",
                table: "Properties",
                newName: "IX_Properties_AgentId");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "Amenities",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Amenities",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "phone",
                table: "Agents",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "Agents",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "Agents",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Agents",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "url",
                table: "PropertyImages",
                newName: "Url");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "PropertyImages",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "sort_order",
                table: "PropertyImages",
                newName: "SortOrder");

            migrationBuilder.RenameColumn(
                name: "property_id",
                table: "PropertyImages",
                newName: "PropertyId");

            migrationBuilder.RenameColumn(
                name: "is_primary",
                table: "PropertyImages",
                newName: "IsPrimary");

            migrationBuilder.RenameIndex(
                name: "ix_property_images_property_id",
                table: "PropertyImages",
                newName: "IX_PropertyImages_PropertyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PropertyAmenities",
                table: "PropertyAmenities",
                columns: new[] { "AmenitiesId", "PropertiesId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Properties",
                table: "Properties",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Amenities",
                table: "Amenities",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Agents",
                table: "Agents",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PropertyImages",
                table: "PropertyImages",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Properties_Agents_AgentId",
                table: "Properties",
                column: "AgentId",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyAmenities_Amenities_AmenitiesId",
                table: "PropertyAmenities",
                column: "AmenitiesId",
                principalTable: "Amenities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyAmenities_Properties_PropertiesId",
                table: "PropertyAmenities",
                column: "PropertiesId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyImages_Properties_PropertyId",
                table: "PropertyImages",
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
