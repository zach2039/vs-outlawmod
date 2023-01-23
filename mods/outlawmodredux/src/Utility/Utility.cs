using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace OutlawModRedux
{
    static class Utility
    {
        public static bool AnyPlayersOnlineInSurvivalMode( ICoreAPI api )
        {
            IPlayer[] playersOnline = api.World.AllOnlinePlayers;
            foreach ( IPlayer player in playersOnline )
            {
                if (player.WorldData.CurrentGameMode == EnumGameMode.Survival)
                    return true;
            }

            return false;
        }

        public static bool AnyPlayersOnlineInSurvivalMode( ICoreServerAPI sapi )
        {
            IPlayer[] playersOnline = sapi.World.AllOnlinePlayers;
            foreach (IPlayer player in playersOnline)
            {
                if (player.WorldData.CurrentGameMode == EnumGameMode.Survival)
                    return true;
            }

            return false;
        }

        public static bool AnyPlayersOnlineInSurvivalMode( ICoreClientAPI capi )
        {
            IPlayer[] playersOnline = capi.World.AllOnlinePlayers;
            foreach (IPlayer player in playersOnline)
            {
                if (player.WorldData.CurrentGameMode == EnumGameMode.Survival)
                    return true;
            }

            return false;
        }

        //Note: This function networks a debug message, use this sparingly because it can cause massive hitches.
        public static void DebugLogToPlayerChat(ICoreServerAPI sapi, string text )
        {
            if (!OMGlobalConstants.devMode)
                return;

            string message = "[Outlaw Mod Debug] " + text;

            IPlayer[] playersOnline = sapi.World.AllOnlinePlayers;
            foreach (IPlayer player in playersOnline)
            {
                IServerPlayer serverPlayer = player as IServerPlayer;
                serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
            }            
        }

        public static void DebugLogMessage(ICoreAPI api, string text )
        {
            if (!OMGlobalConstants.devMode)
                return;

            string message = "[Outlaw Mod Debug] " + text;
            api.Logger.Debug(message);
        }

        //We want a function that looks at the average levels of all the tools on the player and decided which "era" they are currently in.
        //We could then use this to decide which Outlaws to spawn based on player tech level.
        /*
        public static int GetAverageTechLevelOfOnlinePlayers( ICoreAPI api )
        {
            IPlayer[] playersOnline = api.World.AllOnlinePlayers;
            foreach (IPlayer player in playersOnline)
            {
                player.InventoryManager.Inventories
            }

            return 0;
        }
        */
    }
}