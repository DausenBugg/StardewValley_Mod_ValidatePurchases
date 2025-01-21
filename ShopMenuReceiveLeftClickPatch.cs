using HarmonyLib;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.GameData.Shops;
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
                    ItemStockInformation stock = __instance.itemPriceAndStock[salable];

                    int purchaseAmount = stock.Price * countTaken;
                    ModEntry.Instance.Monitor.Log($"Intercepted purchase event. Item: {salable.DisplayName}, Amount: {purchaseAmount}", LogLevel.Info);

                    if (purchaseAmount >= ModEntry.Instance.Config.MinimumPurchaseAmount)
                    {
                        // Trigger validation dialog for all players
                        ModEntry.Instance.ValidatePurchase(purchaseAmount, salable.DisplayName, Game1.player, salable, countTaken);
                        return false; // Prevent default purchase behavior
                    }
                    else
                    {
                        // Deduct money and add the item to the player's inventory
                        if (Game1.player.Money >= purchaseAmount)
                        {
                            Game1.player.Money -= purchaseAmount;
                            ModEntry.Instance.Monitor.Log($"Deducted {purchaseAmount} from player. Adding item to inventory.", LogLevel.Info);
                            Game1.player.addItemByMenuIfNecessary((Item)salable.GetSalableInstance());
                            Game1.playSound("purchase"); // Play purchase sound effect
                        }
                        else
                        {
                            ModEntry.Instance.Monitor.Log("Player does not have enough money to complete the purchase.", LogLevel.Info);
                        }
                        return false; // Prevent default purchase behavior
                    }
                }
            }

            return true; // Allow default behavior if no purchase is intercepted
        }
    }
}