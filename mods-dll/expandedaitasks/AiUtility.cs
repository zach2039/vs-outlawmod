using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
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

        public static void SetMasterHerdList( Entity ent, List<Entity> herdList )
        {
            List<long> herdListEntIds = new List<long>();
            foreach( Entity agent in herdList )
            {
                if (agent != null)
                    herdListEntIds.Add(agent.EntityId);
            }

            long[] herdEntIdArray = herdListEntIds.ToArray();
            ent.Attributes.SetBytes("herdMembers", SerializerUtil.Serialize(herdEntIdArray));
        }

        public static List<Entity> GetMasterHerdList( Entity ent, bool includeDead )
        {
            List<Entity> herdMembers = new List<Entity>();
            if ( ent.Attributes.HasAttribute("herdMembers") )
            {
                long[] herdEntIdArray = SerializerUtil.Deserialize<long[]>(ent.Attributes.GetBytes("herdMembers"));

                foreach( long id in herdEntIdArray)
                {
                    Entity herdMember = ent.World.GetEntityById(id);

                    if ( herdMember != null && ( herdMember.Alive || includeDead ) )
                        herdMembers.Add( herdMember );
                }
            }

            return herdMembers;
        }

        public static void JoinSameHerdAsEntity( Entity newMember, Entity currentMember )
        {
            Debug.Assert(newMember is EntityAgent, "Only entity agents can join a herd.");
            Debug.Assert(currentMember is EntityAgent, "Entity " + currentMember + " is not a member of a herd");

            EntityAgent newMemberAgent = newMember as EntityAgent;
            EntityAgent currentMemberAgent = currentMember as EntityAgent;

            newMemberAgent.HerdId = currentMemberAgent.HerdId;

            //Remove me from my old herd.
            List<Entity> oldHerdMembers = GetMasterHerdList(newMember, true);
            oldHerdMembers.Remove(newMember);

            //Inform members of my old herd.
            foreach (Entity herdMember in oldHerdMembers)
                SetMasterHerdList(herdMember, oldHerdMembers);

            //Add me to the new herd.
            List<Entity> newHerdMembers = GetMasterHerdList(currentMember, true);
            newHerdMembers.Add(newMember);

            //Inform members of my new herd.
            foreach (Entity herdMember in newHerdMembers)
                SetMasterHerdList(herdMember, newHerdMembers);
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
                            if (baseTargetable.TargetEntity != null && baseTargetable.TargetEntity.Alive)
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        public static double CalculateInjuryRatio( Entity ent )
        {
            ITreeAttribute treeAttribute = ent.WatchedAttributes.GetTreeAttribute("health");

            if (treeAttribute != null)
            {
                double currentHealth = treeAttribute.GetFloat("currenthealth"); ;
                double maxHealth = treeAttribute.GetFloat("maxhealth"); ;

                return (maxHealth - currentHealth) / maxHealth;
            }

            return 0.0;
        }

        public static double CalculateHerdInjuryRatio(List<Entity> herdMembers)
        {
            if (herdMembers.Count == 0)
                return 0;

            double totalCurrentHealth = 0f;
            double totalMaxHealth = 0f;
            foreach (Entity herdMember in herdMembers)
            {
                ITreeAttribute treeAttribute = herdMember.WatchedAttributes.GetTreeAttribute("health");

                if (treeAttribute != null)
                {
                    totalCurrentHealth += treeAttribute.GetFloat("currenthealth"); ;
                    totalMaxHealth += treeAttribute.GetFloat("maxhealth"); ;
                }
            }

            return (totalMaxHealth - totalCurrentHealth) / totalMaxHealth;
        }

        public static double PercentOfHerdAlive(List<Entity> herdMembers)
        {
            if (herdMembers.Count == 0)
                return 0;

            int aliveCount = 0;
            foreach (Entity herdMember in herdMembers)
            {
                if (herdMember.Alive)
                    aliveCount++;
            }

            return aliveCount / herdMembers.Count;
        }

        public static List<Entity> GetHerdMembersInRangeOfPos( List<Entity> herdMembers, Vec3d pos, float range )
        {
            List<Entity> herdMembersInRange = new List<Entity>();
            foreach( Entity herdMember in herdMembers)
            {
                double distSqr = herdMember.ServerPos.XYZ.SquareDistanceTo(pos);
                if (distSqr <= range * range) ;
                herdMembersInRange.Add(herdMember);
            }
            return herdMembersInRange;
        }
    }
}