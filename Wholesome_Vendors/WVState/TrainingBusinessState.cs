using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using System;
using System.Collections.Generic;
using System.Threading;
using WholesomeToolbox;
using WholesomeVendors.Database.Models;
using WholesomeVendors.Managers;
using WholesomeVendors.Utils;
using WholesomeVendors.WVSettings;
using wManager.Wow.Bot.Tasks;
using wManager.Wow.Enums;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;



namespace WholesomeVendors.WVState
{
    public class TrainingBusinessState : State
    {
        public override string DisplayName { get; set; } = "商业技能 Training";

        private readonly IPluginCacheManager _pluginCacheManager;
        private readonly IMemoryDBManager _memoryDBManager;
        private readonly IVendorTimerManager _vendorTimerManager;
        private readonly IBlackListManager _blackListManager;

        private ModelCreatureTemplate _trainerNpc;
        private bool _enabledInSetting;
        private Object obj = new object();
              

        private string subname = null;
        private bool IsNeedToTrain()
        {
            lock(obj)
            {
                // 任意时刻 钱不够2个银币就不去学了（剥皮小刀77铜币，矿工锄76铜币，1级技能9个铜币 x2）
                if(ObjectManager.Me.GetMoneyCopper <= 200)
                { 
                    return false;
                }
                //矿工没有矿工锄
                if (SpellManager.KnowSpell("Find Minerals") && ItemsManager.GetItemCountByIdLUA(2901) < 1)
                {
                    Logger.Log("矿工没有矿工锄，必须购买");
                    subname = "采矿供应商";
                    return true;
                }
                //ItemsManager.GetItemCountByIdLUA(7005) < 1)
                // 剥皮没有剥皮刀
                if (SpellManager.KnowSpell("Skinning") && ItemsManager.GetItemCountByIdLUA(7005) < 1)
                {
                    Logger.Log("剥皮没有剥皮刀，必须购买");
                    subname = "制皮供应商";
                    return true;
                }

                // 需要但是还没习得该技能
                if ((PluginSettings.CurrentSetting.AllowTrainBusinessOfHerbs && !SpellManager.KnowSpell("Find Herbs") )
                    ||
                    (SpellManager.KnowSpell("Find Herbs") && (Skill.GetMaxValue(SkillLine.Herbalism) <= 25 + Skill.GetValue(SkillLine.Herbalism)))
                    )
                {
                    Logging.Write("需要学习草药");
                    subname = "草药学训练师";
                    return true;
                }
                // 需要但是还没习得该技能
                if ((PluginSettings.CurrentSetting.AllowTrainBusinessOfMining && !SpellManager.KnowSpell("Find Minerals"))
                    ||
                    (SpellManager.KnowSpell("Find Minerals") && (Skill.GetMaxValue(SkillLine.Mining) <= 25 + Skill.GetValue(SkillLine.Mining)))
                    )
                {
                    Logging.Write("需要学习采矿");
                    subname = "采矿训练师";
                    return true;
                }

                // 需要但是还没习得该技能
                if ((PluginSettings.CurrentSetting.AllowTrainBusinessOfSkin && !SpellManager.KnowSpell("Skinning"))
                    || (SpellManager.KnowSpell("Skinning") && (Skill.GetMaxValue(SkillLine.Skinning) <= 25 + Skill.GetValue(SkillLine.Skinning)))
                    )
                {
                    Logging.Write("需要学习剥皮");
                    subname = "剥皮训练师";
                    return true;
                }

                // 习得该技能需要学习下一等级
                // 特别注意的是这里采矿的技能是熔炼...或者Find Minerals或者Smelting
                //if (SpellManager.KnowSpell("Find Minerals") && (Skill.GetMaxValue(SkillLine.Mining) <= 25 + Skill.GetValue(SkillLine.Mining)))
                //{
                //    Logging.Write("需要训练采矿");
                //    subname = "采矿训练师";
                //    return true;
                //}

                // 习得该技能需要学习下一等级
                //if (SpellManager.KnowSpell("Find Herbs") && (Skill.GetMaxValue(SkillLine.Herbalism) <= 25 + Skill.GetValue(SkillLine.Herbalism)))
                //{
                //    Logging.Write("需要训练草药");
                //    subname = "草药学训练师";
                //    return true;
                //}

                // 习得该技能需要学习下一等级
                //if (SpellManager.KnowSpell("Skinning") && (Skill.GetMaxValue(SkillLine.Skinning) <= 25 + Skill.GetValue(SkillLine.Skinning)))
                //{
                //    Logging.Write("需要训练剥皮");
                //    subname = "剥皮训练师";
                //    return true;
                //}

                //Logging.Write("插件检测发现【不】需要学习商业技能");
                return false;

            }
            // 反复刷新技能和不断保存有性能问题吧？
            // 但是确实存在技能缓存的问题
            // 以及插件配置了 关闭后就被复原的情况
            //SpellManager.UpdateSpellBook();  
            //PluginSettings.CurrentSetting.Save();           
            
        }
        
