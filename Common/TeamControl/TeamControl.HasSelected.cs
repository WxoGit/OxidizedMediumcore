using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace OxidizedMediumcore.Common.TeamControl;

public sealed class HasSelectedTeam : ModPlayer
{
    public bool HasSelected { get; set; }

    public void ConfirmTeam()
    {
        if (HasSelected)
            return;

        HasSelected = true;
        TeamSelectionPersistence.RegisterTeam(Player.name, Player.team);

        if (Main.netMode != NetmodeID.SinglePlayer)
            SendSync();
    }

    private void SendSync(int toClient = -1, int ignoreClient = -1)
    {
        var packet = Mod.GetPacket();
        packet.Write((byte)OxidizedMediumcore.PacketID.HasSelectedTeamSync);
        packet.Write((byte)Player.whoAmI);
        packet.Write(HasSelected);
        packet.Write((byte)Player.team);
        packet.Send(toClient, ignoreClient);
    }

    public static void HandleSync(BinaryReader reader, int whoAmI)
    {
        byte playerIndex = reader.ReadByte();
        bool value = reader.ReadBoolean();
        byte team = reader.ReadByte();

        var mp = Main.player[playerIndex].GetModPlayer<HasSelectedTeam>();
        mp.HasSelected = value;
        
        if (value)
            Main.player[playerIndex].team = team;

        if (Main.netMode == NetmodeID.Server)
        {
            if (value)
                TeamSelectionPersistence.RegisterTeam(Main.player[playerIndex].name, team);
            mp.SendSync(ignoreClient: whoAmI);
        }
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        if (HasSelected)
            SendSync(toClient: toWho, ignoreClient: fromWho);
    }

    public override void OnEnterWorld()
    {
        if (TeamSelectionPersistence.TryGetTeam(Player.name, out int team))
        {
            Player.team = team;
            HasSelected = true;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // sync team to everyone
                NetMessage.SendData(MessageID.PlayerTeam, -1, -1, null, Player.whoAmI);
                
                // specifically tell the server and other clients that we have selected
                SendSync();
            }
        }
        else
        {
            HasSelected = false;
        }
    }
}

//public sealed class ListTeams : ModCommand
//{
//    public override string Command => "omteamlist";
//    public override CommandType Type => CommandType.Console;
//    public override string Description => "Lists all team selections in this world.";
//    public override void Action(CommandCaller caller, string input, string[] args)
//    {
//        var selections = TeamSelectionPersistence.GetAllSelections();
//        if (selections.Count == 0)
//        {
//            caller.Reply("No team selections registered in this world.");
//            return;
//        }

//        caller.Reply("World Team Selections:");
//        foreach (var (name, team) in selections)
//            caller.Reply($"- {name}: Team {team}");
//    }
//}

public sealed class TeamSelectionPersistence : ModSystem
{
    private static readonly Dictionary<string, int> _selectedTeams = new(System.StringComparer.OrdinalIgnoreCase);

    public static void RegisterTeam(string name, int team)
    {
        if (string.IsNullOrEmpty(name))
            return;

        _selectedTeams[name] = team;
    }

    public static bool TryGetTeam(string name, out int team)
    {
        team = 0;
        return !string.IsNullOrEmpty(name) && _selectedTeams.TryGetValue(name, out team);
    }

    public static IReadOnlyDictionary<string, int> GetAllSelections() => _selectedTeams;

    public override void SaveWorldData(TagCompound tag)
    {
        List<TagCompound> list = [];
        foreach (var kvp in _selectedTeams)
        {
            list.Add(new TagCompound {
                ["n"] = kvp.Key,
                ["t"] = kvp.Value
            });
        }
        tag["selections"] = list;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        _selectedTeams.Clear();
        if (tag.ContainsKey("selections"))
        {
            foreach (var entry in tag.GetList<TagCompound>("selections"))
            {
                string name = entry.GetString("n");
                int team = entry.GetInt("t");
                if (!string.IsNullOrEmpty(name))
                    _selectedTeams[name] = team;
            }
        }
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(_selectedTeams.Count);
        foreach (var kvp in _selectedTeams)
        {
            writer.Write(kvp.Key);
            writer.Write((byte)kvp.Value);
        }
    }

    public override void NetReceive(BinaryReader reader)
    {
        _selectedTeams.Clear();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            string name = reader.ReadString();
            byte team = reader.ReadByte();
            _selectedTeams[name] = team;
        }
    }

    public override void OnWorldUnload()
    {
        _selectedTeams.Clear();
    }
}