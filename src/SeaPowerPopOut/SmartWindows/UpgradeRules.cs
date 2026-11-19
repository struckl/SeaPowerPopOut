using System;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>Why a ship cannot be replaced by the selected one (pure rules, no game types).</summary>
internal enum UpgradeBlock
{
    None,
    Points,
    FleetCap,
}

internal static class UpgradeRules
{
    /// <summary>The crew loses one skill level, never below the lowest.</summary>
    public static int NewSkill(int oldSkill) => Math.Max(0, oldSkill - 1);

    /// <summary>
    /// <paramref name="cost"/> and <paramref name="surcharge"/> are the new ship's price, <paramref name="pending"/> what is
    /// already in the cart (nothing, as the feature requires), <paramref name="refund"/> what the old ship is sold for.
    /// The fleet cap (0 = none) is checked as the game does: the fleet value without the old ship plus the new one.
    /// </summary>
    public static UpgradeBlock Check(int available, int pending, int cost, int surcharge, int refund,
        int cap, int fleetValue, int oldShipCap, int newShipCap)
    {
        if (PointsAfter(available, refund, cost, surcharge) - pending < 0)
            return UpgradeBlock.Points;
        if (cap > 0 && FleetAfter(fleetValue, oldShipCap, newShipCap) > cap)
            return UpgradeBlock.FleetCap;
        return UpgradeBlock.None;
    }

    /// <summary>The campaign points left after the old ship is sold and the new one bought.</summary>
    public static int PointsAfter(int available, int refund, int cost, int surcharge) => available + refund - cost - surcharge;

    /// <summary>The fleet value with the new ship in place of the old one.</summary>
    public static int FleetAfter(int fleetValue, int oldShipCap, int newShipCap) => fleetValue - oldShipCap + newShipCap;
}
