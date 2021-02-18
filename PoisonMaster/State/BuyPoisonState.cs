﻿using PoisonMaster;
using robotManager.FiniteStateMachine;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using wManager.Wow.Bot.Tasks;
using wManager.Wow.Enums;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Timer = robotManager.Helpful.Timer;

public class BuyPoisonState : State
{
    public override string DisplayName => "Buying Poison";

    private WoWLocalPlayer Me = ObjectManager.Me;
    private uint InstantPoison;
    private uint DeadlyPoison;
    private Timer stateTimer = new Timer();
    private DatabaseNPC poisonVendor;


    private readonly Dictionary<int, uint> InstantPoisonDictionary = new Dictionary<int, uint>
    {
        { 79, 43231 },
        { 73, 43230 },
        { 68, 21927 },
        { 60, 8928 },
        { 52, 8927 },
        { 44, 8926 },
        { 36, 6950 },
        { 28, 6949 },
        { 20, 6947 }
    };

    private readonly Dictionary<int, uint> DeadlyPoisonDictionary = new Dictionary<int, uint>
    {
        { 80, 43233 },
        { 76, 43232 },
        { 70, 22054 },
        { 62, 22053 },
        { 60, 20844 },
        { 54, 8985 },
        { 46, 8984 },
        { 38, 2893 },
        { 30, 2892 }
    };

    public override bool NeedToRun
    {
        get
        {
            if (!stateTimer.IsReady
                || !PluginSettings.CurrentSetting.AllowAutobuyPoison
                || ObjectManager.Me.WowClass != WoWClass.Rogue
                || Helpers.GetMoney < 1000
                || ObjectManager.Me.Level < 20)
                return false;

            stateTimer = new Timer(5000);
            SetPoisonToBuy();

            if (ItemsManager.GetItemCountById(InstantPoison) <= 0
                || ObjectManager.Me.Level >= 30 && ItemsManager.GetItemCountById(DeadlyPoison) <= 0)
            {
                poisonVendor = Database.GetPoisonVendor();
                if (poisonVendor == null)
                {
                    Main.Logger("Couldn't find poison vendor");
                    return false;
                }
                return true;
            }
            return false;
        }
    }

    public override void Run()
    {
        Main.Logger("Nearest Vendor from player:\n" + "Name: " + poisonVendor.Name + "[" + poisonVendor.Id + "]\nPosition: " + poisonVendor.Position.ToStringXml() + "\nDistance: " + poisonVendor.Position.DistanceTo(Me.Position) + " yrds");
        int nbInstantPoisonToBuy = 20 - ItemsManager.GetItemCountById(InstantPoison);
        int nbDeadlyPoisonToBuy = 20 - ItemsManager.GetItemCountById(DeadlyPoison);

        if (Me.Position.DistanceTo(poisonVendor.Position) >= 6)
            GoToTask.ToPosition(poisonVendor.Position);

        if (Helpers.NpcIsAbsentOrDead(poisonVendor))
            return;

        // INSTANT POISON
        if (nbInstantPoisonToBuy > 0)
        {
            for (int i = 0; i <= 5; i++)
            {
                GoToTask.ToPositionAndIntecractWithNpc(poisonVendor.Position, poisonVendor.Id, i);
                Helpers.BuyItem(ItemsManager.GetNameById(InstantPoison), nbInstantPoisonToBuy);
                Helpers.AddItemToDoNotSellList(ItemsManager.GetNameById(InstantPoison));
                Helpers.CloseWindow();
                Thread.Sleep(1000);
                if (ItemsManager.GetItemCountById(InstantPoison) >= 20)
                    break;
            }
            Main.Logger($"Failed to buy {InstantPoison}, blacklisting vendor");
            NPCBlackList.AddNPCToBlacklist(poisonVendor.Id);
        }

        // DEADLY POISON
        if (Me.Level >= 30 && nbDeadlyPoisonToBuy > 0)
        {
            for (int i = 0; i <= 5; i++)
            {
                GoToTask.ToPositionAndIntecractWithNpc(poisonVendor.Position, poisonVendor.Id, i);
                Helpers.BuyItem(ItemsManager.GetNameById(DeadlyPoison), 20);
                Helpers.AddItemToDoNotSellList(ItemsManager.GetNameById(DeadlyPoison));
                Helpers.CloseWindow();
                Thread.Sleep(1000);
                if (ItemsManager.GetItemCountById(DeadlyPoison) >= 20)
                    break;
            }
            Main.Logger($"Failed to buy {DeadlyPoison}, blacklisting vendor");
            NPCBlackList.AddNPCToBlacklist(poisonVendor.Id);
        }
    }

    private void SetPoisonToBuy()
    {
        foreach (KeyValuePair<int, uint> instantPoison in InstantPoisonDictionary)
        {
            if (instantPoison.Key <= Me.Level)
            {
                InstantPoison = instantPoison.Value;
                Helpers.AddItemToDoNotSellList(ItemsManager.GetNameById(instantPoison.Value));
                break;
            }
        }

        foreach (KeyValuePair<int, uint> deadlyPoison in DeadlyPoisonDictionary)
        {
            if (deadlyPoison.Key <= Me.Level)
            {
                DeadlyPoison = deadlyPoison.Value;
                Helpers.AddItemToDoNotSellList(ItemsManager.GetNameById(deadlyPoison.Value));
                break;
            }
        }
    }
}