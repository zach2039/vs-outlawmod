﻿using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;


namespace ExpandedAiTasks
{
    public class AiTaskGuard : AiTaskBaseExpandedTargetable
    {
        protected Entity guardedEntity;
        protected Entity attacker;
        protected float attackerStartTargetMs = 0;

        float detectionDistance = 20f;
        float maxDistance = 6f;
        float arriveDistance = 3f;
        float moveSpeed = 0.04f;
        float guardAgroDurationMs = 30000f;
        float guardAgroChaseDist = 40f;

        bool guardHerd = false;
        bool aggroOnProximity = false;
        float aggroProximity = 5f;

        EntityPartitioning partitionUtil;

        protected bool stuck = false;
        protected bool stopNow = false;
        protected bool allowTeleport;
        protected float teleportAfterRange;

        protected Vec3d targetOffset = new Vec3d();

        //Guarding is not an agressive action.
        public override bool AggressiveTargeting => false;

        public AiTaskGuard(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

            base.LoadConfig(taskConfig, aiConfig);

            detectionDistance = taskConfig["detectionDistance"].AsFloat(20f);
            maxDistance = taskConfig["maxDistance"].AsFloat(6f);
            arriveDistance = taskConfig["arriveDistance"].AsFloat(3f);
            moveSpeed = taskConfig["moveSpeed"].AsFloat(0.04f);
            guardAgroDurationMs = taskConfig["guardAgroDurationMs"].AsFloat(30000f);
            guardAgroChaseDist = taskConfig["guardAgroChaseDist"].AsFloat(40f);


            guardHerd = taskConfig["guardHerd"].AsBool(false);
            aggroOnProximity = taskConfig["aggroOnProximity"].AsBool(false);
            aggroProximity = taskConfig["aggroProximity"].AsFloat(5f);

            Debug.Assert(maxDistance >= arriveDistance, "maxDistance must be greater than or equal to arriveDistance for AiTaskGuard on entity " + entity.Code.Path);
        }

        public override bool ShouldExecute()
        {

            if (whenInEmotionState != null)
            {
                if (bhEmo?.IsInEmotionState(whenInEmotionState) == false)
                    return false;
            }

            if (whenNotInEmotionState != null)
            {
                if (bhEmo?.IsInEmotionState(whenNotInEmotionState) == true)
                    return false;
            }

            //If we have been directly attacked, skip guarding.
            if (entity.World.ElapsedMilliseconds - attackedByEntityMs > guardAgroDurationMs)
            {
                attackedByEntity = null;
            }

            if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && entity.World.Rand.NextDouble() < 0.5 && IsTargetableEntity(attackedByEntity, detectionDistance, true))
            {
                //Treat our attacker as if they attacked our guard target.
                attacker = attackedByEntity;
                attackerStartTargetMs = entity.World.ElapsedMilliseconds;
                TrySendGuardedEntityAttackedNotfications(attacker);
                return false;
            }

            //Get the individual we have to guard, if they exist.
            guardedEntity = GetGuardedEntity();

            if (guardHerd && ( guardedEntity == null || !guardedEntity.Alive) )
            {

                UpdateHerdCount();

                if ( guardedEntity == null )
                    guardedEntity = GetBestGuardTargetFromHerd();
            }

            if (guardedEntity == null || !guardedEntity.Alive)
                return false;

            //If someone has attacked our guard entity, tell our targeting behaviors to target the enemy and early out.
            if (attacker == null || !attacker.Alive)
            {
                attacker = partitionUtil.GetNearestEntity(guardedEntity.ServerPos.XYZ, detectionDistance, (e) => IsThreateningGuardedTarget(e, detectionDistance));
                attackerStartTargetMs = entity.World.ElapsedMilliseconds;
            }

            double distToGuardedEntSqr = entity.ServerPos.SquareDistanceTo(guardedEntity.ServerPos.XYZ);

            if ( attacker != null)
            {
                if (distToGuardedEntSqr <= guardAgroChaseDist * guardAgroChaseDist)
                {
                    if ( entity.World.ElapsedMilliseconds <= attackerStartTargetMs + guardAgroDurationMs)
                    {
                        TrySendGuardedEntityAttackedNotfications(attacker);
                        return false;
                    }                    
                }

                //Tell other tasks to clear guard target data.
                attacker = null;
                SendGuardChaseStopNotfications();
                     
            }
            else
            {
                //Tell other tasks to clear guard target data.
                attacker = null;
                SendGuardChaseStopNotfications();
            }

