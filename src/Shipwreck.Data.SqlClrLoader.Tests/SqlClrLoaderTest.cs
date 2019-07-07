using Shipwreck.Data.TestAssembly;
using System.Threading.Tasks;
using Xunit;

namespace Shipwreck.Data
{
    public class SqlClrLoaderTest
    {
        [Fact]
        public async Task TestSqlClr()
        {
            using (var db = new SqlClrLoaderTestDbContext())
            {
                db.Database.Delete();
                db.Database.CreateIfNotExists();

                using (var cmd = db.Database.Connection.CreateCommand())
                {
                    await cmd.Connection.OpenAsync();
                    foreach (var s in SqlClrLoader.GetCreateAssemblyAndFunctionsStatements(typeof(Functions).Assembly))
                    {
                        cmd.CommandText = s;
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                using (var cmd = db.Database.Connection.CreateCommand())
                {
                    cmd.CommandText = SqlClrLoader.GetConfigureClrEnabledStatement(true);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = db.Database.Connection.CreateCommand())
                {
                    foreach (var s in SqlClrLoader.GetDropAssemblyAndFunctionsStatements(typeof(Functions).Assembly))
                    {
                        cmd.CommandText = s;
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }
    }
}