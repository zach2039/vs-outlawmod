using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;


namespace OutlawMod
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