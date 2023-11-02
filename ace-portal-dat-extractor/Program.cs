using ACE.DatLoader;
using ACE.DatLoader.Entity;
using Microsoft.Data.Sqlite;
using Google.Protobuf;

namespace AcePortalDatExtractor;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Provide the path to client_portal.dat as the only argument.");
            return;
        }

        using var connection = new SqliteConnection("Data Source=portal_dat.db");
        using var protobufOutput = File.Create("portal_dat.binpb");
        LoadPortalDatabaseAndWriteTables(args[0], connection, protobufOutput);
        connection.Close();
        protobufOutput.Close();
    }

    static PortalDatDatabase LoadPortalDat(string portalDatLocation, bool addRetiredSkills = false)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        PortalDatDatabase portalDatabase = new(portalDatLocation, false);
        if (addRetiredSkills)
        {
            portalDatabase.SkillTable.AddRetiredSkills();
        }

        Console.WriteLine($"Successfully opened client_portal.dat countaining {portalDatabase.AllFiles.Count} records.");

        return portalDatabase;
    }

    static void LoadPortalDatabaseAndWriteTables(string portalDatLocation, SqliteConnection connection, FileStream protobufOutput)
    {
        connection.Open();

        PortalDatDatabase datDatabase = LoadPortalDat(portalDatLocation);
        PortalDatDB portalDatDB = new();

        LoadSkillTable(datDatabase, connection, portalDatDB);
        LoadXpTable(datDatabase, connection, portalDatDB);
        LoadContractTable(datDatabase, connection, portalDatDB);
        LoadSecondaryAttributeTable(datDatabase, connection, portalDatDB);

        portalDatDB.WriteTo(protobufOutput);
    }

    static void CheckCountsAndThrow(int numSource, int numIntoCollection, SqliteConnection connection, string sqliteTableName)
    {
        long numIntoSqlite = CountForTable(connection, sqliteTableName);
        if (numSource != numIntoCollection || numSource != numIntoSqlite)
        {
            throw new Exception($"Table={sqliteTableName}: Source had {numSource}, collection has {numIntoCollection}, SQLite has {numIntoSqlite}.");
        }
    }

    static void LoadSkillTable(PortalDatDatabase portalDatabase, SqliteConnection connection, PortalDatDB portalDatDB)
    {
        CreateSqliteTable(connection);

        foreach (var skill in portalDatabase.SkillTable.SkillBaseHash)
        {
            SkillBase skillBase = skill.Value;

            InsertSkillIntoSqlite(connection, skill, skillBase);
            InsertSkillIntoCollection(portalDatDB, skill, skillBase);
        }

        CheckCountsAndThrow(portalDatabase.SkillTable.SkillBaseHash.Count, portalDatDB.Skills.Count, connection, "skills");

        static void InsertSkillIntoSqlite(SqliteConnection connection, KeyValuePair<uint, SkillBase> skill, SkillBase skillBase)
        {
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO skills ( id, attr1, attr2, divisor, description, name, icon_id )
                VALUES ( $id, $attr1, $attr2, $divisor, $description, $name, $icon_id )
            ";
            insertCommand.Parameters.AddWithValue("$id", skill.Key);
            insertCommand.Parameters.AddWithValue("$attr1", skillBase.Formula.Attr1);
            insertCommand.Parameters.AddWithValue("$attr2", skillBase.Formula.Attr2);
            insertCommand.Parameters.AddWithValue("$divisor", skillBase.Formula.Z);
            insertCommand.Parameters.AddWithValue("$description", skillBase.Description ?? "");
            insertCommand.Parameters.AddWithValue("$name", skillBase.Name ?? "");
            insertCommand.Parameters.AddWithValue("$icon_id", skillBase.IconId);

            insertCommand.ExecuteNonQuery();
        }

        static void InsertSkillIntoCollection(PortalDatDB portalDatDB, KeyValuePair<uint, SkillBase> skill, SkillBase skillBase)
        {
            portalDatDB.Skills.Add(new Skill
            {
                Id = skill.Key,
                Attr1 = skillBase.Formula.Attr1,
                Attr2 = skillBase.Formula.Attr2,
                Divisor = skillBase.Formula.Z,
                Description = skillBase.Description ?? "",
                Name = skillBase.Name ?? "",
                IconId = skillBase.IconId
            });
        }

        static void CreateSqliteTable(SqliteConnection connection)
        {
            var createSqliteTableCommand = connection.CreateCommand();
            createSqliteTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS skills (
                id INTEGER,
                attr1 INTEGER,
                attr2 INTEGER,
                divisor INTEGER,
                description TEXT,
                name TEXT,
                icon_id INTEGER
            )";
            createSqliteTableCommand.ExecuteNonQuery();
        }
    }



    static void LoadXpTable(PortalDatDatabase portalDatabase, SqliteConnection connection, PortalDatDB portalDatDB)
    {
        var tableDict = new Dictionary<string, (dynamic, dynamic)> {
            {"attribute_xp_list", (portalDatabase.XpTable.AttributeXpList, portalDatDB.AttributeXp)},
            {"vital_xp_list", (portalDatabase.XpTable.VitalXpList, portalDatDB.VitalXp)},
            {"trained_skill_xp_list", (portalDatabase.XpTable.TrainedSkillXpList, portalDatDB.TrainedSkillXp)},
            {"specialized_skill_xp_list", (portalDatabase.XpTable.SpecializedSkillXpList, portalDatDB.SpecializedSkillXp)},
            {"character_level_xp_list", (portalDatabase.XpTable.CharacterLevelXPList, portalDatDB.CharacterLevelXp)}
        };

        foreach (var pair in tableDict)
        {
            string tableName = pair.Key;
            var (list, collection) = pair.Value;

            CreateSqliteTable(tableName, connection);

            if (list is List<uint> uintList)
            {
                InsertIntoXpListSqlite<uint>(pair.Key, list);

                int i = 1;
                foreach (uint cost in uintList.Skip(1))
                {
                    collection.Add(new XpListElementInt { Level = i++, Cost = cost });
                }

            }
            else if (list is List<ulong> ulongList)
            {
                InsertIntoXpListSqlite<ulong>(pair.Key, list);

                int i = 1;
                foreach (ulong cost in ulongList.Skip(1))
                {
                    collection.Add(new XpListElementLong { Level = i++, Cost = cost });
                }
            }

            // -1 because we .Skip(1)
            CheckCountsAndThrow(list.Count - 1, collection.Count, connection, tableName);
        }

        void CreateSqliteTable(string name, SqliteConnection connection)
        {
            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = $"CREATE TABLE IF NOT EXISTS {name} ( level INTEGER, cost INTEGER )";
            createTableCommand.ExecuteNonQuery();
        }

        void InsertIntoXpListSqlite<T>(string listName, List<T> list)
        {
            int i = 1;
            foreach (var level in list.Skip(1))
            {
                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = @"
                    INSERT INTO TABLE_NAME ( level, cost )
                    VALUES ( $level, $cost )
                ".Replace("TABLE_NAME", listName);
                insertCommand.Parameters.AddWithValue("$level", i);
                insertCommand.Parameters.AddWithValue("$cost", level);
                insertCommand.ExecuteNonQuery();
                i++;
            }
        }
    }
    static void LoadContractTable(PortalDatDatabase portalDatabase, SqliteConnection connection, PortalDatDB portalDatDB)
    {
        CreateSqliteTable(connection);

        foreach (var contractKv in portalDatabase.ContractTable.Contracts)
        {
            var contract = contractKv.Value;

            InsertContractIntoSqlite(connection, contractKv, contract);
            InsertContractIntoCollection(portalDatDB, contractKv, contract);

        }

        CheckCountsAndThrow(portalDatabase.ContractTable.Contracts.Count, portalDatDB.Contracts.Count, connection, "contracts");

        static void InsertContractIntoSqlite(SqliteConnection connection, KeyValuePair<uint, ACE.DatLoader.Entity.Contract> contractKv, ACE.DatLoader.Entity.Contract contract)
        {
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO contracts ( id, version, name, description,
                                        description_progress, name_npc_start, name_npc_end, questflag_stamped,
                                        questflag_started, questflag_finished, questflag_progress, questflag_timer,
                                        questflag_repeat_time, location_npc_start, location_npc_end, location_quest_area )
                VALUES ( $id, $version, $name, $description,
                         $description_progress, $name_npc_start, $name_npc_end, $questflag_stamped,
                         $questflag_started, $questflag_finished, $questflag_progress, $questflag_timer,
                         $questflag_repeat_time, $location_npc_start, $location_npc_end, $location_quest_area )
            ";
            insertCommand.Parameters.AddWithValue("$id", contractKv.Key);
            insertCommand.Parameters.AddWithValue("$version", contract.Version);
            insertCommand.Parameters.AddWithValue("$name", contract.ContractName);
            insertCommand.Parameters.AddWithValue("$description", contract.Description);
            insertCommand.Parameters.AddWithValue("$description_progress", contract.DescriptionProgress);
            insertCommand.Parameters.AddWithValue("$name_npc_start", contract.NameNPCStart);
            insertCommand.Parameters.AddWithValue("$name_npc_end", contract.NameNPCEnd);
            insertCommand.Parameters.AddWithValue("$questflag_stamped", contract.QuestflagStamped);
            insertCommand.Parameters.AddWithValue("$questflag_started", contract.QuestflagStarted);
            insertCommand.Parameters.AddWithValue("$questflag_finished", contract.QuestflagFinished);
            insertCommand.Parameters.AddWithValue("$questflag_progress", contract.QuestflagProgress);
            insertCommand.Parameters.AddWithValue("$questflag_timer", contract.QuestflagTimer);
            insertCommand.Parameters.AddWithValue("$questflag_repeat_time", contract.QuestflagRepeatTime);
            insertCommand.Parameters.AddWithValue("$location_npc_start", contract.LocationNPCStart.Frame.ToString());
            insertCommand.Parameters.AddWithValue("$location_npc_end", contract.LocationNPCEnd.Frame.ToString());
            insertCommand.Parameters.AddWithValue("$location_quest_area", contract.LocationQuestArea.Frame.ToString());

            insertCommand.ExecuteNonQuery();
        }

        static void InsertContractIntoCollection(PortalDatDB portalDatDB, KeyValuePair<uint, ACE.DatLoader.Entity.Contract> contractKv, ACE.DatLoader.Entity.Contract contract)
        {
            portalDatDB.Contracts.Add(new Contract
            {
                Id = contractKv.Key,
                Version = contract.Version,
                Name = contract.ContractName,
                Description = contract.Description,
                DescriptionProgress = contract.DescriptionProgress,
                NameNpcStart = contract.NameNPCStart,
                NameNpcEnd = contract.NameNPCEnd,
                QuestflagStamped = contract.QuestflagStamped,
                QuestflagStarted = contract.QuestflagStarted,
                QuestflagFinished = contract.QuestflagFinished,
                QuestflagProgress = contract.QuestflagProgress,
                QuestflagTimer = contract.QuestflagTimer,
                QuestflagRepeatTime = contract.QuestflagRepeatTime,
                LocationNpcStart = contract.LocationNPCStart.Frame.ToString(),
                LocationNpcEnd = contract.LocationNPCEnd.Frame.ToString(),
                LocationQuestArea = contract.LocationQuestArea.Frame.ToString()
            });
        }

        static void CreateSqliteTable(SqliteConnection connection)
        {
            var createSqliteTableCommand = connection.CreateCommand();
            createSqliteTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS contracts (
                id INTEGER,
                version INTEGER,
                name TEXT,
                description TEXT,
                description_progress TEXT,
                name_npc_start TEXT,
                name_npc_end TEXT,
                questflag_stamped TEXT,
                questflag_started TEXT,
                questflag_finished TEXT,
                questflag_progress TEXT,
                questflag_timer TEXT,
                questflag_repeat_time TEXT,
                location_npc_start TEXT,
                location_npc_end TEXT,
                location_quest_area TEXT
            )";
            createSqliteTableCommand.ExecuteNonQuery();
        }

    }
    static void LoadSecondaryAttributeTable(PortalDatDatabase portalDatabase, SqliteConnection connection, PortalDatDB portalDatDB)
    {
        CreateSqliteTable(connection);
        InsertAttributesIntoSqlite(portalDatabase, connection);
        InsertAttributesIntoCollection(portalDatabase, portalDatDB);

        static void InsertAttributesIntoSqlite(PortalDatDatabase portalDatabase, SqliteConnection connection)
        {
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO secondary_attribute ( health_attribute_type, health_attribute_divisor, stamina_attribute_type,
                                                  stamina_attribute_divisor, mana_attribute_type, mana_attribute_divisor )
                VALUES ( $health_attribute_type, $health_attribute_divisor, $stamina_attribute_type,
                         $stamina_attribute_divisor, $mana_attribute_type, $mana_attribute_divisor )
        ";
            insertCommand.Parameters.AddWithValue("$health_attribute_type", portalDatabase.SecondaryAttributeTable.MaxHealth.Formula.Attr1);
            insertCommand.Parameters.AddWithValue("$health_attribute_divisor", portalDatabase.SecondaryAttributeTable.MaxHealth.Formula.Z);
            insertCommand.Parameters.AddWithValue("$stamina_attribute_type", portalDatabase.SecondaryAttributeTable.MaxStamina.Formula.Attr1);
            insertCommand.Parameters.AddWithValue("$stamina_attribute_divisor", portalDatabase.SecondaryAttributeTable.MaxStamina.Formula.Z);
            insertCommand.Parameters.AddWithValue("$mana_attribute_type", portalDatabase.SecondaryAttributeTable.MaxMana.Formula.Attr1);
            insertCommand.Parameters.AddWithValue("$mana_attribute_divisor", portalDatabase.SecondaryAttributeTable.MaxMana.Formula.Z);

            insertCommand.ExecuteNonQuery();
        }

        static void InsertAttributesIntoCollection(PortalDatDatabase portalDatabase, PortalDatDB portalDatDB)
        {
            portalDatDB.SecondaryAttributes = new()
            {
                HealthAttributeType = portalDatabase.SecondaryAttributeTable.MaxHealth.Formula.Attr1,
                HealthAttributeDivisor = portalDatabase.SecondaryAttributeTable.MaxHealth.Formula.Z,
                StaminaAttributeType = portalDatabase.SecondaryAttributeTable.MaxStamina.Formula.Attr1,
                StaminaAttributeDivisor = portalDatabase.SecondaryAttributeTable.MaxStamina.Formula.Z,
                ManaAttributeType = portalDatabase.SecondaryAttributeTable.MaxMana.Formula.Attr1,
                ManaAttributeDivisor = portalDatabase.SecondaryAttributeTable.MaxMana.Formula.Z
            };
        }

        static void CreateSqliteTable(SqliteConnection connection)
        {
            var createSqliteTableCommand = connection.CreateCommand();
            createSqliteTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS secondary_attribute (
                health_attribute_type INTEGER,
                health_attribute_divisor INTEGER,
                stamina_attribute_type INTEGER,
                stamina_attribute_divisor INTEGER,
                mana_attribute_type INTEGER,
                mana_attribute_divisor INTEGER
            )";
            createSqliteTableCommand.ExecuteNonQuery();
        }
    }

    static long CountForTable(SqliteConnection connection, string tableName)
    {
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM " + tableName + ";";
        return (long)(countCommand.ExecuteScalar() ?? 0);
    }
}