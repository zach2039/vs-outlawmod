using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace OutlawMod
{
    public class EntityHound : EntityAgent
    {
        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            if (Api.Side == EnumAppSide.Server)
            {
                //Check if the Outlaw is disabled in the config.
                if (!Utility.OutlawTypeEnabled(this.Code.FirstPathPart()))
                {
                    Utility.DebugLogToPlayerChat(Api as ICoreServerAPI, "Cannot Spawn " + this.Code.Path + " at: " + this.Pos + ". This Hound type is disabled in the config.");
                    this.Die(EnumDespawnReason.Removed, null);

                    return;
                }
            }
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();

            if (!Utility.OutlawTypeEnabled(this.Code.FirstPathPart()) && Api.Side == EnumAppSide.Server)
            {
                Utility.DebugLogToPlayerChat(Api as ICoreServerAPI, "Attempted to load " + this.Code.Path + " at: " + this.Pos + ". This Hound type is disabled in the config, removing.");
                this.Die(EnumDespawnReason.Removed, null);

                return;
            }
        }
    }
}