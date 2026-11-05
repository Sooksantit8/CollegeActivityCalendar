using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CollegeActivityCalendar.Data.Migrations
{
    /// <inheritdoc />
    public partial class TeacherAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeacherAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherProfileId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    GradeLevelId = table.Column<int>(type: "int", nullable: true),
                    ClassroomId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_GradeLevels_GradeLevelId",
                        column: x => x.GradeLevelId,
                        principalTable: "GradeLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_TeacherProfiles_TeacherProfileId",
                        column: x => x.TeacherProfileId,
                        principalTable: "TeacherProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_ClassroomId",
                table: "TeacherAssignments",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_DepartmentId_GradeLevelId",
                table: "TeacherAssignments",
                columns: new[] { "DepartmentId", "GradeLevelId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_GradeLevelId",
                table: "TeacherAssignments",
                column: "GradeLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_TeacherProfileId_DepartmentId_GradeLevelId_ClassroomId",
                table: "TeacherAssignments",
                columns: new[] { "TeacherProfileId", "DepartmentId", "GradeLevelId", "ClassroomId" },
                unique: true);

            // ย้ายข้อมูลเดิม: ครูที่เคยรับผิดชอบแผนก = ดูแลทั้งแผนก
            migrationBuilder.Sql("""
                INSERT INTO [TeacherAssignments] ([TeacherProfileId], [DepartmentId], [GradeLevelId], [ClassroomId])
                SELECT [TeacherProfileId], [DepartmentId], NULL, NULL FROM [TeacherDepartments];
                """);

            migrationBuilder.DropTable(
                name: "TeacherDepartments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeacherDepartments",
                columns: table => new
                {
                    TeacherProfileId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherDepartments", x => new { x.TeacherProfileId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_TeacherDepartments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherDepartments_TeacherProfiles_TeacherProfileId",
                        column: x => x.TeacherProfileId,
                        principalTable: "TeacherProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherDepartments_DepartmentId",
                table: "TeacherDepartments",
                column: "DepartmentId");

            // ย้อนกลับได้เฉพาะแผนก ขอบเขตรายชั้นปีและรายห้องจะกลายเป็นทั้งแผนก
            migrationBuilder.Sql("""
                INSERT INTO [TeacherDepartments] ([TeacherProfileId], [DepartmentId])
                SELECT DISTINCT [TeacherProfileId], [DepartmentId] FROM [TeacherAssignments];
                """);

            migrationBuilder.DropTable(
                name: "TeacherAssignments");
        }
    }
}
