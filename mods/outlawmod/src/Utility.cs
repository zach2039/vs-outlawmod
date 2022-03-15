using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;


namespace OutlawMod
{
    static class Utility
    {
        //todo: these always return true if survival is loaded, even if we are not in survival mode. We need a better way to check for this.

        //I THINK WHAT WE WANT HERE IS TO LOOK FOR THE WORLD'S GAMEMODE, NOT THE MOD LOAD STATE. FIGURE OUT HOW TO DO THIS.

        public static bool IsSurvivalMode( ICoreAPI api )
        { 
            return api.ModLoader.IsModSystemEnabled("Vintagestory.GameContent.SurvivalCoreSystem");
        }

        public static bool IsSurvivalMode( ICoreServerAPI sapi )
        {
            return sapi.ModLoader.IsModSystemEnabled("Vintagestory.GameContent.SurvivalCoreSystem");
        }

        public static bool IsSurivivalMode( ICoreClientAPI capi )
        {
            return capi.ModLoader.IsModSystemEnabled("Vintagestory.GameContent.SurvivalCoreSystem");
        }
    }
}