using robotManager.Events;
using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using robotManager.Products;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using WholesomeToolbox;
using WholesomeVendors;
using WholesomeVendors.Managers;
using WholesomeVendors.Utils;
using WholesomeVendors.WVSettings;
using WholesomeVendors.WVState;
using wManager;
using wManager.Plugin;
using wManager.Wow.Enums;
using wManager.Wow.ObjectManager;
using Timer = robotManager.Helpful.Timer;

public class Main : IPlugin
{
    public static bool IsLaunched;
    private Timer stateAddTimer;
    public static string version = FileVersionInfo.GetVersionInfo(Others.GetCurrentDirectory + @"\Plugins\Wholesome_Vendors.dll").FileVersion;
    private bool _statesAdded;
    private IVendorTimerManager _vendorTimerManager;
    private IBlackListManager _blackListManager;
    private IPluginCacheManager _pluginCacheManager;
    private IMemoryDBManager _memoryDBManager;

    public void Initialize()
    {
        try
        {
            PluginSettings.Load();
            // 还是不要覆盖默认系统设置，因为单开本插件出现过满了不卖的情况，不知道为啥
            Helpers.OverrideWRobotUserSettings(); 
            WTSettings.AddRecommendedBlacklistZones();
            WTSettings.AddRecommendedOffmeshConnections();
            WTTransport.AddRecommendedTransportsOffmeshes();
            WTSettings.AddItemForceMailList(new List<string>()
            {
                //草药
                "银叶草",
                "宁神花",
                "地根草",
                "魔皇草",
                "雨燕草",
                "石南草",
                "荆棘藻",
                "跌打草",
                "墓地苔",
                "野钢花",
                "皇血草",
                "活根草",
                "枯叶草",
                "金棘草",
                "卡德加的胡须",
                "龙齿草",
                "野葡萄藤",
                "火焰花",
                "紫莲花",
                "阿尔萨斯之泪",
                "太阳草",
                "幽灵菇",
                "盲目草",
                "格罗姆之血",
                "黄金参",
                "梦叶草",
                "山鼠草",
                "哀伤苔",
                "冰盖草",
                "泰罗果",
                "梦露花",
                "魔草",
                "血藤",
                "黑莲花",
                "邪雾草",
                "烈焰菇",
                "远古苔",
                "梦魇草",
                "法力蓟",
                "噩梦藤",
                "虚空花",
                "魔莲花",
                "死亡荨麻",
                "塔兰德拉的玫瑰",
                "卷丹",
                "金苜蓿",
                "蛇信草",
                "冰棘草",
                "巫妖花",
                "雪莲花",
                // 矿类
                "铜矿石",
                "银矿石",
                "锡矿石",
                "金矿石",
                "铁矿石",
                "瑟银矿石",
                "秘银矿石",
                "真银矿石",
                "黑铁矿石",
                "魔铁矿石",
                "精金矿石",
                "恒金矿石",
                "氪金矿石",
                "钴矿石",
                "萨隆邪铁矿石",
                "泰坦神铁矿石",
                "沉重的石头",
                "坚固的石头",
                "粗糙的石头",
                "劣质的石头",
                // 皮革
                "轻皮",
                "中皮",
                "重皮",
                "重毛皮",
                "厚皮",
                "硬甲皮",
                "魔化皮",
                "结缔皮",
                "重结缔皮",
                "北地皮",
                "魔皮",
                "厚北地皮",
                "极地毛皮",
                "冰冷的龙鳞",
                "冰虫鳞片",
                // 布匹
                "亚麻布",
                "毛料",
                "丝绸",
                "符文布",
                "灵纹布",
                "霜纹布"

            });
            // 既不卖 也不邮寄的
            WTSettings.AddItemToDoNotSellAndMailList(new List<string>()
            {
                "炉石",
                "矿工锄",
                "盗贼工具",
                "剥皮小刀",
                "致命药膏",
                "速效药膏",
                "致伤药膏",
                
            
            });

            _vendorTimerManager = new VendorTimerManager();
            _vendorTimerManager.Initialize();
            _blackListManager = new BlackListManager(_vendorTimerManager);
            _blackListManager.Initialize();
            _memoryDBManager = new MemoryDBManager(_blackListManager);
            _memoryDBManager.Initialize();
            _pluginCacheManager = new PluginCacheManager(_memoryDBManager);
            _pluginCacheManager.Initialize();

            /*
            if (AutoUpdater.CheckUpdate(version))
            {
                Logger.Log("New version downloaded, restarting, please wait");
                Helpers.Restart();
                return;
            }
            */

            Logger.Log($"Launching version {version} on client {WTLua.GetWoWVersion}");

            FiniteStateMachineEvents.OnRunState += StateAddEventHandler;

            if (PluginSettings.CurrentSetting.DrinkNbToBuy > 0 || PluginSettings.CurrentSetting.FoodNbToBuy > 0)
            {
                wManagerSetting.CurrentSetting.TryToUseBestBagFoodDrink = true;
                wManagerSetting.CurrentSetting.Save();
            }

            if (PluginSettings.CurrentSetting.FirstLaunch)
            {
                if (ObjectManager.Me.WowClass == WoWClass.Rogue)
                {
                    PluginSettings.CurrentSetting.BuyPoison = true;
                }
                if (ObjectManager.Me.WowClass == WoWClass.Hunter)
                {
                    PluginSettings.CurrentSetting.AmmoAmount = 2000;
                }
                PluginSettings.CurrentSetting.LastLevelTrained = (int)ObjectManager.Me.Level;

                PluginSettings.CurrentSetting.FirstLaunch = false;
                PluginSettings.CurrentSetting.Save();
            }

            IsLaunched = true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Something gone wrong!\n" + ex.Message + "\n" + ex.StackTrace);
        }
    }

