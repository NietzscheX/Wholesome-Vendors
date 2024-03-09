using MahApps.Metro.Controls;
using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Windows.Media;
using System.Xml.Linq;
using WholesomeToolbox;
using WholesomeVendors.Database.Models;
using WholesomeVendors.Managers;
using WholesomeVendors.Utils;
using WholesomeVendors.WVSettings;
using wManager;
using wManager.Wow.Bot.Tasks;
using wManager.Wow.Class;
using wManager.Wow.Enums;
using wManager.Wow.Helpers;
using wManager.Wow.Helpers.FightClassCreator;
using wManager.Wow.ObjectManager;



namespace WholesomeVendors.WVState
{
    public class ClassQuestState : State
    {
        public override string DisplayName { get; set; } = "职业任务";

        private readonly IPluginCacheManager _pluginCacheManager;
        private readonly IMemoryDBManager _memoryDBManager;
        private readonly IVendorTimerManager _vendorTimerManager;
        private readonly IBlackListManager _blackListManager;

        //private ModelCreatureTemplate _trainerNpc;
        private bool _enabledInSetting;
        private Object obj = new object();

        private List<string> _questItems = new List<string>(){"驯兽棒"};
        private int CurrentQuestId { get; set; }
        //private bool _isNeedToRun { get; set; } = false; // 默认是不需要运行的


        //private string subname = null;
        



        public ClassQuestState(
            IMemoryDBManager memoryDBManager,
            IPluginCacheManager pluginCacheManager,
            IVendorTimerManager vendorTimerManager,
            IBlackListManager blackListManager)
        {
            _memoryDBManager = memoryDBManager;
            _pluginCacheManager = pluginCacheManager;
            _vendorTimerManager = vendorTimerManager;
            _blackListManager = blackListManager;
            _enabledInSetting = PluginSettings.CurrentSetting.AllowClassQuest;
        }


        private bool IsNeedToDoClassQuest()
        {
           
                // 小于十级不需要
                if (ObjectManager.Me.Level < 10)
                    return false;


                switch (ObjectManager.Me.WowClass)
                {
                    // LR 驯服野兽
                    // Tame Beast (Id found: 1515, Name found: Tame Beast, NameInGame found: 驯服野兽, Know = True, IsSpellUsable = True)
                    case WoWClass.Hunter: 
                        // 学会了就不需要做职业任务 返回false
                        // 没学会就需要 返回true
                        return SpellManager.KnowSpell("Revive Pet") ? false : true;

                    // SS 召唤虚空行者
                    // Summon Voidwalker (Id found: 697, Name found: Summon Voidwalker, NameInGame found: 召唤虚空行者, Know = True, IsSpellUsable = False)
                    case WoWClass.Warlock:                        
                        return SpellManager.KnowSpell("Summon Voidwalker") ? false : true;

                    // ZS 防御姿态
                    case WoWClass.Warrior:
                        return SpellManager.KnowSpell("Defensive Stance") ? false : true;
                }  

            return false;
        }



        public override bool NeedToRun
        {
            get
            {
                // 任意一个满足 该状态就不执行
                if (!Main.IsLaunched // 程序未启动不执行                    
                    || !_enabledInSetting // 设置中关闭不执行                    
                    || _pluginCacheManager.InLoadingScreen // 蓝条
                    || Fight.InFight  // 战斗中不执行
                    || ObjectManager.Me.IsOnTaxi // 交通工具上不执行
                    || _pluginCacheManager.IsInInstance   // 副本里不执行                             
                    || !Conditions.InGameAndConnectedAndAliveAndProductStartedNotInPause)//暂停状态不执行
                {
                   
                    return false;
                }

                /*
                Logger.Log(string.Format("!Main.IsLaunched = {0}", !Main.IsLaunched));
                Logger.Log(string.Format("!_enabledInSetting={0}", !_enabledInSetting));                
                Logger.Log(string.Format("_pluginCacheManager.InLoadingScreen = {0}", _pluginCacheManager.InLoadingScreen));                
                Logger.Log(string.Format("Fight.InFight = {0}", Fight.InFight));                
                Logger.Log(string.Format("ObjectManager.Me.IsOnTaxi = {0}", ObjectManager.Me.IsOnTaxi));
                Logger.Log(string.Format("_pluginCacheManager.IsInInstance = {0}", _pluginCacheManager.IsInInstance));
                Logger.Log(string.Format("!Conditions.InGameAndConnectedAndAliveAndProductStartedNotInPause = {0}", !Conditions.InGameAndConnectedAndAliveAndProductStartedNotInPause));

                //Logger.LogError("职业任务可能需要运行 -> NeedToRun()结果为True!");                
                */
                return IsNeedToDoClassQuest();

            }
        }

