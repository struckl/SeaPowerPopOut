using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SeaPower;
using SeapowerUI.ViewModels;

namespace SeaPowerPopOut.SmartWindows;

using Item = LinearCampaignTaskForceBuilderRosterItem;

/// <summary>One ship of the task force that the selected ship can replace.</summary>
internal sealed class UpgradeCandidate
{
    public Item Item;
    public string Name;
    public bool Decommission;          // sold at the decommission price (heavily damaged) instead of the dismiss price
    public int Refund, RefundBase;     // with / without the value of the loadout that is released
    public int OldSkill, NewSkill;
    public int Cost, Surcharge, Available, FleetValue, Cap, OldCap, NewCap;
    public int Aircraft;               // aircraft that are assigned to the ship (they are moved to the new one)
    public UpgradeBlock Block;
}

/// <summary>
/// The game side of "Upgrade ship": reads the Task Force Builder and its status view model and performs the swap. The
/// builder and the roster item are public; the rules, the refunds and the actions are internal or private, so they are
/// reached by reflection, all resolved once. If one is missing in a game build the whole feature stays off.
/// </summary>
internal static class UpgradeGame
{
    private static bool resolved, ok;
    private static FieldInfo statusField, crewField, creditsField;
    private static PropertyInfo activeItems, historical;
    private static MethodInfo canDismiss, canDecommission, dismissRefund, decommissionRefund, dismissTotal, decommissionTotal;
    private static MethodInfo dismiss, remove, assign;
    private static MethodInfo scarcity, addSelected, confirm, refreshKeys;
    private static MethodInfo countsAgainstCap, fleetCap, loadoutCap, scarcityCap, openIni, minCredits, skillName;

    public static bool Available
    {
        get
        {
            if (!resolved)
                Resolve();
            return ok;
        }
    }

    private static void Resolve()
    {
        resolved = true;
        Type builder = typeof(LinearCampaignTaskForceBuilderViewModel);
        Type status = typeof(LinearCampaignTaskForceStatusViewModel);
        Type rules = AccessTools.TypeByName("SeapowerUI.ViewModels.LinearCampaignTaskForceModeRules");
        Type crew = AccessTools.TypeByName("SeapowerUI.ViewModels.LinearCampaignTaskForceCrewSkillRules");
        if (rules == null || crew == null)
        {
            ModLog.Warn("Upgrade ship: campaign rule classes not found (game updated?) - feature off.");
            return;
        }

        statusField = AccessTools.Field(builder, "_statusViewModel");
        activeItems = AccessTools.Property(status, "ActiveItems");
        historical = AccessTools.Property(status, "UsesHistoricalReinforcements");     // only in newer builds
        canDismiss = AccessTools.Method(status, "CanDismiss", new[] { typeof(Item) });
        canDecommission = AccessTools.Method(status, "CanDecommission", new[] { typeof(Item) });
        dismissRefund = AccessTools.Method(status, "CalculateDismissRefund", new[] { typeof(Item), typeof(int) });
        decommissionRefund = AccessTools.Method(status, "CalculateDecommissionRefund", new[] { typeof(Item) });
        dismissTotal = AccessTools.Method(status, "CalculateDismissRefundTotal", new[] { typeof(Item), typeof(int) });
        decommissionTotal = AccessTools.Method(status, "CalculateDecommissionRefundTotal", new[] { typeof(Item) });
        dismiss = AccessTools.Method(status, "DismissFromTaskForce", new[] { typeof(Item), typeof(int), typeof(int) });
        remove = AccessTools.Method(status, "RemoveFromTaskForce");      // (item, refund, preserve[, decommission])
        assign = AccessTools.Method(status, "AssignAircraft", new[] { typeof(Item), typeof(Item), typeof(bool) });
        scarcity = AccessTools.Method(builder, "GetItemScarcitySurcharge", new[] { typeof(Item) });
        addSelected = AccessTools.Method(builder, "AddSelectedItem", new[] { typeof(bool) });
        confirm = AccessTools.Method(builder, "ConfirmTaskForceSelection", Type.EmptyTypes);
        refreshKeys = AccessTools.Method(builder, "RefreshCurrentTaskForceOwnershipKeys", Type.EmptyTypes);
        countsAgainstCap = AccessTools.Method(rules, "CountsAgainstFleetCap", new[] { typeof(Item) });
        fleetCap = AccessTools.Method(rules, "GetFleetCapValue", new[] { typeof(Item) });
        scarcityCap = AccessTools.Method(rules, "GetScarcitySurchargeCapValue", new[] { typeof(Item) });
        openIni = AccessTools.Method(rules, "OpenCampaignIni", Type.EmptyTypes);
        loadoutCap = openIni == null ? null : AccessTools.Method(rules, "GetAppliedLoadoutCapValue", new[] { openIni.ReturnType, typeof(Item) });
        minCredits = AccessTools.Method(crew, "GetMinimumSurvivalCreditsForSkill", new[] { typeof(ObjectBase.CrewSkill) });
        skillName = AccessTools.Method(crew, "GetDisplayName", new[] { typeof(ObjectBase.CrewSkill) });
        // The crew skill of a bought ship is set after the purchase: the roster item has only a getter.
        crewField = AccessTools.Field(typeof(Item), "<CrewSkill>k__BackingField");
        creditsField = AccessTools.Field(typeof(Item), "<CrewSkillSurvivalCredits>k__BackingField");

        var needed = new object[] { statusField, activeItems, canDismiss, canDecommission, dismissRefund, decommissionRefund,
            dismissTotal, decommissionTotal, dismiss, remove, assign, scarcity, addSelected, confirm, refreshKeys,
            countsAgainstCap, fleetCap, scarcityCap, openIni, loadoutCap, minCredits, skillName, crewField, creditsField };
        if (needed.Any(m => m == null))
        {
            ModLog.Warn("Upgrade ship: a game member was not found (game updated?) - feature off.");
            return;
        }
        ok = true;
    }

