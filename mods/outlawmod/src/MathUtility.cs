
using System;
using Vintagestory.API.MathTools;

namespace OutlawMod
{
    public class MathUtility
    {
        public static double GraphClampedValue( double valueMin, double valueMax, double graphMin, double graphMax, double valueToGraph )
        {
            double clampedValueToGraph = GameMath.Clamp( valueToGraph, valueMin, valueMax );

            double graphValuePercentage = clampedValueToGraph / valueMax;

            double graphDiffrence = graphMax - graphMin;

            double graphPosition = graphDiffrence * graphValuePercentage;

            double graphedValue = graphPosition + graphMin;

            return graphedValue;
        }
    }
}