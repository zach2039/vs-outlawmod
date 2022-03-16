using Vintagestory.API.Common;


namespace OutlawMod
{
    public class BlockEntityOutlawDeterent : BlockEntity
    {
        //We need to be able to save and load all BlockEntityOutlawDeterents in the world so that we can check for them when outlaws attempt to spawn.
        //We need a living list of these blocks so that we don't have a need to save them at runtime.
        //This requires saving out the list to world save data and rebuilding it on server load.
        
        //Todo: Determine if this block entity class is nessisary or if we can do the aforementioned functionality in the BlockOutlawDeterent.cs file.
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
        }
    }
}