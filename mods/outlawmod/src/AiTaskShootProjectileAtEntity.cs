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

namespace OutlawMod
{
    public class AiTaskShootProjectileAtEntity : AiTaskBaseTargetable
    {
        int durationMs;
        int releaseAtMs;
        long lastSearchTotalMs;

        float minVertDist = 2f;
        float minDist = 3f;
        float maxDist = 15f;
        float minRangeDistOffTarget = 0.0f; //this is the number of blocks off target a projectile will stray at min range.
        float maxRangeDistOffTarget = 0.0f; //this is the number of blocks off target a projectile will stray at max range.
        float maxVelocity = 1.0f;

        float damage = 1.0f;
        float damageFalloffPercent = 0.0f;      //Percentage reduction do base damage when falloff distance hits max.
        float damageFalloffStartDist = -1.0f;   //Distance in blocks where damage falloff begins.
        float damageFalloffEndDist = -1.0f;     //Distance in blocks where damage falloff hits full percent value.

        string projectileItem = "arrow-copper";

        EntityPartitioning partitionUtil;

        float accum = 0;
        bool didThrow;

        float minTurnAnglePerSec;
        float maxTurnAnglePerSec;
        float curTurnRadPerSec;

        Random rnd = new Random(420691337); //Make this use the world gen seed.

        public AiTaskShootProjectileAtEntity(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

            base.LoadConfig(taskConfig, aiConfig);

            this.durationMs = taskConfig["durationMs"].AsInt(1500);
            this.releaseAtMs = taskConfig["releaseAtMs"].AsInt(1000);
            this.minDist = taskConfig["minDist"].AsFloat(3f);
            this.minVertDist = taskConfig["minVertDist"].AsFloat(2f);
            this.maxDist = taskConfig["maxDist"].AsFloat(15f);
            this.minRangeDistOffTarget = taskConfig["minRangeDistOffTarget"].AsFloat(0.0f);
            this.maxRangeDistOffTarget = taskConfig["maxRangeDistOffTarget"].AsFloat(0.0f);
            this.maxVelocity = taskConfig["maxVelocity"].AsFloat(1.0f);
            this.damage = taskConfig["damage"].AsFloat(1.0f);
            this.damageFalloffPercent = taskConfig["damageFalloffPercent"].AsFloat(0.0f);
            this.damageFalloffStartDist = taskConfig["damageFalloffStartDist"].AsFloat(-1.0f);
            this.damageFalloffEndDist = taskConfig["damageFalloffEndDist"].AsFloat(-1.0f);
            this.projectileItem = taskConfig["projectileItem"].AsString("arrow-copper");

            //Error checking for bad json values.
            Debug.Assert(damageFalloffPercent >= 0.0f && damageFalloffPercent <= 1.0f, "AiTaskValue damageFalloffPercent must be a 0.0 to 1.0 value.");
            Debug.Assert(damageFalloffStartDist < damageFalloffEndDist || damageFalloffEndDist < 0.0f, "AiTaskValue damageFalloffStartDist: " + damageFalloffStartDist + " cannot be greater than damageFalloffEndDist: " + damageFalloffEndDist);
        }


        public override bool ShouldExecute()
        {
            // React immediately on hurt, otherwise only 1/10 chance of execution
            if (rand.NextDouble() > 0.1f && (whenInEmotionState == null || bhEmo?.IsInEmotionState(whenInEmotionState) != true)) return false;

            if (whenInEmotionState != null && bhEmo?.IsInEmotionState(whenInEmotionState) != true) return false;
            if (whenNotInEmotionState != null && bhEmo?.IsInEmotionState(whenNotInEmotionState) == true) return false;
            if (whenInEmotionState == null && rand.NextDouble() > 0.5f) return false;
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;

            float range = maxDist;
            lastSearchTotalMs = entity.World.ElapsedMilliseconds;
            Vec3d ownPos = entity.ServerPos.XYZ;

            targetEntity = partitionUtil.GetNearestEntity(entity.ServerPos.XYZ, range, (e) => IsTargetableEntity(e, range) && hasDirectContact(e, range, range / 2f));

            return targetEntity != null;
        }

