using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;

namespace OutlawMod
{

    public class EntityOutlaw: EntityHumanoid
    {

        const float MIN_DISTANCE_TO_WORLD_SPAWN = 250; //todo: replace this with a config file setting.

        const bool DEUBUG_PRINTS = true;

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

            //todo: make it so outlaws only get blocked from spawning in survival mode.

            if ( Utility.IsSurvivalMode( Api ) )
            {
                Vec3d spawnPosition = Pos.XYZ;

                //Make it so Outlaws can't spawn in the starting area.
                EntityPos defaultWorldSpawn = this.Api.World.DefaultSpawnPosition;
                double distToWorldSpawnSqr = spawnPosition.HorizontalSquareDistanceTo(defaultWorldSpawn.XYZ);

                if (distToWorldSpawnSqr < (MIN_DISTANCE_TO_WORLD_SPAWN * MIN_DISTANCE_TO_WORLD_SPAWN))
                {
                    if (DEUBUG_PRINTS)
                        Api.World.Logger.Warning("Spawn Failed for " + this.Code.Path + " at: " + spawnPosition + ". Too Close to Player Starting Spawn Area.");
                
                    //We do this post spawn, so that spawning system doesn't spend additinal frames repeatedly failing and taking up frames for something that will fail 100% of the time.
                    this.Die( EnumDespawnReason.Removed, null );
                    return;
                }

                //Do Not Allow Outlaws to Spawn on Claimed Land.
                //Make this a config setting.
                //TODO: MAKE SURE THIS ISN"T CRAZY EXPENSIVE!
                List<LandClaim> landclaims = Api.World.Claims.All;
                foreach(LandClaim landclaim in landclaims)
                {
                    if ( landclaim.PositionInside(spawnPosition) )
                    {
                        if (DEUBUG_PRINTS)
                            Api.World.Logger.Warning("Spawn Failed for " + this.Code.Path + " at: " + spawnPosition + ". Attempted to spawn on landclaim " + landclaim.Description );

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

    }

}