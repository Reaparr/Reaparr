using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reaparr.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeQuartzSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_T_NEXT_FIRE_TIME",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_T_NFT_ST",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_T_STATE",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_J_REQ_RECOVERY",
                table: "QRTZ_JOB_DETAILS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_FT_JOB_GROUP",
                table: "QRTZ_FIRED_TRIGGERS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_FT_JOB_NAME",
                table: "QRTZ_FIRED_TRIGGERS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_FT_JOB_REQ_RECOVERY",
                table: "QRTZ_FIRED_TRIGGERS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_FT_TRIG_GROUP",
                table: "QRTZ_FIRED_TRIGGERS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_FT_TRIG_INST_NAME",
                table: "QRTZ_FIRED_TRIGGERS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_FT_TRIG_NAME",
                table: "QRTZ_FIRED_TRIGGERS");

            migrationBuilder.RenameIndex(
                name: "IX_QRTZ_TRIGGERS_SCHED_NAME_JOB_NAME_JOB_GROUP",
                table: "QRTZ_TRIGGERS",
                newName: "IDX_QRTZ_T_J");

            migrationBuilder.RenameIndex(
                name: "IDX_QRTZ_FT_TRIG_NM_GP",
                table: "QRTZ_FIRED_TRIGGERS",
                newName: "IDX_QRTZ_FT_T_G");

            migrationBuilder.AddColumn<int>(
                name: "CONTINUATION_CONDITION",
                table: "QRTZ_TRIGGERS",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CONTINUES_TRIGGER_GROUP",
                table: "QRTZ_TRIGGERS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CONTINUES_TRIGGER_NAME",
                table: "QRTZ_TRIGGERS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXECUTION_GROUP",
                table: "QRTZ_TRIGGERS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FIXED_DELAY",
                table: "QRTZ_TRIGGERS",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MISFIRE_THRESHOLD",
                table: "QRTZ_TRIGGERS",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OVERLAP_POLICY",
                table: "QRTZ_TRIGGERS",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PAUSED_AT",
                table: "QRTZ_TRIGGERS",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PAUSED_BY",
                table: "QRTZ_TRIGGERS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PAUSE_REASON",
                table: "QRTZ_TRIGGERS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PREFERRED_NODE",
                table: "QRTZ_TRIGGERS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PREFERRED_NODE_AUTO",
                table: "QRTZ_TRIGGERS",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RETRY_ATTEMPT",
                table: "QRTZ_TRIGGERS",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RETRY_POLICY",
                table: "QRTZ_TRIGGERS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QUARTZ_VERSION",
                table: "QRTZ_SCHEDULER_STATE",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PAUSED_AT",
                table: "QRTZ_PAUSED_TRIGGER_GRPS",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PAUSED_BY",
                table: "QRTZ_PAUSED_TRIGGER_GRPS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PAUSE_REASON",
                table: "QRTZ_PAUSED_TRIGGER_GRPS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EXECUTION_GROUP",
                table: "QRTZ_FIRED_TRIGGERS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "INTERRUPT_REQUESTED_TIME",
                table: "QRTZ_FIRED_TRIGGERS",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PROGRESS",
                table: "QRTZ_FIRED_TRIGGERS",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PROGRESS_MESSAGE",
                table: "QRTZ_FIRED_TRIGGERS",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QRTZ_EXECUTION_HISTORY",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "text", nullable: false),
                    ENTRY_ID = table.Column<string>(type: "text", nullable: false),
                    INSTANCE_NAME = table.Column<string>(type: "text", nullable: false),
                    JOB_NAME = table.Column<string>(type: "text", nullable: false),
                    JOB_GROUP = table.Column<string>(type: "text", nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "text", nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "text", nullable: false),
                    FIRED_TIME = table.Column<long>(type: "bigint", nullable: false),
                    RUN_TIME = table.Column<long>(type: "bigint", nullable: false),
                    SUCCEEDED = table.Column<bool>(type: "bit", nullable: false),
                    ERROR_MESSAGE = table.Column<string>(type: "text", nullable: true),
                    RETRY_ATTEMPT = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RETRY_SCHEDULED = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EXECUTION_LOG = table.Column<string>(type: "text", nullable: true),
                    FIRE_INSTANCE_ID = table.Column<string>(type: "nvarchar(140)", nullable: true),
                    JOB_INPUT = table.Column<string>(type: "text", nullable: true),
                    JOB_INPUT_TOO_LARGE = table.Column<bool>(type: "bit", nullable: true),
                    MANUAL = table.Column<bool>(type: "bit", nullable: true),
                    METRICS = table.Column<string>(type: "text", nullable: true),
                    RESULT = table.Column<int>(type: "integer", nullable: true),
                    SUMMARY = table.Column<string>(type: "nvarchar(1000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_EXECUTION_HISTORY", x => new { x.SCHED_NAME, x.ENTRY_ID });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_JOB_STATUS",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "nvarchar(120)", nullable: false),
                    JOB_GROUP = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    JOB_NAME = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    CONSECUTIVE_FAILURES = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    FAILURE_COUNT = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    FIRST_FIRED_TIME = table.Column<long>(type: "bigint", nullable: false),
                    LAST_ENTRY_ID = table.Column<string>(type: "nvarchar(140)", nullable: true),
                    LAST_FAILURE_MESSAGE = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    LAST_FAILURE_TIME = table.Column<long>(type: "bigint", nullable: true),
                    LAST_FIRED_TIME = table.Column<long>(type: "bigint", nullable: false),
                    LAST_INSTANCE_NAME = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    LAST_RESULT = table.Column<int>(type: "integer", nullable: false),
                    LAST_RUN_TIME = table.Column<long>(type: "bigint", nullable: false),
                    LAST_SUCCESS_TIME = table.Column<long>(type: "bigint", nullable: true),
                    LAST_SUMMARY = table.Column<string>(type: "nvarchar(1000)", nullable: true),
                    RUN_COUNT = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_JOB_STATUS", x => new { x.SCHED_NAME, x.JOB_GROUP, x.JOB_NAME });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_MISFIRE_HISTORY",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "text", nullable: false),
                    ENTRY_ID = table.Column<string>(type: "text", nullable: false),
                    INSTANCE_NAME = table.Column<string>(type: "text", nullable: false),
                    TRIGGER_NAME = table.Column<string>(type: "text", nullable: false),
                    TRIGGER_GROUP = table.Column<string>(type: "text", nullable: false),
                    JOB_NAME = table.Column<string>(type: "text", nullable: true),
                    JOB_GROUP = table.Column<string>(type: "text", nullable: true),
                    MISFIRE_TIME = table.Column<long>(type: "bigint", nullable: false),
                    SCHED_TIME = table.Column<long>(type: "bigint", nullable: true),
                    REASON = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_MISFIRE_HISTORY", x => new { x.SCHED_NAME, x.ENTRY_ID });
                });

            migrationBuilder.CreateTable(
                name: "QRTZ_PAUSED_JOB_GRPS",
                columns: table => new
                {
                    SCHED_NAME = table.Column<string>(type: "text", nullable: false),
                    JOB_GROUP = table.Column<string>(type: "text", nullable: false),
                    PAUSE_REASON = table.Column<string>(type: "text", nullable: true),
                    PAUSED_BY = table.Column<string>(type: "text", nullable: true),
                    PAUSED_AT = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QRTZ_PAUSED_JOB_GRPS", x => new { x.SCHED_NAME, x.JOB_GROUP });
                });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_C",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "CALENDAR_NAME" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_G_N",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "TRIGGER_GROUP", "TRIGGER_NAME" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_NFT_ST",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "SCHED_NAME", "TRIGGER_STATE", "NEXT_FIRE_TIME", "PRIORITY", "MISFIRE_INSTR" },
                descending: new[] { false, false, false, true, false });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_J_G_N",
                table: "QRTZ_JOB_DETAILS",
                columns: new[] { "SCHED_NAME", "JOB_GROUP", "JOB_NAME" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_INST_JOB_REQ_RCVRY",
                table: "QRTZ_FIRED_TRIGGERS",
                columns: new[] { "SCHED_NAME", "INSTANCE_NAME", "REQUESTS_RECOVERY" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_J_G",
                table: "QRTZ_FIRED_TRIGGERS",
                columns: new[] { "SCHED_NAME", "JOB_NAME", "JOB_GROUP" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_EH_FIRE_INST",
                table: "QRTZ_EXECUTION_HISTORY",
                columns: new[] { "SCHED_NAME", "FIRE_INSTANCE_ID" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_EH_FIRED_TIME",
                table: "QRTZ_EXECUTION_HISTORY",
                columns: new[] { "SCHED_NAME", "FIRED_TIME" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_EH_INST",
                table: "QRTZ_EXECUTION_HISTORY",
                columns: new[] { "SCHED_NAME", "INSTANCE_NAME" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_EH_JOB_TIME",
                table: "QRTZ_EXECUTION_HISTORY",
                columns: new[] { "SCHED_NAME", "JOB_GROUP", "JOB_NAME", "FIRED_TIME" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_EH_TRIG_TIME",
                table: "QRTZ_EXECUTION_HISTORY",
                columns: new[] { "SCHED_NAME", "TRIGGER_GROUP", "TRIGGER_NAME", "FIRED_TIME" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_MH_INST",
                table: "QRTZ_MISFIRE_HISTORY",
                columns: new[] { "SCHED_NAME", "INSTANCE_NAME" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_MH_MISFIRE_TIME",
                table: "QRTZ_MISFIRE_HISTORY",
                columns: new[] { "SCHED_NAME", "MISFIRE_TIME" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QRTZ_EXECUTION_HISTORY");

            migrationBuilder.DropTable(
                name: "QRTZ_JOB_STATUS");

            migrationBuilder.DropTable(
                name: "QRTZ_MISFIRE_HISTORY");

            migrationBuilder.DropTable(
                name: "QRTZ_PAUSED_JOB_GRPS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_T_C",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_T_G_N",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_T_NFT_ST",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_J_G_N",
                table: "QRTZ_JOB_DETAILS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_FT_INST_JOB_REQ_RCVRY",
                table: "QRTZ_FIRED_TRIGGERS");

            migrationBuilder.DropIndex(
                name: "IDX_QRTZ_FT_J_G",
                table: "QRTZ_FIRED_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "CONTINUATION_CONDITION",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "CONTINUES_TRIGGER_GROUP",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "CONTINUES_TRIGGER_NAME",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "EXECUTION_GROUP",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "FIXED_DELAY",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "MISFIRE_THRESHOLD",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "OVERLAP_POLICY",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "PAUSED_AT",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "PAUSED_BY",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "PAUSE_REASON",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "PREFERRED_NODE",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "PREFERRED_NODE_AUTO",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "RETRY_ATTEMPT",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "RETRY_POLICY",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "QUARTZ_VERSION",
                table: "QRTZ_SCHEDULER_STATE");

            migrationBuilder.DropColumn(
                name: "PAUSED_AT",
                table: "QRTZ_PAUSED_TRIGGER_GRPS");

            migrationBuilder.DropColumn(
                name: "PAUSED_BY",
                table: "QRTZ_PAUSED_TRIGGER_GRPS");

            migrationBuilder.DropColumn(
                name: "PAUSE_REASON",
                table: "QRTZ_PAUSED_TRIGGER_GRPS");

            migrationBuilder.DropColumn(
                name: "EXECUTION_GROUP",
                table: "QRTZ_FIRED_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "INTERRUPT_REQUESTED_TIME",
                table: "QRTZ_FIRED_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "PROGRESS",
                table: "QRTZ_FIRED_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "PROGRESS_MESSAGE",
                table: "QRTZ_FIRED_TRIGGERS");

            migrationBuilder.RenameIndex(
                name: "IDX_QRTZ_T_J",
                table: "QRTZ_TRIGGERS",
                newName: "IX_QRTZ_TRIGGERS_SCHED_NAME_JOB_NAME_JOB_GROUP");

            migrationBuilder.RenameIndex(
                name: "IDX_QRTZ_FT_T_G",
                table: "QRTZ_FIRED_TRIGGERS",
                newName: "IDX_QRTZ_FT_TRIG_NM_GP");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_NEXT_FIRE_TIME",
                table: "QRTZ_TRIGGERS",
                column: "NEXT_FIRE_TIME");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_NFT_ST",
                table: "QRTZ_TRIGGERS",
                columns: new[] { "NEXT_FIRE_TIME", "TRIGGER_STATE" });

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_T_STATE",
                table: "QRTZ_TRIGGERS",
                column: "TRIGGER_STATE");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_J_REQ_RECOVERY",
                table: "QRTZ_JOB_DETAILS",
                column: "REQUESTS_RECOVERY");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_JOB_GROUP",
                table: "QRTZ_FIRED_TRIGGERS",
                column: "JOB_GROUP");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_JOB_NAME",
                table: "QRTZ_FIRED_TRIGGERS",
                column: "JOB_NAME");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_JOB_REQ_RECOVERY",
                table: "QRTZ_FIRED_TRIGGERS",
                column: "REQUESTS_RECOVERY");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_TRIG_GROUP",
                table: "QRTZ_FIRED_TRIGGERS",
                column: "TRIGGER_GROUP");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_TRIG_INST_NAME",
                table: "QRTZ_FIRED_TRIGGERS",
                column: "INSTANCE_NAME");

            migrationBuilder.CreateIndex(
                name: "IDX_QRTZ_FT_TRIG_NAME",
                table: "QRTZ_FIRED_TRIGGERS",
                column: "TRIGGER_NAME");
        }
    }
}
