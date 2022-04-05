using System;
using System.Diagnostics;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ExpandedAiTasks
{
    public class AiTaskMorale : AiTaskBaseTargetable
    {
        ICoreServerAPI sapi;
        Vec3d targetPos = new Vec3d();
        
        float moveSpeed = 0.02f;
        float routDistance = 31f;
        float rallyTimeAfterRoutMs = 5000;
        bool cancelOnHurt = false;

        double minMorale = 0.5f;
        double maxMorale = 0.8f;
        float moraleRange = 15f;

        string[] poiSourcesOfFearNames = new string[0];
        double[] poiSourcesOfFearWeights = new double[0];
        Dictionary<string,double> poiSourcesOfFearWeightsByName = new Dictionary<string,double>();

        bool useGroupMorale = false;
        bool canRoutFromAnyEnemy = false;

        double moraleLevel = 0.0f;
        double fearLevel = 0.0f;
        
        long fleeStartMs;
        bool stuck;

        protected List<Entity> herdMembers = new List<Entity>();
        
        static POIRegistry poiregistry;
        double poiSourceOfFearTotalWeight = 0;

        float stepHeight;

        EntityPartitioning partitionUtil;

        Vec3d tmpVec = new Vec3d();
        Vec3d collTmpVec = new Vec3d();

        bool cancelNow;
        public override bool AggressiveTargeting => false;


        public AiTaskMorale(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            partitionUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

            base.LoadConfig(taskConfig, aiConfig);

            moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
            cancelOnHurt = taskConfig["cancelOnHurt"].AsBool(false);
            routDistance = taskConfig["routDistance"].AsFloat(31);
            rallyTimeAfterRoutMs = taskConfig["rallyTimeAfterRouteMs"].AsInt(9000);

            minMorale = taskConfig["minMorale"].AsDouble(0.5f);
            maxMorale = taskConfig["maxMorale"].AsDouble(0.8f);
            moraleRange = taskConfig["moraleRange"].AsFloat(15f);

            useGroupMorale = taskConfig["useGroupMorale"].AsBool(false);
            canRoutFromAnyEnemy= taskConfig["canRoutFromAnyEnemy"].AsBool(false);

            poiSourcesOfFearNames = taskConfig["poiSourcesOfFearNames"].AsArray<string>();
            poiSourcesOfFearWeights = taskConfig["poiSourcesOfFearWeights"].AsArray<double>();

            Debug.Assert(minMorale <= maxMorale, "minMorale must be less than or equal to maxMorale.");

            //Determin this Ai's morale level.
            moraleLevel = minMorale + ( ( maxMorale - minMorale ) * entity.World.Rand.NextDouble() );

            //Build data tables that relate the things the ai is afraid to fear weight values.
            BuildSourceOfFearTables();

            if (entity.Api.Side == EnumAppSide.Server)
            {
                sapi = entity.Api as ICoreServerAPI;
                poiregistry = sapi.ModLoader.GetModSystem<POIRegistry>();
            }
        }




        public override bool ShouldExecute()
        {
            soundChance = Math.Min(1.01f, soundChance + 1 / 500f);

            if (whenInEmotionState != null && bhEmo?.IsInEmotionState(whenInEmotionState) != true) 
                return false;

            if (whenNotInEmotionState != null && bhEmo?.IsInEmotionState(whenNotInEmotionState) == true) 
                return false;

            // Double exec chance, but therefore halved here again to increase response speed for creature when aggressive
            //if (whenInEmotionState == null && rand.NextDouble() > 0.5f) 
            //    return false;

            if (useGroupMorale)
            {
                if (herdMembers.Count == 0)
                {
                    herdMembers = new List<Entity>();
                    partitionUtil.GetNearestEntity(entity.ServerPos.XYZ, moraleRange, (e) => CountHerdMembers(e, moraleRange));
                }
                else
                {
                    UpdateHerdCount();
                }
            }

            fearLevel = 0;

            //We need to calculate the Ai's fear level and route if we are too scared.
            double injuryRatio = 0.0;
            if (useGroupMorale)
                injuryRatio = CalculateHerdInjuryRatio();
            else
                injuryRatio = CalculateIndividualInjuryRatio();

            Vec3d ownPos = entity.ServerPos.XYZ;

            //TO DO:ONCE WE'VE BUILT ALL OUR FEAR WEIGHT TABLES, WE SHOULD POPULATE ANY OF THE ENTITY RELATED ONES DURING THIS SEARCH.
            //REPLACE IsTargetableEntity WITH A FUNCTION THAT CALLS IT, BUT ALSO GENERATES THE FEAR WEIGHTS WE WILL NEED LATER DURING OUR SEARCH.
            targetEntity = (EntityAgent)partitionUtil.GetNearestEntity(ownPos, moraleRange, (e) => IsTargetableEntity(e, moraleRange, canRoutFromAnyEnemy));

            if (targetEntity != null)
            {
                //Take the target's injury ratio into account.
                double targetInjuryRatio = CalculateEntityInjuryRatio(targetEntity);

                double poiSourcesOfFearWeight = 0;
                if (poiSourcesOfFearNames.Length > 0)
                    poiSourcesOfFearWeight = GetPoiSourceOfFearWeight();


                //We should be less scared if we're winning.
                fearLevel = Math.Max(injuryRatio - targetInjuryRatio, 0) + poiSourcesOfFearWeight;

                if ( fearLevel >= moraleLevel)
                {
                    UpdateTargetPos();
                    return true;
                }
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
            foreach (Entity agent in herdMembers)
            {
                if (agent == null)
                    continue;

                currentMembers.Add(agent);
            }

            herdMembers = currentMembers;
        }

        public override void StartExecute()
        {
            base.StartExecute();

            cancelNow = false;

            var bh = entity.GetBehavior<EntityBehaviorControlledPhysics>();
            stepHeight = bh == null ? 0.6f : bh.stepHeight;

            soundChance = Math.Max(0.025f, soundChance - 0.2f);

            float size = targetEntity.SelectionBox.XSize;

            pathTraverser.WalkTowards(targetPos, moveSpeed, size + 0.2f, OnGoalReached, OnStuck);

            fleeStartMs = entity.World.ElapsedMilliseconds;
            stuck = false;
        }


        public override bool ContinueExecute(float dt)
        {
            if (world.Rand.NextDouble() < 0.2)
            {
                UpdateTargetPos();
                pathTraverser.CurrentTarget.X = targetPos.X;
                pathTraverser.CurrentTarget.Y = targetPos.Y;
                pathTraverser.CurrentTarget.Z = targetPos.Z;
            }

            if (entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos.XYZ) > routDistance * routDistance)
            {
                return false;
            }

            return !stuck && targetEntity.Alive && (entity.World.ElapsedMilliseconds - fleeStartMs < rallyTimeAfterRoutMs) && !cancelNow && pathTraverser.Active;
        }



        private void UpdateTargetPos()
        {
            float yaw = (float)Math.Atan2(targetEntity.ServerPos.X - entity.ServerPos.X, targetEntity.ServerPos.Z - entity.ServerPos.Z);

            // Simple steering behavior
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw - GameMath.PI / 2);

            // Running into wall?
            if (traversable(tmpVec))
            {
                targetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw - GameMath.PI / 2);
                return;
            }

            // Try 90 degrees left
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw - GameMath.PI);
            if (traversable(tmpVec))
            {
                targetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw - GameMath.PI);
                return;
            }

            // Try 90 degrees right
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, yaw);
            if (traversable(tmpVec))
            {
                targetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, yaw);
                return;
            }

            // Run towards target o.O
            tmpVec = tmpVec.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            tmpVec.Ahead(0.9, 0, -yaw);
            targetPos.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z).Ahead(10, 0, -yaw);
        }


        public override void OnEntityHurt(DamageSource source, float damage)
        {
            base.OnEntityHurt(source, damage);

            if (cancelOnHurt) cancelNow = true;
        }


        bool traversable(Vec3d pos)
        {
            return
                !world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, pos, false) ||
                !world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, collTmpVec.Set(pos).Add(0, Math.Min(1, stepHeight), 0), false)
            ;
        }


        public override void FinishExecute(bool cancelled)
        {
            pathTraverser.Stop();
            base.FinishExecute(cancelled);
        }


        private void OnStuck()
        {
            stuck = true;
        }

        private void OnGoalReached()
        {
            pathTraverser.Retarget();
        }

        private void BuildSourceOfFearTables()
        {
            Debug.Assert(poiSourcesOfFearNames.Length == poiSourcesOfFearWeights.Length, "poiSourcesOfFearNames entry count does not match poiSourcesOfFearWeights entry count, these must match exactly.");

            //Build table for pois that scare the ai.
            for ( int i = 0; i < poiSourcesOfFearNames.Length; i++)
            {
                poiSourcesOfFearWeightsByName.Add(poiSourcesOfFearNames[i], poiSourcesOfFearWeights[i]);
            }

            //To do: We need to do this with entityCodes and a weight array so that individual entities can scare this Ai more than others.

        }

        private double CalculateEntityInjuryRatio(Entity ent)
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

        private double CalculateIndividualInjuryRatio()
        {
            ITreeAttribute treeAttribute = entity.WatchedAttributes.GetTreeAttribute("health");

            if (treeAttribute != null)
            {
                double currentHealth = treeAttribute.GetFloat("currenthealth"); ;
                double maxHealth = treeAttribute.GetFloat("maxhealth"); ;

                return (maxHealth - currentHealth) / maxHealth;
            }

            return 0.0;
        }

        private double CalculateHerdInjuryRatio()
        {
            if (herdMembers.Count == 0)
                return 0f;

            double totalCurrentHealth = 0f;
            double totalMaxHealth = 0f;
            foreach ( Entity herdMember in herdMembers )
            {
                ITreeAttribute treeAttribute = entity.WatchedAttributes.GetTreeAttribute("health");

                if (treeAttribute != null)
                {
                    totalCurrentHealth += treeAttribute.GetFloat("currenthealth"); ;
                    totalMaxHealth += treeAttribute.GetFloat("maxhealth"); ;
                }
            }

            return (totalMaxHealth - totalCurrentHealth) / totalMaxHealth;
        }

        private double GetPoiSourceOfFearWeight()
        {
            poiSourceOfFearTotalWeight = 0;
            poiregistry.WalkPois(entity.ServerPos.XYZ, moraleRange, PoiSourceOfFearMatcher);

            return poiSourceOfFearTotalWeight;
        }

        public bool PoiSourceOfFearMatcher(IPointOfInterest poi)
        {  
            if (poiSourcesOfFearWeightsByName.ContainsKey(poi.Type) )
            {
                poiSourceOfFearTotalWeight += poiSourcesOfFearWeightsByName[poi.Type];
                return true;
            }
            
            return false;
        }
    }
}
