using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Reflection;
using System.Text;

namespace Shipwreck.Data
{
    public class SqlClrLoader
    {
        private static readonly Dictionary<Type, string> _TYPES = new Dictionary<Type, string>()
        {
            [typeof(bool)] = "bit",
            [typeof(SqlBoolean)] = "bit",

            [typeof(byte)] = "tinyint",
            [typeof(SqlByte)] = "tinyint",
            [typeof(short)] = "smallint",
            [typeof(SqlInt16)] = "smallint",
            [typeof(int)] = "int",
            [typeof(SqlInt32)] = "int",
            [typeof(long)] = "bigint",
            [typeof(SqlInt64)] = "bigint",

            [typeof(float)] = "real",
            [typeof(SqlSingle)] = "real",
            [typeof(double)] = "float",
            [typeof(SqlDouble)] = "float",
            [typeof(decimal)] = "decimal",
            [typeof(SqlDecimal)] = "decimal",

            [typeof(DateTime)] = "datetime2",
            [typeof(DateTimeOffset)] = "datetimeoffset",

            [typeof(string)] = "nvarchar(max)",
            [typeof(SqlString)] = "nvarchar(max)",

            [typeof(SqlBinary)] = "varbinary(max)",
        };

        public static string GetConfigureClrEnabledStatement(bool enabled)
        {
            var sb = new StringBuilder();
            AppendConfigureClrEnabledStatement(sb, enabled);
            return sb.ToString();
        }

        public static string GetCreateAssemblyAndFunctionsStatement(Assembly @assembly)
        {
            var sb = new StringBuilder();

            AppendCreateAssemblyStatement(sb, @assembly);
            sb.AppendLine("GO");

            foreach (var t in @assembly.GetExportedTypes())
            {
                foreach (var method in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var attr = method.GetCustomAttribute<SqlFunctionAttribute>();
                    if (attr != null)
                    {
                        AppendCreateFunctionStatement(sb, method);
                        sb.AppendLine("GO");
                    }
                }
            }
            return sb.ToString();
        }

        public static IEnumerable<string> GetCreateAssemblyAndFunctionsStatements(Assembly @assembly)
        {
            var sb = new StringBuilder();

            AppendCreateAssemblyStatement(sb, @assembly);
            yield return sb.ToString();
            sb.Clear();

            foreach (var t in @assembly.GetExportedTypes())
            {
                foreach (var method in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var attr = method.GetCustomAttribute<SqlFunctionAttribute>();
                    if (attr != null)
                    {
                        AppendCreateFunctionStatement(sb, method);
                        yield return sb.ToString();
                        sb.Clear();
                    }
                }
            }
        }

        public static string GetCreateAssemblyStatement(Assembly @assembly)
        {
            var sb = new StringBuilder();

            AppendCreateAssemblyStatement(sb, @assembly);

            return sb.ToString();
        }

        public static string GetCreateFunctionStatement(MethodInfo method)
        {
            var sb = new StringBuilder();

            AppendCreateFunctionStatement(sb, method);

            return sb.ToString();
        }

        public static string GetDropAssemblyAndFunctionsStatement(Assembly @assembly)
        {
            var n = @assembly.GetName();
            var sb = new StringBuilder();

            foreach (var t in @assembly.GetExportedTypes())
            {
                foreach (var method in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var attr = method.GetCustomAttribute<SqlFunctionAttribute>();
                    if (attr == null)
                    {
                        continue;
                    }

                    AppendDropFunctionStatement(sb, method);
                    sb.AppendLine("GO");
                }
            }

            AppendDropAssemblyStatement(sb, @assembly);
            sb.AppendLine("GO");
            return sb.ToString();
        }

        public static IEnumerable<string> GetDropAssemblyAndFunctionsStatements(Assembly @assembly)
        {
            var n = @assembly.GetName();
            var sb = new StringBuilder();

            foreach (var t in @assembly.GetExportedTypes())
            {
                foreach (var method in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var attr = method.GetCustomAttribute<SqlFunctionAttribute>();
                    if (attr == null)
                    {
                        continue;
                    }

                    AppendDropFunctionStatement(sb, method);
                    yield return sb.ToString();
                    sb.Clear();
                }
            }

            AppendDropAssemblyStatement(sb, @assembly);
            yield return sb.ToString();
            sb.Clear();
        }

        public static string GetDropAssemblyStatement(Assembly @assembly)
        {
            var sb = new StringBuilder();

            AppendDropAssemblyStatement(sb, @assembly);

            return sb.ToString();
        }

        public static string GetDropFunctionStatement(MethodInfo method)
        {
            var sb = new StringBuilder();

            AppendDropFunctionStatement(sb, method);

            return sb.ToString();
        }

        #region Append

        public static void AppendCreateAssemblyStatement(StringBuilder sb, Assembly @assembly)
        {
            var n = @assembly.GetName();
            sb.Append("CREATE ASSEMBLY [").Append(n.Name).Append("] FROM 0x");
            foreach (var b in File.ReadAllBytes(@assembly.Location))
            {
                sb.Append(b.ToString("X2"));
            }
        }

        public static void AppendCreateFunctionStatement(StringBuilder sb, MethodInfo method)
        {
            var attr = method.GetCustomAttribute<SqlFunctionAttribute>()
                ?? throw new ArgumentException();

            sb.Append("CREATE FUNCTION [").Append(method.Name).AppendLine("]");
            sb.AppendLine("(");
            {
                var ps = method.GetParameters();
                for (var i = 0; i < ps.Length; i++)
                {
                    var p = ps[i];
                    sb.Append(i == 0 ? "      " : "    , ").Append('@').Append(p.Name).Append(' ');
                    sb.AppendLine(_TYPES[p.ParameterType]);
                }
            }
            sb.AppendLine(")");
            if (string.IsNullOrEmpty(attr.FillRowMethodName))
            {
                sb.Append("RETURNS ").AppendLine(_TYPES[method.ReturnType]);
            }
            else
            {
                sb.AppendLine("RETURNS TABLE");
                sb.AppendLine("(");

                var ps = method.DeclaringType.GetMethod(attr.FillRowMethodName).GetParameters();
                for (var i = 1; i < ps.Length; i++)
                {
                    var p = ps[i];
                    sb.Append(i == 1 ? "      " : "    , ").Append(p.Name).Append(' ');
                    sb.AppendLine(_TYPES[p.ParameterType.GetElementType()]);
                }

                sb.AppendLine(")");
            }
            sb.AppendLine("AS");

            var n = method.DeclaringType.Assembly.GetName();
            sb.Append("EXTERNAL NAME [").Append(n.Name).Append("].[").Append(method.DeclaringType.FullName).Append("].[").Append(method.Name).AppendLine("]");
        }

        public static void AppendDropAssemblyStatement(StringBuilder sb, Assembly @assembly)
        {
            var n = @assembly.GetName();
            sb.AppendLine($"DROP ASSEMBLY [{n.Name}]");
        }

        public static void AppendDropFunctionStatement(StringBuilder sb, MethodInfo method)
        {
            sb.Append("DROP FUNCTION [").Append(method.Name).AppendLine("]");
        }

        public static void AppendConfigureClrEnabledStatement(StringBuilder sb, bool enabled)
        {
            sb.AppendLine("EXEC sp_configure 'clr enabled' , ").Append(enabled ? 1 : 0).Append(";");
            sb.AppendLine("RECONFIGURE;");
        }

        #endregion Append
    }
}