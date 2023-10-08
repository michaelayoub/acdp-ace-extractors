using System.Reflection;
using Microsoft.Data.Sqlite;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length != 1) {
            Console.WriteLine("Provide the path to ACE.Entity.dll as the only argument.");
            return;
        }
        
        using var connection = new SqliteConnection("Data Source=enums.db");
        LoadAssemblyAndWriteEnums(args[0], connection);
    }

    static void LoadAssemblyAndWriteEnums(string assemblyLocation, SqliteConnection connection)
    {
        connection.Open();

        var asm = Assembly.LoadFrom(assemblyLocation);
        var enums = asm.GetTypes().Where(x => x.IsEnum && x.Namespace != null && x.Namespace.StartsWith("ACE.Entity.Enum"));

        foreach (Type type in enums)
        {
            var command = connection.CreateCommand();
            if (type.Name == "PropertyInt" || type.Name == "PropertyDataId")
            {
                command.CommandText = "CREATE TABLE IF NOT EXISTS " + type.Name + "(value BLOB, label TEXT, extensionEnum TEXT)";
            }
            else
            {
                command.CommandText = "CREATE TABLE IF NOT EXISTS " + type.Name + "(value BLOB, label TEXT)";
            }
            command.ExecuteNonQuery();

            int count = 0;
            Type underlyingType = Enum.GetUnderlyingType(type);
            foreach (var enumValue in Enum.GetValues(type))
            {
                var enumLabel = enumValue.ToString() ?? throw new Exception("Invalid enumeration found.");

                var insertCommand = connection.CreateCommand();
                if (type.Name == "PropertyInt" && EnumExtension.PropertyIntExtensions.ContainsKey(enumLabel))
                {
                    insertCommand.CommandText = @"
                        INSERT INTO TABLE_NAME (value, label, extensionEnum)
                        VALUES ($value, $label, $extensionEnum)
                    ".Replace("TABLE_NAME", type.Name);
                    insertCommand.Parameters.AddWithValue("$extensionEnum", EnumExtension.PropertyIntExtensions[enumLabel]);
                }
                else if (type.Name == "PropertyDataId" && EnumExtension.PropertyDataIdExtensions.ContainsKey(enumLabel))
                {
                    insertCommand.CommandText = @"
                        INSERT INTO TABLE_NAME (value, label, extensionEnum)
                        VALUES ($value, $label, $extensionEnum)
                    ".Replace("TABLE_NAME", type.Name);
                    insertCommand.Parameters.AddWithValue("$extensionEnum", EnumExtension.PropertyDataIdExtensions[enumLabel]);
                }
                else
                {
                    insertCommand.CommandText = @"
                        INSERT INTO TABLE_NAME (value, label)
                        VALUES ($value, $label)
                    ".Replace("TABLE_NAME", type.Name);
                }

                insertCommand.Parameters.AddWithValue("$value", Convert.ChangeType(enumValue, underlyingType));
                insertCommand.Parameters.AddWithValue("$label", enumValue.ToString());
                insertCommand.ExecuteNonQuery();
                count++;
            }

            Console.WriteLine($"Added enumeration {type.Name} with {count} values.");
        }
    }
}