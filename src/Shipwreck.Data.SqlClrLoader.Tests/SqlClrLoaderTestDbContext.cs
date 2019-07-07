using System.Data.Entity;

namespace Shipwreck.Data
{
    internal sealed class SqlClrLoaderTestDbContext : DbContext
    {
        static SqlClrLoaderTestDbContext()
        {
            Database.SetInitializer(new DropCreateDatabaseAlways<SqlClrLoaderTestDbContext>());
        }
    }
}