        public override void Run()
        {

            Logger.LogError("职业任务 -> Run()");
            // 几个任务id必须完成
            List<int> QuestIds = new List<int>() {9484,9486,9485,9673}; // 血精灵 很奇怪先是84 86 

            /*
            // 首先要把职业任务的任务物品添加到防止删除列表
            
            List<string> dnsList = wManagerSetting.CurrentSetting.DoNotSellList;
            int WAQlistStartIndex = dnsList.IndexOf("WAQStart");
            int WAQlistEndIndex = dnsList.IndexOf("WAQEnd");
            int WAQListLength = WAQlistEndIndex - WAQlistStartIndex - 1;
            List<string> listQuestItems = dnsList.GetRange(WAQlistStartIndex + 1, WAQListLength);
            foreach(string questItem in _questItems)
            {
                if (!listQuestItems.Contains(questItem))
                {
                    dnsList[WAQlistStartIndex + 1] = questItem;
                    wManagerSetting.CurrentSetting.DoNotSellList = dnsList;
                    wManagerSetting.CurrentSetting.Save();
                }
            }
            */
            



            foreach (int id in QuestIds)
            {
                // 未完成
                if (!Quest.GetQuestCompleted(id) )
                {
                    Logger.LogError(id + "未完成，现在去完成它");
                    CurrentQuestId = id;
                    break;
                }
                CurrentQuestId = -1;
            }

            
            if ( CurrentQuestId < 0 )
            {
                Logger.LogError("任务已经全部完成");                
                return;
            }

            // 去完成任务        
            if(DoQuestAction( CurrentQuestId ))
            {
                Logger.LogError(string.Format("任务{0}完成",CurrentQuestId));
            }
        }

        struct Npc
        {
            public int Entry;
            public Vector3 Position;
        }

        private bool DoQuestAction(int questID)
        {   
            Npc startNpc = new Npc();
            Npc monster = new Npc();
            Npc endNpc = new Npc();
            bool result = false;
            switch (questID)
            {
                case 9484:
                    startNpc.Entry = 15399;
                    startNpc.Position = new Vector3(8980.84, -7458.36, 86.68821, "None");
                    // Name: 达恩·晨行者中尉 (Entry: 15399 )
                    monster.Entry = 15650;
                    monster.Position = new Vector3(8903.431, -7550.425, 108.9099, "None");

                    endNpc.Entry = 15399;
                    endNpc.Position = new Vector3(8980.84, -7458.36, 86.68821, "None");
                    break;

                case 9486:
                    startNpc.Entry = 15399;
                    startNpc.Position = new Vector3(8980.84, -7458.36, 86.68821, "None");
                    // Name: 老魔泉豹 (Entry: 15652 )
                    monster.Entry = 15652;
                    monster.Position = new Vector3(8903.129, -7585.331, 118.0005, "None");

                    endNpc.Entry = 15399;
                    endNpc.Position = new Vector3(8980.84, -7458.36, 86.68821, "None");
                    break;

                case 9485:

                    startNpc.Entry = 15399;
                    startNpc.Position = new Vector3(8980.84, -7458.36, 86.68821, "None");

                    //Name: 迷雾蝠 (Entry: 16353 )
                    monster.Entry = 16353;                    
                    monster.Position = new Vector3(7956.326, -6854.566, 59.24934, "None");

                    endNpc.Entry = 15399;
                    endNpc.Position = new Vector3(8980.84, -7458.36, 86.68821, "None");
                    break;

                case 9673:

                    
                    startNpc.Entry = 15399;
                    startNpc.Position = new Vector3(8980.84, -7458.36, 86.68821, "None");

                    var choice = Others.Random(0,1);

                    if (choice == 0)
                    {
                        // 抓个龙鹰
                        monster.Entry = 15650;
                        monster.Position = new Vector3(8903.431, -7550.425, 108.9099, "None");
                    }
                    else if(choice == 1)
                    {
                        // 抓个猫科
                        monster.Entry = 15652;
                        monster.Position = new Vector3(8903.129, -7585.331, 118.0005, "None");
                    }else
                    {
                        // 抓个蝙蝠                        
                        monster.Entry = 16353;
                        monster.Position = new Vector3(7956.326, -6854.566, 59.24934, "None");

                    }
                    // 在银月城交任务
                    // Name: 哈森尼斯(Entry: 16675)
                    // new Vector3(9926.74, -7396.38, 13.63674, "None");
                    endNpc.Entry = 16675;
                    endNpc.Position = new Vector3(9926.74, -7396.38, 13.63674, "None");


                    break;
            }
            
            // 未完成 也不拥有
            if (!Quest.HasQuest(questID) && !Quest.GetQuestCompleted(questID))
            {
                Logger.LogError("接职业任务" + questID);
                PickUp(startNpc, questID);
            }

            // 拥有且未完成 
            if(Quest.HasQuest(questID) && !Quest.IsObjectiveComplete(1, questID))
            {
                /*
                // 这一步就算完成了
                SpellManager.UpdateSpellBook();
                if (questID == 9673 && SpellManager.KnowSpell("Tame Beast"))
                {
                    Logger.LogError("交职业任务" + questID);

                    // 先尝试召唤宠物
                    // 只有没有宠物才去抓
                   
                        Logger.LogError("我没有宠物，我需要抓一个宠物");
                        Pulse(monster, questID, "使用驯服野兽技能抓取BB"); // 抓个bb再去交 会导致一个bug 就是反复抓...
                   

                    TurnIn(endNpc, questID); 
                }
                else
                {
                    Logger.LogError("做职业任务" + questID);
                    Pulse(monster, questID, "驯兽棒");
                }
                */

                Logger.LogError("做职业任务" + questID);
                Pulse(monster, questID, "驯兽棒");
                if (questID == 9673)
                {
                    Logger.LogError("交职业任务" + questID);
                    TurnIn(endNpc, questID);
                }


            }

            // 拥有且完成            
            if(Quest.HasQuest(questID) && Quest.IsObjectiveComplete(1, questID))
            {
                Logger.LogError("交职业任务" + questID);
                TurnIn(endNpc, questID);
                SpellManager.UpdateSpellBook();
            }

                        

            return result;
        }

       