        /*
        private bool IsNeedToTrain()
        {
            return _skills.Where(kvp => SpellManager.KnowSpell(kvp.Key) && (Skill.GetMaxValue(kvp.Value) == Skill.GetValue(kvp.Value))).Count() > 0;            
        }
        */



        public TrainingBusinessState(
            IMemoryDBManager memoryDBManager,
            IPluginCacheManager pluginCacheManager,
            IVendorTimerManager vendorTimerManager,
            IBlackListManager blackListManager)
        {
            _memoryDBManager = memoryDBManager;
            _pluginCacheManager = pluginCacheManager;
            _vendorTimerManager = vendorTimerManager;
            _blackListManager = blackListManager;
            _enabledInSetting = PluginSettings.CurrentSetting.AllowTrainBusiness;
        }

        public override bool NeedToRun
        {
            get
            {
                if (!Main.IsLaunched
                    || !_enabledInSetting
                    || !IsNeedToTrain()
                    || _pluginCacheManager.InLoadingScreen
                    || !_pluginCacheManager.BagsRecorded
                    || Fight.InFight
                    || !PluginSettings.CurrentSetting.AllowTrain
                    || ObjectManager.Me.IsOnTaxi
                    || _pluginCacheManager.IsInInstance
                    || _pluginCacheManager.IsInOutlands
                    || (ContinentId)Usefuls.ContinentId == ContinentId.Northrend
                    || (ContinentId)Usefuls.ContinentId == ContinentId.Azeroth && ObjectManager.Me.WowClass == WoWClass.Druid
                    || !Conditions.InGameAndConnectedAndAliveAndProductStartedNotInPause)
                {
                    /*
                    Logger.LogError(string.Format("!Main.IsLaunched = {0}", !Main.IsLaunched));
                    Logger.LogError(string.Format("!_enabledInSetting = {0}", !_enabledInSetting));                    
                    Logger.LogError(string.Format("!IsNeedToTrain() = {0}", !IsNeedToTrain()));
                    Logger.LogError(string.Format("_pluginCacheManager.InLoadingScreen = {0}", _pluginCacheManager.InLoadingScreen));
                    Logger.LogError(string.Format("!_pluginCacheManager.BagsRecorded = {0}", !_pluginCacheManager.BagsRecorded));
                    Logger.LogError(string.Format("Fight.InFight = {0}", Fight.InFight));
                    Logger.LogError(string.Format("!PluginSettings.CurrentSetting.AllowTrain = {0}", !PluginSettings.CurrentSetting.AllowTrain));
                    Logger.LogError(string.Format("ObjectManager.Me.IsOnTaxi = {0}", ObjectManager.Me.IsOnTaxi));
                    Logger.LogError(string.Format("_pluginCacheManager.IsInInstance = {0}", _pluginCacheManager.IsInInstance));
                    Logger.LogError(string.Format("_pluginCacheManager.IsInOutlands = {0}", _pluginCacheManager.IsInOutlands));
                    Logger.LogError(string.Format("(ContinentId)Usefuls.ContinentId == ContinentId.Northrend = {0}", (ContinentId)Usefuls.ContinentId == ContinentId.Northrend));
                    Logger.LogError(string.Format("(ContinentId)Usefuls.ContinentId == ContinentId.Azeroth && ObjectManager.Me.WowClass == WoWClass.Druid = {0}", (ContinentId)Usefuls.ContinentId == ContinentId.Azeroth && ObjectManager.Me.WowClass == WoWClass.Druid));
                    Logger.LogError(string.Format("!Conditions.InGameAndConnectedAndAliveAndProductStartedNotInPause = {0}", !Conditions.InGameAndConnectedAndAliveAndProductStartedNotInPause));

                    Logger.LogError("以上任意一个条件为真，NeedToRun，返回false");
                    */
                    return false;
                }
                //Logger.LogError("NeedToRun 返回true");
                return true;               
            }
        }

