
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

        private Harmony harmony;

        //We need this mod to execute as early as possible so other mods can use it.
        public override double ExecuteOrder()
        {
            return 0.0;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            //Apply AiExpandedTask Patches if they haven't already been applied.
            if (ExpandedAiTasksHarmonyPatcher.ShouldPatch())
                ExpandedAiTasksHarmonyPatcher.ApplyPatches();

            RegisterAiTasksShared();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            //We need to make sure we don't double register with outlaw mod, if that mod loaded first.
            if (!AiTaskRegistry.TaskTypes.ContainsKey("shootatentity"))
                AiTaskRegistry.Register<AiTaskShootProjectileAtEntity>("shootatentity");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("engageentity"))
                AiTaskRegistry.Register<AiTaskPursueAndEngageEntity>("engageentity");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("stayclosetoherd"))
                AiTaskRegistry.Register<AiTaskStayCloseToHerd>("stayclosetoherd");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("eatdead"))
                AiTaskRegistry.Register<AiTaskEatDeadEntities>("eatdead");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("morale"))
                AiTaskRegistry.Register<AiTaskMorale>("morale");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("melee"))
                AiTaskRegistry.Register<AiTaskExpandedMeleeAttack>("melee");

            if (!AiTaskRegistry.TaskTypes.ContainsKey("guard"))
                AiTaskRegistry.Register<AiTaskGuard>("guard");
        }

        private void RegisterAiTasksShared()
        {
            //We need to make sure we don't double register with outlaw mod, if that mod loaded first.
            if (!AiTaskRegistry.TaskTypes.ContainsKey("shootatentity"))
                AiTaskRegistry.Register("shootatentity", typeof(AiTaskShootProjectileAtEntity));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("engageentity"))
                AiTaskRegistry.Register("engageentity", typeof(AiTaskPursueAndEngageEntity));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("stayclosetoherd"))
                AiTaskRegistry.Register("stayclosetoherd", typeof(AiTaskStayCloseToHerd));

            if(!AiTaskRegistry.TaskTypes.ContainsKey("eatdead"))
                AiTaskRegistry.Register("eatdead", typeof(AiTaskEatDeadEntities));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("morale"))
                AiTaskRegistry.Register("morale", typeof(AiTaskMorale));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("melee"))
                AiTaskRegistry.Register("melee", typeof(AiTaskExpandedMeleeAttack));

            if (!AiTaskRegistry.TaskTypes.ContainsKey("guard"))
                AiTaskRegistry.Register("guard", typeof(AiTaskGuard));
        }
    }
}
