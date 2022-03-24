using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace OutlawMod
{

    public class EntityOutlaw: EntityHumanoid
    {

        public static OrderedDictionary<string, TraderPersonality> Personalities = new OrderedDictionary<string, TraderPersonality>()
        {
            { "formal", new TraderPersonality(1, 1, 0.9f) },
            { "balanced", new TraderPersonality(1.2f, 0.9f, 1.1f) },
            { "lazy", new TraderPersonality(1.65f, 0.7f, 0.9f) },
            { "rowdy", new TraderPersonality(0.75f, 1f, 1.8f) },
        };

        public string Personality
        {
            get { return WatchedAttributes.GetString("personality", "rowdy"); }
            set
            {
                WatchedAttributes.SetString("personality", value);
                talkUtil?.SetModifiers(Personalities[value].TalkSpeedModifier, Personalities[value].PitchModifier, Personalities[value].VolumneModifier);
            }
        }

        POIRegistry poiregistry;

        private const float MAX_SPAWN_EXCLUSION_POI_SEARCH_DIST = 250;

        const string SPAWN_EXCLUSION_POI_TYPE = "outlawSpawnBlocker";

        private static bool DEUBUG_PRINTS = false;

        //Spawn Exclusion Search Vars
        private bool spawnIsBlockedByBlocker = false; //This must be reset whenever we do an OnEntitySpawn call. It is set by the PoiMatcher function to skip additional once we've found a ISpawnBlocker that will block our spawn request.
        private Vec3d currentSpawnTryPosition;

        public EntityTalkUtil talkUtil;

        //Look at Entity.cs in the VAPI project for more functions you can override.

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            poiregistry = api.ModLoader.GetModSystem<POIRegistry>();

            //Assign Personality for Classic Voice
            if ( World.Side == EnumAppSide.Client && OMGlobalConstants.outlawsUseClassicVintageStoryVoices)
            {
                this.talkUtil = new EntityTalkUtil(api as ICoreClientAPI, this);

                Personality = Personalities.GetKeyAtIndex(World.Rand.Next(Personalities.Count));

                AssetLocation voiceSound = new AssetLocation(properties.Attributes?["classicVoice"].ToString());
                
                if (voiceSound != null)
                    talkUtil.soundName = voiceSound;

                this.Personality = this.Personality; // to update the talkutil
            }
        }

        /// <summary>
        /// Called when after the got loaded from the savegame (not called during spawn)
        /// </summary>
        public override void OnEntityLoaded()
        {
            DEUBUG_PRINTS = OMGlobalConstants.devMode;
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

                ////////////////////////////////////////////
                ///BLOCKED BY START SPAWN SAFE ZONE CHECK///
                ////////////////////////////////////////////


                //Make it so outlaws only get blocked if any players are in survival mode.
                if (Utility.AnyPlayersOnlineInSurvivalMode(Api) || OMGlobalConstants.devMode)
                {
                    
                    double totalDays = Api.World.Calendar.TotalDays;
                    double safeZoneDaysLeft = Math.Max(OMGlobalConstants.startingSpawnSafeZoneLifetimeInDays - totalDays, 0);
                    double safeZoneRadius = OMGlobalConstants.startingSpawnSafeZoneRadius;
                    
                    //If our starting safety zone has a lifetime (any negative value means never despawn).
                    if (OMGlobalConstants.startingSafeZoneHasLifetime)
                    {
                        //If we are cofigured to shrink the safe zone over time. Figure out how big the spawn zone should be on this calender day.
                        if ( OMGlobalConstants.startingSafeZoneShrinksOverlifetime )
                        {
                            safeZoneRadius = MathUtility.GraphClampedValue( OMGlobalConstants.startingSpawnSafeZoneLifetimeInDays, 0, OMGlobalConstants.startingSpawnSafeZoneRadius, 0, safeZoneDaysLeft);
                        }
                        //If we are cofigured to have our safe zone to come to a hard stop after X days.
                        else
                        {
                            safeZoneRadius = safeZoneDaysLeft > 0 ? safeZoneRadius : 0;
                        }
                    }                        

                    //If we have an eternal safe zone, or a zone that has lifetime days remaining, try to block spawns.
                    if ( !OMGlobalConstants.startingSafeZoneHasLifetime || ( safeZoneDaysLeft > 0 ) )
                    {
                        //Make it so Outlaws can't spawn in the starting area.
                        EntityPos defaultWorldSpawn = this.Api.World.DefaultSpawnPosition;
                        double distToWorldSpawnSqr = currentSpawnTryPosition.HorizontalSquareDistanceTo(defaultWorldSpawn.XYZ);

                        if (distToWorldSpawnSqr < (safeZoneRadius * safeZoneRadius))
                        {
                            if (DEUBUG_PRINTS)
                                Api.World.Logger.Warning("Spawn Failed for " + this.Code.Path + " at: " + currentSpawnTryPosition + ". Too Close to Player Starting Spawn Area.");

                            //We do this post spawn, so that spawning system doesn't spend additinal frames repeatedly failing and taking up frames for something that will fail 100% of the time.
                            this.Die(EnumDespawnReason.Removed, null);
                            return;
                        }
                    }

                    /////////////////////////////////
                    ///BLOCKED BY LAND CLAIM CHECK///
                    /////////////////////////////////             

                    //Do Not Allow Outlaws to Spawn on Claimed Land. (If Config Says So)
                    if (OMGlobalConstants.claminedLandBlocksOutlawSpawns)
                    {
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
                    }

                    ////////////////////////////////////////
                    ///BLOCKED BY SPAWN BLOCKER POI CHECK///
                    //////////////////////////////////////// 

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

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (World.Side == EnumAppSide.Client && OMGlobalConstants.outlawsUseClassicVintageStoryVoices )
            {
                talkUtil.OnGameTick(dt);
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            switch ( packetid )
            {
                
                case 1001: //hurt

                    if (!Alive) 
                        return;
                        
                    talkUtil.Talk(EnumTalkType.Hurt);

                    break;
                
                case 1002: //Death

                    talkUtil.Talk(EnumTalkType.Death);

                    break;

                case 1003: //Melee Attack, Shoot at Entity

                    if (!Alive)
                        return;

                    talkUtil.Talk(EnumTalkType.Complain);

                    break;

                case 1004: //Flee Entity

                    if (!Alive)
                        return;

                    talkUtil.Talk(EnumTalkType.Hurt2);

                    break;

                case 1005: //Seek Entity

                    if (!Alive)
                        return;

                    talkUtil.Talk(EnumTalkType.Laugh);

                    break;
            }
            
        }

        public override void PlayEntitySound(string type, IPlayer dualCallByPlayer = null, bool randomizePitch = true, float range = 24)
        {
            //If the config says use classic vintage story voices, use instrument voices.
            if ( OMGlobalConstants.outlawsUseClassicVintageStoryVoices )
            {
                if (World.Side == EnumAppSide.Server)
                {
                    switch (type)
                    {
                        case "hurt":
                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1001);

                            return;

                            break;

                        case "death":
                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1002);

                            return;

                            break;

                        case "meleeattack":
                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1003);
                            return;

                            break;

                        case "shootatentity":
                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1003);
                            return;

                            break;

                        case "fleeentity":

                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1004);
                            return;

                            break;

                        case "seekentity":

                            (World.Api as ICoreServerAPI).Network.BroadcastEntityPacket(this.EntityId, 1005);
                            return;

                            break;

                    }
                }
                else if (World.Side == EnumAppSide.Client)
                {
                    //Certain sound events originate on the client.
                    switch (type)
                    {
                        case "idle":

                            talkUtil.Talk(EnumTalkType.Idle);
                            return;

                            break;
                    }
                }
            }
            else
            {
                //Otherwise use Outlaw Mod VO Lines.
                base.PlayEntitySound(type, dualCallByPlayer, randomizePitch, range);
            }            
        }
    }
}