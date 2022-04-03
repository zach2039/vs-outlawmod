using System;
using System.Diagnostics;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ExpandedAiTasks
{
    public class AiTaskPursueAndEngageEntity : AiTaskBaseTargetable
    {
        protected Vec3d targetPos;

        //Json Fields
        protected float pursueSpeed = 0.2f;
        protected float pursueRange = 25f;
        protected string pursueAnimation = "run";
        protected float engageSpeed = 0.1f;
        protected float engageRange = 5f;
        protected string engageAnimation = "walk";
        protected float maxFollowTime = 60;
        protected float maxTargetHealth = -1.0f;

        //State Vars
        protected bool stopNow = false;

        protected float currentFollowTime = 0;

        protected bool alarmHerd = false;
        protected bool packHunting = false; //Each individual herd member's maxTargetHealth value will equal maxTargetHealth * number of herd members.

        protected bool siegeMode;

        protected long finishedMs;

        protected long lastSearchTotalMs;

        protected EntityPartitioning partitionUtil;
        protected float extraTargetDistance = 0f;

        protected bool lowTempMode;

        protected int searchWaitMs = 4000;

        protected List<Entity> herdMembers = new List<Entity>();

        private eInternalMovementState internalMovementState = eInternalMovementState.Pursuing;

        private enum eInternalMovementState
        {
            Pursuing,
            Engaging
        }

        public AiTaskPursueAndEngageEntity(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

            base.LoadConfig(taskConfig, aiConfig);

            pursueSpeed = taskConfig["pursueSpeed"].AsFloat(0.02f);
            pursueRange = taskConfig["pursueRange"].AsFloat(25f);
            pursueAnimation = taskConfig["pursueAnimation"].AsString("run");
            engageSpeed = taskConfig["engageSpeed"].AsFloat(0.01f);
            engageRange = taskConfig["engageRange"].AsFloat(5f);
            engageAnimation = taskConfig["engageAnimation"].AsString("walk");
            maxTargetHealth = taskConfig["maxTargetHealth"].AsFloat(-1.0f);

            extraTargetDistance = taskConfig["extraTargetDistance"].AsFloat(0f);
            maxFollowTime = taskConfig["maxFollowTime"].AsFloat(60);
            
            alarmHerd = taskConfig["alarmHerd"].AsBool(false);
            packHunting = taskConfig["packHunting"].AsBool(false);

            retaliateAttacks = taskConfig["retaliateAttacks"].AsBool(true);

            Debug.Assert(pursueRange > engageRange, "pursueRange must be a greater value to engageRange.");
        }


        public override bool ShouldExecute()
        {

            if (whenInEmotionState != null)
            {
                if (bhEmo?.IsInEmotionState(whenInEmotionState) == false)
                    return false;
            }

            if ( whenNotInEmotionState != null )
            { 
                if (bhEmo?.IsInEmotionState(whenNotInEmotionState) == true)
                    return false;
            }
                            
            if (lastSearchTotalMs + searchWaitMs > entity.World.ElapsedMilliseconds) 
                return false;

            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) 
                return false;


            float range = pursueRange;


            lastSearchTotalMs = entity.World.ElapsedMilliseconds;


            if (entity.World.ElapsedMilliseconds - attackedByEntityMs > 30000)
            {
                attackedByEntity = null;
            }

            if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && entity.World.Rand.NextDouble() < 0.5 && IsTargetableEntity(attackedByEntity, range, true))
            {
                targetEntity = attackedByEntity;
            }

            if (packHunting)
            {
                if (herdMembers.Count == 0)
                {
                    herdMembers = new List<Entity>();
                    partitionUtil.GetNearestEntity(entity.ServerPos.XYZ, range, (e) => CountHerdMembers(e, range));
                }
                else
                {
                    UpdateHerdCount();
                }
            }

            //Aquire a target if we don't have one.
            if ( targetEntity == null || !targetEntity.Alive || ( targetEntity != attackedByEntity ) )
            targetEntity = partitionUtil.GetNearestEntity(entity.ServerPos.XYZ, range, (e) => IsEntityTargetableByPack(e, range));

            if (targetEntity != null)
            {
                if ((alarmHerd) && entity.HerdId > 0)
                {
                    entity.World.GetNearestEntity(entity.ServerPos.XYZ, range, range, (e) =>
                    {
                        EntityAgent agent = e as EntityAgent;
                        if (e.EntityId != entity.EntityId && agent != null && agent.Alive && agent.HerdId == entity.HerdId)
                        {
                            agent.Notify("pursueEntity", targetEntity);
                        }

                        return false;
                    });
                }

                targetPos = targetEntity.ServerPos.XYZ;

                if (entity.ServerPos.SquareDistanceTo(targetPos) <= MinDistanceToTarget())
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private bool CountHerdMembers(Entity e, float range, bool ignoreEntityCode = false)
        {
            EntityAgent agent = e as EntityAgent;
            if (agent != null && agent.Alive && agent.HerdId == entity.HerdId)
            {
                herdMembers.Add(agent);
            }

            return false;
        }

        private void UpdateHerdCount()
        {
            List<Entity> currentMembers = new List<Entity>();
            foreach( Entity agent in herdMembers)
            {
                if (agent == null)
                    continue;

                if ( !agent.Alive )
                    continue;

                currentMembers.Add(agent);
            }

            herdMembers = currentMembers;
        }

        private bool IsEntityTargetableByPack(Entity e, float range, bool ignoreEntityCode = false)
        {

            EntityAgent agent = e as EntityAgent;

            if (agent == null)
                return false;

            //If we are pack hunting.
            if (packHunting)
            {
                float packTargetMaxHealth = maxTargetHealth * herdMembers.Count;

                ITreeAttribute treeAttribute = agent.WatchedAttributes.GetTreeAttribute("health");

                if (treeAttribute == null)
                    return false;

                float targetHealth = treeAttribute.GetFloat("currenthealth");

                if ( packTargetMaxHealth < targetHealth )
                    return false;

            }
            else
            {
                if ( maxTargetHealth > 0 )
                {
                    ITreeAttribute treeAttribute = agent.WatchedAttributes.GetTreeAttribute("health");
                    
                    if (treeAttribute == null)
                        return false;
                    
                    float targetHealth = treeAttribute.GetFloat("currenthealth");                   

                    if (maxTargetHealth < targetHealth)
                        return false;
                }
            }

            return IsTargetableEntity(e, range, ignoreEntityCode);
        }

        public float MinDistanceToTarget()
        {
            return extraTargetDistance + Math.Max(0.1f, targetEntity.SelectionBox.XSize / 2 + entity.SelectionBox.XSize / 4);
        }

        public override void StartExecute()
        {
            base.StartExecute();

            stopNow = false;
            siegeMode = false;

            bool giveUpWhenNoPath = targetPos.SquareDistanceTo(entity.Pos.XYZ) < 12 * 12;
            int searchDepth = 3500;
            // 1 in 20 times we do an expensive search
            if (world.Rand.NextDouble() < 0.05)
            {
                searchDepth = 10000;
            }

            float moveSpeed = GetMovementSpeedForState(internalMovementState);

            if (!pathTraverser.NavigateTo(targetPos.Clone(), moveSpeed, MinDistanceToTarget(), OnGoalReached, OnStuck, giveUpWhenNoPath, searchDepth, true))
            {
                // If we cannot find a path to the target, let's circle it!
                float angle = (float)Math.Atan2(entity.ServerPos.X - targetPos.X, entity.ServerPos.Z - targetPos.Z);

                double randAngle = angle + 0.5 + world.Rand.NextDouble() / 2;

                double distance = 4 + world.Rand.NextDouble() * 6;

                double dx = GameMath.Sin(randAngle) * distance;
                double dz = GameMath.Cos(randAngle) * distance;
                targetPos = targetPos.AddCopy(dx, 0, dz);

                int tries = 0;
                bool ok = false;
                BlockPos tmp = new BlockPos((int)targetPos.X, (int)targetPos.Y, (int)targetPos.Z);

                int dy = 0;
                while (tries < 5)
                {
                    // Down ok?
                    if (world.BlockAccessor.GetBlock(tmp.X, tmp.Y - dy, tmp.Z).SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, new Vec3d(tmp.X + 0.5, tmp.Y - dy + 1, tmp.Z + 0.5), false))
                    {
                        ok = true;
                        targetPos.Y -= dy;
                        targetPos.Y++;
                        siegeMode = true;
                        break;
                    }

                    // Down ok?
                    if (world.BlockAccessor.GetBlock(tmp.X, tmp.Y + dy, tmp.Z).SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, new Vec3d(tmp.X + 0.5, tmp.Y + dy + 1, tmp.Z + 0.5), false))
                    {
                        ok = true;
                        targetPos.Y += dy;
                        targetPos.Y++;
                        siegeMode = true;
                        break;

                    }

                    tries++;
                    dy++;
                }

                ok = ok && pathTraverser.NavigateTo(targetPos.Clone(), moveSpeed, MinDistanceToTarget(), OnGoalReached, OnStuck, giveUpWhenNoPath, searchDepth, true);

                stopNow = !ok;
            }

            currentFollowTime = 0;

            //play a sound associated with this action.
            entity.PlayEntitySound("engageentity", null, true);
        }


        float lastPathUpdateSeconds;
        public override bool ContinueExecute(float dt)
        {
            currentFollowTime += dt;
            lastPathUpdateSeconds += dt;

            eInternalMovementState lastMovementState = internalMovementState;
            UpdateMovementState();

            //Depending on whether we are pursuing or engaging, determine the distance our target has to move for us to recompute our path.
            //When we are engaging (close range follow) we need to recompute more often so we can say on our target.
            float minRecomputeNavDistance = internalMovementState == eInternalMovementState.Engaging ? 1 * 1 : 3 * 3;

            if (!siegeMode && ( lastPathUpdateSeconds >= 0.75f || 
                targetPos.SquareDistanceTo(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z) >= minRecomputeNavDistance ||
                internalMovementState != lastMovementState) )
            {
                targetPos.Set(targetEntity.ServerPos.X + targetEntity.ServerPos.Motion.X * 10, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetEntity.ServerPos.Motion.Z * 10);

                pathTraverser.NavigateTo(targetPos, GetMovementSpeedForState(internalMovementState), MinDistanceToTarget(), OnGoalReached, OnStuck, false, 2000, true);
                lastPathUpdateSeconds = 0;
            }

            if (!siegeMode)
            {
                pathTraverser.CurrentTarget.X = targetEntity.ServerPos.X;
                pathTraverser.CurrentTarget.Y = targetEntity.ServerPos.Y;
                pathTraverser.CurrentTarget.Z = targetEntity.ServerPos.Z;
            }

            Cuboidd targetBox = targetEntity.SelectionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.SelectionBox.Y2 / 2, 0).Ahead(entity.SelectionBox.XSize / 2, 0, entity.ServerPos.Yaw);
            double distance = targetBox.ShortestDistanceFrom(pos);

            bool inCreativeMode = (targetEntity as EntityPlayer)?.Player?.WorldData.CurrentGameMode == EnumGameMode.Creative;

            float minDist = MinDistanceToTarget();
            float range = pursueRange;

            return
                currentFollowTime < maxFollowTime &&
                distance < range * range &&
                (distance > minDist || (targetEntity is EntityAgent ea && ea.ServerControls.TriesToMove)) &&
                targetEntity.Alive &&
                !inCreativeMode &&
                !stopNow
            ;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            finishedMs = entity.World.ElapsedMilliseconds;

            if ( engageAnimation != null )
                entity.AnimManager.StopAnimation(engageAnimation);
            
            if ( pursueAnimation != null)
                entity.AnimManager.StopAnimation(pursueAnimation);

            pathTraverser.Stop();
        }


        public override bool Notify(string key, object data)
        {
            if (key == "pursueEntity")
            {
                targetEntity = (Entity)data;
                targetPos = targetEntity.ServerPos.XYZ;
                return true;
            }

            return false;
        }


        private void OnStuck()
        {
            stopNow = true;
        }

        private void OnGoalReached()
        {
            if (!siegeMode)
            {
                pathTraverser.Retarget();
            }
            else
            {
                stopNow = true;
            }

        }

        private void UpdateMovementState()
        {
            if (this.entity.Pos.SquareDistanceTo(targetPos) <= engageRange * engageRange)
            {
                //Engage State
                internalMovementState = eInternalMovementState.Engaging;

                if (pursueAnimation != null)
                    entity.AnimManager.StopAnimation(pursueAnimation);

                if (engageAnimation != null)
                    entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = engageAnimation, Code = engageAnimation }.Init());
            }
            else
            {
                //Pursue State
                internalMovementState = eInternalMovementState.Pursuing;

                if (engageAnimation != null)
                    entity.AnimManager.StopAnimation(engageAnimation);

                if (pursueAnimation != null)
                    entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = pursueAnimation, Code = pursueAnimation }.Init());
            }
        }

        private float GetMovementSpeedForState( eInternalMovementState movementState )
        {
            switch (movementState)
            {
                case eInternalMovementState.Engaging:
                    return engageSpeed;
                case eInternalMovementState.Pursuing:
                    return pursueSpeed;
            }

            Debug.Assert(false, "Invalid intermal move state.");
            return 0.0f;
        }
    }
}
