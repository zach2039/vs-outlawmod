using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;


namespace OutlawMod
{
    //This is a base class that should be used to derive blocks that block outlaw spawns.
    public class BlockStocks : Block
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, blockSel);
            AssetLocation blockCode = CodeWithVariants(new string[] { "horizontalorientation" }, new string[] { horVer[0].Code });
            Block block = world.BlockAccessor.GetBlock(blockCode);
            if (block == null) return false;

            Block blockBelow = world.BlockAccessor.GetBlock(blockSel.Position.DownCopy());
            bool solidBelow = blockBelow.SideSolid[BlockFacing.UP.Index];

            if (!solidBelow)
                return false;

            world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position);

            return true;
        }
    }
}