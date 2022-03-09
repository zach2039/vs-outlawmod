
    using System.Diagnostics;
    using System.Text;
    using Vintagestory.API.Common.Entities;
    using Vintagestory.GameContent;

namespace OutlawMod
{    

    public class OutlawEntityBehaviorHarvestable : EntityBehaviorHarvestable
    {
        public OutlawEntityBehaviorHarvestable(Entity entity) : base(entity)
        {

        }

        public override void GetInfoText(StringBuilder infotext)
        {
            //Todo: Figure Out how we can register this.
        }
    }
}

