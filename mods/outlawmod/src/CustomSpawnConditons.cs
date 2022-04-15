
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

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

            string type = properties.Code.FirstPathPart();

            //This may be a good location to spawn things that have to spawn in specific locations or on specific block materials.
            switch (type)
            {
                case "looter":
                case "poacher-spear":
                case "poacher-archer":
                case "bandit-axe":
                case "bandit-spear":
                case "bandit-knife":
                case "yeoman-archer":
                    return ShouldSpawnOutlawOfType(ref properties, spawnPosition);
                case "hound-feral":
                case "hound-hunting":
                    return ShouldSpawnHoundOfType(ref properties, spawnPosition);
            }

            return true;
        }

        private bool ShouldSpawnOutlawOfType( ref EntityProperties properties, Vec3d spawnPosition )
        {
            string type = properties.Code.FirstPathPart();     
            
            //Check if the Outlaw is disabled in the config first.
            if ( Utility.OutlawTypeEnabled(type) == false )
            {
                Utility.DebugLogMessage(sapi as ICoreAPI, "Cannot Spawn " + properties.Code.Path + " at: " + spawnPosition + ". This Outlaw type is disabled in the config.");
                return false;
            }

            //Check spawn rules.
            return OutlawSpawnEvaluator.CanSpawn(spawnPosition, properties.Code);
        }

        private bool ShouldSpawnHoundOfType( ref EntityProperties properties, Vec3d spawnPosition)
        {
            string type = properties.Code.FirstPathPart();

            if (Utility.OutlawTypeEnabled(type) == false)
            {
                Utility.DebugLogMessage(sapi as ICoreAPI, "Cannot Spawn " + properties.Code.Path + " at: " + spawnPosition + ". This Hound type is disabled in the config.");
                return false;
            }

            return true;
        }

    }
}
