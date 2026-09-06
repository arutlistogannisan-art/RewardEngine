using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
namespace RewardEngine.Infrastructure.Migrations;

[DbContext(typeof(RewardEngineDbContext))]
[Migration("20250101000000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("CashbackRules", t => new { Id = t.Column<long>("bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn), Category = t.Column<string>("character varying(40)", maxLength: 40, nullable: false), Percentage = t.Column<decimal>("numeric(5,2)", nullable: false), MonthlyLimit = t.Column<decimal>("numeric(18,2)", nullable: false), IsActive = t.Column<bool>("boolean", nullable: false), CreatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false), UpdatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false) }, constraints: t => t.PrimaryKey("PK_CashbackRules", x => x.Id));
        m.CreateTable("OutboxMessages", t => new { Id = t.Column<Guid>("uuid", nullable: false), EventType = t.Column<string>("character varying(200)", maxLength: 200, nullable: false), Payload = t.Column<string>("jsonb", nullable: false), CreatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false), ProcessedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: true), RetryCount = t.Column<int>("integer", nullable: false), NextAttemptAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: true), LastError = t.Column<string>("character varying(2000)", maxLength: 2000, nullable: true) }, constraints: t => t.PrimaryKey("PK_OutboxMessages", x => x.Id));
        m.CreateTable("ProcessedEvents", t => new { EventId = t.Column<Guid>("uuid", nullable: false), ProcessedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false) }, constraints: t => t.PrimaryKey("PK_ProcessedEvents", x => x.EventId));
        m.CreateTable("Users", t => new { Id = t.Column<long>("bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn), Name = t.Column<string>("character varying(200)", maxLength: 200, nullable: false), CreatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false) }, constraints: t => t.PrimaryKey("PK_Users", x => x.Id));
        m.CreateTable("Transactions", t => new { Id = t.Column<long>("bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn), UserId = t.Column<long>("bigint", nullable: false), Amount = t.Column<decimal>("numeric(18,2)", nullable: false), Category = t.Column<string>("character varying(40)", maxLength: 40, nullable: false), Merchant = t.Column<string>("character varying(300)", maxLength: 300, nullable: false), Status = t.Column<string>("character varying(40)", maxLength: 40, nullable: false), CreatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false) }, constraints: t => { t.PrimaryKey("PK_Transactions", x => x.Id); t.ForeignKey("FK_Transactions_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Restrict); });
        m.CreateTable("CashbackRewards", t => new { Id = t.Column<long>("bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn), TransactionId = t.Column<long>("bigint", nullable: false), UserId = t.Column<long>("bigint", nullable: false), Amount = t.Column<decimal>("numeric(18,2)", nullable: false), Percentage = t.Column<decimal>("numeric(5,2)", nullable: false), CreatedAt = t.Column<DateTimeOffset>("timestamp with time zone", nullable: false) }, constraints: t => { t.PrimaryKey("PK_CashbackRewards", x => x.Id); t.ForeignKey("FK_CashbackRewards_Transactions_TransactionId", x => x.TransactionId, "Transactions", "Id", onDelete: ReferentialAction.Restrict); t.ForeignKey("FK_CashbackRewards_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Restrict); });
        m.AddCheckConstraint("CK_CashbackRules_Percentage", "CashbackRules", "\"Percentage\" >= 0 AND \"Percentage\" <= 100");
        m.AddCheckConstraint("CK_CashbackRules_MonthlyLimit", "CashbackRules", "\"MonthlyLimit\" >= 0");
        m.CreateIndex("IX_CashbackRules_Category", "CashbackRules", "Category", unique: true);
        m.CreateIndex("IX_OutboxMessages_ProcessedAt", "OutboxMessages", "ProcessedAt");
        m.CreateIndex("IX_OutboxMessages_NextAttemptAt", "OutboxMessages", "NextAttemptAt");
        m.CreateIndex("IX_ProcessedEvents_EventId", "ProcessedEvents", "EventId", unique: true);
        m.CreateIndex("IX_Transactions_UserId", "Transactions", "UserId");
        m.CreateIndex("IX_Transactions_CreatedAt", "Transactions", "CreatedAt");
        m.CreateIndex("IX_CashbackRewards_UserId", "CashbackRewards", "UserId");
        m.CreateIndex("IX_CashbackRewards_TransactionId", "CashbackRewards", "TransactionId", unique: true);
        var createdAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        string[] categories = ["Other", "Restaurants", "Taxi", "Fuel", "Groceries", "Entertainment"];
        decimal[] percentages = [1, 5, 10, 7, 3, 5];
        decimal[] limits = [1000, 3000, 2000, 2500, 2000, 1500];
        for (var i = 0; i < categories.Length; i++)
            m.InsertData(
                table: "CashbackRules",
                columns: ["Id", "Category", "Percentage", "MonthlyLimit", "IsActive", "CreatedAt", "UpdatedAt"],
                columnTypes: ["bigint", "character varying(40)", "numeric(5,2)", "numeric(18,2)", "boolean", "timestamp with time zone", "timestamp with time zone"],
                values: [(long)i + 1, categories[i], percentages[i], limits[i], true, createdAt, createdAt]);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("CashbackRewards");
        m.DropTable("OutboxMessages");
        m.DropTable("ProcessedEvents");
        m.DropTable("CashbackRules");
        m.DropTable("Transactions");
        m.DropTable("Users");
    }
}
