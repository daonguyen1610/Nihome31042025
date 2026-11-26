using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NihomeBackend.Data;

namespace nihomebackend.tests.Data;

public sealed class SwitchDriveFoldersToPrdV2MigrationTests
{
    private const string OldPaths =
        "'01_Khao_sat','01_CRM_PreDesign','02_Thiet_ke/01_So_bo_Concept','02_Thiet_ke/02_Co_so'," +
        "'02_Thiet_ke/03_Chi_tiet_ShopDrawing','03_Xin_phep_Phap_ly','04_Thi_cong_Nghiem_thu'," +
        "'05_Cung_ung_Vat_tu','06_Tai_chinh_Hop_dong'";

    [Theory]
    [InlineData("01_Khao_sat", "01_CoHoi_BaoGia/01_KhaoSat", 0)]
    [InlineData("Custom_Survey", "Custom_Survey", 1)]
    public void Upgrade_switches_only_the_old_default_tree_and_drops_its_folder_links(
        string surveyFolder, string expectedSurveyFolder, long expectedLinks)
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var migrations = db.GetService<IMigrationsAssembly>();
        var id = migrations.Migrations.Keys.Single(key => key.EndsWith("_SwitchDriveFoldersToPrdV2", StringComparison.Ordinal));
        var migration = migrations.CreateMigration(migrations.Migrations[id], db.Database.ProviderName!);
        Exec("CREATE TABLE google_drive_credentials (SurveyMediaFolder TEXT, CrmPreDesignFolder TEXT, " +
            "DesignConceptFolder TEXT, DesignBasicFolder TEXT, DesignShopDrawingFolder TEXT, LegalPermitsFolder TEXT, " +
            "ConstructionAcceptanceFolder TEXT, ProcurementFolder TEXT, FinanceContractsFolder TEXT)");
        Exec("CREATE TABLE project_drive_folders (Id INTEGER PRIMARY KEY)");
        Exec($"INSERT INTO google_drive_credentials VALUES ({OldPaths.Replace("01_Khao_sat", surveyFolder)})");
        Exec("INSERT INTO project_drive_folders VALUES (1)");
        Exec("CREATE TABLE project_documents (Id INTEGER PRIMARY KEY, Category TEXT, SourceEntityType TEXT)");
        Exec("INSERT INTO project_documents VALUES (1, 'ConstructionAcceptance', 'HandoverRecord'), " +
            "(2, 'ConstructionAcceptance', 'AsBuiltDocument'), (3, 'ConstructionAcceptance', 'AcceptanceRecord')");
        Exec("CREATE TABLE translations (\"Key\" TEXT, LanguageCode TEXT, Value TEXT)");
        Exec("INSERT INTO translations VALUES " +
            "('operationalProjects.documents.category.FinanceContracts', 'vi', 'Tài chính và hợp đồng'), " +
            "('operationalProjects.documents.category.FinanceContracts', 'en', 'Edited by an administrator')");

        Apply(migration.UpOperations);

        Assert.Equal(expectedSurveyFolder, Scalar("SELECT SurveyMediaFolder FROM google_drive_credentials"));
        Assert.Equal(expectedLinks, Scalar("SELECT COUNT(*) FROM project_drive_folders"));
        // As-built and handover files belong to 08_BanGiao_BaoHanh; acceptance records stay in 05/03.
        Assert.Equal(2L, Scalar("SELECT COUNT(*) FROM project_documents WHERE Category = 'Handover'"));
        Assert.Equal("ConstructionAcceptance", Scalar("SELECT Category FROM project_documents WHERE Id = 3"));
        Assert.Equal("Hợp đồng", Scalar("SELECT Value FROM translations WHERE LanguageCode = 'vi'"));
        Assert.Equal("Edited by an administrator", Scalar("SELECT Value FROM translations WHERE LanguageCode = 'en'"));

        Apply(migration.DownOperations);

        Assert.Equal(surveyFolder, Scalar("SELECT SurveyMediaFolder FROM google_drive_credentials"));
        Assert.Equal(0L, Scalar("SELECT COUNT(*) FROM project_documents WHERE Category = 'Handover'"));
        Assert.Equal("Tài chính và hợp đồng", Scalar("SELECT Value FROM translations WHERE LanguageCode = 'vi'"));

        void Exec(string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        object Scalar(string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteScalar()!;
        }
        void Apply(IReadOnlyList<MigrationOperation> operations)
        {
            foreach (var operation in operations.OfType<SqlOperation>()) Exec(operation.Sql.Replace("N'", "'"));
        }
    }
}