        private bool PickUp(Npc npc, int questID)
        {           
            while (!GoToTask.ToPosition(npc.Position))
            {                
                if (MovementManager.InMovement)
                {
                    Thread.Sleep(100);
                }
            }

            // 到达NPC
            WoWUnit npc01 = ObjectManager.GetNearestWoWUnit(ObjectManager.GetWoWUnitByEntry(npc.Entry));
            if(npc01 != null)
            {
                Interact.InteractGameObject(npc01.GetBaseAddress);
                Quest.SelectGossipAvailableQuest(1);
                Thread.Sleep(1000);
                Quest.AcceptQuest();
            }
            

            return Quest.HasQuest(questID);
        }

        // 驯兽棒
        private bool Pulse(Npc monster, int questID, string itemName="驯兽棒")
        {
            // 要处理一个特殊情况，就宠物死亡，因为现在没掌握复活宠物无法召唤，然后宠物UI也消失了，法术书也消失了，没法再驯服新的宠物，此时应该如何判断？
            /* 此时
             * ObjectManager.Me.PetNumber 总是返回0，不靠谱。
             * ObjectManager.Pet.IsDead    ，死亡和没有宠物都返回true，不靠谱
             * ObjectManager.Pet.IsValid   ，死亡和没有宠物都返回false，不靠谱
             * ObjectManager.Pet.IsAlive   ，死亡和没有宠物都返回false，不靠谱
             * 使用召唤宠物技能，提示 你的宠物已经死亡。
             * 使用驯服野兽，提示 你宠物太多了
             * 使用解散野兽，提示 你没有宠物
             * 目前还不会复活宠物，此时如何判断我到底有没有宠物？
             * Lua.LuaDoString("PetAbandon();PetDismiss()"); 这个是在宠物UI在的时候 不管死亡还是存活都可以工作，但是在宠物UI不存在的时候 不能工作
             * 也就是说这里没法通过技能释放 获取lua错误报错来反馈技能的释放情况
             * 这里就不得不引入manager来进行管理了....
             */


            if (!ObjectManager.Pet.IsValid)
            {
                // 不管是没有宠物还是 宠物已经死亡都会到这里来
                // 这里通过Lua Error报错来获取是否有宠物           

                // 先使用一个技能触发错误 看看是什么原因
                SpellManager.CastSpellByNameLUA("召唤宠物");
                Thread.Sleep(1000);
                if (_pluginCacheManager.IsAlreadyHavePet)
                {
                    Logger.LogError("Manager返回 我已经有宠物了 不需要再抓....");
                    return true;
                }
            }

            /*
            if (ItemsManager.GetItemCountByNameLUA(itemName) < 1)
            {
                // 看起来物品不存在 只能放弃这个任务 重新接
                Logger.LogError("物品不存在？？？本来应该放弃重新接但是，有延迟... 导致这个判断不可靠");
                Quest.AbandonLastQuest();
                return false;
                // 算了算了还是修改WAQ的逻辑吧
            }
            */            

            while (!GoToTask.ToPosition(monster.Position))
            {
                if (MovementManager.InMovement)
                {
                    Thread.Sleep(100);
                }
            }            
                        
            // Name: 疯狂的龙鹰 (Entry: 15650 )
            // 对目标使用技能
            WoWUnit monster01 = ObjectManager.GetNearestWoWUnit(ObjectManager.GetWoWUnitByEntry(monster.Entry));
            if (monster01 != null)
            {
                MovementManager.Face(monster01);
                Thread.Sleep(2000);

                
                if(monster01.GetDistance > 26)
                {
                    GoToTask.ToPosition(monster01.Position);
                }
                ObjectManager.Me.Target = monster01.Guid;
                Fight.StopFight();

                /*
                ObjectManager.Me.Target = monster01.Guid;
                Fight.StopFight();
                var path = PathFinder.FindPath(monster01.Position);
                foreach (Vector3 p in path)
                {
                    MovementManager.MoveTo(p);
                    if (monster01.GetDistance2D < 20 )
                    {
                        MovementManager.Face(monster01);
                        MovementManager.StopMove();
                        break;
                        //Lua.RunMacroText("/cast Tame Beast");
                        //Usefuls.WaitIsCasting();
                    }
                }
                */



                SpellManager.UpdateSpellBook();
                // 为了避免BB挂了没法复活的情况导致判断失败，需要先解散一次 （经过测试发现没法解散...)               
                //Dismiss Pet(Id found: 2641, Name found: Dismiss Pet, NameInGame found: 解散野兽, Know = True, IsSpellUsable = True)
                //Lua.LuaDoString("PetAbandon();PetDismiss()");
                //Interact.InteractGameObject(monster01.GetBaseAddress);
                //Thread.Sleep(3000);



                if (SpellManager.KnowSpell("Tame Beast"))
                {
                    DisplayName = "职业任务-驯服野兽";
                    Logger.LogError("用技能抓一个BB先");
                    Lua.LuaDoString("PetAbandon();PetDismiss()");
                    MovementManager.Face(monster01);
                    Thread.Sleep(2000);
                    SpellManager.CastSpellByNameLUA("驯服野兽");
                    Usefuls.WaitIsCasting();

                    //然后需要自动释放这些123号技能（特别是低吼）
                    //ObjectManager.Pet.
                    //SpellManager.
                    Lua.LuaDoString($@"
                    for i = 1, 10, 1 do
                        local name, subtext, texture, isToken, isActive, autoCastAllowed, autoCastEnabled = GetPetActionInfo(i);                        
                        if (autoCastAllowed and not autoCastEnabled ) then
                          print(name)
                          ToggleSpellAutocast(name, ""pet"")
                        end                                              
                    end");


                }
                else
                {
                    DisplayName = "职业任务-使用驯兽棒";
                    Logger.LogError("用驯兽棒抓一个BB先");
                    Lua.LuaDoString("PetAbandon();PetDismiss()");
                    MovementManager.Face(monster01);
                    ItemsManager.UseContainerItemByNameOrId(itemName);
                    Usefuls.WaitIsCasting();
                    
                }                                

                
                
            }
            // b
            return Quest.IsObjectiveComplete(1, questID) || ObjectManager.Pet.IsAlive || SpellManager.KnowSpell("Tame Beast");
            
        }
        
        private bool TurnIn(Npc npc,int questID)
        {
            while (!GoToTask.ToPosition(npc.Position))
            {
                if (MovementManager.InMovement)
                {
                    Thread.Sleep(100);
                }
            }
            // 到达NPC
            WoWUnit npc01 = ObjectManager.GetNearestWoWUnit(ObjectManager.GetWoWUnitByEntry(npc.Entry));
            if (npc01 != null)
            {
                Interact.InteractGameObject(npc01.GetBaseAddress);
                Thread.Sleep(3000);
                Quest.SelectGossipActiveQuest(1);
                Thread.Sleep(3000);
                Quest.CompleteQuest(1);
            }

            SpellManager.UpdateSpellBook();
            return Quest.GetQuestCompleted(questID);
        }
    }
}