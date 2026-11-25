using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogiMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntegridadCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KitHijoCodigo",
                table: "KitComponentes",
                type: "TEXT",
                nullable: true,
                computedColumnSql: "CASE WHEN \"EsHijoKit\" = 1 THEN \"HijoCodigo\" ELSE NULL END",
                stored: false);

            migrationBuilder.AddColumn<string>(
                name: "ProductoHijoSku",
                table: "KitComponentes",
                type: "TEXT",
                nullable: true,
                computedColumnSql: "CASE WHEN \"EsHijoKit\" = 0 THEN \"HijoCodigo\" ELSE NULL END",
                stored: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Productos_Precio",
                table: "Productos",
                sql: "CAST(\"Precio\" AS NUMERIC) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Productos_Stock",
                table: "Productos",
                sql: "\"Stock\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoDetalleComponentes_Sku",
                table: "PedidoDetalleComponentes",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_Sku",
                table: "MovimientosInventario",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_KitComponentes_KitHijoCodigo",
                table: "KitComponentes",
                column: "KitHijoCodigo");

            migrationBuilder.CreateIndex(
                name: "IX_KitComponentes_ProductoHijoSku",
                table: "KitComponentes",
                column: "ProductoHijoSku");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KitComponentes_Cantidad",
                table: "KitComponentes",
                sql: "\"Cantidad\" > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_KitComponentes_Kits_KitHijoCodigo",
                table: "KitComponentes",
                column: "KitHijoCodigo",
                principalTable: "Kits",
                principalColumn: "Codigo",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_KitComponentes_Productos_ProductoHijoSku",
                table: "KitComponentes",
                column: "ProductoHijoSku",
                principalTable: "Productos",
                principalColumn: "Sku",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosInventario_Productos_Sku",
                table: "MovimientosInventario",
                column: "Sku",
                principalTable: "Productos",
                principalColumn: "Sku",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PedidoDetalleComponentes_Productos_Sku",
                table: "PedidoDetalleComponentes",
                column: "Sku",
                principalTable: "Productos",
                principalColumn: "Sku",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KitComponentes_Kits_KitHijoCodigo",
                table: "KitComponentes");

            migrationBuilder.DropForeignKey(
                name: "FK_KitComponentes_Productos_ProductoHijoSku",
                table: "KitComponentes");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosInventario_Productos_Sku",
                table: "MovimientosInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_PedidoDetalleComponentes_Productos_Sku",
                table: "PedidoDetalleComponentes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Productos_Precio",
                table: "Productos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Productos_Stock",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_PedidoDetalleComponentes_Sku",
                table: "PedidoDetalleComponentes");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosInventario_Sku",
                table: "MovimientosInventario");

            migrationBuilder.DropIndex(
                name: "IX_KitComponentes_KitHijoCodigo",
                table: "KitComponentes");

            migrationBuilder.DropIndex(
                name: "IX_KitComponentes_ProductoHijoSku",
                table: "KitComponentes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KitComponentes_Cantidad",
                table: "KitComponentes");

            migrationBuilder.DropColumn(
                name: "KitHijoCodigo",
                table: "KitComponentes");

            migrationBuilder.DropColumn(
                name: "ProductoHijoSku",
                table: "KitComponentes");
        }
    }
}
