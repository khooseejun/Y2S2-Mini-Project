using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantSystem.Migrations
{
    /// <summary>
    /// Brings the EF migration history in line with the database that actually ships with this
    /// project. The Database (DB.mdf) was edited out-of-band in Visual Studio at some point, so
    /// these columns already exist while no migration ever created them.
    ///
    /// Every statement below is therefore guarded with a raw SQL IF EXISTS / IF NOT EXISTS check so
    /// this migration is safe to run on BOTH:
    ///   * the shipped DB.mdf (where the objects already exist), and
    ///   * a fresh database built from the earlier migrations (where they do not).
    /// </summary>
    public partial class SyncModelWithDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Users.Address
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users','Address') IS NULL
    ALTER TABLE [Users] ADD [Address] nvarchar(250) NULL;");

            // Users.IsBlocked
            migrationBuilder.Sql(@"
IF COL_LENGTH('Users','IsBlocked') IS NULL
    ALTER TABLE [Users] ADD [IsBlocked] bit NOT NULL CONSTRAINT [DF_Users_IsBlocked] DEFAULT CAST(0 AS bit);");

            // Orders.DeliveryAddress
            migrationBuilder.Sql(@"
IF COL_LENGTH('Orders','DeliveryAddress') IS NULL
    ALTER TABLE [Orders] ADD [DeliveryAddress] nvarchar(250) NULL;");

            // Products.ImageUrl -> nullable (matches the C# model: uploads arrive via IFormFile)
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('Products') AND name = 'ImageUrl' AND is_nullable = 0)
    ALTER TABLE [Products] ALTER COLUMN [ImageUrl] nvarchar(250) NULL;");

            // Products.Description -> nullable
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('Products') AND name = 'Description' AND is_nullable = 0)
    ALTER TABLE [Products] ALTER COLUMN [Description] nvarchar(500) NULL;");

            // OrderDetails.Notes is not in the C# entity any more — drop it only if present.
            // Its DEFAULT constraint (added by the UpdateDB migration) must go first, otherwise
            // SQL Server refuses the DROP COLUMN.
            migrationBuilder.Sql(@"
DECLARE @df sysname;
SELECT @df = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('OrderDetails') AND c.name = 'Notes';
IF @df IS NOT NULL
    EXEC('ALTER TABLE [OrderDetails] DROP CONSTRAINT [' + @df + ']');");

            migrationBuilder.Sql(@"
IF COL_LENGTH('OrderDetails','Notes') IS NOT NULL
    ALTER TABLE [OrderDetails] DROP COLUMN [Notes];");

            // Index + FK on Products.CategoryId (restrict, so a non-empty category cannot be deleted).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID('Products') AND name = 'IX_Products_CategoryId')
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE name = 'FK_Products_Categories_CategoryId' AND parent_object_id = OBJECT_ID('Products'))
    ALTER TABLE [Products] WITH CHECK
        ADD CONSTRAINT [FK_Products_Categories_CategoryId]
        FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([CategoryId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally a no-op: these objects pre-date the migration, so reverting must not
            // remove columns the application relies on.
        }
    }
}