    // ---- reading ------------------------------------------------------------------------------------------------

    public static LinearCampaignTaskForceStatusViewModel StatusOf(LinearCampaignTaskForceBuilderViewModel vm) =>
        (LinearCampaignTaskForceStatusViewModel)statusField.GetValue(vm);

    public static bool UsesHistoricalReinforcements(LinearCampaignTaskForceStatusViewModel status) =>
        historical != null && (bool)historical.GetValue(status);

    public static string SkillName(int skill) => (string)skillName.Invoke(null, new object[] { (ObjectBase.CrewSkill)skill });

    private static List<Item> ItemsOf(LinearCampaignTaskForceStatusViewModel status) =>
        ((IEnumerable<Item>)activeItems.GetValue(status)).Where(i => i != null).ToList();

    /// <summary>Why the button is off, or null when there is something to choose from.</summary>
    public static string Unavailable(LinearCampaignTaskForceBuilderViewModel vm, out List<UpgradeCandidate> candidates)
    {
        candidates = new List<UpgradeCandidate>();
        Item selected = vm.SelectedRosterItem;
        if (selected == null || selected.IsAirUnit)
            return "up_nosel";
        LinearCampaignTaskForceStatusViewModel status = StatusOf(vm);
        if (status == null)
            return "up_nostatus";
        if (UsesHistoricalReinforcements(status))
            return "up_hist";
        if (vm.PendingPurchaseItems.Count > 0 || vm.HasPendingTransactions)
            return "up_pending";
        candidates = Candidates(vm, status, selected);
        if (candidates.Count == 0)
            return "up_none";
        return candidates.Any(c => c.Block == UpgradeBlock.None) ? null : BlockKey(candidates[0].Block);
    }

    public static string BlockKey(UpgradeBlock block) => block == UpgradeBlock.Points ? "up_points" : block == UpgradeBlock.FleetCap ? "up_cap" : null;

    private static List<UpgradeCandidate> Candidates(LinearCampaignTaskForceBuilderViewModel vm,
        LinearCampaignTaskForceStatusViewModel status, Item selected)
    {
        var list = new List<UpgradeCandidate>();
        List<Item> items = ItemsOf(status);
        // The game refuses a variant that is already in the task force; the old ship would be gone for nothing.
        if (items.Any(i => !i.IsAirUnit && SameVariant(i, selected)))
            return list;

        int surcharge = (int)scarcity.Invoke(null, new object[] { selected });
        object ini = openIni.Invoke(null, null);
        int newCap = 0;
        if ((bool)countsAgainstCap.Invoke(null, new object[] { selected }))
        {
            newCap = (int)fleetCap.Invoke(null, new object[] { selected })
                + (int)loadoutCap.Invoke(null, new[] { ini, selected })
                + (int)scarcityCap.Invoke(null, new object[] { selected });
        }

        foreach (Item ship in items)
        {
            if (ship.IsAirUnit || !string.Equals(ship.Category, selected.Category, StringComparison.OrdinalIgnoreCase))
                continue;
            bool sold = (bool)canDismiss.Invoke(status, new object[] { ship });
            bool decommission = !sold && (bool)canDecommission.Invoke(status, new object[] { ship });
            if (!sold && !decommission)
                continue;

            int oldCap = (bool)countsAgainstCap.Invoke(null, new object[] { ship }) ? (int)fleetCap.Invoke(null, new object[] { ship }) : 0;
            int refundBase = decommission
                ? (int)decommissionRefund.Invoke(status, new object[] { ship })
                : (int)dismissRefund.Invoke(status, new object[] { ship, 1 });
            int refund = decommission
                ? (int)decommissionTotal.Invoke(status, new object[] { ship })
                : (int)dismissTotal.Invoke(status, new object[] { ship, 1 });
            int old = (int)ship.CrewSkill;
            var candidate = new UpgradeCandidate
            {
                Item = ship,
                Name = ship.InspectionTitle ?? ship.UnitType,
                Decommission = decommission,
                RefundBase = refundBase,
                Refund = refund,
                OldSkill = old,
                NewSkill = UpgradeRules.NewSkill(old),
                Cost = selected.Cost,
                Surcharge = surcharge,
                Available = vm.AvailablePoints,
                FleetValue = vm.ProjectedFleetValue,
                Cap = vm.PointCap,
                OldCap = oldCap,
                NewCap = newCap,
                Aircraft = AssignedAircraft(items, ship),
            };
            candidate.Block = UpgradeRules.Check(candidate.Available, vm.PendingPurchaseCost, candidate.Cost, surcharge,
                refundBase, candidate.Cap, candidate.FleetValue, oldCap, newCap);
            list.Add(candidate);
        }
        return list;
    }