        public override void Run()
        {

            //Logger.LogError("状态机--执行Run");

            // 获取最近的商业技能商人 剥皮 采矿 采药这三样
            _trainerNpc = _memoryDBManager.GetNearestBusinessTrainer(subname);
            if (_trainerNpc == null)
            {
                Logger.LogError(string.Format("Run里 _memoryDBManager.GetNearestBusinessTrainer({0})的结果为空;", subname));
                return;
            }

            Vector3 trainerPosition = _trainerNpc.Creature.GetSpawnPosition;         

           

            if (!Helpers.TravelToVendorRange(_vendorTimerManager, _trainerNpc, DisplayName)
             || Helpers.NpcIsAbsentOrDead(_blackListManager, _trainerNpc))
            {
                return;
            }
            
            // 到达后
            if (subname == "采矿供应商" && ItemsManager.GetItemCountByIdLUA(2901) < 1)
            {
                // 采矿供应商
                // 矿工锄  (ID= 2901 )
                // Name: 高里纳 (Entry: 3358 )
                // new Vector3(2025.08, -4708.91, 27.03121, "None")                
                //GoToTask.ToPositionAndIntecractWithNpc(new Vector3(2025.08, -4708.91, 27.03121, "None"), 3358);
                if(GoToTask.ToPositionAndIntecractWithNpc(trainerPosition, _trainerNpc.entry))
                {
                    Vendor.BuyItem("矿工锄", 1);
                    return;
                }                                
            }

            // 没有剥皮小刀
            if (subname == "制皮供应商" && ItemsManager.GetItemCountByIdLUA(7005) < 1)
            {
                // 制皮供应商
                // 剥皮小刀  (ID= 7005 )
                // Name: 达玛尔 (Entry: 3366 )
                // new Vector3(1848.01, -4564.89, 24.98736, "None")             
                //GoToTask.ToPositionAndIntecractWithNpc(new Vector3(1848.01, -4564.89, 24.98736, "None"), 3366);
                if(GoToTask.ToPositionAndIntecractWithNpc(trainerPosition, _trainerNpc.entry))
                {
                    Vendor.BuyItem("剥皮小刀", 1);
                    return;
                }
                
            }


            if(subname.Contains("训练师"))
            {
                // 训练
                for (int i = 0; i <= 5; i++)
                {
                    Logger.Log($"Attempt {i + 1}");
                    GoToTask.ToPositionAndIntecractWithNpc(trainerPosition, _trainerNpc.entry, i);
                    Thread.Sleep(1000);
                    WTGossip.ClickOnFrameButton("StaticPopup1Button2"); // discard hearthstone popup
                    if (Lua.LuaDoString<int>($"return ClassTrainerFrame:IsVisible()") > 0)
                    {
                        Trainer.TrainingSpell();
                        Thread.Sleep(800 + Usefuls.Latency);
                        SpellManager.UpdateSpellBook();
                        PluginSettings.CurrentSetting.LastLevelTrained = (int)ObjectManager.Me.Level;
                        PluginSettings.CurrentSetting.Save();
                        Helpers.CloseWindow();
                        return;
                    }
                    Helpers.CloseWindow();
                }
            }
            

          

        }
    }
}