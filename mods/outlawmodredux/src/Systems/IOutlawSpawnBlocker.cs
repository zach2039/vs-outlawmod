using System.Collections.Generic;
using Vintagestory.GameContent;

namespace OutlawModRedux
{
    public interface IOutlawSpawnBlocker : IPointOfInterest
    {
        float blockingRange();
    }
}