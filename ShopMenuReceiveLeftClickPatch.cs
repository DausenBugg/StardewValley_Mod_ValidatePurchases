using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using StardewModdingAPI;

namespace ValidatePurchases
{
    [HarmonyPatch(typeof(ShopMenu), "receiveLeftClick")]
    public static class ShopMenuReceiveLeftClickPatch
    {
        public static bool Prefix(int x, int y, bool playSound, ShopMenu __instance)
        {
            foreach (ClickableComponent clickableComponent in __instance.forSaleButtons)
            {
                if (clickableComponent.containsPoint(x, y))
                {
                    int index = __instance.forSaleButtons.IndexOf(clickableComponent);
                    int actualIndex = __instance.currentItemIndex + index; // Calculate the correct item index
                    ISalable salable = __instance.forSale[actualIndex];
                    int countTaken = 1; // Assuming 1 item is taken, adjust as needed
                    int purchaseAmount = __instance.itemPriceAndStock[salable].Price * countTaken;

                    ModEntry.Instance.Monitor.Log($">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>", LogLevel.Info);
                    ModEntry.Instance.Monitor.Log($"Intercepted purchase event. Item: {salable.DisplayName}, Amount: {purchaseAmount}", LogLevel.Info);

                    if (ModEntry.Instance.Config.MaximumPurchaseAmount == -1)
                    {
                        return true;
                    }

                    if (ModEntry.Instance.PurchaseApproved)
                    {
                        ModEntry.Instance.PurchaseApproved = false;
                        return true;
                    }

                    if (purchaseAmount >= ModEntry.Instance.Config.MaximumPurchaseAmount)
                    {
                        // Store the state before closing the shop
                        ShopMenuState.StoreState(__instance, actualIndex, x, y);

                        // Trigger validation dialog for all players
                        ModEntry.Instance.ValidatePurchase(purchaseAmount, salable.DisplayName, Game1.player, salable, countTaken, __instance, actualIndex);
                        return false; // Prevent default purchase behavior
                    }
                }
            }

            return true; // Allow default behavior if no purchase is intercepted
        }

        public static class ShopMenuState
        {
            public static int CurrentItemIndex { get; set; }
            public static int ClickedItemIndex { get; set; }
            public static int ClickX { get; set; }
            public static int ClickY { get; set; }
            public static ShopMenu? LastShopMenu { get; set; }

            public static void StoreState(ShopMenu shopMenu, int clickedItemIndex, int x, int y)
            {
                CurrentItemIndex = shopMenu.currentItemIndex;
                ClickedItemIndex = clickedItemIndex;
                ClickX = x;
                ClickY = y;
                LastShopMenu = shopMenu;
            }
        }
    }
}