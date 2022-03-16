using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace OutlawMod
{

    public class EntityOutlaw: EntityHumanoid
    {

        POIRegistry poiregistry;

        const float MIN_DISTANCE_TO_WORLD_SPAWN = 250; //todo: replace this with a config file setting.
        const float MAX_SPAWN_EXCLUSION_POI_SEARCH_DIST = 250;

        const string SPAWN_EXCLUSION_POI_TYPE = "outlawSpawnBlocker";

        const bool DEUBUG_PRINTS = true;

        //Spawn Exclusion Search Vars
        private bool spawnIsBlockedByBlocker = false; //This must be reset whenever we do an OnEntitySpawn call. It is set by the PoiMatcher function to skip additional once we've found a ISpawnBlocker that will block our spawn request.
        private Vec3d currentSpawnTryPosition; 

        //Look at Entity.cs in the VAPI project for more functions you can override.

        /// <summary>
        /// Called when this entity got created or loaded
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="api"></param>
        /// <param name="InChunkIndex3d"></param>
        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            poiregistry = api.ModLoader.GetModSystem<POIRegistry>();
        }

        /// <summary>
        /// Called when after the got loaded from the savegame (not called during spawn)
        /// </summary>
        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
        }

        /// <summary>
        /// Called when the entity spawns (not called when loaded from the savegame).
        /// </summary>
        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            //Only run on server side, because POIs are serveside only.
            if (Api.Side == EnumAppSide.Server)
            {
                currentSpawnTryPosition = Pos.XYZ;
                spawnIsBlockedByBlocker = false;

                //todo: make it so outlaws only get blocked from spawning in survival mode.
                if (Utility.IsSurvivalMode(Api))
                {
                    //Make it so Outlaws can't spawn in the starting area.
                    EntityPos defaultWorldSpawn = this.Api.World.DefaultSpawnPosition;
                    double distToWorldSpawnSqr = currentSpawnTryPosition.HorizontalSquareDistanceTo(defaultWorldSpawn.XYZ);

                    if (distToWorldSpawnSqr < (MIN_DISTANCE_TO_WORLD_SPAWN * MIN_DISTANCE_TO_WORLD_SPAWN))
                    {
                        if (DEUBUG_PRINTS)
                            Api.World.Logger.Warning("Spawn Failed for " + this.Code.Path + " at: " + currentSpawnTryPosition + ". Too Close to Player Starting Spawn Area.");

                        //We do this post spawn, so that spawning system doesn't spend additinal frames repeatedly failing and taking up frames for something that will fail 100% of the time.
                        this.Die(EnumDespawnReason.Removed, null);
                        return;
                    }

                    //Do Not Allow Outlaws to Spawn on Claimed Land.
                    //Make this a config setting.
                    List<LandClaim> landclaims = Api.World.Claims.All;
                    foreach (LandClaim landclaim in landclaims)
                    {
                        if (landclaim.PositionInside(currentSpawnTryPosition))
                        {
                            if (DEUBUG_PRINTS)
                                Api.World.Logger.Warning("Spawn Failed for " + this.Code.Path + " at: " + currentSpawnTryPosition + ". Attempted to spawn on landclaim " + landclaim.Description);

                            this.Die(EnumDespawnReason.Removed, null);
                            return;
                        }

                    }

                    //Do Not Allow Outlaws to Spawn near BlockEntityOutlawDeterents.
                    poiregistry.WalkPois(currentSpawnTryPosition, MAX_SPAWN_EXCLUSION_POI_SEARCH_DIST, SpawnExclusionMatcher);
                    if (spawnIsBlockedByBlocker == true)
                    {
                        if (DEUBUG_PRINTS)
                            Api.World.Logger.Warning("Spawn Failed for " + this.Code.Path + " at: " + currentSpawnTryPosition + ". Attempted to spawn within spawn blocker");

                        this.Die(EnumDespawnReason.Removed, null);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Called when the entity despawns
        /// </summary>
        /// <param name="despawn"></param>
        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            base.OnEntityDespawn(despawn);
        }

        public bool SpawnExclusionMatcher( IPointOfInterest poi )
        {
            if (poi.Type == SPAWN_EXCLUSION_POI_TYPE && !spawnIsBlockedByBlocker)
            {
                IOutlawSpawnBlocker spawnBlocker = (IOutlawSpawnBlocker)poi;

                float distToBlockerSqr = spawnBlocker.Position.SquareDistanceTo(currentSpawnTryPosition);
                float blockingRange = spawnBlocker.blockingRange();

                //We have found a blocker that blocks our spawn, no need to run compuations on other pois.
                if ( distToBlockerSqr <= blockingRange * blockingRange)
                    spawnIsBlockedByBlocker = true;

                return true;
            }
                
            return false;
        }


    }

}