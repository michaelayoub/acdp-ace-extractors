using System.Reflection;
using Microsoft.Data.Sqlite;
using Google.Protobuf;

internal class Program
{
    public static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Provide the path to ACE.Entity.dll as the only argument.");
            return;
        }

        using var connection = new SqliteConnection("Data Source=enums.db");
        using var protobufOutput = File.Create("enums.binpb");
        LoadAssemblyAndWriteEnums(args[0], connection, protobufOutput);
        connection.Close();
        protobufOutput.Close();
    }

    static void LoadAssemblyAndWriteEnums(string assemblyLocation, SqliteConnection connection, FileStream protobufOutput)
    {
        connection.Open();
        EnumerationDB enumerationDB = InitializeEnumerationDB();

        var asm = Assembly.LoadFrom(assemblyLocation);
        var aceEnums = asm.GetTypes().Where(x => x.IsEnum && x.Namespace != null && x.Namespace.StartsWith("ACE.Entity.Enum"));

        foreach (Type aceEnumType in aceEnums)
        {
            CreateSqliteTableForType(aceEnumType, connection);
            Enumeration enumeration = CreateEnumerationForAceEnum(aceEnumType);

            int count = 0;
            foreach (var aceEnumValue in Enum.GetValues(aceEnumType))
            {
                var aceEnumLabel = aceEnumValue.ToString() ?? throw new Exception("Invalid enumeration found.");

                InsertAceEnumIntoSqlite(connection, aceEnumType, aceEnumValue, aceEnumLabel);
                InsertAceEnumIntoCollection(enumeration, aceEnumType, aceEnumValue, aceEnumLabel);

                count++;
            }

            enumerationDB.Enums.Add(enumeration);

            CheckCountsAndThrow(count, enumeration.Values.Count, connection, aceEnumType.Name);

        }

        enumerationDB.WriteTo(protobufOutput);
    }

    static EnumerationDB InitializeEnumerationDB()
    {
        return new()
        {
            GitHash = "N/A",
            Enums = { }
        };
    }

    static void CreateSqliteTableForType(Type type, SqliteConnection connection)
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
    }

    static Enumeration CreateEnumerationForAceEnum(Type type)
    {
        return new()
        {
            Name = type.Name,
            Values = { }
        };
    }

    static void InsertAceEnumIntoSqlite(SqliteConnection connection, Type type, object enumValue, string enumLabel)
    {
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

        insertCommand.Parameters.AddWithValue("$value", Convert.ChangeType(enumValue, typeof(long)));
        insertCommand.Parameters.AddWithValue("$label", enumValue.ToString());
        insertCommand.ExecuteNonQuery();
    }

    static void InsertAceEnumIntoCollection(Enumeration enumeration, Type type, object enumValue, string enumLabel)
    {
        if (type.Name == "PropertyInt" && EnumExtension.PropertyIntExtensions.ContainsKey(enumLabel))
        {
            enumeration.Values.Add(new EnumerationValue()
            {
                Value = (long)Convert.ChangeType(enumValue, typeof(long)),
                Label = enumValue.ToString(),
                Extension = EnumExtension.PropertyIntExtensions[enumLabel]
            });
        }
        else if (type.Name == "PropertyDataId" && EnumExtension.PropertyDataIdExtensions.ContainsKey(enumLabel))
        {
            enumeration.Values.Add(new EnumerationValue()
            {
                Value = (long)Convert.ChangeType(enumValue, typeof(long)),
                Label = enumValue.ToString(),
                Extension = EnumExtension.PropertyDataIdExtensions[enumLabel]
            });
        }
        else
        {
            enumeration.Values.Add(new EnumerationValue()
            {
                Value = (long)Convert.ChangeType(enumValue, typeof(long)),
                Label = enumValue.ToString()
            });
        }
    }

    static long CountForTable(SqliteConnection connection, string tableName)
    {
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM " + tableName + ";";
        return (long)(countCommand.ExecuteScalar() ?? 0);
    }

    static void CheckCountsAndThrow(int numSource, int numIntoCollection, SqliteConnection connection, string sqliteTableName)
    {
        long numIntoSqlite = CountForTable(connection, sqliteTableName);
        if (numSource != numIntoCollection || numSource != numIntoSqlite)
        {
            throw new Exception($"Table={sqliteTableName}: Source had {numSource}, collection has {numIntoCollection}, SQLite has {numIntoSqlite}.");
        }
    }
}