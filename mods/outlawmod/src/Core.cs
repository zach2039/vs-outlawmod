
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

        public override void Start(ICoreAPI api)
        {
            this.api = api;

            base.Start(api);

            //Debug.WriteLine("Outlaw Mod Started Sucessfully.");

            harmony = new Harmony("com.grifthegnome.outlawmod.causeofdeath");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            RegisterEntitiesShared();
            RegisterEntityBehaviorsShared();
            RegisterAiTasksShared();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            AiTaskRegistry.Register<AiTaskShootProjectileAtEntity>("shootatentity");
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }

        private void RegisterEntitiesShared()
        {
            
        }

        private void RegisterEntityBehaviorsShared()
        {
            
        }

        private void RegisterAiTasksShared()
        {
            AiTaskRegistry.Register("shootatentity", typeof(AiTaskShootProjectileAtEntity));
        }
    }
}
