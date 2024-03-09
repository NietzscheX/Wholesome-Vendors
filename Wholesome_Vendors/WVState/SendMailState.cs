using robotManager.FiniteStateMachine;
using robotManager.Helpful;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WholesomeVendors.Database.Models;
using WholesomeVendors.Managers;
using WholesomeVendors.Utils;
using WholesomeVendors.WVSettings;
using wManager.Wow.Bot.Tasks;
using wManager.Wow.Enums;
using wManager.Wow.Helpers;
using wManager.Wow.ObjectManager;
using Timer = robotManager.Helpful.Timer;
using wManager;

namespace WholesomeVendors.WVState
{
    public class SendMailState : State
    {
        private ModelGameObjectTemplate _mailBox;
        private string _recipient;
        private int _nbFreeSlotsOnNeedToRun;
        private bool _usingDungeonProduct;
        private Timer _stateTimer = new Timer();

        public override string DisplayName { get; set; } = "WV Send Mail";

        private readonly IPluginCacheManager _pluginCacheManager;
        private readonly IMemoryDBManager _memoryDBManager;
        private readonly IVendorTimerManager _vendorTimerManager;
        private readonly IBlackListManager _blackListManager;
        private int MinFreeSlots => PluginSettings.CurrentSetting.MinFreeSlots;

        public SendMailState(
            IMemoryDBManager memoryDBManager,
            IPluginCacheManager pluginCacheManager,
            IVendorTimerManager vendorTimerManager,
            IBlackListManager blackListManager)
        {
            _usingDungeonProduct = Helpers.UsingDungeonProduct();
            //_recipient = GetRecipient();
            _memoryDBManager = memoryDBManager;
            _pluginCacheManager = pluginCacheManager;
            _vendorTimerManager = vendorTimerManager;
            _blackListManager = blackListManager;
        }

        // 邮件对象由#隔开
        // 核心#斩蛇当道#逆向思维#毛毛虫的春天
        // 为了方便还是搞个配置文件吧
        // 如果配置文件里面有内容则之间读取配置文件的内容，否则读取插件里的内容，最后读取wr的设置
        private string GetRecipient()
        {
            //Random random = new Random();
            int randomIndex;
            List<string> names = new List<string>();

            string mailConf = Others.GetCurrentDirectory + @"\Plugins\sendmail.conf";

            // 不存在就创建空白文件
            if (!File.Exists(mailConf))
            {                
                using (StreamWriter sw = File.CreateText(mailConf)) { }
                Logger.Log("配置文件创建成功");
                return "";
            }

            // 文件存在就读取内容            
            names.AddRange(File.ReadAllLines(mailConf));
            // 文件有内容直接返回
            if (names.Count > 0)
            {            
                //randomIndex = random.Next(names.Count);
                randomIndex = Others.Random(0, names.Count -1);
                //Logger.Log("从配置文件中选择邮寄对象 > " + names[randomIndex]);
                return names[randomIndex].Trim();
            }


            // 文件没内容就下一步看插件的内容
            // 读取插件中的配置
            string recipients = PluginSettings.CurrentSetting.MailingRecipient;
            if (!string.IsNullOrEmpty(recipients))
            {
                char[] delimiter = { '#' };
                names.AddRange(recipients.Split(delimiter));
                if (names.Count > 0)
                {
                    randomIndex = Others.Random(0, names.Count - 1 );
                    Logger.Log("从插件中选择邮寄对象 > " + names[randomIndex]);
                    return names[randomIndex].Trim();

                }
            }

            // 返回wr的默认设置
            
            //Logger.Log("从WR设置中选择邮寄对象 > " + wManagerSetting.CurrentSetting.MailRecipient.Trim());
            return wManagerSetting.CurrentSetting.MailRecipient.Trim();
        }

        public override bool NeedToRun
        {
            get
            {
                //_recipient = GetRecipient();
                if (!PluginSettings.CurrentSetting.AllowMail
                    || !_stateTimer.IsReady
                    || !_pluginCacheManager.BagsRecorded
                    //|| string.IsNullOrEmpty(_recipient)
                    || _pluginCacheManager.ItemsToMail.Count <= 0
                    || !Main.IsLaunched
                    || _pluginCacheManager.InLoadingScreen
                    || Fight.InFight
                    || _pluginCacheManager.IsInInstance
                    || ObjectManager.Me.IsOnTaxi
                    || !Conditions.InGameAndConnectedAndAliveAndProductStartedNotInPause)
                {
                    return false;
                }

                //_recipient = GetRecipient();
                _nbFreeSlotsOnNeedToRun = _pluginCacheManager.NbFreeSlots;

                //Logger.Log($"{_pluginCacheManager.ItemsToMail.Count} items to mail");

                // Normal
                if (_nbFreeSlotsOnNeedToRun <= MinFreeSlots
                    || _usingDungeonProduct && _pluginCacheManager.ItemsToMail.Count > 5)
                {
                    _mailBox = _memoryDBManager.GetNearestMailBoxFromMe(int.MaxValue);
                    if (_mailBox != null)
                    {
                        DisplayName = $"Sending mail to {_recipient} ({_pluginCacheManager.ItemsToMail.Count} items to send)";
                        return true;
                    }
                }

                // Drive-by
                if (_pluginCacheManager.ItemsToMail.Count > 5)
                {
                    _mailBox = _memoryDBManager.GetNearestMailBoxFromMe(PluginSettings.CurrentSetting.DriveByDistance);
                    if (_mailBox != null)
                    {
                        DisplayName = $"Drive-by mail to {_recipient} ({_pluginCacheManager.ItemsToMail.Count} items to send)";
                        return true;
                    }
                }

                // Drive-by on sell
                if (_pluginCacheManager.ItemsToMail.Count > 0
                    && _pluginCacheManager.ItemsToSell.Count > 5)
                {
                    _mailBox = _memoryDBManager.GetNearestMailBoxFromMe(PluginSettings.CurrentSetting.DriveByDistance);
                    if (_mailBox != null)
                    {
                        DisplayName = $"Drive-by mail to {_recipient} ({_pluginCacheManager.ItemsToMail.Count} items to send)";
                        return true;
                    }
                }

                return false;
            }
        }

