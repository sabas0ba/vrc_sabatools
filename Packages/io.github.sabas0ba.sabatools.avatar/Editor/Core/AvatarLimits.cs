// Avatar limits that are just numbers, kept away from the SDK types so they
// can be executed and tested without Unity. See the core package's Editor/Core
// for why the split exists.
namespace SabaTools.Inspect.Avatar
{
    /// <summary>
    /// Published VRChat avatar limits, as of 2026-08. Reference values: VRChat
    /// revises them, and this package's CI does not check them against the live
    /// documentation. See
    /// https://creators.vrchat.com/avatars/expression-menu-and-controls/ and
    /// https://creators.vrchat.com/avatars/animator-parameters/
    /// </summary>
    public static class AvatarLimits
    {
        /// <summary>Total bits an avatar's Expression Parameters may occupy.</summary>
        public const int ExpressionParameterBits = 256;

        /// <summary>Controls one Expression Menu may hold.</summary>
        public const int ControlsPerMenu = 8;

        /// <summary>
        /// Bit cost of one parameter by its VRChat value type name
        /// (<c>Bool</c>, <c>Int</c>, <c>Float</c>). Unknown names cost the
        /// widest of the three, so an SDK addition is over- rather than
        /// under-estimated.
        /// </summary>
        public static int BitsFor(string valueTypeName)
        {
            switch (valueTypeName)
            {
                case "Bool": return 1;
                case "Int": return 8;
                case "Float": return 8;
                default: return 8;
            }
        }

        /// <summary>
        /// How full the parameter budget is, 0..1 and beyond. Over 1 means the
        /// avatar cannot be uploaded.
        /// </summary>
        public static float BudgetFraction(int usedBits)
        {
            return (float)usedBits / ExpressionParameterBits;
        }

        /// <summary>
        /// True when the used bits leave so little room that adding one more
        /// Int or Float would overflow. Warning-worthy but not yet an error.
        /// </summary>
        public static bool IsNearlyFull(int usedBits)
        {
            return usedBits <= ExpressionParameterBits
                && ExpressionParameterBits - usedBits < BitsFor("Int");
        }
    }
}
