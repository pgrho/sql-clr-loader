using Shipwreck.Data.TestAssembly;
using System.Data.SqlClient;
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

        [Fact]
        public async Task TestSqlClrIfExists()
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
                    cmd.CommandText = SqlClrLoader.GetDropFunctionIfExistsStatement(nameof(Functions.NegateBoolean));
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = db.Database.Connection.CreateCommand())
                {
                    cmd.CommandText = SqlClrLoader.GetDropFunctionIfExistsStatement(nameof(Functions.NegateBoolean));
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = db.Database.Connection.CreateCommand())
                {
                    cmd.CommandText = SqlClrLoader.GetDropAssemblyIfExistsStatement(typeof(Functions).Assembly.GetName().Name);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        [Fact]
        public async Task TestSchema()
        {
            using (var db = new SqlClrLoaderTestDbContext())
            {
                db.Database.Delete();
                db.Database.CreateIfNotExists();

                using (var cmd = db.Database.Connection.CreateCommand())
                {
                    await cmd.Connection.OpenAsync();
                    cmd.CommandText = SqlClrLoader.GetCreateAssemblyStatement(typeof(Functions).Assembly);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = db.Database.Connection.CreateCommand())
                {
                    cmd.CommandText = SqlClrLoader.GetCreateFunctionStatement(typeof(Functions).GetMethod(nameof(Functions.NegateBoolean)), schema: "dbo");
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = db.Database.Connection.CreateCommand())
                {
                    cmd.CommandText = SqlClrLoader.GetDropFunctionIfExistsStatement(nameof(Functions.NegateBoolean), schema: "dbo");
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = db.Database.Connection.CreateCommand())
                {
                    cmd.CommandText = SqlClrLoader.GetDropAssemblyIfExistsStatement(typeof(Functions).Assembly.GetName().Name);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        [Theory]
        [InlineData(PermissionSet.Safe)]
        [InlineData(PermissionSet.ExternalAccess)]
        [InlineData(PermissionSet.Unsafe)]
        public async Task TestPermissionSet(PermissionSet ps)
        {
            try
            {
                using (var db = new SqlClrLoaderTestDbContext())
                {
                    db.Database.Delete();
                    db.Database.CreateIfNotExists();

                    using (var cmd = db.Database.Connection.CreateCommand())
                    {
                        await cmd.Connection.OpenAsync();
                        cmd.CommandText = SqlClrLoader.GetCreateAssemblyStatement(typeof(Functions).Assembly, ps);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    using (var cmd = db.Database.Connection.CreateCommand())
                    {
                        cmd.CommandText = SqlClrLoader.GetDropAssemblyIfExistsStatement(typeof(Functions).Assembly.GetName().Name);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (SqlException ex) when (ex.Number == 10327)
            {
            }
        }
    }
}