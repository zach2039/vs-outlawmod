
using System;
using Vintagestory.API.MathTools;

namespace ExpandedAiTasks
{
    public class MathUtility
    {
        public static double GraphClampedValue( double inputStart, double inputEnd, double outputStart, double outputEnd, double inputValue )
        {
            double clampedInputValue = GameMath.Clamp(inputValue, inputStart, inputEnd);
            double slope = 1.0 * (outputEnd - outputStart) / (inputEnd - inputStart);
            double outputValue = outputStart + slope * (clampedInputValue - inputStart);
            return outputValue;
        }
    }
}