    private static bool SameVariant(Item a, Item b) =>
        string.Equals(a.UnitType, b.UnitType, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.VariantReference, b.VariantReference, StringComparison.OrdinalIgnoreCase);

    private static int AssignedAircraft(List<Item> items, Item ship)
    {
        int count = 0;
        foreach (Item air in items.Where(i => i.IsAirUnit))
        {
            if (air.AircraftAssignments.TryGetValue(ship.InstanceId ?? "", out int n))
                count += n;
        }
        return count;
    }

    // ---- doing --------------------------------------------------------------------------------------------------

    /// <summary>
    /// Sells the old ship, buys the selected one with the lowered crew skill and confirms the purchase. Returns a text key
    /// for a problem the player should know about (the old ship is sold already then), or null.
    /// </summary>
    public static string Perform(LinearCampaignTaskForceBuilderViewModel vm, UpgradeCandidate c)
    {
        LinearCampaignTaskForceStatusViewModel status = StatusOf(vm);
        Item selected = vm.SelectedRosterItem;
        if (status == null || selected == null || c.Block != UpgradeBlock.None)
            return "up_failed";
        string oldId = c.Item.InstanceId ?? "";

        // The aircraft of the old ship, to put them on the new one.
        var moved = new List<KeyValuePair<Item, int>>();
        foreach (Item air in ItemsOf(status).Where(i => i.IsAirUnit))
        {
            if (air.AircraftAssignments.TryGetValue(oldId, out int n) && n > 0)
                moved.Add(new KeyValuePair<Item, int>(air, n));
        }

        ModLog.Info($"Upgrade ship: {c.Name} ({SkillName(c.OldSkill)}, {(c.Decommission ? "decommission" : "dismiss")} +{c.RefundBase}) -> {selected.InspectionTitle}");
        if (c.Decommission)
        {
            object[] args = { c.Item, c.RefundBase, false, true };
            Call(remove, status, args.Take(remove.GetParameters().Length).ToArray());
        }
        else
        {
            Call(dismiss, status, new object[] { c.Item, c.RefundBase, 1 });
        }
        Call(refreshKeys, vm);

        int before = vm.PendingPurchaseItems.Count;
        Call(addSelected, vm, false);
        if (vm.PendingPurchaseItems.Count <= before)
        {
            ModLog.Warn("Upgrade ship: the Builder did not take the new ship (the old one is sold, its points are back).");
            return "up_notbought";
        }

        string newId = null;
        var lowered = (ObjectBase.CrewSkill)c.NewSkill;
        foreach (Item bought in vm.PendingPurchaseItems.Skip(before).Where(i => !i.IsAirUnit))
        {
            newId = bought.InstanceId;
            crewField.SetValue(bought, lowered);
            creditsField.SetValue(bought, minCredits.Invoke(null, new object[] { lowered }));
        }
        Call(confirm, vm);
        string problem = null;
        if (vm.PendingPurchaseItems.Count > 0)
        {
            ModLog.Warn($"Upgrade ship: the purchase was not confirmed ({vm.PendingPurchaseItems.Count} item(s) wait in the Builder).");
            problem = "up_notconfirmed";
        }
        if (newId != null)
            MoveAircraft(status, moved, newId);
        return problem;
    }

    private static void MoveAircraft(LinearCampaignTaskForceStatusViewModel status, List<KeyValuePair<Item, int>> moved, string shipId)
    {
        try
        {
            int done = 0;
            foreach (KeyValuePair<Item, int> entry in moved)
            {
                for (int i = 0; i < entry.Value; i++)
                {
                    List<Item> items = ItemsOf(status);
                    Item ship = items.FirstOrDefault(s => !s.IsAirUnit && string.Equals(s.InstanceId, shipId, StringComparison.OrdinalIgnoreCase));
                    Item air = items.FirstOrDefault(a => a.IsAirUnit && SameVariant(a, entry.Key));
                    if (ship == null || air == null)
                        return;
                    air.AircraftAssignments.TryGetValue(shipId, out int was);
                    Call(assign, status, air, ship, false);
                    air.AircraftAssignments.TryGetValue(shipId, out int now);
                    if (now <= was)
                        return;                  // no room on the new ship
                    done++;
                }
            }
            ModLog.Info($"Upgrade ship: {done} aircraft moved to the new ship");
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Upgrade ship (aircraft): {ex.Message}");
        }
    }

    private static object Call(MethodInfo method, object target, params object[] args)
    {
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException ?? ex;
        }
    }
}
