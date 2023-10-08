using ACE.DatLoader.FileTypes;
using ACE.DatLoader;
using ACE.DatLoader.Entity;
using Microsoft.Data.Sqlite;
using System;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length != 1) {
            Console.WriteLine("Provide the path to client_portal.dat as the only argument.");
            return;
        }

        PortalDatDatabase datDatabase = LoadPortalDat(args[0]);

        using var connection = new SqliteConnection("Data Source=portal_dat.db");

        connection.Open();

        LoadSkillTable(datDatabase, connection);
        LoadXpTable(datDatabase, connection);
        LoadContractTable(datDatabase, connection);
        LoadSecondaryAttributeTable(datDatabase, connection);
    }

    static PortalDatDatabase LoadPortalDat(string portalDatLocation, bool addRetiredSkills = false)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        PortalDatDatabase portalDatabase = new PortalDatDatabase(portalDatLocation, false);
        if (addRetiredSkills)
        {
            portalDatabase.SkillTable.AddRetiredSkills();
        }

        Console.WriteLine($"Successfully opened client_portal.dat countaining {portalDatabase.AllFiles.Count} records.");

        return portalDatabase;
    }

    static void LoadSkillTable(PortalDatDatabase portalDatabase, SqliteConnection connection)
    {
        var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS skills (
                id INTEGER,
                attr1 INTEGER,
                attr2 INTEGER,
                divisor INTEGER,
                description TEXT,
                name TEXT,
                icon_id INTEGER
            )
        ";
        createTableCommand.ExecuteNonQuery();

        foreach (var skill in portalDatabase.SkillTable.SkillBaseHash)
        {
            SkillBase skillBase = skill.Value;

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
    }
    static void LoadXpTable(PortalDatDatabase portalDatabase, SqliteConnection connection)
    {
        void CreateTable(string name, SqliteConnection connection)
        {
            var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = $"CREATE TABLE IF NOT EXISTS {name} ( level INTEGER, cost INTEGER )";
            createTableCommand.ExecuteNonQuery();
        }

        void InsertIntoXPTable<T>(string tableName, List<T> list)
        {
            int i = 1;
            foreach (var level in list.Skip(1))
            {
                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = @"
                    INSERT INTO TABLE_NAME ( level, cost )
                    VALUES ( $level, $cost )
                ".Replace("TABLE_NAME", tableName);
                insertCommand.Parameters.AddWithValue("$level", i);
                insertCommand.Parameters.AddWithValue("$cost", level);
                insertCommand.ExecuteNonQuery();

                i++;
            }
        }

        foreach (var (tableName, list) in new List<(string, List<uint>)> {
            ("attribute_xp_list", portalDatabase.XpTable.AttributeXpList),
            ("vital_xp_list", portalDatabase.XpTable.VitalXpList),
            ("trained_skill_xp_list", portalDatabase.XpTable.TrainedSkillXpList),
            ("specialized_skill_xp_list", portalDatabase.XpTable.SpecializedSkillXpList),
        })
        {
            CreateTable(tableName, connection);
            InsertIntoXPTable<uint>(tableName, list);
        }

        CreateTable("character_level_xp_list", connection);
        InsertIntoXPTable<ulong>("character_level_xp_list", portalDatabase.XpTable.CharacterLevelXPList);

    }
    static void LoadContractTable(PortalDatDatabase portalDatabase, SqliteConnection connection)
    {
        var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
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
            )
        ";
        createTableCommand.ExecuteNonQuery();

        foreach (var contractKv in portalDatabase.ContractTable.Contracts)
        {
            var contract = contractKv.Value;

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
    }
    static void LoadSecondaryAttributeTable(PortalDatDatabase portalDatabase, SqliteConnection connection)
    {
        var createTableCommand = connection.CreateCommand();
        createTableCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS secondary_attribute (
                health_attribute_type INTEGER,
                health_attribute_divisor INTEGER,
                stamina_attribute_type INTEGER,
                stamina_attribute_divisor INTEGER,
                mana_attribute_type INTEGER,
                mana_attribute_divisor INTEGER
            )
        ";
        createTableCommand.ExecuteNonQuery();

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
}