using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.ServerMods;

namespace OutlawMod
{

    public class CustomSpawnConditions : ModSystem
    {
        ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.sapi = api;

            sapi.Event.OnTrySpawnEntity += Event_OnTrySpawnEntity;
        }

        private bool Event_OnTrySpawnEntity(ref EntityProperties properties, Vec3d spawnPosition, long herdId)
        {

            //This may be a good location to spawn things that have to spawn in specific locations or on specific block materials.
            if (properties.Code.Path.StartsWithFast("bandit"))
            {                
                return ShouldSpawnBandit( ref properties, spawnPosition, herdId );
            }     
            else if (properties.Code.Path.StartsWithFast("looter"))
            {                
                return ShouldSpawnLooter(ref properties, spawnPosition, herdId);
            }
            else if (properties.Code.Path.StartsWithFast("yeoman"))
            {                
                return ShouldSpawnYeoman(ref properties, spawnPosition, herdId);
            }
            else if (properties.Code.Path.StartsWithFast("poacher"))
            {                
                return ShouldSpawnPoacher(ref properties, spawnPosition, herdId);
            }



            return true;
        }

        private bool ShouldSpawnBandit(ref EntityProperties properties, Vec3d spawnPosition, long herdId )
        {
            return true;
        }

        private bool ShouldSpawnLooter(ref EntityProperties properties, Vec3d spawnPosition, long herdId )
        {
            return true;
        }

        private bool ShouldSpawnYeoman(ref EntityProperties properties, Vec3d spawnPosition, long herdId)
        {
            return true;
        }

        private bool ShouldSpawnPoacher(ref EntityProperties properties, Vec3d spawnPosition, long herdId)
        {
            return true;
        }
    }
}
