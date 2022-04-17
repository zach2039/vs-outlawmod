
using System.Diagnostics;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using HarmonyLib;
using ExpandedAiTasks;

namespace ExpandedAiTasksLoader
{

    public class ExpandedAiTasksLoaderCore : ModSystem
    {

        //We need this mod to execute as early as possible so other mods can use it.
        public override double ExecuteOrder()
        {
            return 0.0;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            ExpandedAiTasksDeployment.Deploy(api);
        }
    }
}
