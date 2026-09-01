using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealEstate.Api.Migrations
{
    /// <inheritdoc />
    public partial class SnakeCaseNaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Favorites_AspNetUsers_UserId",
                table: "Favorites");

            migrationBuilder.DropForeignKey(
                name: "FK_Favorites_Listings_ListingId",
                table: "Favorites");

            migrationBuilder.DropForeignKey(
                name: "FK_Inquiries_AspNetUsers_SenderId",
                table: "Inquiries");

            migrationBuilder.DropForeignKey(
                name: "FK_Inquiries_Listings_ListingId",
                table: "Inquiries");

            migrationBuilder.DropForeignKey(
                name: "FK_ListingImages_Listings_ListingId",
                table: "ListingImages");

            migrationBuilder.DropForeignKey(
                name: "FK_Listings_AspNetUsers_OwnerId",
                table: "Listings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Listings",
                table: "Listings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Inquiries",
                table: "Inquiries");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Favorites",
                table: "Favorites");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserTokens",
                table: "AspNetUserTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUsers",
                table: "AspNetUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserRoles",
                table: "AspNetUserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserLogins",
                table: "AspNetUserLogins");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetUserClaims",
                table: "AspNetUserClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetRoles",
                table: "AspNetRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetRoleClaims",
                table: "AspNetRoleClaims");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ListingImages",
                table: "ListingImages");

            migrationBuilder.RenameTable(
                name: "Listings",
                newName: "listings");

            migrationBuilder.RenameTable(
                name: "Inquiries",
                newName: "inquiries");

            migrationBuilder.RenameTable(
                name: "Favorites",
                newName: "favorites");

            migrationBuilder.RenameTable(
                name: "ListingImages",
                newName: "listing_images");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "listings",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "listings",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "State",
                table: "listings",
                newName: "state");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "listings",
                newName: "price");

            migrationBuilder.RenameColumn(
                name: "Longitude",
                table: "listings",
                newName: "longitude");

            migrationBuilder.RenameColumn(
                name: "Latitude",
                table: "listings",
                newName: "latitude");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "listings",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "City",
                table: "listings",
                newName: "city");

            migrationBuilder.RenameColumn(
                name: "Bedrooms",
                table: "listings",
                newName: "bedrooms");

            migrationBuilder.RenameColumn(
                name: "Bathrooms",
                table: "listings",
                newName: "bathrooms");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "listings",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "ZipCode",
                table: "listings",
                newName: "zip_code");

            migrationBuilder.RenameColumn(
                name: "YearBuilt",
                table: "listings",
                newName: "year_built");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "listings",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "PropertyType",
                table: "listings",
                newName: "property_type");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "listings",
                newName: "owner_id");

            migrationBuilder.RenameColumn(
                name: "ListingType",
                table: "listings",
                newName: "listing_type");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "listings",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "AreaSqFt",
                table: "listings",
                newName: "area_sq_ft");

            migrationBuilder.RenameColumn(
                name: "AddressLine",
                table: "listings",
                newName: "address_line");

            migrationBuilder.RenameIndex(
                name: "IX_Listings_Latitude_Longitude",
                table: "listings",
                newName: "ix_listings_latitude_longitude");

            migrationBuilder.RenameIndex(
                name: "IX_Listings_City",
                table: "listings",
                newName: "ix_listings_city");

            migrationBuilder.RenameIndex(
                name: "IX_Listings_OwnerId",
                table: "listings",
                newName: "ix_listings_owner_id");

            migrationBuilder.RenameColumn(
                name: "Message",
                table: "inquiries",
                newName: "message");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "inquiries",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "SenderPhone",
                table: "inquiries",
                newName: "sender_phone");

            migrationBuilder.RenameColumn(
                name: "SenderName",
                table: "inquiries",
                newName: "sender_name");

            migrationBuilder.RenameColumn(
                name: "SenderId",
                table: "inquiries",
                newName: "sender_id");

            migrationBuilder.RenameColumn(
                name: "SenderEmail",
                table: "inquiries",
                newName: "sender_email");

            migrationBuilder.RenameColumn(
                name: "ListingId",
                table: "inquiries",
                newName: "listing_id");

            migrationBuilder.RenameColumn(
                name: "IsRead",
                table: "inquiries",
                newName: "is_read");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "inquiries",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_Inquiries_SenderId",
                table: "inquiries",
                newName: "ix_inquiries_sender_id");

            migrationBuilder.RenameIndex(
                name: "IX_Inquiries_ListingId",
                table: "inquiries",
                newName: "ix_inquiries_listing_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "favorites",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "favorites",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "ListingId",
                table: "favorites",
                newName: "listing_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "favorites",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_Favorites_UserId_ListingId",
                table: "favorites",
                newName: "ix_favorites_user_id_listing_id");

            migrationBuilder.RenameIndex(
                name: "IX_Favorites_ListingId",
                table: "favorites",
                newName: "ix_favorites_listing_id");

            migrationBuilder.RenameColumn(
                name: "Value",
                table: "AspNetUserTokens",
                newName: "value");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "AspNetUserTokens",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "LoginProvider",
                table: "AspNetUserTokens",
                newName: "login_provider");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "AspNetUserTokens",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "AspNetUsers",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "AspNetUsers",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserName",
                table: "AspNetUsers",
                newName: "user_name");

            migrationBuilder.RenameColumn(
                name: "TwoFactorEnabled",
                table: "AspNetUsers",
                newName: "two_factor_enabled");

            migrationBuilder.RenameColumn(
                name: "SecurityStamp",
                table: "AspNetUsers",
                newName: "security_stamp");

            migrationBuilder.RenameColumn(
                name: "PhoneNumberConfirmed",
                table: "AspNetUsers",
                newName: "phone_number_confirmed");

            migrationBuilder.RenameColumn(
                name: "PhoneNumber",
                table: "AspNetUsers",
                newName: "phone_number");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "AspNetUsers",
                newName: "password_hash");

            migrationBuilder.RenameColumn(
                name: "NormalizedUserName",
                table: "AspNetUsers",
                newName: "normalized_user_name");

            migrationBuilder.RenameColumn(
                name: "NormalizedEmail",
                table: "AspNetUsers",
                newName: "normalized_email");

            migrationBuilder.RenameColumn(
                name: "LockoutEnd",
                table: "AspNetUsers",
                newName: "lockout_end");

            migrationBuilder.RenameColumn(
                name: "LockoutEnabled",
                table: "AspNetUsers",
                newName: "lockout_enabled");

            migrationBuilder.RenameColumn(
                name: "LastName",
                table: "AspNetUsers",
                newName: "last_name");

            migrationBuilder.RenameColumn(
                name: "FirstName",
                table: "AspNetUsers",
                newName: "first_name");

            migrationBuilder.RenameColumn(
                name: "EmailConfirmed",
                table: "AspNetUsers",
                newName: "email_confirmed");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "AspNetUsers",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ConcurrencyStamp",
                table: "AspNetUsers",
                newName: "concurrency_stamp");

            migrationBuilder.RenameColumn(
                name: "AccessFailedCount",
                table: "AspNetUsers",
                newName: "access_failed_count");

            migrationBuilder.RenameColumn(
                name: "RoleId",
                table: "AspNetUserRoles",
                newName: "role_id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "AspNetUserRoles",
                newName: "user_id");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                newName: "ix_asp_net_user_roles_role_id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "AspNetUserLogins",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "ProviderDisplayName",
                table: "AspNetUserLogins",
                newName: "provider_display_name");

            migrationBuilder.RenameColumn(
                name: "ProviderKey",
                table: "AspNetUserLogins",
                newName: "provider_key");

            migrationBuilder.RenameColumn(
                name: "LoginProvider",
                table: "AspNetUserLogins",
                newName: "login_provider");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                newName: "ix_asp_net_user_logins_user_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "AspNetUserClaims",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "AspNetUserClaims",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "ClaimValue",
                table: "AspNetUserClaims",
                newName: "claim_value");

            migrationBuilder.RenameColumn(
                name: "ClaimType",
                table: "AspNetUserClaims",
                newName: "claim_type");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                newName: "ix_asp_net_user_claims_user_id");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "AspNetRoles",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "AspNetRoles",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "NormalizedName",
                table: "AspNetRoles",
                newName: "normalized_name");

            migrationBuilder.RenameColumn(
                name: "ConcurrencyStamp",
                table: "AspNetRoles",
                newName: "concurrency_stamp");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "AspNetRoleClaims",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "RoleId",
                table: "AspNetRoleClaims",
                newName: "role_id");

            migrationBuilder.RenameColumn(
                name: "ClaimValue",
                table: "AspNetRoleClaims",
                newName: "claim_value");

            migrationBuilder.RenameColumn(
                name: "ClaimType",
                table: "AspNetRoleClaims",
                newName: "claim_type");

            migrationBuilder.RenameIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                newName: "ix_asp_net_role_claims_role_id");

            migrationBuilder.RenameColumn(
                name: "Url",
                table: "listing_images",
                newName: "url");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "listing_images",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "SortOrder",
                table: "listing_images",
                newName: "sort_order");

            migrationBuilder.RenameColumn(
                name: "ListingId",
                table: "listing_images",
                newName: "listing_id");

            migrationBuilder.RenameColumn(
                name: "IsPrimary",
                table: "listing_images",
                newName: "is_primary");

            migrationBuilder.RenameIndex(
                name: "IX_ListingImages_ListingId",
                table: "listing_images",
                newName: "ix_listing_images_listing_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_listings",
                table: "listings",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_inquiries",
                table: "inquiries",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_favorites",
                table: "favorites",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_asp_net_user_tokens",
                table: "AspNetUserTokens",
                columns: new[] { "user_id", "login_provider", "name" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_asp_net_users",
                table: "AspNetUsers",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_asp_net_user_roles",
                table: "AspNetUserRoles",
                columns: new[] { "user_id", "role_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_asp_net_user_logins",
                table: "AspNetUserLogins",
                columns: new[] { "login_provider", "provider_key" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_asp_net_user_claims",
                table: "AspNetUserClaims",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_asp_net_roles",
                table: "AspNetRoles",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_asp_net_role_claims",
                table: "AspNetRoleClaims",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_listing_images",
                table: "listing_images",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                table: "AspNetRoleClaims",
                column: "role_id",
                principalTable: "AspNetRoles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_user_claims_asp_net_users_user_id",
                table: "AspNetUserClaims",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_user_logins_asp_net_users_user_id",
                table: "AspNetUserLogins",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                table: "AspNetUserRoles",
                column: "role_id",
                principalTable: "AspNetRoles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_user_roles_asp_net_users_user_id",
                table: "AspNetUserRoles",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                table: "AspNetUserTokens",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_favorites_listings_listing_id",
                table: "favorites",
                column: "listing_id",
                principalTable: "listings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_favorites_users_user_id",
                table: "favorites",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_inquiries_listings_listing_id",
                table: "inquiries",
                column: "listing_id",
                principalTable: "listings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_inquiries_users_sender_id",
                table: "inquiries",
                column: "sender_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_listing_images_listings_listing_id",
                table: "listing_images",
                column: "listing_id",
                principalTable: "listings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_listings_users_owner_id",
                table: "listings",
                column: "owner_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "fk_asp_net_user_claims_asp_net_users_user_id",
                table: "AspNetUserClaims");

            migrationBuilder.DropForeignKey(
                name: "fk_asp_net_user_logins_asp_net_users_user_id",
                table: "AspNetUserLogins");

            migrationBuilder.DropForeignKey(
                name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "fk_asp_net_user_roles_asp_net_users_user_id",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                table: "AspNetUserTokens");

            migrationBuilder.DropForeignKey(
                name: "fk_favorites_listings_listing_id",
                table: "favorites");

            migrationBuilder.DropForeignKey(
                name: "fk_favorites_users_user_id",
                table: "favorites");

            migrationBuilder.DropForeignKey(
                name: "fk_inquiries_listings_listing_id",
                table: "inquiries");

            migrationBuilder.DropForeignKey(
                name: "fk_inquiries_users_sender_id",
                table: "inquiries");

            migrationBuilder.DropForeignKey(
                name: "fk_listing_images_listings_listing_id",
                table: "listing_images");

            migrationBuilder.DropForeignKey(
                name: "fk_listings_users_owner_id",
                table: "listings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_listings",
                table: "listings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_inquiries",
                table: "inquiries");

            migrationBuilder.DropPrimaryKey(
                name: "pk_favorites",
                table: "favorites");

            migrationBuilder.DropPrimaryKey(
                name: "pk_asp_net_user_tokens",
                table: "AspNetUserTokens");

            migrationBuilder.DropPrimaryKey(
                name: "pk_asp_net_users",
                table: "AspNetUsers");

            migrationBuilder.DropPrimaryKey(
                name: "pk_asp_net_user_roles",
                table: "AspNetUserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "pk_asp_net_user_logins",
                table: "AspNetUserLogins");

            migrationBuilder.DropPrimaryKey(
                name: "pk_asp_net_user_claims",
                table: "AspNetUserClaims");

            migrationBuilder.DropPrimaryKey(
                name: "pk_asp_net_roles",
                table: "AspNetRoles");

            migrationBuilder.DropPrimaryKey(
                name: "pk_asp_net_role_claims",
                table: "AspNetRoleClaims");

            migrationBuilder.DropPrimaryKey(
                name: "pk_listing_images",
                table: "listing_images");

            migrationBuilder.RenameTable(
                name: "listings",
                newName: "Listings");

            migrationBuilder.RenameTable(
                name: "inquiries",
                newName: "Inquiries");

            migrationBuilder.RenameTable(
                name: "favorites",
                newName: "Favorites");

            migrationBuilder.RenameTable(
                name: "listing_images",
                newName: "ListingImages");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "Listings",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "Listings",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "state",
                table: "Listings",
                newName: "State");

            migrationBuilder.RenameColumn(
                name: "price",
                table: "Listings",
                newName: "Price");

            migrationBuilder.RenameColumn(
                name: "longitude",
                table: "Listings",
                newName: "Longitude");

            migrationBuilder.RenameColumn(
                name: "latitude",
                table: "Listings",
                newName: "Latitude");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "Listings",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "city",
                table: "Listings",
                newName: "City");

            migrationBuilder.RenameColumn(
                name: "bedrooms",
                table: "Listings",
                newName: "Bedrooms");

            migrationBuilder.RenameColumn(
                name: "bathrooms",
                table: "Listings",
                newName: "Bathrooms");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Listings",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "zip_code",
                table: "Listings",
                newName: "ZipCode");

            migrationBuilder.RenameColumn(
                name: "year_built",
                table: "Listings",
                newName: "YearBuilt");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "Listings",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "property_type",
                table: "Listings",
                newName: "PropertyType");

            migrationBuilder.RenameColumn(
                name: "owner_id",
                table: "Listings",
                newName: "OwnerId");

            migrationBuilder.RenameColumn(
                name: "listing_type",
                table: "Listings",
                newName: "ListingType");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Listings",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "area_sq_ft",
                table: "Listings",
                newName: "AreaSqFt");

            migrationBuilder.RenameColumn(
                name: "address_line",
                table: "Listings",
                newName: "AddressLine");

            migrationBuilder.RenameIndex(
                name: "ix_listings_latitude_longitude",
                table: "Listings",
                newName: "IX_Listings_Latitude_Longitude");

            migrationBuilder.RenameIndex(
                name: "ix_listings_city",
                table: "Listings",
                newName: "IX_Listings_City");

            migrationBuilder.RenameIndex(
                name: "ix_listings_owner_id",
                table: "Listings",
                newName: "IX_Listings_OwnerId");

            migrationBuilder.RenameColumn(
                name: "message",
                table: "Inquiries",
                newName: "Message");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Inquiries",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "sender_phone",
                table: "Inquiries",
                newName: "SenderPhone");

            migrationBuilder.RenameColumn(
                name: "sender_name",
                table: "Inquiries",
                newName: "SenderName");

            migrationBuilder.RenameColumn(
                name: "sender_id",
                table: "Inquiries",
                newName: "SenderId");

            migrationBuilder.RenameColumn(
                name: "sender_email",
                table: "Inquiries",
                newName: "SenderEmail");

            migrationBuilder.RenameColumn(
                name: "listing_id",
                table: "Inquiries",
                newName: "ListingId");

            migrationBuilder.RenameColumn(
                name: "is_read",
                table: "Inquiries",
                newName: "IsRead");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Inquiries",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_inquiries_sender_id",
                table: "Inquiries",
                newName: "IX_Inquiries_SenderId");

            migrationBuilder.RenameIndex(
                name: "ix_inquiries_listing_id",
                table: "Inquiries",
                newName: "IX_Inquiries_ListingId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Favorites",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "Favorites",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "listing_id",
                table: "Favorites",
                newName: "ListingId");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "Favorites",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_favorites_user_id_listing_id",
                table: "Favorites",
                newName: "IX_Favorites_UserId_ListingId");

            migrationBuilder.RenameIndex(
                name: "ix_favorites_listing_id",
                table: "Favorites",
                newName: "IX_Favorites_ListingId");

            migrationBuilder.RenameColumn(
                name: "value",
                table: "AspNetUserTokens",
                newName: "Value");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "AspNetUserTokens",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "login_provider",
                table: "AspNetUserTokens",
                newName: "LoginProvider");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "AspNetUserTokens",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "AspNetUsers",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "AspNetUsers",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_name",
                table: "AspNetUsers",
                newName: "UserName");

            migrationBuilder.RenameColumn(
                name: "two_factor_enabled",
                table: "AspNetUsers",
                newName: "TwoFactorEnabled");

            migrationBuilder.RenameColumn(
                name: "security_stamp",
                table: "AspNetUsers",
                newName: "SecurityStamp");

            migrationBuilder.RenameColumn(
                name: "phone_number_confirmed",
                table: "AspNetUsers",
                newName: "PhoneNumberConfirmed");

            migrationBuilder.RenameColumn(
                name: "phone_number",
                table: "AspNetUsers",
                newName: "PhoneNumber");

            migrationBuilder.RenameColumn(
                name: "password_hash",
                table: "AspNetUsers",
                newName: "PasswordHash");

            migrationBuilder.RenameColumn(
                name: "normalized_user_name",
                table: "AspNetUsers",
                newName: "NormalizedUserName");

            migrationBuilder.RenameColumn(
                name: "normalized_email",
                table: "AspNetUsers",
                newName: "NormalizedEmail");

            migrationBuilder.RenameColumn(
                name: "lockout_end",
                table: "AspNetUsers",
                newName: "LockoutEnd");

            migrationBuilder.RenameColumn(
                name: "lockout_enabled",
                table: "AspNetUsers",
                newName: "LockoutEnabled");

            migrationBuilder.RenameColumn(
                name: "last_name",
                table: "AspNetUsers",
                newName: "LastName");

            migrationBuilder.RenameColumn(
                name: "first_name",
                table: "AspNetUsers",
                newName: "FirstName");

            migrationBuilder.RenameColumn(
                name: "email_confirmed",
                table: "AspNetUsers",
                newName: "EmailConfirmed");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "AspNetUsers",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "concurrency_stamp",
                table: "AspNetUsers",
                newName: "ConcurrencyStamp");

            migrationBuilder.RenameColumn(
                name: "access_failed_count",
                table: "AspNetUsers",
                newName: "AccessFailedCount");

            migrationBuilder.RenameColumn(
                name: "role_id",
                table: "AspNetUserRoles",
                newName: "RoleId");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "AspNetUserRoles",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "ix_asp_net_user_roles_role_id",
                table: "AspNetUserRoles",
                newName: "IX_AspNetUserRoles_RoleId");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "AspNetUserLogins",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "provider_display_name",
                table: "AspNetUserLogins",
                newName: "ProviderDisplayName");

            migrationBuilder.RenameColumn(
                name: "provider_key",
                table: "AspNetUserLogins",
                newName: "ProviderKey");

            migrationBuilder.RenameColumn(
                name: "login_provider",
                table: "AspNetUserLogins",
                newName: "LoginProvider");

            migrationBuilder.RenameIndex(
                name: "ix_asp_net_user_logins_user_id",
                table: "AspNetUserLogins",
                newName: "IX_AspNetUserLogins_UserId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "AspNetUserClaims",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "AspNetUserClaims",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "claim_value",
                table: "AspNetUserClaims",
                newName: "ClaimValue");

            migrationBuilder.RenameColumn(
                name: "claim_type",
                table: "AspNetUserClaims",
                newName: "ClaimType");

            migrationBuilder.RenameIndex(
                name: "ix_asp_net_user_claims_user_id",
                table: "AspNetUserClaims",
                newName: "IX_AspNetUserClaims_UserId");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "AspNetRoles",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "AspNetRoles",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "normalized_name",
                table: "AspNetRoles",
                newName: "NormalizedName");

            migrationBuilder.RenameColumn(
                name: "concurrency_stamp",
                table: "AspNetRoles",
                newName: "ConcurrencyStamp");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "AspNetRoleClaims",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "role_id",
                table: "AspNetRoleClaims",
                newName: "RoleId");

            migrationBuilder.RenameColumn(
                name: "claim_value",
                table: "AspNetRoleClaims",
                newName: "ClaimValue");

            migrationBuilder.RenameColumn(
                name: "claim_type",
                table: "AspNetRoleClaims",
                newName: "ClaimType");

            migrationBuilder.RenameIndex(
                name: "ix_asp_net_role_claims_role_id",
                table: "AspNetRoleClaims",
                newName: "IX_AspNetRoleClaims_RoleId");

            migrationBuilder.RenameColumn(
                name: "url",
                table: "ListingImages",
                newName: "Url");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "ListingImages",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "sort_order",
                table: "ListingImages",
                newName: "SortOrder");

            migrationBuilder.RenameColumn(
                name: "listing_id",
                table: "ListingImages",
                newName: "ListingId");

            migrationBuilder.RenameColumn(
                name: "is_primary",
                table: "ListingImages",
                newName: "IsPrimary");

            migrationBuilder.RenameIndex(
                name: "ix_listing_images_listing_id",
                table: "ListingImages",
                newName: "IX_ListingImages_ListingId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Listings",
                table: "Listings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Inquiries",
                table: "Inquiries",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Favorites",
                table: "Favorites",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserTokens",
                table: "AspNetUserTokens",
                columns: new[] { "UserId", "LoginProvider", "Name" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUsers",
                table: "AspNetUsers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserRoles",
                table: "AspNetUserRoles",
                columns: new[] { "UserId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserLogins",
                table: "AspNetUserLogins",
                columns: new[] { "LoginProvider", "ProviderKey" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetUserClaims",
                table: "AspNetUserClaims",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetRoles",
                table: "AspNetRoles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetRoleClaims",
                table: "AspNetRoleClaims",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ListingImages",
                table: "ListingImages",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_AspNetUsers_UserId",
                table: "Favorites",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_Listings_ListingId",
                table: "Favorites",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Inquiries_AspNetUsers_SenderId",
                table: "Inquiries",
                column: "SenderId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Inquiries_Listings_ListingId",
                table: "Inquiries",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ListingImages_Listings_ListingId",
                table: "ListingImages",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Listings_AspNetUsers_OwnerId",
                table: "Listings",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
