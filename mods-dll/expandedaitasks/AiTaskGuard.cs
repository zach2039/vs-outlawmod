using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace ExpandedAiTasks
{
    public class AiTaskGuard : AiTaskBaseExpandedTargetable
    {
        Entity guardedEntity;

        public AiTaskGuard(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            //To Do: Configure Guard Behavior.

            //We want to be able to guard both individuals and groups.
        }

        public override bool ShouldExecute()
        {

            guardedEntity = GetGuardedEntity();

            if (guardedEntity == null) 
                return false;

            //If our guarded entity/entities were attacked, notify pursue and engage and melee task behaviors.

            //Otherwise, follow our guard target.

            return false;
        }

        /*
        public override bool IsTargetableEntity(Entity e, float range, bool ignoreEntityCode = false)
        {
            if (!base.IsTargetableEntity(e, range, ignoreEntityCode)) return false;

            return e.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.AllTasks?.FirstOrDefault(task => {
                return task is AiTaskStayCloseToGuardedEntity at && at.guardedEntity == guardedEntity;
            }) != null;
        }
        */
    }
}
