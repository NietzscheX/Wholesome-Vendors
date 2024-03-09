using robotManager.Helpful;
using robotManager.Products;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WholesomeToolbox;
using WholesomeVendors.Database.Models;
using WholesomeVendors.Utils;
using WholesomeVendors.WVSettings;
using wManager;
using wManager.Wow.Enums;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;

namespace WholesomeVendors.Managers
{
    internal class PluginCacheManager : IPluginCacheManager
    {
        private readonly IMemoryDBManager _memoryDBManager;

        public bool BagsRecorded { get; private set; }
        public List<WVItem> BagItems { get; private set; } = new List<WVItem>();
        public List<WVItem> ItemsToSell { get; private set; } = new List<WVItem>();
        public List<WVItem> ItemsToMail { get; private set; } = new List<WVItem>();
        public List<WVItem> UnMailableItems { get; private set; } = new List<WVItem>();
        public int NbFreeSlots { get; private set; }
        public string RangedWeaponType { get; private set; }
        public int Money { get; private set; }
        public int EmptyContainerSlots { get; private set; }
        public bool IsInInstance { get; private set; }
        public bool IsInBloodElfStartingZone { get; private set; } // 血精灵
        public bool IsInDraeneiStartingZone { get; private set; } // 德莱尼
        public bool IsInOutlands { get; private set; }
        public int RidingSkill { get; private set; }
        public List<int> KnownMountSpells { get; private set; } = new List<int>();
        public bool InLoadingScreen { get; private set; }
        public int NbAmmosInBags { get; private set; }
        public int NbDrinksInBags { get; private set; }
        public int NbFoodsInBags { get; private set; }
        public int NbDeadlyPoisonsInBags { get; private set; }
        public int NbInstantPoisonsInBags { get; private set; }
        public List<ModelItemTemplate> UsableAmmos { get; private set; } = new List<ModelItemTemplate>();
        public List<(SkillLine, int)> WeaponsSpellsToLearn { get; private set; } = new List<(SkillLine, int)>();
        public List<string> KnownSkills { get; private set; } = new List<string>();     // 已学技能？
        public bool IsAlreadyHavePet { get; private set; }

        private object _cacheLock = new object();



        public PluginCacheManager(IMemoryDBManager memoryDBManager)
        {
            _memoryDBManager = memoryDBManager;
        }