        public override void Run()
        {
            Vector3 mailBoxPosition = _mailBox.GameObject.GetSpawnPosition;

            if (ObjectManager.Me.Position.DistanceTo(mailBoxPosition) >= 30)
            {
                Logger.Log(DisplayName);
                GoToTask.ToPosition(mailBoxPosition, 30);
                return;
            }

            if (Helpers.MailboxIsAbsent(_blackListManager, _mailBox))
            {
                return;
            }
            _recipient = GetRecipient();

            Logger.Log($"Mailbox found. Sending mail to {_recipient} ({_pluginCacheManager.ItemsToMail.Count} items)");

            // make stacks by 12
            List<List<WVItem>> mailStacks = new List<List<WVItem>>();
            List<WVItem> bufferMailStack = new List<WVItem>();
            foreach (WVItem item in _pluginCacheManager.ItemsToMail)
            {
                bufferMailStack.Add(item);
                if (bufferMailStack.Count >= 12 || item == _pluginCacheManager.ItemsToMail.Last())
                {
                    mailStacks.Add(new List<WVItem>(bufferMailStack));
                    bufferMailStack.Clear();
                }
            }

            for (int k = 0; k < 5; k++)
            {
                Logger.Log($"Attempt {k + 1}");
                GoToTask.ToPositionAndIntecractWithGameObject(_mailBox.GameObject.GetSpawnPosition, _mailBox.entry);
                Thread.Sleep(1000);
                bool mailFrameDisplayed = Lua.LuaDoString<bool>($" return MailFrameTab2:IsVisible();");                
                if (!mailFrameDisplayed)
                {
                    continue;
                }

                for (int i = 0; i < mailStacks.Count; i++)
                {
                    Logger.Log($"Send stack {i + 1} with {mailStacks[i].Count} items :");
                    GoToTask.ToPositionAndIntecractWithGameObject(_mailBox.GameObject.GetSpawnPosition, _mailBox.entry);
                    Lua.LuaDoString($@"
                        MailFrameTab2:Click();
                        SendMailNameEditBox:SetText(""{_recipient}"");
                        SendMailSubjectEditBox:SetText(""Hey"");
                    ");
                    Thread.Sleep(500);

                    for (int j = 0; j < mailStacks[i].Count; j++)
                    {
                        Lua.LuaDoString($"UseContainerItem({mailStacks[i][j].InBag}, {mailStacks[i][j].InSlot});");
                        Thread.Sleep(300);
                    }

                    int mailCost = Lua.LuaDoString<int>("return GetSendMailPrice();");
                    if (mailCost > _pluginCacheManager.Money)
                    {
                        Logger.LogError($"Not enough money to send mail. Disabling mailing for 15 minutes.");
                        _stateTimer = new Timer(1000 * 60 * 15);
                        return;
                    }

                    Lua.LuaDoString("SendMailMailButton:Click();");
                    Thread.Sleep(1000);
                    Mail.CloseMailFrame();

                    // force sell what we were unable to send
                    foreach (WVItem item in mailStacks[i])
                    {
                        if (_pluginCacheManager.BagItems.Exists(bagItem => bagItem.Entry == item.Entry && bagItem.InBag == item.InBag && bagItem.InSlot == item.InSlot))
                        {
                            Logger.LogError($"Unable to send {item.Name}, removing from mail list.");
                            _pluginCacheManager.SetItemToUnMailable(item);
                        }
                    }
                }

                _stateTimer = new Timer(1000 * 60 * 5);
                return;
            }

            Logger.Log($"Failed to send mail, blacklisting mailbox");
            _blackListManager.AddNPCToBlacklist(_mailBox.entry);
        }

        private List<WoWItemQuality> GetListQualityToMail()
        {
            List<WoWItemQuality> listQualityMail = new List<WoWItemQuality>();

            if (PluginSettings.CurrentSetting.MailGrayItems)
                listQualityMail.Add(WoWItemQuality.Poor);
            if (PluginSettings.CurrentSetting.MailWhiteItems)
                listQualityMail.Add(WoWItemQuality.Common);
            if (PluginSettings.CurrentSetting.MailGreenItems)
                listQualityMail.Add(WoWItemQuality.Uncommon);
            if (PluginSettings.CurrentSetting.MailBlueItems)
                listQualityMail.Add(WoWItemQuality.Rare);
            if (PluginSettings.CurrentSetting.MailPurpleItems)
                listQualityMail.Add(WoWItemQuality.Epic);

            return listQualityMail;
        }
    }
}