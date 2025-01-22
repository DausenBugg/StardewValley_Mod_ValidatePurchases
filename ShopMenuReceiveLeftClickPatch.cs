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
            // Your custom purchase logic here
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

                    if (purchaseAmount >= ModEntry.Instance.Config.MinimumPurchaseAmount)
                    {
                        // Trigger validation dialog for all players
                        ModEntry.Instance.ValidatePurchase(purchaseAmount, salable.DisplayName, Game1.player, salable, countTaken, __instance, actualIndex);
                        return false; // Prevent default purchase behavior
                    }
                }
            }

            return true; // Allow default behavior if no purchase is intercepted
        }
    }
}