using System.Linq;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace ExpandedAiTasks
{
    public static class AiUtility
    {
        public static void SetGuardedEntity(Entity ent, Entity entToGuarded)
        {
            if (entToGuarded is EntityPlayer)
            {
                EntityPlayer guardedPlayer = entToGuarded as EntityPlayer;
                ent.WatchedAttributes.SetString("guardedPlayerUid", guardedPlayer.PlayerUID);
                ent.WatchedAttributes.MarkPathDirty("guardedPlayerUid");
            }

            ent.WatchedAttributes.SetLong("guardedEntityId", entToGuarded.EntityId);
            ent.WatchedAttributes.MarkPathDirty("guardedEntityId");
        }

        public static void SetLastAttacker( Entity ent, DamageSource damageSource)
        {
            if (damageSource.SourceEntity is EntityPlayer)
            {
                ent.Attributes.SetString("lastPlayerAttackerUid", (damageSource.SourceEntity as EntityPlayer).PlayerUID);
                ent.Attributes.SetDouble("lastTimeAttackedMs", ent.World.ElapsedMilliseconds);

                if (ent.Attributes.HasAttribute("lastEntAttackerEntityId"))
                    ent.Attributes.RemoveAttribute("lastEntAttackerEntityId");
            }
            else if (damageSource.SourceEntity != null)
            {
                ent.Attributes.SetLong("lastEntAttackerEntityId", damageSource.SourceEntity.EntityId);
                ent.Attributes.SetDouble("lastTimeAttackedMs", ent.World.ElapsedMilliseconds);

                if (ent.Attributes.HasAttribute("lastPlayerAttackerUid"))
                    ent.Attributes.RemoveAttribute("lastPlayerAttackerUid");
            }

            ent.Attributes.MarkAllDirty();
        }

        public static Entity GetLastAttacker( Entity ent )
        {
            string Uid = ent.Attributes.GetString("lastPlayerAttackerUid");
            if (Uid != null)
            {
                return ent.World.PlayerByUid(Uid)?.Entity;
            }

            long entId = ent.Attributes.GetLong("lastEntAttackerEntityId", 0L);
            return ent.World.GetEntityById(entId);
        }

        public static double GetLastTimeAttackedMs( Entity ent)
        {
            //There's an issue where where lastTimeAttackedMs this is saved on the ent, which we don't want.
            //Until we can find a way to store and update this at runtime without native behavior saving it,
            //we have to manually zero out the loaded bad value. 
            double lastAttackedMs = ent.Attributes.GetDouble("lastTimeAttackedMs");
            if ( lastAttackedMs > ent.World.ElapsedMilliseconds)
            {
                ent.Attributes.SetDouble("lastTimeAttackedMs", 0);
                lastAttackedMs = 0;
            }

            return lastAttackedMs;
        }

        public static bool IsInCombat( Entity ent )
        {
            if ( ent is EntityAgent)
            {
                AiTaskManager taskManager = ent.GetBehavior<EntityBehaviorTaskAI>().TaskManager;

                if (taskManager != null)
                {
                    List<IAiTask> tasks = taskManager.AllTasks;
                    foreach (IAiTask task in tasks)
                    {
                        if (task is AiTaskBaseTargetable)
                        {
                            AiTaskBaseTargetable baseTargetable = (AiTaskBaseTargetable)task;
                            
                            //If not an agressive action.
                            if (!baseTargetable.AggressiveTargeting)
                                continue;

                            //If we have a target entity and hostile intent, then we are in combat.
                            if (baseTargetable.TargetEntity != null)
                                return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}