using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityContext.Migrations
{
    public partial class DashboardAndVideoSupport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tutorials",
                columns: table => new
                {
                    VideoTutorialId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SortableName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModuleUrl = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tutorials", x => x.VideoTutorialId);
                });

            migrationBuilder.CreateTable(
                name: "Widgets",
                columns: table => new
                {
                    DashboardWidgetId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TitleTemplate = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SystemName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DiagnosticsQueryId = table.Column<int>(type: "int", nullable: false),
                    Area = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomQueryString = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Template = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Widgets", x => x.DashboardWidgetId);
                    table.ForeignKey(
                        name: "FK_Widgets_DiagnosticsQueries_DiagnosticsQueryId",
                        column: x => x.DiagnosticsQueryId,
                        principalTable: "DiagnosticsQueries",
                        principalColumn: "DiagnosticsQueryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorialStreams",
                columns: table => new
                {
                    TutorialStreamId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LanguageTag = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VideoTutorialId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorialStreams", x => x.TutorialStreamId);
                    table.ForeignKey(
                        name: "FK_TutorialStreams_Tutorials_VideoTutorialId",
                        column: x => x.VideoTutorialId,
                        principalTable: "Tutorials",
                        principalColumn: "VideoTutorialId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserWidgets",
                columns: table => new
                {
                    UserWidgetId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DashboardWidgetId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CustomQueryString = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWidgets", x => x.UserWidgetId);
                    table.ForeignKey(
                        name: "FK_UserWidgets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWidgets_Widgets_DashboardWidgetId",
                        column: x => x.DashboardWidgetId,
                        principalTable: "Widgets",
                        principalColumn: "DashboardWidgetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WidgetParams",
                columns: table => new
                {
                    DashboardParamId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DashboardWidgetId = table.Column<int>(type: "int", nullable: false),
                    ParameterName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    InputType = table.Column<int>(type: "int", nullable: false),
                    InputConfig = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WidgetParams", x => x.DashboardParamId);
                    table.ForeignKey(
                        name: "FK_WidgetParams_Widgets_DashboardWidgetId",
                        column: x => x.DashboardWidgetId,
                        principalTable: "Widgets",
                        principalColumn: "DashboardWidgetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TutorialStreamBlob",
                columns: table => new
                {
                    TutorialStreamBlobId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    TutorialStreamId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorialStreamBlob", x => x.TutorialStreamBlobId);
                    table.ForeignKey(
                        name: "FK_TutorialStreamBlob_TutorialStreams_TutorialStreamId",
                        column: x => x.TutorialStreamId,
                        principalTable: "TutorialStreams",
                        principalColumn: "TutorialStreamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorialStreamBlob_TutorialStreamId",
                table: "TutorialStreamBlob",
                column: "TutorialStreamId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorialStreams_VideoTutorialId",
                table: "TutorialStreams",
                column: "VideoTutorialId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWidgets_DashboardWidgetId",
                table: "UserWidgets",
                column: "DashboardWidgetId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWidgets_TenantId",
                table: "UserWidgets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WidgetParams_DashboardWidgetId",
                table: "WidgetParams",
                column: "DashboardWidgetId");

            migrationBuilder.CreateIndex(
                name: "IX_Widgets_DiagnosticsQueryId",
                table: "Widgets",
                column: "DiagnosticsQueryId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorialStreamBlob");

            migrationBuilder.DropTable(
                name: "UserWidgets");

            migrationBuilder.DropTable(
                name: "WidgetParams");

            migrationBuilder.DropTable(
                name: "TutorialStreams");

            migrationBuilder.DropTable(
                name: "Widgets");

            migrationBuilder.DropTable(
                name: "Tutorials");
        }
    }
}
