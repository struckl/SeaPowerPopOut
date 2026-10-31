using System;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>The rules for dismissing a damaged ship in the campaign. No game types.</summary>
internal static class DismissRules
{
    /// <summary>
    /// A ship the game would not let go may be dismissed when it is a ship or submarine (not an air unit, not stationed
    /// at a campaign airbase), the campaign does not use historical reinforcements, it is not destroyed, and it can be
    /// repaired.
    /// </summary>
    public static bool MayDismissDamaged(bool isAirUnit, bool atAirbase, bool historicalReinforcements, bool destroyed, bool repairable) =>
        !isAirUnit && !atAirbase && !historicalReinforcements && !destroyed && repairable;

    /// <summary>A repairable ship is only worth what it will be after repair: the refund is reduced by the repair cost.</summary>
    public static int RefundAfterRepair(int refund, int repairCost) =>
        refund <= 0 || repairCost <= 0 ? refund : Math.Max(0, refund - repairCost);

    /// <summary>The ship has damage that can be repaired: facilities exist, the damage kind is allowed, and it has a state to repair.</summary>
    public static bool IsRepairable(bool facilitiesAvailable, bool allowedDamage, bool disallowedDamage, bool hasStateSection) =>
        facilitiesAvailable && allowedDamage && !disallowedDamage && hasStateSection;
}
