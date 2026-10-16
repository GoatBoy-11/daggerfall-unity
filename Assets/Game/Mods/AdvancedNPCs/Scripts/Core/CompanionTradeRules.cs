using System;
namespace AdvancedNPCs.Core
{
    public static class CompanionTradeRules
    {
        // Native weights are floats. Tolerance covers float arithmetic at an exact fit, never a whole extra item.
        public static int Maximum(int available, double unitWeight, double carried, double capacity)
        {
            if(available<=0 || !Finite(unitWeight) || !Finite(carried) || !Finite(capacity) || unitWeight<0 || carried<0 || capacity<0) return 0;
            if(unitWeight==0) return available;
            double room=capacity-carried;
            double tolerance=Math.Min(0.00001,unitWeight*0.00001);
            if(room+tolerance<unitWeight) return 0;
            return (int)Math.Min(available,Math.Floor((room+tolerance)/unitWeight));
        }
        static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
