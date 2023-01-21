using System.Collections.Generic;
using Vintagestory.GameContent;

namespace OutlawMod
{
    public interface IOutlawSpawnBlocker : IPointOfInterest
    {
        float blockingRange();
    }
}