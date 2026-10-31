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
        if (pending + cost + surcharge > available + refund)
            return UpgradeBlock.Points;
        if (cap > 0 && fleetValue - oldShipCap + newShipCap > cap)
            return UpgradeBlock.FleetCap;
        return UpgradeBlock.None;
    }
}
