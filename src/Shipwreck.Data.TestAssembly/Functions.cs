using Microsoft.SqlServer.Server;

namespace Shipwreck.Data.TestAssembly
{
    public static class Functions
    {
        [SqlFunction(IsDeterministic = true)]
        public static bool NegateBoolean(bool b)
            => !b;
    }
}