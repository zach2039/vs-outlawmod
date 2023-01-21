﻿using System;
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
    public class AiTaskPursueAndEngageEntity : AiTaskBaseExpandedTargetable
    {
        protected Vec3d targetPos;
        protected Vec3d withdrawPos = new Vec3d();

        Entity guardTargetAttackedByEntity = null;

        //Json Fields
        protected float pursueSpeed = 0.2f;
        protected float pursueRange = 25f;
        protected string pursueAnimation = "run";
        protected float engageSpeed = 0.1f;
        protected float engageRange = 5f;
        protected string engageAnimation = "walk";

        protected float arriveRange = 1.0f;
        protected float arriveVerticalRange = 1.0f;

        protected float maxFollowTime = 60;
        protected float maxTargetHealth = -1.0f;

        protected bool withdrawIfNoPath = false;
        protected float withdrawDist = 20.0f;
        protected float withdrawDistDamaged = 30.0f;
        protected float withdrawEndTime = 30.0f;
        protected string withdrawAnimation = "idle";

        protected bool alarmHerd = false;
        protected bool packHunting = false; //Each individual herd member's maxTargetHealth value will equal maxTargetHealth * number of herd members.

        //State Vars
        protected bool stopNow = false;

        protected float currentFollowTime = 0;
        protected float currentWithdrawTime = 0;
        protected float withdrawTargetMoveDistBeforeEncroaching = 0.0f;

        protected long finishedMs;

        protected long lastSearchTotalMs;

        protected EntityPartitioning partitionUtil;
        protected float extraTargetDistance = 0f;

        protected bool lowTempMode;

        protected int searchWaitMs = 250;

        private eInternalMovementState internalMovementState = eInternalMovementState.Pursuing;

        private enum eInternalMovementState
        {
            Pursuing,
            Engaging,
            Arrived
        }

        bool hasPath = false;
        private int consecutivePathFailCount = 0;

        float stepHeight;
        Vec3d tmpVec = new Vec3d();
        Vec3d collTmpVec = new Vec3d();

        protected float minTurnAnglePerSec;
        protected float maxTurnAnglePerSec;
        protected float curTurnRadPerSec;

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

            arriveRange = taskConfig["arriveRange"].AsFloat(1.0f);
            arriveVerticalRange = taskConfig["arriveVerticalRange"].AsFloat(1.0f);

            maxTargetHealth = taskConfig["maxTargetHealth"].AsFloat(-1.0f);

            withdrawIfNoPath = taskConfig["withdrawIfNoPath"].AsBool(false);
            withdrawDist = taskConfig["withdrawDist"].AsFloat(20.0f);
            withdrawDistDamaged = taskConfig["withdrawDistDamaged"].AsFloat(30.0f);
            withdrawEndTime = taskConfig["withdrawEndTime"].AsFloat(30.0f);
            withdrawAnimation = taskConfig["withdrawAnimation"].AsString("idle");

            extraTargetDistance = taskConfig["extraTargetDistance"].AsFloat(0f);
            maxFollowTime = taskConfig["maxFollowTime"].AsFloat(60);
            
            alarmHerd = taskConfig["alarmHerd"].AsBool(false);
            packHunting = taskConfig["packHunting"].AsBool(false);

            retaliateAttacks = taskConfig["retaliateAttacks"].AsBool(true);

            Debug.Assert(pursueRange > engageRange, "pursueRange must be a greater value to engageRange.");

            //Get Turning Speed Values
            if (entity?.Properties.Server?.Attributes != null)
            {
                minTurnAnglePerSec = entity.Properties.Server.Attributes.GetTreeAttribute("pathfinder").GetFloat("minTurnAnglePerSec", 250);
                maxTurnAnglePerSec = entity.Properties.Server.Attributes.GetTreeAttribute("pathfinder").GetFloat("maxTurnAnglePerSec", 450);
            }
            else
            {
                minTurnAnglePerSec = 250;
                maxTurnAnglePerSec = 450;
            }

            curTurnRadPerSec = minTurnAnglePerSec + (float)entity.World.Rand.NextDouble() * (maxTurnAnglePerSec - minTurnAnglePerSec);
            curTurnRadPerSec *= GameMath.DEG2RAD * 50 * 0.02f;
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
            targetEntity = null;

            lastSearchTotalMs = entity.World.ElapsedMilliseconds;

            if (entity.World.ElapsedMilliseconds - attackedByEntityMs > 30000 )
            {
                attackedByEntity = null;
            }

            if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && entity.World.Rand.NextDouble() < 0.5 && IsTargetableEntity(attackedByEntity, range, true))
            {
                targetEntity = attackedByEntity;
            }
            else if (guardTargetAttackedByEntity != null && guardTargetAttackedByEntity.Alive)
            {
                targetEntity = guardTargetAttackedByEntity;
            }
            else
            {
                guardTargetAttackedByEntity = null;
            }

            if (packHunting || alarmHerd)
            {
                UpdateHerdCount();
            }

            //Aquire a target if we don't have one.
            if ( targetEntity == null || !targetEntity.Alive )
            {
                targetEntity = AquireNewTarget();
            }
                

            if (targetEntity != null)
            {
                TryAlarmHerd();
                
                targetPos = targetEntity.ServerPos.XYZ;
                withdrawPos = targetPos.Clone();
                withdrawTargetMoveDistBeforeEncroaching = Math.Max(1.0f, withdrawDist / 4);

                return true;
            }

            return false;
        }

        private bool IsEntityTargetableByPack(Entity ent, float range, bool ignoreEntityCode = false)
        {
            if (!(ent is EntityAgent))
                return false;

            //If we are pack hunting.
            if (packHunting)
            {
                float packTargetMaxHealth = maxTargetHealth * herdMembers.Count;

                ITreeAttribute treeAttribute = ent.WatchedAttributes.GetTreeAttribute("health");

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
                    ITreeAttribute treeAttribute = ent.WatchedAttributes.GetTreeAttribute("health");
                    
                    if (treeAttribute == null)
                        return false;
                    
                    float targetHealth = treeAttribute.GetFloat("currenthealth");                   

                    if (maxTargetHealth < targetHealth)
                        return false;
                }
            }

            if (!IsTargetableEntity(ent, range, ignoreEntityCode))
                return false;

            return hasDirectContact(ent, range, range / 2);
        }

        public float MinDistanceToTarget()
        {
            return extraTargetDistance + Math.Max(0.1f, targetEntity.SelectionBox.XSize / 2 + entity.SelectionBox.XSize / 4);
        }

        public override void StartExecute()
        {
            base.StartExecute();

            consecutivePathFailCount = 0;
            stopNow = false;

            var bh = entity.GetBehavior<EntityBehaviorControlledPhysics>();
            stepHeight = bh == null ? 0.6f : bh.stepHeight;

            bool giveUpWhenNoPath = targetPos.SquareDistanceTo(entity.Pos.XYZ) < 12 * 12;
            int searchDepth = 3500;
            // 1 in 20 times we do an expensive search
            if (world.Rand.NextDouble() < 0.05)
            {
                searchDepth = 10000;
            }

            float moveSpeed = GetMovementSpeedForState(internalMovementState);

            hasPath = pathTraverser.NavigateTo(targetPos.Clone(), moveSpeed, MinDistanceToTarget(), OnGoalReached, OnStuck, giveUpWhenNoPath, searchDepth);
            if (!hasPath)
            {
                UpdateWithdrawPos();
                bool witdrawOk = pathTraverser.NavigateTo(withdrawPos.Clone(), moveSpeed, MinDistanceToTarget(), OnGoalReached, OnStuck, giveUpWhenNoPath, searchDepth);

                stopNow = !witdrawOk;
            }

            currentFollowTime = 0;
            currentWithdrawTime = 0;
            consecutivePathFailCount = 0;

            if ( !stopNow )
            {
                //play a sound associated with this action.
                entity.PlayEntitySound("engageentity", null, true);
            }
            
        }

        float lastPathUpdateSeconds;
        bool reachedWithdrawPosition = false;
        Entity targetLastUpdate = null;
        public override bool ContinueExecute(float dt)
        {
            AiUtility.UpdateLastTimeEntityInCombatMs(entity);

            if (targetEntity == null)
                return false;

            currentFollowTime += dt;
            lastPathUpdateSeconds += dt;

            eInternalMovementState lastMovementState = internalMovementState;
            UpdateMovementState();

            double distToTargetEntitySqr = entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.XYZ);

            //Depending on whether we are pursuing or engaging, determine the distance our target has to move for us to recompute our path.
            //When we are engaging (close range follow) we need to recompute more often so we can say on our target.
            float minRecomputeNavDistance = internalMovementState == eInternalMovementState.Engaging ? 1 * 1 : 3 * 3;
            bool activelyMoving = targetPos.SquareDistanceTo(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z) >= minRecomputeNavDistance;

            if ( lastPathUpdateSeconds >= 0.75f ||
                activelyMoving || internalMovementState != lastMovementState ||
                targetEntity != targetLastUpdate)
            {
                if (activelyMoving)
                    targetPos.Set(targetEntity.ServerPos.X + targetEntity.ServerPos.Motion.X * 10, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + targetEntity.ServerPos.Motion.Z * 10);
                else
                    targetPos.Set(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);

                bool giveUpWhenNoPath = withdrawIfNoPath;

                hasPath = pathTraverser.NavigateTo(targetPos, GetMovementSpeedForState(internalMovementState), MinDistanceToTarget(), OnGoalReached, OnStuck, giveUpWhenNoPath, 2000);
                lastPathUpdateSeconds = 0;

                if (hasPath)
                {
                    consecutivePathFailCount = 0;
                }
                else
                {
                    consecutivePathFailCount++;
                }
            
            }

            //Update our tareget this update.
            targetLastUpdate = targetEntity;

            if ( hasPath || !withdrawIfNoPath )
            {
                currentWithdrawTime = 0.0f;
                reachedWithdrawPosition = false;
                pathTraverser.CurrentTarget.X = targetEntity.ServerPos.X;
                pathTraverser.CurrentTarget.Y = targetEntity.ServerPos.Y;
                pathTraverser.CurrentTarget.Z = targetEntity.ServerPos.Z;
            }
            else if ( withdrawIfNoPath )
            {
                currentWithdrawTime += dt;

                //Try to withdraw based on our health level.
                bool injured = false;
                ITreeAttribute treeAttribute = entity.WatchedAttributes.GetTreeAttribute("health");

                if (treeAttribute != null)
                {
                    float currentHealth = treeAttribute.GetFloat("currenthealth");
                    float maxHealth = treeAttribute.GetFloat("maxhealth");

                    //If we are below half health or recently damaged, retreat farther.
                    if ( currentHealth < maxHealth * 0.5 || attackedByEntityMs < 15000 && attackedByEntity != null )
                    {
                        injured = true;
                    }
                }

                float withdrawRange = injured ? withdrawDistDamaged : withdrawDist;
                double encroachRange = withdrawRange - withdrawTargetMoveDistBeforeEncroaching;
                bool targetEncroaching = distToTargetEntitySqr <= encroachRange * encroachRange;
                //Withdraw till we reach our withdraw range, otherwise, only move if the target encroaches (moves closer while we still have no path).
                if (!reachedWithdrawPosition && distToTargetEntitySqr <= withdrawRange * withdrawRange || targetEncroaching)
                {
                    UpdateWithdrawPos();

                    float size = targetEntity.SelectionBox.XSize;
                    pathTraverser.WalkTowards(withdrawPos, GetMovementSpeedForState(internalMovementState), size + 0.2f, OnGoalReached, OnStuck);
                }
                else
                {

                    reachedWithdrawPosition = true;

                    pathTraverser.Stop();

                    if (engageAnimation != null)
                        entity.AnimManager.StopAnimation(engageAnimation);

                    if (pursueAnimation != null)
                        entity.AnimManager.StopAnimation(pursueAnimation);

                    if (withdrawAnimation != null)
                        entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = withdrawAnimation, Code = withdrawAnimation }.Init());

                    //Turn to face target.
                    Vec3f targetVec = new Vec3f();

                    targetVec.Set(
                        (float)(targetEntity.ServerPos.X - entity.ServerPos.X),
                        (float)(targetEntity.ServerPos.Y - entity.ServerPos.Y),
                        (float)(targetEntity.ServerPos.Z - entity.ServerPos.Z)
                    );

                    targetVec.Normalize();

                    float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);

                    float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
                    entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt, curTurnRadPerSec * dt);
                    entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;
                }
            }

            //if we have reached our target for the time being.
            if (internalMovementState == eInternalMovementState.Arrived)
            {
                pathTraverser.Stop();

                //Turn to face target.
                Vec3f targetVec = new Vec3f();

                targetVec.Set(
                    (float)(targetEntity.ServerPos.X - entity.ServerPos.X),
                    (float)(targetEntity.ServerPos.Y - entity.ServerPos.Y),
                    (float)(targetEntity.ServerPos.Z - entity.ServerPos.Z)
                );

                targetVec.Normalize();

                float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);

                float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
                entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt, curTurnRadPerSec * dt);
                entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;
            }

            // If we have been attacked by a new target, try transitioning aggro without canceling our behavior
            // Do the same if our current target has started routing from the battle.
            if ( attackedByEntity != null && attackedByEntity.Alive && attackedByEntity != targetEntity || AiUtility.IsRoutingFromBattle(targetEntity))
            {
                Entity newTarget = AquireNewTarget();
                if (newTarget != null && newTarget != targetEntity)
                {
                    targetEntity = newTarget;
                    TryAlarmHerd();
                }
                    
            }
            //If our path keeps failing, every 10 failures see if we can aquire a new target.
            else if ( consecutivePathFailCount / 10 >= 1.0f && consecutivePathFailCount % 10 == 0 )
            {
                Entity newTarget = AquireNewTarget();
                if (newTarget != null && newTarget != targetEntity)
                    stopNow = true;
            }
                
            Cuboidd targetBox = targetEntity.SelectionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.SelectionBox.Y2 / 2, 0).Ahead(entity.SelectionBox.XSize / 2, 0, entity.ServerPos.Yaw);
            double distance = targetBox.ShortestDistanceFrom(pos);

            bool inCreativeMode = (targetEntity as EntityPlayer)?.Player?.WorldData.CurrentGameMode == EnumGameMode.Creative;

            //float minDist = MinDistanceToTarget();
            float range = pursueRange;

            return
                currentFollowTime < maxFollowTime &&
                currentWithdrawTime < withdrawEndTime &&
                distance < range * range &&
                //(distance > minDist || (targetEntity is EntityAgent ea && ea.ServerControls.TriesToMove) &&
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

            targetEntity = null;
            pathTraverser.Stop();
        }


        public override bool Notify(string key, object data)
        {
            if (key == "pursueEntity")
            {
                //If we don't have a target, assist our group.
                if (targetEntity == null )
                {
                    //If we are in range of our ally, respond.
                    EntityTargetPairing targetPairing = (EntityTargetPairing)data;
                    Entity herdMember = targetPairing.entityTargeting;
                    Entity newTarget = targetPairing.targetEntity;

                    double distSqr = entity.ServerPos.XYZ.SquareDistanceTo(herdMember.ServerPos.XYZ);
                    if ( distSqr <= pursueRange * pursueRange )
                    {
                        targetEntity = newTarget;
                        targetPos = targetEntity.ServerPos.XYZ;
                        return true;
                    }
                }
            }
            else if ( key == "entityRouted")
            {
                //If our target has routed, stop pursuing.
                if (targetEntity == (Entity)data)
                {
                    stopNow = true;
                    return true;
                }
            }
            else if ( key == "haltMovement")
            {
                //If another task has requested we halt, stop pursuing.
                if (entity == (Entity)data)
                {
                    stopNow = true;
                    return true;
                }
            }
            else if (key == "entityAttackedGuardedEntity")
            {
                //If a guard task tells us our guard target has been attacked, pursue and engage the attacker.
                if ((Entity)data != null && guardTargetAttackedByEntity != (Entity)data)
                {
                    guardTargetAttackedByEntity = (Entity)data;
                    targetEntity = guardTargetAttackedByEntity;
                    targetPos = targetEntity.ServerPos.XYZ;
                    return true;
                }
            }
            //Clear the entity that attacked our guard target.
            else if (key == "guardChaseStop")
            {
                if (targetEntity == guardTargetAttackedByEntity)
                    stopNow = true;

                guardTargetAttackedByEntity = null;
                return false;
            }
            else if (key == "clearTargetHistory")
            {
                ClearTargetHistory();
                return false;
            }

            return false;
        }


        private void OnStuck()
        {
            stopNow = true;
        }

        private void OnGoalReached()
        {
            pathTraverser.Retarget();
        }

        private void UpdateMovementState()
        {
            Vec3d entityVertival = new Vec3d(0, this.entity.ServerPos.XYZ.Y, 0);
            Vec3d targetVertival = new Vec3d(0, targetEntity.ServerPos.XYZ.Y, 0);

            double distSqr = this.entity.ServerPos.SquareDistanceTo(targetPos);
            double distSqrVertical = targetVertival.SquareDistanceTo(entityVertival);

            if ( distSqr <= arriveRange * arriveRange && distSqrVertical <= arriveVerticalRange * arriveVerticalRange)
            {
                internalMovementState = eInternalMovementState.Arrived;

                if (pursueAnimation != null)
                    entity.AnimManager.StopAnimation(pursueAnimation);

                if (engageAnimation != null)
                    entity.AnimManager.StopAnimation(engageAnimation);

                if (withdrawAnimation != null)
                    entity.AnimManager.StopAnimation(withdrawAnimation);
            }
            else if (distSqr <= engageRange * engageRange)
            {
                //Engage State
                internalMovementState = eInternalMovementState.Engaging;

                if (pursueAnimation != null)
                    entity.AnimManager.StopAnimation(pursueAnimation);

                if (withdrawAnimation != null)
                    entity.AnimManager.StopAnimation(withdrawAnimation);

                if (engageAnimation != null)
                    entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = engageAnimation, Code = engageAnimation }.Init());
            }
            else
            {
                //Pursue State
                internalMovementState = eInternalMovementState.Pursuing;

                if (engageAnimation != null)
                    entity.AnimManager.StopAnimation(engageAnimation);

                if (withdrawAnimation != null)
                    entity.AnimManager.StopAnimation(withdrawAnimation);

                if (pursueAnimation != null)
                    entity.AnimManager.StartAnimation(new AnimationMetaData() { Animation = pursueAnimation, Code = pursueAnimation }.Init());
            }
        }

        private float GetMovementSpeedForState( eInternalMovementState movementState )
        {
            switch (movementState)
            {
                case eInternalMovementState.Arrived:
                    return 0.0f;
                case eInternalMovementState.Engaging:
                    return engageSpeed;
                case eInternalMovementState.Pursuing:
                    return pursueSpeed;
            }

            Debug.Assert(false, "Invalid intermal move state.");
            return 0.0f;
        }

        private void UpdateWithdrawPos()
        {
            float yaw = (float)Math.Atan2(targetEntity.ServerPos.X - entity.ServerPos.X, targetEntity.ServerPos.Z - entity.ServerPos.Z);

            // Simple steering behavior
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw - GameMath.PI / 2);

            // Running into wall?
            if (IsTraversable(tmpVec))
            {
                withdrawPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw - GameMath.PI / 2);
                return;
            }

            // Try 90 degrees left
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw - GameMath.PI);
            if (IsTraversable(tmpVec))
            {
                withdrawPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw - GameMath.PI);
                return;
            }

            // Try 90 degrees right
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw);
            if (IsTraversable(tmpVec))
            {
                withdrawPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw);
                return;
            }

            // Run towards target o.O
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, -yaw);
            withdrawPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, -yaw);

        }

        bool IsTraversable(Vec3d pos)
        {
            return
                !entity.World.CollisionTester.IsColliding(entity.World.BlockAccessor, entity.SelectionBox, pos, false) ||
                !entity.World.CollisionTester.IsColliding(entity.World.BlockAccessor, entity.SelectionBox, collTmpVec.Set(pos).Add(0, Math.Min(1, stepHeight), 0), false)
            ;
        }

        private List<Entity> potentialTargets = new List<Entity>();
        private List<Entity> potentialRoutingTargets = new List<Entity>();

        private Entity AquireNewTarget()
        {
            float range = pursueRange;
            potentialTargets.Clear();
            potentialRoutingTargets.Clear();
            partitionUtil.WalkEntityPartitions(entity.ServerPos.XYZ, range, (e) => BucketTargetBasedOnCombatState(e, range));

            Entity target = null;
            if ( potentialTargets.Count > 0 )
            {
                Entity bestTarget = potentialTargets[0];
                double bestDistSqr = entity.ServerPos.XYZ.SquareDistanceTo(bestTarget.ServerPos.XYZ);
                foreach (Entity ent in potentialTargets )
                {
                    double distSqr = entity.ServerPos.XYZ.SquareDistanceTo(ent.ServerPos.XYZ);
                    if ( distSqr <= bestDistSqr)
                    {
                        bestTarget = ent;
                        bestDistSqr = distSqr;
                    }    
                }

                target = bestTarget;
            }
            else if( potentialRoutingTargets.Count > 0 )
            {
                Entity bestTarget = potentialRoutingTargets[0];
                double bestDistSqr = entity.ServerPos.XYZ.SquareDistanceTo(bestTarget.ServerPos.XYZ);
                foreach (Entity ent in potentialRoutingTargets)
                {
                    double distSqr = entity.ServerPos.XYZ.SquareDistanceTo(ent.ServerPos.XYZ);
                    if (distSqr <= bestDistSqr)
                    {
                        bestTarget = ent;
                        bestDistSqr = distSqr;
                    }
                }

                target = bestTarget;
            }

            return target;
        }

        private bool BucketTargetBasedOnCombatState( Entity ent, float range )
        {
            if (!IsEntityTargetableByPack(ent, range))
                return false;

            //Don't Chase Ai that are already routing.
            if (AiUtility.IsRoutingFromBattle(ent))
            {
                potentialRoutingTargets.Add(ent);
            }
            else
            {
                potentialTargets.Add(ent);
            }

            return true;
        }

        private void TryAlarmHerd()
        {
            if ((alarmHerd) && entity.HerdId > 0)
            {

                foreach ( EntityAgent herdMember in herdMembers)
                {
                    if (herdMember.EntityId != entity.EntityId && herdMember.Alive && herdMember.HerdId == entity.HerdId)
                    {
                        EntityTargetPairing targetPairing = new EntityTargetPairing( entity, targetEntity );
                        herdMember.Notify("pursueEntity", targetPairing);
                    }
                        
                }
            }
        }
    }
}