        public override void StartExecute()
        {
            accum = 0;
            didThrow = false;

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



        public override bool ContinueExecute(float dt)
        {
            Vec3f targetVec = new Vec3f();

            targetVec.Set(
                (float)(targetEntity.ServerPos.X - entity.ServerPos.X),
                (float)(targetEntity.ServerPos.Y - entity.ServerPos.Y),
                (float)(targetEntity.ServerPos.Z - entity.ServerPos.Z)
            );

            float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);

            float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
            entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt, curTurnRadPerSec * dt);
            entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;

            if (Math.Abs(yawDist) > 0.02) return true;

            if (animMeta != null)
            {
                animMeta.EaseInSpeed = 1f;
                animMeta.EaseOutSpeed = 1f;
                entity.AnimManager.StartAnimation(animMeta);
            }

            accum += dt;

            if (accum > releaseAtMs / 1000f && !didThrow)
            {
                didThrow = true;

                double pitchDir = 1.0;
                double yawDir = 1.0;

                double pitchCoinToss = rnd.NextDouble();
                double yawCoinToss = rnd.NextDouble();

                if (pitchCoinToss > 0.5f)
                    pitchDir = -1.0f;

                if ( yawCoinToss > 0.5f )
                    yawDir = -1.0f;

                Vec3d shotStartPosition = entity.ServerPos.XYZ.Add(0, entity.LocalEyePos.Y, 0);
                Vec3d shotTargetPos = targetEntity.ServerPos.XYZ.Add(0, targetEntity.LocalEyePos.Y, 0);
                
                //Todo: Get this working with target velocity so we can lead our targets.
                shotTargetPos = shotTargetPos.Add( targetEntity.ServerPos.Motion );

                float distToTargetSqr = shotStartPosition.SquareDistanceTo(shotTargetPos);

                double distanceOffTarget = MathUtility.GraphClampedValue(minDist * minDist, maxDist * maxDist, minRangeDistOffTarget, maxRangeDistOffTarget, distToTargetSqr);

                double rndPitch = (rnd.NextDouble() * distanceOffTarget) * pitchDir;
                double rndYaw = (rnd.NextDouble() * distanceOffTarget) * yawDir;

                Vec3d shotDriftDirection = new Vec3d(0.0f, rndPitch, rndYaw);


                Vec3d shotTargetPosWithDrift = shotTargetPos.Add( shotDriftDirection.X, shotDriftDirection.Y, shotDriftDirection.Z );

                double distf = Math.Pow(shotStartPosition.SquareDistanceTo(shotTargetPosWithDrift), 0.1);

                Debug.WriteLine("Distance: " + distf);

                Vec3d velocity = (shotTargetPosWithDrift - shotStartPosition).Normalize() * GameMath.Clamp(distf, 0.1f, maxVelocity);

                if( damageFalloffStartDist < 0.0f )
                    damageFalloffStartDist = maxDist;

                if( damageFalloffEndDist < 0.0f )
                    damageFalloffEndDist = maxDist;

                float currentFalloffPercentile = (float) MathUtility.GraphClampedValue(damageFalloffStartDist * damageFalloffStartDist, damageFalloffEndDist * damageFalloffEndDist, 0.0f, damageFalloffPercent, distToTargetSqr);
                float projectileDamage = damage - (damage * currentFalloffPercentile);

                EntityProperties type = entity.World.GetEntityType(new AssetLocation(projectileItem));
                Entity projectile = entity.World.ClassRegistry.CreateEntity(type);
                ((EntityProjectile)projectile).FiredBy = entity;
                ((EntityProjectile)projectile).Damage = projectileDamage;
                ((EntityProjectile)projectile).ProjectileStack = new ItemStack(entity.World.GetItem(new AssetLocation(projectileItem)));
                ((EntityProjectile)projectile).DropOnImpactChance = 0.0f; //Todo: Make shot projectiles always break so players cannot farm them.
                ((EntityProjectile)projectile).Weight = 0.0f;

                projectile.ServerPos.SetPos(entity.SidedPos.BehindCopy(0.21).XYZ.Add(0, entity.LocalEyePos.Y, 0));
                projectile.ServerPos.Motion.Set(velocity);

                projectile.Pos.SetFrom(entity.ServerPos);
                projectile.World = entity.World;
                ((EntityProjectile)projectile).SetRotation();

                entity.World.SpawnEntity(projectile);
            }

            return accum < durationMs / 1000f;
        }




        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);

        }
    }
}