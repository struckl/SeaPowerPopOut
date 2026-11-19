using SeapowerUI;
using UnityEngine;
using ContextMenu = Noesis.ContextMenu;

namespace SeaPowerPopOut;

/// <summary>
/// The game opens its unit menu (right-click on a unit in the formation manager) from the context menu of a canvas of
/// the main game view, <c>MainGameView.ContextMenuCanvas</c> - so on the main screen, wherever the click was. The
/// pop-out screen gets a copy of that menu, on a canvas in the scaled layer of the windows so it comes out at their size.
/// <para>The Smart Windows unit panel menu opens the same game menu through the same canvas: a right click on the pop-out
/// monitor lends the canvas only until the next frame, so the two never meet.</para>
/// </summary>
internal sealed class UnitMenuCopy
{
    private readonly Noesis.Canvas homeCanvas, menuCanvas;
    private readonly ContextMenu homeMenu, menu;
    private MainGameView lentTo;

    private UnitMenuCopy(Noesis.Canvas homeCanvas, ContextMenu homeMenu, Noesis.Canvas menuCanvas)
    {
        this.homeCanvas = homeCanvas;
        this.homeMenu = homeMenu;
        this.menuCanvas = menuCanvas;
        // Its place is the mouse; the target only makes it part of this screen (scale, see-through cut-out).
        menu = new ContextMenu { ItemContainerStyle = homeMenu.ItemContainerStyle, PlacementTarget = menuCanvas };
        foreach (object key in homeMenu.Resources.Keys)   // the templates of its entries
            menu.Resources[key] = homeMenu.Resources[key];
        // As on the game's menu: right-clicks on it do nothing.
        menu.PreviewMouseRightButtonDown += (sender, args) => args.Handled = true;
        menu.PreviewMouseRightButtonUp += (sender, args) => args.Handled = true;
        menuCanvas.ContextMenu = menu;
    }

    /// <summary>The copy on <paramref name="menuCanvas"/>, or null when the game's menu is not there.</summary>
    public static UnitMenuCopy Build(MainGameView mainView, Noesis.Canvas menuCanvas)
    {
        Noesis.Canvas homeCanvas = mainView.ContextMenuCanvas;
        ContextMenu homeMenu = homeCanvas?.ContextMenu;
        if (homeMenu == null || menuCanvas == null)
        {
            ModLog.Warn("The game's unit menu wasn't found - right-clicking a unit on the pop-out monitor opens it on the main screen.");
            return null;
        }
        return new UnitMenuCopy(homeCanvas, homeMenu, menuCanvas);
    }

    /// <summary>
    /// A right-click on the pop-out screen: until the next frame the main game view's menu canvas is ours, so a unit
    /// menu the click opens opens over here (the game looks the canvas up each time it opens the menu).
    /// </summary>
    public void Lend()
    {
        MainGameView view = MainGameView.Instance;
        if (view == null || !ReferenceEquals(view.ContextMenuCanvas, homeCanvas))
            return;
        view.ContextMenuCanvas = menuCanvas;
        lentTo = view;
    }

    public void Return()
    {
        if (lentTo == null)
            return;
        if (ReferenceEquals(lentTo.ContextMenuCanvas, menuCanvas))
            lentTo.ContextMenuCanvas = homeCanvas;
        lentTo = null;
    }

    /// <summary>
    /// A click reaches only the view of the monitor it was on, so the unit menu open on the other screen would stay
    /// open. Closes it, as a click anywhere beside the menu does on one screen.
    /// </summary>
    public void CloseElsewhere(int screenDisplay)
    {
        if (!(Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)))
            return;
        ContextMenu other = GameMouse.OnDisplay(screenDisplay, out _) ? homeMenu : menu;
        if (other.IsOpen)
            other.IsOpen = false;
    }
}