        public void Initialize()
        {
            lock (_cacheLock)
            {
                // Make sure all skills are expanded
                Lua.LuaDoString($@"
                    local skills = {{}};
                    for i = 1, 100 do
                        local skillName, header, isExpanded, skillRank, numTempPoints, skillModifier, skillMaxRank, 
                    isAbandonable, stepCost, rankCost, minLevel, skillCostType, skillDescription = GetSkillLineInfo(i);
                        if skillName ~= nil then
                            if (header ~= nil) then
                                ExpandSkillHeader(i);
                            end        
                        end
                    end
                ");

                RecordRangedWeaponType();
                RecordKnownMounts();
                RecordSkills();
                RecordBags();
                RecordMoney();
                RecordContinentAndInstancee();
                SanitizeDNSAndDNMLists();
                EventsLuaWithArgs.OnEventsLuaStringWithArgs += OnEventsLuaWithArgs;
                //Radar3D.OnDrawEvent += Radar3DOnDrawEvent;
                //Radar3D.Pulse();
            }
        }

        public void Dispose()
        {
            lock (_cacheLock)
            {
                EventsLuaWithArgs.OnEventsLuaStringWithArgs -= OnEventsLuaWithArgs;
                //Radar3D.OnDrawEvent -= Radar3DOnDrawEvent;
            }
        }

        private void Radar3DOnDrawEvent()
        {
            int height = 30;
            foreach (WVItem item in ItemsToSell)
            {
                Color itemColor = Color.Purple;
                if (item.Quality == 0) itemColor = Color.Gray;
                if (item.Quality == 1) itemColor = Color.White;
                if (item.Quality == 2) itemColor = Color.Green;
                if (item.Quality == 3) itemColor = Color.Blue;
                Radar3D.DrawString(item.Name, new Vector3(300, height += 20, 0), 10, itemColor);
            }
            int heightMail = 30;
            foreach (WVItem item in ItemsToMail)
            {
                Color itemColor = Color.Purple;
                if (item.Quality == 0) itemColor = Color.Gray;
                if (item.Quality == 1) itemColor = Color.White;
                if (item.Quality == 2) itemColor = Color.Green;
                if (item.Quality == 3) itemColor = Color.Blue;
                Radar3D.DrawString(item.Name, new Vector3(600, heightMail += 20, 0), 10, itemColor);
            }
        }       
        
        private void OnEventsLuaWithArgs(string id, List<string> args)
        {
            switch (id)
            {
                case "PLAYER_UNGHOST":                                    
                    var item = BagItems.Where(i => i.SubClass == "食物和饮料").First();
                    if(item != null)
                    {
                        /*                        
                        Logger.LogError("比如CC感知到了复活，就立刻刷BUFF、召唤BB之类的会打断吃喝的进程");
                        Logger.LogError("如果直接Product.InPause会导致可能出现一直在pause的复杂情况 因为pause的优先级很高");
                        Logger.LogError("应该如何处理呢？");                                                
                        */
                        Logger.LogError("复活了尝试吃喝--可能会失败因为复活后状态改变了，触发了很多新的事件和状态变更");
                        ItemsManager.UseItem(item.Name);                        
                    }                        
                    break;
                case "PLAYER_LEVEL_UP":
                    RecordRangedWeaponType(); // because of ammo record
                    SanitizeDNSAndDNMLists();
                    break;
                case "BAG_UPDATE":
                    RecordBags();
                    break;
                case "PLAYER_EQUIPMENT_CHANGED":
                    RecordRangedWeaponType();
                    SanitizeDNSAndDNMLists();
                    break;
                case "PLAYER_MONEY":
                    RecordMoney();
                    break;
                //case "WORLD_MAP_UPDATE":
                case "PLAYER_ENTERING_WORLD":
                case "PLAYER_LEAVING_WORLD":
                    CacheInLoadingScreen();
                    RecordContinentAndInstancee();
                    SanitizeDNSAndDNMLists();
                    break;
                case "SKILL_LINES_CHANGED":
                    RecordSkills();
                    break;
                case "COMPANION_LEARNED":
                    RecordKnownMounts();
                    break;
                case "COMPANION_UNLEARNED":
                    RecordKnownMounts();
                    break;
                case "INSTANCE_LOCK_STOP":
                case "COMMENTATOR_ENTER_WORLD":
                    CacheInLoadingScreen();
                    break;
                // 新增一个技能lua红字错误
                case "UI_ERROR_MESSAGE":
                    if (args[0].Contains("你已经控制了一个召唤生物") || args[0].Contains("你的宠物太多了") || args[0].Contains("你的宠物已经死亡"))
                    {
                        IsAlreadyHavePet = true;
                    }
                    if (args[0].Contains("你无法召唤宠物"))
                    {
                        IsAlreadyHavePet = false;

                    }

                    break;
            }
        }

        public void SanitizeDNSAndDNMLists()
        {
            if (IsInInstance) return;

            /*
             * We sanitize the DNS and DNM lists but we don't actually use them when selling or mailing
             * We strictly only sell and mail what is recorded during bag update
             * The DNS and DNM lists are there to protect items from other plugins
             */

            List<string> itemsToRemove = new List<string>();

            List<string> itemsToRemoveFromDNSList = wManagerSetting.CurrentSetting.DoNotSellList
                .Where(dnsItem => _memoryDBManager.GetAllPoisons.Exists(poison => poison.Name == dnsItem)
                    || _memoryDBManager.GetAllAmmos.Exists(ammo => ammo.Name == dnsItem)
                    || _memoryDBManager.GetAllFoods.Exists(food => food.Name == dnsItem)
                    || _memoryDBManager.GetAllDrinks.Exists(drink => drink.Name == dnsItem))
                .ToList();
            List<string> itemsToRemoveFromDNMList = wManagerSetting.CurrentSetting.DoNotMailList
                .Where(dnmItem => _memoryDBManager.GetAllPoisons.Exists(poison => poison.Name == dnmItem)
                    || _memoryDBManager.GetAllAmmos.Exists(ammo => ammo.Name == dnmItem)
                    || _memoryDBManager.GetAllFoods.Exists(food => food.Name == dnmItem)
                    || _memoryDBManager.GetAllDrinks.Exists(drink => drink.Name == dnmItem))
                .ToList();

            itemsToRemove.AddRange(itemsToRemoveFromDNSList);
            itemsToRemove.AddRange(itemsToRemoveFromDNMList);

            List<string> allItemsToRemove = itemsToRemove.Distinct().ToList();
            List<string> itemsToAdd = GetAllUsableItems().Select(item => item.Name).ToList();
            allItemsToRemove.RemoveAll(item => itemsToAdd.Contains(item));

            WTSettings.RemoveItemFromDoNotSellAndMailList(allItemsToRemove);
            WTSettings.AddItemToDoNotSellAndMailList(itemsToAdd);
        }

        private List<ModelItemTemplate> GetAllUsableItems()
        {
            List<ModelItemTemplate> result = new List<ModelItemTemplate>();

            // poisons
            if (PluginSettings.CurrentSetting.BuyPoison)
            {
                ModelItemTemplate deadlyPoison = _memoryDBManager.GetDeadlyPoisons.Find(p => p.RequiredLevel <= ObjectManager.Me.Level);
                if (deadlyPoison != null)
                {
                    result.Add(deadlyPoison);
                }
                ModelItemTemplate instantPoison = _memoryDBManager.GetInstantPoisons.Find(p => p.RequiredLevel <= ObjectManager.Me.Level);
                if (instantPoison != null)
                {
                    result.Add(instantPoison);
                }
            }

            // ammo
            if (PluginSettings.CurrentSetting.AmmoAmount > 0)
            {
                result.AddRange(UsableAmmos);
            }

            // food
            if (PluginSettings.CurrentSetting.FoodNbToBuy > 0)
            {
                result.AddRange(_memoryDBManager.GetAllUsableFoods());
            }

            // drink
            if (PluginSettings.CurrentSetting.DrinkNbToBuy > 0)
            {
                result.AddRange(_memoryDBManager.GetAllUsableDrinks());
            }

            return result;
        }

        private void CacheInLoadingScreen()
        {
            InLoadingScreen = true;
            Task.Run(async delegate
            {
                await Task.Delay(2000);
                InLoadingScreen = false;
            });
        }

        public void RecordKnownMounts()
        {
            int[] mountsIds = WTItem.WotlKGetKnownMountsIds();
            foreach (int id in mountsIds)
            {
                if (id > 0 && !KnownMountSpells.Contains(id))
                {
                    KnownMountSpells.Add(id);
                }
            }
        }

        private void RecordSkills()
        {
            RidingSkill = Skill.GetValue(SkillLine.Riding);
            List<SkillLine> myClassWeaponSkillsToLearn = classWeaponSkills[ObjectManager.Me.WowClass]
                .Where(skill => Skill.GetValue(skill) <= 0)
                .ToList();
            List<(SkillLine, int)> weaponSpellsToLearn = new List<(SkillLine, int)>();
            foreach (KeyValuePair<SkillLine, int> kvp in _skillSpells)
            {
                if (myClassWeaponSkillsToLearn.Contains(kvp.Key))
                {
                    weaponSpellsToLearn.Add((kvp.Key, kvp.Value));
                }
            }

            string[] luaSkills = Lua.LuaDoString<string[]>($@"
                local skills = {{}};
                for i = 1, GetNumSkillLines() do
                    local skillName, header, isExpanded, skillRank, numTempPoints, skillModifier, skillMaxRank, 
                        isAbandonable, stepCost, rankCost, minLevel, skillCostType, skillDescription = GetSkillLineInfo(i);
                    if skillName ~= nil then
                        if (header == nil) then
                            table.insert(skills, skillName);
                        end        
                    end
                end
                return unpack(skills);
            ");

            KnownSkills = luaSkills.ToList();
            WeaponsSpellsToLearn = weaponSpellsToLearn;
        }

        private void RecordBags()
        {
            lock (_cacheLock)
            {
                RecordBagItems();
                RecordBagSlotsAndFreeSlots();
                if (PluginSettings.CurrentSetting.AllowSell)
                {
                    RecordItemsToSell();
                }
                if (PluginSettings.CurrentSetting.AllowMail)
                {
                    RecordItemsToMail();
                }
            }
        }

        private void RecordBagSlotsAndFreeSlots()
        {
            // Could be put together with bag items record?
            string[] result = Lua.LuaDoString<string[]>($@"
                    local result = {{}}

                    local nbEmptyBagSlots = 0;
                    for i=0,3,1
                    do
                        local bagLink = GetContainerItemLink(0, 0-i);
                        if (bagLink == nil) then
                            nbEmptyBagSlots = nbEmptyBagSlots + 1
                        end
                    end
                    table.insert(result, nbEmptyBagSlots);

                    local nbFreeSlots = 0;
                    for i = 0, 5, 1 do
                        local numberOfFreeSlots, BagType = GetContainerNumFreeSlots(i);
                        if BagType == 0 then
                            nbFreeSlots = nbFreeSlots + numberOfFreeSlots;
                        end
                    end
                    table.insert(result, nbFreeSlots);
                    
                    return unpack(result);                    
                ");

            if (result.Length > 1)
            {
                EmptyContainerSlots = int.Parse(result[0]);
                NbFreeSlots = int.Parse(result[1]);
            }
            else
            {
                Logger.LogError($"RecordEmptyContainerSlots() -> Couldn't unpack result!");
            }
        }

        private void RecordContinentAndInstancee()
        {
            IsInBloodElfStartingZone = WTLocation.ZoneInBloodElfStartingZone(WTLocation.GetRealZoneText);
            IsInDraeneiStartingZone = WTLocation.ZoneInDraneiStartingZone(WTLocation.GetRealZoneText);
            IsInOutlands = WTLocation.PlayerInOutlands();
            IsInInstance = WTLocation.IsInInstance();
        }

        private void RecordMoney()
        {
            lock (_cacheLock)
            {
                Money = (int)ObjectManager.Me.GetMoneyCopper;
            }
        }

        private void RecordRangedWeaponType()
        {
            lock (_cacheLock)
            {
                uint myRangedWeapon = ObjectManager.Me.GetEquipedItemBySlot(InventorySlot.INVSLOT_RANGED);
                if (myRangedWeapon != 0)
                {
                    List<WoWItem> equippedItems = EquippedItems.GetEquippedItems();
                    foreach (WoWItem equippedItem in equippedItems)
                    {
                        if (equippedItem.GetItemInfo.ItemSubType == "弩" || equippedItem.GetItemInfo.ItemSubType == "弓")
                        {
                            RangedWeaponType = "Bows";
                            UsableAmmos = _memoryDBManager.GetAllAmmos.FindAll(ammo => ammo.Subclass == 2 && ammo.RequiredLevel <= ObjectManager.Me.Level);
                            return;
                        }
                        if (equippedItem.GetItemInfo.ItemSubType == "枪械")
                        {
                            RangedWeaponType = "Guns";
                            UsableAmmos = _memoryDBManager.GetAllAmmos.FindAll(ammo => ammo.Subclass == 3 && ammo.RequiredLevel <= ObjectManager.Me.Level);
                            return;
                        }
                    }
                }
                RangedWeaponType = null;
                UsableAmmos = new List<ModelItemTemplate>();
            }
        }

        private void RecordBagItems()
        {
            lock (_cacheLock)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                // Record all items in bags
                string[] itemsFromLua = Lua.LuaDoString<string[]>($@"
                    local result = {{}};
                    for i=0, 4, 1 do
                        local bagLink = GetContainerItemLink(0, i-4);
                        -- bags 0 to 4 (right to left)
                        if (bagLink ~= nil or i == 0) then
                            local containerNbSlots = GetContainerNumSlots(i)
                            -- Get all items in bag
                            for j=1, containerNbSlots do
                                local itemLink = GetContainerItemLink(i, j);
                                if itemLink ~= nil then 
                                    local name, link, quality, iLevel, reqLevel, class, subclass, maxStack, equipSlot, texture, vendorPrice = GetItemInfo(itemLink);
                                    local _, count, _, _, _, _, _ = GetContainerItemInfo(i, j);
                                    local entry = GetContainerItemID(i, j);
                                    local iteminfo = name .. ""£"" .. link .. ""£"" .. quality .. ""£"" .. iLevel .. ""£"" .. reqLevel .. ""£"" .. class .. ""£"" .. subclass .. ""£"" .. maxStack .. ""£"" .. equipSlot .. ""£"" .. texture .. ""£"" .. vendorPrice .. ""£"" .. entry .. ""£"" .. i .. ""£"" .. j .. ""£"" .. count;
                                    table.insert(result, iteminfo);
                                end;
                            end;
                        end
                    end
                    return unpack(result);
                ");

                if (itemsFromLua.Length <= 0 || string.IsNullOrEmpty(itemsFromLua[0]))
                {
                    BagsRecorded = false;
                    Logger.LogError($"[RecordBagItems] LUA info was empty");
                    return;
                }
                BagsRecorded = true;
                BagItems.Clear();
                foreach (string item in itemsFromLua)
                {
                    BagItems.Add(new WVItem(item));
                }

                // Record nb ammos in bags
                int nbAmmosInBags = 0;
                foreach (WVItem bagItem in BagItems)
                {                    
                    if (UsableAmmos.Exists(ua => ua.Entry == bagItem.Entry))
                    {
                        nbAmmosInBags += bagItem.Count;
                    }
                }
                NbAmmosInBags = nbAmmosInBags;

                // Record nb Drinks in bags
                int nbDrinksInBags = 0;
                List<ModelItemTemplate> allDrinks = _memoryDBManager.GetAllUsableDrinks();
                string drinkToSet = null;
                foreach (WVItem item in BagItems)
                {
                    if (allDrinks.Exists(ua => ua.Entry == item.Entry))
                    {
                        nbDrinksInBags += item.Count;
                        drinkToSet = item.Name;
                    }
                }

                if (drinkToSet != null && wManagerSetting.CurrentSetting.DrinkName != drinkToSet)
                {
                    Logger.Log($"Setting drink to {drinkToSet}");
                    wManagerSetting.CurrentSetting.DrinkName = drinkToSet;
                    wManagerSetting.CurrentSetting.Save();
                }
                NbDrinksInBags = nbDrinksInBags;

                // Record nb Foods in bags
                int nbFoodsInBags = 0;
                List<ModelItemTemplate> allFoods = _memoryDBManager.GetAllUsableFoods();
                string foodToSet = null;
                foreach (WVItem item in BagItems)
                {
                    if (allFoods.Exists(ua => ua.Entry == item.Entry))
                    {
                        nbFoodsInBags += item.Count;
                        foodToSet = item.Name;
                    }
                }

                if (foodToSet != null && wManagerSetting.CurrentSetting.FoodName != foodToSet)
                {
                    Logger.Log($"Setting food to {foodToSet}");
                    wManagerSetting.CurrentSetting.FoodName = foodToSet;
                    wManagerSetting.CurrentSetting.Save();
                }
                NbFoodsInBags = nbFoodsInBags;

                // Record nb Poisons in bags
                int nbDeadlysInBags = 0;
                int nbInstantsInBags = 0;
                foreach (WVItem item in BagItems)
                {
                    if (_memoryDBManager.GetDeadlyPoisons.Exists(p => p.Entry == item.Entry))
                    {
                        nbDeadlysInBags += item.Count;
                    }
                    if (_memoryDBManager.GetInstantPoisons.Exists(p => p.Entry == item.Entry))
                    {
                        nbInstantsInBags += item.Count;
                    }
                }
                NbDeadlyPoisonsInBags = nbDeadlysInBags;
                NbInstantPoisonsInBags = nbInstantsInBags;
            }
        }

        public void SetItemToUnMailable(WVItem umItem)
        {
            UnMailableItems.Add(umItem);
            RecordItemsToMail();
        }

        private void RecordItemsToMail()
        {
            lock (_cacheLock)
            {
                List<WVItem> listItemsToMail = new List<WVItem>();
                foreach (WVItem item in BagItems)
                {
                    if (UnMailableItems.Exists(umItem => umItem.Entry == item.Entry && umItem.InBag == item.InBag && umItem.InSlot == item.InSlot))
                    {
                        continue;
                    }

                    if (wManagerSetting.CurrentSetting.ForceMailList.Contains(item.Name))
                    {
                        listItemsToMail.Add(item);
                        continue;
                    }

                    if (!ShouldMailByQuality(item)
                        || wManagerSetting.CurrentSetting.DoNotMailList.Contains(item.Name)
                        || item.SellPrice <= 0
                        || ItemMightBeEquippableLater(item)) // Don't send items that can potentially be equipped later
                    {
                        continue;
                    }

                    listItemsToMail.Add(item);
                }
                ItemsToMail = listItemsToMail;
            }
        }

        private void RecordItemsToSell()
        {
            lock (_cacheLock)
            {
                List<WVItem> listItemsToSell = new List<WVItem>();

                foreach (WVItem item in BagItems)
                {
                    if (wManagerSetting.CurrentSetting.ForceSellList.Contains(item.Name))
                    {
                        listItemsToSell.Add(item);
                        continue;
                    }

                    if (!ShouldSellByQuality(item)
                        || item.SellPrice <= 0
                        || ItemMightBeEquippableLater(item)) // Don't sell items that can potentially be equipped later
                    {
                        continue;
                    }

                    if (!wManagerSetting.CurrentSetting.DoNotSellList.Contains(item.Name)
                        && !GetAllUsableItems().Exists(usableItem => usableItem.Entry == item.Entry))
                    {
                        listItemsToSell.Add(item);
                    }
                }

                ItemsToSell = listItemsToSell;
            }
        }

        private bool ItemMightBeEquippableLater(WVItem item)
        {
            string sublclass = item.SubClass;
            
            //if (sublclass == "One-Handed Swords") sublclass = "Swords";
            //if (sublclass == "One-Handed Axes") sublclass = "Axes";
            //if (sublclass == "One-Handed Maces") sublclass = "Maces";

            if (sublclass == "单手剑") sublclass = "剑";
            if (sublclass == "单手斧") sublclass = "斧";
            if (sublclass == "单手锤") sublclass = "锤";

            bool result = item.IsEquippable
                && KnownSkills.Contains(sublclass)
                && item.EquipSlot != "INVTYPE_AMMO"
                && item.Quality > 1
                && item.ReqLevel > ObjectManager.Me.Level;
            return result;
        }

        private bool ShouldSellByQuality(WVItem item)
        {
            if (item.Quality == 0 && PluginSettings.CurrentSetting.SellGrayItems) return true;
            if (item.Quality == 1 && PluginSettings.CurrentSetting.SellWhiteItems) return true;
            if (item.Quality == 2 && PluginSettings.CurrentSetting.SellGreenItems) return true;
            if (item.Quality == 3 && PluginSettings.CurrentSetting.SellBlueItems) return true;
            if (item.Quality == 4 && PluginSettings.CurrentSetting.SellPurpleItems) return true;
            return false;
        }

        private bool ShouldMailByQuality(WVItem item)
        {
            if (item.Quality == 0 && PluginSettings.CurrentSetting.MailGrayItems) return true;
            if (item.Quality == 1 && PluginSettings.CurrentSetting.MailWhiteItems) return true;
            if (item.Quality == 2 && PluginSettings.CurrentSetting.MailGreenItems) return true;
            if (item.Quality == 3 && PluginSettings.CurrentSetting.MailBlueItems) return true;
            if (item.Quality == 4 && PluginSettings.CurrentSetting.MailPurpleItems) return true;
            return false;
        }

        public bool HaveEnoughMoneyFor(int amount, ModelItemTemplate item) => Money >= item.BuyPrice * amount / item.BuyCount;

        private Dictionary<SkillLine, int> _skillSpells = new Dictionary<SkillLine, int>()
        {
            { SkillLine.Axes, 196 },
            { SkillLine.TwoHandedAxes, 197 },
            { SkillLine.Maces, 198 },
            { SkillLine.TwoHandedMaces, 199 },
            { SkillLine.Polearms, 200 },
            { SkillLine.Swords, 201 },
            { SkillLine.TwoHandedSwords, 202 },
            { SkillLine.Staves, 227 },
            { SkillLine.Bows, 264 },
            { SkillLine.Guns, 266 },
            { SkillLine.Daggers, 1180 },
            { SkillLine.Crossbows, 5011 },
            { SkillLine.FistWeapons, 15590 },
        };
        /*
         * 
         * 关于自动职业任务 按职业、种族进行分类
         * 
         * Dictionary<WoWClass.猎人,List<int>>
         * Dictionary<WoWClass,>
         * 
         */
        /*
        private Dictionary<WoWClass, Dictionary<WoWRace,List<int>>> classQuests = new Dictionary<WoWClass, Dictionary<WoWRace, List<int>>>()
        {
            // 猎人
            { WoWClass.Hunter, new Dictionary<WoWRace, List<int>>()
            {
                // 血精灵
                { WoWRace.BloodElf,new List<int>{
                    9617,
                    9484,
                    9486,
                    9485,
                    9673
                } },
                
                // 亡灵
                { WoWRace.Undead,new List<int>{


                } },                

                // 兽人
                { WoWRace.Orc,new List<int>{
                    6068,
                    6062,
                    6083,
                    6082,
                    6081
                } },
                // 巨魔
                { WoWRace.Troll,new List<int>{
                    6068,
                    6062,
                    6083,
                    6082,
                    6081
                } },
                // 牛头人
                { WoWRace.Tauren,new List<int>{
                    6065,
                    6061,
                    6087,
                    6088,
                    6089
                } },
                //
            } },

            // 战士
            { WoWClass.Warrior, new Dictionary<WoWRace, List<int>>()
            {
                // 血精灵
                { WoWRace.BloodElf,new List<int>{


                } },
                
                // 亡灵
                { WoWRace.Undead,new List<int>{


                } },                

                // 兽人
                { WoWRace.Orc,new List<int>{


                } },
                // 巨魔
                { WoWRace.Troll,new List<int>{


                } },
                // 牛头人
                { WoWRace.Tauren,new List<int>{


                } },
                //
            } },

        };
        */

        private Dictionary<WoWClass, List<SkillLine>> classWeaponSkills = new Dictionary<WoWClass, List<SkillLine>>()
        {
            { WoWClass.DeathKnight, new List<SkillLine>()
                {
                    SkillLine.Axes,
                    SkillLine.TwoHandedAxes,
                    SkillLine.Swords,
                    SkillLine.TwoHandedSwords,
                    SkillLine.Maces,
                    SkillLine.TwoHandedMaces,
                    SkillLine.Polearms,
                }},
            { WoWClass.Druid, new List<SkillLine>()
                {
                    SkillLine.Maces,
                    SkillLine.TwoHandedMaces,
                    SkillLine.Polearms,
                    SkillLine.Staves,
                    SkillLine.Daggers,
                    SkillLine.FistWeapons,
                }},
            { WoWClass.Hunter, new List<SkillLine>()
                {
                    SkillLine.Axes,
                    SkillLine.TwoHandedAxes,
                    SkillLine.Swords,
                    SkillLine.TwoHandedSwords,
                    SkillLine.Polearms,
                    SkillLine.Staves,
                    SkillLine.Daggers,
                    SkillLine.FistWeapons,
                    SkillLine.Bows,
                    SkillLine.Crossbows,
                    SkillLine.Guns,
                }},
            { WoWClass.Mage, new List<SkillLine>()
                {
                    SkillLine.Swords,
                    SkillLine.Staves,
                    SkillLine.Daggers,
                    SkillLine.Wands,
                }},
            { WoWClass.Paladin, new List<SkillLine>()
                {
                    SkillLine.Axes,
                    SkillLine.TwoHandedAxes,
                    SkillLine.Swords,
                    SkillLine.TwoHandedSwords,
                    SkillLine.Maces,
                    SkillLine.TwoHandedMaces,
                    SkillLine.Polearms,
                }},
            { WoWClass.Priest, new List<SkillLine>()
                {
                    SkillLine.Maces,
                    SkillLine.Staves,
                    SkillLine.Daggers,
                    SkillLine.Wands,
                }},
            { WoWClass.Rogue, new List<SkillLine>()
                {
                    SkillLine.Axes,
                    SkillLine.Maces,
                    SkillLine.Swords,
                    SkillLine.Daggers,
                    SkillLine.FistWeapons,
                    SkillLine.Bows,
                    SkillLine.Crossbows,
                    SkillLine.Guns,
                    SkillLine.Thrown,
                }},
            { WoWClass.Shaman, new List<SkillLine>()
                {
                    SkillLine.Axes,
                    SkillLine.TwoHandedAxes,
                    SkillLine.Maces,
                    SkillLine.TwoHandedMaces,
                    SkillLine.Staves,
                    SkillLine.Daggers,
                    SkillLine.FistWeapons,
                }},
            { WoWClass.Warlock, new List<SkillLine>()
                {
                    SkillLine.Swords,
                    SkillLine.Staves,
                    SkillLine.Daggers,
                    SkillLine.Wands,
                }},
            { WoWClass.Warrior, new List<SkillLine>()
                {
                    SkillLine.Axes,
                    SkillLine.TwoHandedAxes,
                    SkillLine.Swords,
                    SkillLine.TwoHandedSwords,
                    SkillLine.Maces,
                    SkillLine.TwoHandedMaces,
                    SkillLine.Polearms,
                    SkillLine.Staves,
                    SkillLine.Daggers,
                    SkillLine.FistWeapons,
                    SkillLine.Bows,
                    SkillLine.Crossbows,
                    SkillLine.Guns,
                    SkillLine.Thrown,
                }},
        };
    }
}