    public void Dispose()
    {
        _blackListManager?.Dispose();
        _vendorTimerManager?.Dispose();
        _memoryDBManager?.Dispose();
        _pluginCacheManager?.Dispose();
        IsLaunched = false;
        Helpers.RestoreWRobotUserSettings();
        FiniteStateMachineEvents.OnRunState -= StateAddEventHandler;
        Logger.Log("Disposed");
    }

    public void Settings()
    {
        PluginSettings.Load();
        PluginSettings.CurrentSetting.ShowConfiguration();
        PluginSettings.CurrentSetting.Save();
    }

    private void StateAddEventHandler(Engine engine, State state, CancelEventArgs canc)
    {
        if (_statesAdded)
        {
            Logger.Log($"States added");
            FiniteStateMachineEvents.OnRunState -= StateAddEventHandler;
            return;
        }

        if (engine.States.Count <= 5 && Products.ProductName != "WRotation")
        {
            if (stateAddTimer == null)
            {
                Helpers.SoftRestart(); // hack to wait for correct engine to trigger
            }
            return;
        }

        if (!engine.States.Exists(eng => eng.DisplayName == "To Town"))
        {
            Logger.LogError("The product you're currently using doesn't have a To Town state. Can't start.");
            Dispose();
            return;
        }

        if (stateAddTimer == null)
        {
            stateAddTimer = new Timer();
        }

        if (stateAddTimer.IsReady && engine != null)
        {
            stateAddTimer = new Timer(3000);

            // From bottom to top priority            
            WTState.AddState(engine, new TrainWeaponsState(_memoryDBManager, _pluginCacheManager, _vendorTimerManager, _blackListManager), "To Town");
            WTState.AddState(engine, new BuyPoisonState(_memoryDBManager, _pluginCacheManager, _vendorTimerManager, _blackListManager), "To Town");
            WTState.AddState(engine, new BuyBagsState(_memoryDBManager, _pluginCacheManager, _vendorTimerManager, _blackListManager), "To Town");
            WTState.AddState(engine, new BuyMountState(_memoryDBManager, _pluginCacheManager, _vendorTimerManager, _blackListManager), "To Town");
            WTState.AddState(engine, new TrainingState(_memoryDBManager, _pluginCacheManager, _vendorTimerManager, _blackListManager), "To Town");            
            WTState.AddState(engine, new BuyFoodState(_memoryDBManager, _pluginCacheManager, _vendorTimerManager, _blackListManager), "To Town");
            WTState.AddState(engine, new BuyDrinkState(_memoryDBManager, _pluginCacheManager, _vendorTimerManager, _blackListManager), "To Town");
            WTState.AddState(engine, new BuyAmmoState(_memoryDBManager, _pluginCacheManager, _vendorTimerManager, _blackListManager), "To Town");
            WTState.AddState(engine, new RepairState(_memoryDBManager, _pluginCacheManager, _vendorTimerManager, _blackListManager), "To Town");
            WTState.AddState(engine, new SellState(_memoryDBManager, _pluginCacheManager, _vendorTimerManager, _blackListManager), "To Town");
            WTState.AddState(engine, new SendMailState(_memoryDBManager, _pluginCacheManager, _vendorTimerManager, _blackListManager), "To Town");
            WTState.AddState(engine, new TrainingBusinessState(_memoryDBManager, _pluginCacheManager, _vendorTimerManager, _blackListManager), "To Town");
            WTState.AddState(engine, new ClassQuestState(_memoryDBManager, _pluginCacheManager, _vendorTimerManager, _blackListManager), "To Town");

            engine.States.ForEach(s => Logger.Log($"state {s.DisplayName} with prio {s.Priority}"));

            engine.RemoveStateByName("Trainers");
            _statesAdded = true;
        }
    }
}