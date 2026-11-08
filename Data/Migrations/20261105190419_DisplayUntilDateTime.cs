using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CollegeActivityCalendar.Data.Migrations
{
    /// <inheritdoc />
    public partial class DisplayUntilDateTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "DisplayUntil",
                table: "Events",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            // เดิมแสดงถึงสิ้นวันที่กำหนด จึงตั้งเป็นนาทีสุดท้ายของวัน มิฉะนั้นกิจกรรมจะหายตั้งแต่เที่ยงคืนต้นวัน
            migrationBuilder.Sql("UPDATE Events SET DisplayUntil = DATEADD(MINUTE, 1439, DisplayUntil) WHERE DisplayUntil IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateOnly>(
                name: "DisplayUntil",
                table: "Events",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
