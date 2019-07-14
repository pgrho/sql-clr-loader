using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Reflection;
using System.Text;

namespace Shipwreck.Data
{
    public static class SqlClrLoader
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

        public static string GetCreateAssemblyAndFunctionsStatement(Assembly assembly, PermissionSet permissionSet = PermissionSet.Default)
        {
            var sb = new StringBuilder();

            AppendCreateAssemblyStatement(sb, assembly, permissionSet);
            sb.AppendLine("GO");

            foreach (var t in assembly.GetExportedTypes())
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

        public static IEnumerable<string> GetCreateAssemblyAndFunctionsStatements(Assembly assembly, PermissionSet permissionSet = PermissionSet.Default)
        {
            var sb = new StringBuilder();

            AppendCreateAssemblyStatement(sb, assembly, permissionSet);
            yield return sb.ToString();
            sb.Clear();

            foreach (var t in assembly.GetExportedTypes())
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

        public static string GetCreateAssemblyStatement(Assembly assembly, PermissionSet permissionSet = PermissionSet.Default)
            => new StringBuilder().AppendCreateAssemblyStatement(assembly, permissionSet).ToString();

        public static string GetCreateFunctionStatement(MethodInfo method, string functionName = null, string schema = null)
        {
            var sb = new StringBuilder();

            AppendCreateFunctionStatement(sb, method, functionName: functionName, schema: schema);

            return sb.ToString();
        }

        public static string GetDropAssemblyAndFunctionsStatement(Assembly assembly)
        {
            var sb = new StringBuilder();

            foreach (var t in assembly.GetExportedTypes())
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

            AppendDropAssemblyStatement(sb, assembly);
            sb.AppendLine("GO");
            return sb.ToString();
        }

        public static IEnumerable<string> GetDropAssemblyAndFunctionsStatements(Assembly assembly)
        {
            var sb = new StringBuilder();

            foreach (var t in assembly.GetExportedTypes())
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

            AppendDropAssemblyStatement(sb, assembly);
            yield return sb.ToString();
            sb.Clear();
        }

        public static string GetDropAssemblyStatement(Assembly assembly)
            => new StringBuilder().AppendDropAssemblyStatement(assembly).ToString();

        public static string GetDropAssemblyIfExistsStatement(string assembly)
            => new StringBuilder()
                .AppendIfExistsStatement(assembly)
                .Append("    ").AppendDropAssemblyStatement(assembly).ToString();

        public static string GetDropFunctionStatement(MethodInfo method)
            => new StringBuilder().AppendDropFunctionStatement(method).ToString();

        public static string GetDropFunctionIfExistsStatement(string name, bool isScalar = true, string schema = null)
            => new StringBuilder()
                .AppendIfExistsStatement(isScalar ? "FS" : "FT", name)
                .AppendDropFunctionStatement(name, schema: schema).ToString();

        #region Append

        public static StringBuilder AppendCreateAssemblyStatement(this StringBuilder sb, Assembly assembly, PermissionSet permissionSet = PermissionSet.Default)
        {
            var n = assembly.GetName();
            sb.Append("CREATE ASSEMBLY [").Append(n.Name).Append("] FROM 0x");
            foreach (var b in File.ReadAllBytes(assembly.Location))
            {
                sb.Append(b.ToString("X2"));
            }
            switch (permissionSet)
            {
                case PermissionSet.Safe:
                    sb.Append(" WITH PERMISSION_SET = SAFE");
                    break;

                case PermissionSet.ExternalAccess:
                    sb.Append(" WITH PERMISSION_SET = EXTERNAL_ACCESS");
                    break;

                case PermissionSet.Unsafe:
                    sb.Append(" WITH PERMISSION_SET = UNSAFE");
                    break;
            }
            return sb;
        }

        public static StringBuilder AppendCreateFunctionStatement(this StringBuilder sb, MethodInfo method, string functionName = null, string schema = null)
        {
            var attr = method.GetCustomAttribute<SqlFunctionAttribute>()
                ?? throw new ArgumentException();

            sb.Append("CREATE FUNCTION ");
            if (!string.IsNullOrEmpty(schema))
            {
                sb.Append("[").Append(schema).Append("].");
            }
            sb.Append("[").Append(functionName ?? method.Name).AppendLine("]");
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
            return sb.Append("EXTERNAL NAME [").Append(n.Name).Append("].[").Append(method.DeclaringType.FullName).Append("].[").Append(method.Name).AppendLine("]");
        }

        public static StringBuilder AppendDropAssemblyStatement(this StringBuilder sb, Assembly assembly)
            => sb.AppendDropAssemblyStatement(assembly.GetName().Name);

        public static StringBuilder AppendDropAssemblyStatement(this StringBuilder sb, string assemblyName)
            => sb.AppendLine($"DROP ASSEMBLY [{assemblyName}]");

        public static StringBuilder AppendDropFunctionStatement(this StringBuilder sb, MethodInfo method)
            => sb.AppendDropFunctionStatement(method.Name);

        public static StringBuilder AppendDropFunctionStatement(this StringBuilder sb, string functionName, string schema = null)
        {
            sb.Append("DROP FUNCTION ");
            if (!string.IsNullOrEmpty(schema))
            {
                sb.Append("[").Append(schema).Append("].");
            }
            return sb.Append("[").Append(functionName).AppendLine("]");
        }

        public static StringBuilder AppendConfigureClrEnabledStatement(this StringBuilder sb, bool enabled)
        {
            sb.AppendLine("EXEC sp_configure 'clr enabled' , ").Append(enabled ? 1 : 0).Append(";");
            return sb.AppendLine("RECONFIGURE;");
        }

        public static StringBuilder AppendIfExistsStatement(this StringBuilder sb, string name)
            => sb.Append("IF EXISTS(SELECT * FROM sys.assemblies WHERE name = '").Append(name).AppendLine("')");

        public static StringBuilder AppendIfNotExistsStatement(this StringBuilder sb, string name)
            => sb.Append("IF NOT EXISTS(SELECT * FROM sys.assemblies WHERE name = '").Append(name).AppendLine("')");

        public static StringBuilder AppendIfChangedStatement(this StringBuilder sb, Assembly assembly)
        {
            var n = assembly.GetName();
            sb.Append(
                "IF NOT EXISTS(SELECT * FROM sys.assembly_files WHERE name = '").Append(n.Name).Append("' AND content = 0x");

            foreach (var b in File.ReadAllBytes(assembly.Location))
            {
                sb.Append(b.ToString("X2"));
            }

            return sb.AppendLine(")");
        }

        public static StringBuilder AppendIfNotChangedStatement(this StringBuilder sb, Assembly assembly)
        {
            var n = assembly.GetName();
            sb.Append(
                "IF EXISTS(SELECT * FROM sys.assembly_files WHERE name = '").Append(n.Name).Append("' AND content = 0x");

            foreach (var b in File.ReadAllBytes(assembly.Location))
            {
                sb.Append(b.ToString("X2"));
            }

            return sb.AppendLine(")");
        }

        public static StringBuilder AppendIfExistsStatement(this StringBuilder sb, string type, string name)
            => sb.Append("IF EXISTS(SELECT * FROM sys.all_objects WHERE type = '").Append(type).Append("' AND name = '").Append(name).AppendLine("')");

        public static StringBuilder AppendIfNotExistsStatement(this StringBuilder sb, string type, string name)
            => sb.Append("IF NOT EXISTS(SELECT * FROM sys.all_objects WHERE type = '").Append(type).Append("' AND name = '").Append(name).AppendLine("')");

        #endregion Append
    }
}