﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Shops;
using StardewValley.Menus;
using StardewValley_Mod_ValidatePurchases;

namespace ValidatePurchases
{
    public class ModEntry : Mod
    {
        public static ModEntry Instance { get; private set; } = null!;
        public ModConfig Config { get; private set; } = null!;
        private int requiredValidations;
        private int currentValidations;
        private readonly List<Farmer> validatedPlayers = new();
        private PendingPurchase? pendingPurchase;
        private System.Timers.Timer? validationTimer;

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            this.Config = helper.ReadConfig<ModConfig>();
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;
            helper.Events.Multiplayer.PeerDisconnected += this.OnPeerDisconnected;
            this.Monitor.Log("Mod entry point initialized.", LogLevel.Info);

            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll();
            this.Monitor.Log("Harmony patch applied.", LogLevel.Info);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.Monitor.Log("Save loaded event triggered.", LogLevel.Info);
            this.Helper.Events.Display.MenuChanged += this.OnMenuChanged;
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (e.NewMenu is ShopMenu shopMenu)
            {
                this.Monitor.Log("Hooked into shop menu purchase event.", LogLevel.Info);
            }
        }

        private void OnPeerDisconnected(object? sender, PeerDisconnectedEventArgs e)
        {
            this.Monitor.Log("Peer disconnected. Updating required validations.", LogLevel.Info);
            UpdateRequiredValidations();
        }

        private void UpdateRequiredValidations()
        {
            int totalPlayers = Game1.getAllFarmers().Count(farmer => farmer.IsMainPlayer || farmer.isActive());
            if (totalPlayers == 2)
            {
                this.requiredValidations = 1; // Special case for 2 players
            }
            else
            {
                this.requiredValidations = (int)Math.Ceiling(totalPlayers / 2.0);
            }
            this.Monitor.Log($"Updated required validations: {requiredValidations}", LogLevel.Info);
        }

        public void ValidatePurchase(int purchaseAmount, string displayName, Farmer who, ISalable salable, int countTaken)
        {
            this.Monitor.Log($"Validating purchase. Item: {displayName}, Amount: {purchaseAmount}, Minimum required: {Config.MinimumPurchaseAmount}", LogLevel.Info);

            // Store purchase details for later use
            this.pendingPurchase = new PendingPurchase
            {
                PurchaseAmount = purchaseAmount,
                DisplayName = displayName,
                Who = who,
                Salable = salable,
                CountTaken = countTaken
            };

            this.Monitor.Log($"Pending purchase set: {this.pendingPurchase.DisplayName}, Amount: {this.pendingPurchase.PurchaseAmount}", LogLevel.Info);

            // Calculate required validations
            UpdateRequiredValidations();

            // Reset current validations
            this.currentValidations = 0;
            this.validatedPlayers.Clear();

            // Send purchase request to all players
            this.Helper.Multiplayer.SendMessage(
                new PurchaseRequestMessage
                {
                    PurchaseAmount = purchaseAmount,
                    DisplayName = displayName
                },
                "ShowValidationDialog",
                new[] { this.ModManifest.UniqueID }
            );

            // Start validation timer
            this.StartValidationTimer();
        }

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.Type == "ShowValidationDialog" && e.FromModID == this.ModManifest.UniqueID)
            {
                this.Monitor.Log("Received ShowValidationDialog message.", LogLevel.Info);
                var message = e.ReadAs<PurchaseRequestMessage>();
                this.ShowValidationDialog(message.PurchaseAmount, message.DisplayName);
            }
            else if (e.Type == "PurchaseResponse" && e.FromModID == this.ModManifest.UniqueID)
            {
                this.Monitor.Log("Received PurchaseResponse message.", LogLevel.Info);
                var message = e.ReadAs<PurchaseResponseMessage>();
                this.HandlePurchaseResponse(message.IsApproved);
            }
        }

        private void ShowValidationDialog(int purchaseAmount, string displayName)
        {
            this.Monitor.Log($"Showing validation dialog for purchase amount: {purchaseAmount}, Item: {displayName}", LogLevel.Info);

            List<Response> choices = new List<Response>
            {
                new Response("Approve", "Approve"),
                new Response("Decline", "Decline")
            };

            Game1.currentLocation.createQuestionDialogue(
                $"A player wants to make a purchase of {purchaseAmount} gold for {displayName}. Do you approve?",
                choices.ToArray(),
                new GameLocation.afterQuestionBehavior(this.OnValidationResponse)
            );
        }

        private void OnValidationResponse(Farmer who, string response)
        {
            this.Monitor.Log($"Validation response received. Response: {response}", LogLevel.Info);

            bool isApproved = response == "Approve";
            this.Helper.Multiplayer.SendMessage(
                new PurchaseResponseMessage { IsApproved = isApproved },
                "PurchaseResponse",
                new[] { this.ModManifest.UniqueID },
                new[] { this.pendingPurchase?.Who.UniqueMultiplayerID ?? 0 }
            );
        }

        private void HandlePurchaseResponse(bool isApproved)
        {
            if (isApproved)
            {
                currentValidations++;
                this.Monitor.Log($"Current validations: {currentValidations}/{requiredValidations}", LogLevel.Info);

                if (currentValidations >= requiredValidations)
                {
                    this.ApprovePurchase();
                }
            }
        }

        private void ApprovePurchase()
        {
            this.Monitor.Log("Purchase approved.", LogLevel.Info);

            if (this.pendingPurchase != null)
            {
                this.pendingPurchase.Who.Money -= this.pendingPurchase.PurchaseAmount;
                this.pendingPurchase.Who.addItemByMenuIfNecessary((Item)this.pendingPurchase.Salable.GetSalableInstance());
                this.pendingPurchase = null;
                Game1.playSound("purchase"); // Play purchase sound effect
                this.Monitor.Log("Purchase Succeeded.", LogLevel.Info);
            }

            this.ResetValidations();
        }

        private void DeclinePurchase()
        {
            this.Monitor.Log("Purchase declined.", LogLevel.Info);
            this.ResetValidations();
        }

        private void ResetValidations()
        {
            this.currentValidations = 0;
            this.requiredValidations = 0;
            this.validatedPlayers.Clear();
            this.StopValidationTimer();
        }

        private void StartValidationTimer()
        {
            this.validationTimer = new System.Timers.Timer(15000); // 15 seconds
            this.validationTimer.Elapsed += OnValidationTimeout;
            this.validationTimer.AutoReset = false;
            this.validationTimer.Start();
        }

        private void StopValidationTimer()
        {
            if (this.validationTimer != null)
            {
                this.validationTimer.Stop();
                this.validationTimer.Dispose();
                this.validationTimer = null;
            }
        }

        private void OnValidationTimeout(object? sender, ElapsedEventArgs e)
        {
            this.Monitor.Log("Validation timeout reached.", LogLevel.Info);
            this.DeclinePurchase();
        }

        private class PendingPurchase
        {
            public int PurchaseAmount { get; set; }
            public string DisplayName { get; set; } = null!;
            public Farmer Who { get; set; } = null!;
            public ISalable Salable { get; set; } = null!;
            public int CountTaken { get; set; }
        }
    }

    internal class PurchaseRequestMessage
    {
        public int PurchaseAmount { get; set; }
        public string DisplayName { get; set; } = null!;
    }

    internal class PurchaseResponseMessage
    {
        public bool IsApproved { get; set; }
    }

    internal class ApprovalMessage
    {
        public int PurchaseAmount { get; set; }
        public string DisplayName { get; set; } = null!;
    }
}