            return distToGuardedEntSqr > maxDistance * maxDistance;
        }

        public override void StartExecute()
        {
            base.StartExecute();

            float size = guardedEntity.SelectionBox.XSize;

            pathTraverser.NavigateTo(guardedEntity.ServerPos.XYZ, moveSpeed, size + 0.2f, OnGoalReached, OnStuck, false, 1000);

            targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);

            stuck = false;
            stopNow = false;
        }

        protected EntityAgent GetBestGuardTargetFromHerd()
        {
            EntityAgent bestGuardTarget = null;
            double bestGuardDistSqr = -1.0;

            foreach( EntityAgent herdMember in herdMembers)
            {
                //We can only guard herd members that are set to be guardable.
                if (!IsGuardableEntity(herdMember))
                    continue;

                if ( bestGuardTarget != null )
                {
                    double distSqr = entity.ServerPos.XYZ.SquareDistanceTo( herdMember.ServerPos.XYZ );
                    if ( distSqr < bestGuardDistSqr )
                    {
                        bestGuardTarget = herdMember;
                        bestGuardDistSqr = distSqr;
                    }
                }
                else
                {
                    bestGuardTarget = herdMember;
                    bestGuardDistSqr = entity.ServerPos.XYZ.SquareDistanceTo( herdMember.ServerPos.XYZ );
                }
            }

            return bestGuardTarget;
        }

        
        public bool IsThreateningGuardedTarget(Entity ent, float range)
        {
            if (!base.IsTargetableEntity(ent, range, true)) 
                return false;

            //Don't aggro if herd member.
            if (ent is EntityAgent)
            {
                EntityAgent agent = ent as EntityAgent;
                if (agent.HerdId == entity.HerdId)
                    return false;
            }

            //If we will aggro if anything gets too close, (even if it's not in a hositle beahvior).
            if (aggroOnProximity && IsProximityTarget(ent))
            {
                double distSqr = ent.ServerPos.XYZ.SquareDistanceTo( guardedEntity.ServerPos.XYZ );
                if (distSqr <= aggroProximity * aggroProximity)
                    return true;
            }

            //If this entity is the last entity to attack our guarded target and attacked in the our specified time window.
            double lastTimeAttackedMs = AiUtility.GetLastTimeAttackedMs(guardedEntity);
            if (AiUtility.GetLastAttacker(guardedEntity) == ent && entity.World.ElapsedMilliseconds <= lastTimeAttackedMs + guardAgroDurationMs)
                return true;

            //If entity is an Ai with hostile intentions.
            var tasks = ent.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.ActiveTasksBySlot;
            return tasks?.FirstOrDefault(task => {
                return task is AiTaskBaseTargetable at && at.TargetEntity == guardedEntity && at.AggressiveTargeting;
            }) != null;
        }

        protected bool IsProximityTarget( Entity ent)
        {
            if (ent is EntityPlayer)
                return true;

            return false;
        }

        float targetUpdateTime = 0f;
        float nextTargetCheckTime = 0f;
        const float GUARD_TARGET_CHECK_INTERVAL = 0.25f;

        public override bool ContinueExecute(float dt)
        {
            targetUpdateTime += dt;

            if (guardedEntity == null || !guardedEntity.Alive)
                return false;

            if (nextTargetCheckTime <= targetUpdateTime)
            {
                //If someone has attacked our guard entity, tell our targeting behaviors to target the enemy and early out.
                Entity attacker = partitionUtil.GetNearestEntity(guardedEntity.ServerPos.XYZ, maxDistance, (e) => IsThreateningGuardedTarget(e, detectionDistance));
                if (attacker != null)
                {
                    TrySendGuardedEntityAttackedNotfications(attacker);
                    return false;
                }
                else
                {
                    //Tell other tasks to clear guard target data.
                    SendGuardChaseStopNotfications();
                }

                nextTargetCheckTime = targetUpdateTime + GUARD_TARGET_CHECK_INTERVAL;
            }

                
            double x = guardedEntity.ServerPos.X + targetOffset.X;
            double y = guardedEntity.ServerPos.Y;
            double z = guardedEntity.ServerPos.Z + targetOffset.Z;

            pathTraverser.CurrentTarget.X = x;
            pathTraverser.CurrentTarget.Y = y;
            pathTraverser.CurrentTarget.Z = z;

            float dist = entity.ServerPos.SquareDistanceTo(x, y, z);

            if (dist < arriveDistance * arriveDistance)
            {
                pathTraverser.Stop();
                return false;
            }

            if (allowTeleport && dist > teleportAfterRange * teleportAfterRange && entity.World.Rand.NextDouble() < 0.05)
            {
                TryTeleport();
            }

            return !stuck && !stopNow && pathTraverser.Active;
        }

        protected bool IsGuardableEntity( Entity ent)
        {
            if (ent == null || !ent.Alive || ent.EntityId == entity.EntityId)
                return false;

            if (targetEntityCodesExact.Contains(ent.Code.Path))
            {
                return true;
            }

            for (int i = 0; i < targetEntityCodesBeginsWith.Length; i++)
            {
                if (ent.Code.Path.StartsWithFast(targetEntityCodesBeginsWith[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private Vec3d FindDecentTeleportPos()
        {
            var ba = entity.World.BlockAccessor;
            var rnd = entity.World.Rand;

            Vec3d pos = new Vec3d();
            BlockPos bpos = new BlockPos();
            for (int i = 0; i < 20; i++)
            {
                double rndx = rnd.NextDouble() * 10 - 5;
                double rndz = rnd.NextDouble() * 10 - 5;
                pos.Set(guardedEntity.ServerPos.X + rndx, guardedEntity.ServerPos.Y, guardedEntity.ServerPos.Z + rndz);

                for (int j = 0; j < 8; j++)
                {
                    // Produces: 0, -1, 1, -2, 2, -3, 3
                    int dy = (1 - (j % 2) * 2) * (int)Math.Ceiling(j / 2f);

                    bpos.Set((int)pos.X, (int)(pos.Y + dy + 0.5), (int)pos.Z);
                    Block aboveBlock = ba.GetBlock(bpos);
                    var boxes = aboveBlock.GetCollisionBoxes(ba, bpos);
                    if (boxes != null && boxes.Length > 0) continue;

                    bpos.Set((int)pos.X, (int)(pos.Y + dy - 0.1), (int)pos.Z);
                    Block belowBlock = ba.GetBlock(bpos);
                    boxes = belowBlock.GetCollisionBoxes(ba, bpos);
                    if (boxes == null || boxes.Length == 0) continue;

                    return pos;
                }
            }

            return null;
        }


        protected void TryTeleport()
        {
            if (!allowTeleport) return;
            Vec3d pos = FindDecentTeleportPos();
            if (pos != null) entity.TeleportTo(pos);
        }


        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
        }

        protected void OnStuck()
        {
            stuck = true;
            TryTeleport();
            pathTraverser.Stop();
        }

        public override void OnNoPath(Vec3d target)
        {
            TryTeleport();
            pathTraverser.Stop();
        }

        protected void OnGoalReached()
        {
            pathTraverser.Stop();
        }

        public bool TrySendGuardedEntityAttackedNotfications( Entity attacker)
        {
            //Don't aggro if our guard target was injured by friendly fire.
            if ( attacker is EntityAgent )
            {
                EntityAgent agentAttacker = attacker as EntityAgent;
                if (agentAttacker.HerdId == entity.HerdId)
                    return false;
            }

            entity.Notify("entityAttackedGuardedEntity", attacker);

            return true;
        }

        public bool SendGuardChaseStopNotfications()
        {
            entity.Notify("guardChaseStop", null);

            return true;
        }

        public override bool Notify(string key, object data)
        {

            if (key == "haltMovement")
            {
                //If another task has requested we halt, stop moving to guard target.
                if (entity == (Entity)data)
                {
                    stopNow = true;
                    return true;
                }
            }
            else if (key == "clearTargetHistory")
            {
                ClearTargetHistory();
                return false;
            }

            return false;
        }
    }
}

