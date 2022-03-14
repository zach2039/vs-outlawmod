
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

            RegisterEntities();
            RegisterEntityBehaviors();
            RegisterAiTasks();
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }

        private void RegisterEntities()
        {
            
        }

        private void RegisterEntityBehaviors()
        {
            
        }

        private void RegisterAiTasks()
        {
            AiTaskRegistry.Register("shootatentity", typeof(AiTaskShootProjectileAtEntity));
        }
    }
}
