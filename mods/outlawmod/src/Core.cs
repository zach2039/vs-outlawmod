
    using System.Diagnostics;
    using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    using Vintagestory.API.Server;
    using Vintagestory.GameContent;

namespace OutlawMod
{

    public class Core : ModSystem
    {
        ICoreAPI api;

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            base.Start(api);

            //Debug.WriteLine("Outlaw Mod Started Sucessfully.");

            //This is where we register our shoot arrow ai behavor once we add it.
            //Example can be found in the VSSurvival Code in AiTaskThrowAtEntity.cs.

            RegisterEntities();
            RegisterEntityBehaviors();
            RegisterAiTasks();

            //api.ModLoader.GetModSystem(  )
            //api.ObjectCache.



        }
    
        private void RegisterEntities()
        {
            
        }

        private void RegisterEntityBehaviors()
        {
            //This is where we register our harvestable behavior.
            //api.RegisterEntityBehaviorClass("harvestable", typeof(OutlawEntityBehaviorHarvestable));
        }

        private void RegisterAiTasks()
        {
            AiTaskRegistry.Register("shootatentity", typeof(AiTaskShootProjectileAtEntity));
        }
    }
}
