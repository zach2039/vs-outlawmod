using System;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using ExpandedAiTasks;

namespace OutlawMod
{
    public class EntityOutlawPoacher : EntityOutlaw
    {

        const int HOUND_COMPANIONS_MIN = 1;
        const int HOUND_COMPANIONS_MAX = 3;
        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
        
            //If we get despawned by debug stuff, don't spawn out hounds.
            if ( !this.Alive )
                return;

            int houndToSpawn = this.World.Rand.Next(HOUND_COMPANIONS_MIN, HOUND_COMPANIONS_MAX);

            for( int i = 0; i < houndToSpawn; i++ )
            {
                AssetLocation code = new AssetLocation("hound-hunting");
                EntityProperties houndProperties = this.World.GetEntityType(code);

                Debug.Assert(houndProperties != null, "Hound Properties are null");

                Entity houndEnt = this.World.ClassRegistry.CreateEntity(houndProperties);
                houndEnt.ServerPos.SetFrom(this.ServerPos);
                houndEnt.Pos.SetFrom(houndEnt.ServerPos);

                if (houndEnt is EntityAgent)
                {
                    EntityAgent houndAgent = (EntityAgent)houndEnt;
                    houndAgent.HerdId = this.HerdId;

                    AiUtility.SetGuardedEntity(houndAgent, this);        
                }

                this.World.SpawnEntity(houndEnt);
            }
        }
    }
}
