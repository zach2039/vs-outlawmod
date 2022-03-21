
using HarmonyLib;
using System.Diagnostics;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace OutlawMod
{

    public class Core : ModSystem
    {
        ICoreAPI api;

        private Harmony harmony;

        private bool usingExpandedAiTasksMod = false;

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            base.Start(api);              

            harmony = new Harmony("com.grifthegnome.outlawmod.causeofdeath");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            ModSystem expandedAiTasksMod = api.ModLoader.GetModSystem("ExpandedAiTasks.ExpandedAiTasksCore");

            if (expandedAiTasksMod != null)
            {
                usingExpandedAiTasksMod = true;
                api.World.Logger.Warning("Outlaw Mod: ExpandedAiTasks.dll Found, we will skip our internal ai task registration so that ExpandedAiTasks mod can handle-intermod dependencies.");
            }
            else
            {
                api.World.Logger.Warning("Outlaw Mod: ExpandedAiTasks.dll Not Found, we will register our internal ai tasks instead. Nothing to worry about here.");
            }

            RegisterEntitiesShared();
            RegisterBlocksShared();
            RegisterBlockEntitiesShared();
            RegisterAiTasksShared();
            RegisterItemsShared();

        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            //We need to make sure we don't double register with Expanded Ai Tasks, if that mod loaded first.
            if (!AiTaskRegistry.TaskTypes.ContainsKey("shootatentity") && !usingExpandedAiTasksMod )
                AiTaskRegistry.Register<AiTaskShootProjectileAtEntity>("shootatentity");
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }

        private void RegisterEntitiesShared()
        {
            api.RegisterEntity("EntityOutlaw", typeof(EntityOutlaw));
        }

        private void RegisterBlocksShared()
        {
            api.RegisterBlockClass("BlockStocks", typeof(BlockStocks));
            api.RegisterBlockClass("BlockHeadOnSpear", typeof(BlockHeadOnSpear));
        }

        private void RegisterBlockEntitiesShared()
        {
            api.RegisterBlockEntityClass("BlockEntityOutlawSpawnBlocker", typeof(BlockEntityOutlawSpawnBlocker));
        }

        private void RegisterAiTasksShared()
        {
            //We need to make sure we don't double register with Expanded Ai Tasks, if that mod loaded first.
            if (!AiTaskRegistry.TaskTypes.ContainsKey("shootatentity") && !usingExpandedAiTasksMod )
                AiTaskRegistry.Register("shootatentity", typeof(AiTaskShootProjectileAtEntity));
        }

        private void RegisterItemsShared()
        {
            api.RegisterItemClass("ItemOutlawHead", typeof(ItemOutlawHead));
        }
    }
}
