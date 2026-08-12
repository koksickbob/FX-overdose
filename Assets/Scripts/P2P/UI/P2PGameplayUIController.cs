using System;
using System.Collections.Generic;
using FXOverdose.P2P.Core;
using FXOverdose.P2P.Infrastructure;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FXOverdose.P2P.UI
{
    /// <summary>기존 GameScene의 화면을 유지하고 입력과 표시 데이터만 P2P 권위 상태로 교체합니다.</summary>
    public sealed class P2PGameplayUIController : MonoBehaviour
    {
        private static readonly Color32 Panel=new(8,20,36,245), Cyan=new(6,182,212,255), Pink=new(255,72,114,255), Text=new(225,242,255,255);
        private TMP_Text price, change, day, time, balance, pnlPct, pnlAmount, health, mental, leverageLabel, marginLabel;
        private TMP_Text leaderboard, eventMessage, resultRanking;
        private Slider marginSlider;
        private RectTransform chartArea;
        private GameObject eventPanel, resultPanel, shopPanel;
        private readonly List<double> prices=new(); private readonly List<GameObject> chartTicks=new();
        private float refresh; private int leverage=10, marginPercent=30;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
        private static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!="GameScene"||P2PNetworkSessionManager.Instance?.IsRunning!=true)return;
            var root=new GameObject(nameof(P2PGameplayUIController));root.AddComponent<P2PGameplayUIController>();
        }

        private void Awake(){DisableSinglePlayerSystems();BindExistingScene();ConfigureExistingControls();BuildMultiplayerOnlyPanels();}
        private void Update(){refresh+=Time.unscaledDeltaTime;if(refresh<.15f)return;refresh=0;RefreshMarket();RefreshPlayer();RefreshCompetition();}

        private static void DisableSinglePlayerSystems()
        {
            DisableAll<GameManager>(); DisableAll<FXOverdose.Trading.MarketSimulationEngine>(); DisableAll<FXOverdose.Trading.TradingController>();
            DisableAll<FXOverdose.UI.Chart.ChartUIController>(); DisableAll<FXOverdose.UI.Chart.TradingPanelUIController>();
            DisableAll<FXOverdose.UI.TopBar.TopStatusBarUIController>(); DisableAll<VitalsValueUI>();
            DisableAll<FXOverdose.Events.ChoiceEventController>(); DisableAll<DynamicShopUI>(); DisableAll<DynamicInventoryUI>();
            DisableAll<ActiveSkillHUDController>(); DisableAll<InventoryItemButton>();
            // P2P에서 허용하지 않는 자동매매, 시간가속, 스킬 UI만 숨깁니다.
            HideNames("Container_AIStyleMode","BtnTabAIStyleMode","ActiveSkillHUD","SkillUpgradePanel","TimeFastForwardButton","FastForwardButton","EnergyDrinkButton");
            // 기존 인벤토리/상점 외형은 유지하되 내용은 P2P 허용 아이템 버튼으로 다시 구성합니다.
            HideNames("ShopPanel");
        }

        private void BindExistingScene()
        {
            price=TextOf("PriceHeaderLabel");change=TextOf("PriceChangeLabel");day=TextOf("DayLabel");time=TextOf("TimeLabel");
            balance=TextOf("BalanceValue");pnlPct=TextOf("PnLPct");pnlAmount=TextOf("PnLAmt");health=TextOf("HealthValue");mental=TextOf("MentalValue");
            leverageLabel=TextOf("LevDisplay");marginLabel=TextOf("MarDisplay");marginSlider=ComponentOf<Slider>("MarginPercentageSlider");
            chartArea=ObjectByName("ChartArea")?.GetComponent<RectTransform>();
        }

        private void ConfigureExistingControls()
        {
            Bind("LongButtonCard",()=>Trade(P2PTradeAction.OpenLong));Bind("ShortButtonCard",()=>Trade(P2PTradeAction.OpenShort));Bind("ClosePositionButton",()=>Trade(P2PTradeAction.ClosePosition));
            Bind("BtnLevMinus",()=>SetLeverage(leverage-5));Bind("BtnLevPlus",()=>SetLeverage(leverage+5));
            Bind("BtnMarMinus",()=>SetMargin(marginPercent-10));Bind("BtnMarPlus",()=>SetMargin(marginPercent+10));
            foreach(int n in new[]{1,5,10,25,50,100,125}){int value=n;Bind("Btn"+n+"x",()=>SetLeverage(value));}
            foreach(int n in new[]{10,25,50,75,100}){int value=n;Bind("BtnMar"+n,()=>SetMargin(value));}
            if(marginSlider!=null){marginSlider.onValueChanged.RemoveAllListeners();marginSlider.onValueChanged.AddListener(v=>SetMargin(Mathf.RoundToInt(v*100)));}
            RebuildItemButton("EnergyDrinkButton","에너지 드링크",()=>Competition(P2PCompetitionAction.UseItem,"energy_drink"),"P2PEnergyDrink");
            AddCareButton("파르페",new Vector2(112,0),()=>Competition(P2PCompetitionAction.UseItem,"dessert"),"P2PDessert","dessert");
            AddCareButton("진정제",new Vector2(224,0),()=>Competition(P2PCompetitionAction.UseItem,"sedative"),"P2PSedative","sedative");
            AddCareButton("영양제",new Vector2(336,0),()=>Competition(P2PCompetitionAction.UseItem,"supplement"),"P2PSupplement","supplement");
            Bind("ShopOpenButton",()=>{if(shopPanel!=null)shopPanel.SetActive(!shopPanel.activeSelf);});
            SetLeverage(10);SetMargin(30);
        }

        private void BuildMultiplayerOnlyPanels()
        {
            Canvas canvas=ObjectByName("TradingViewCanvas")?.GetComponent<Canvas>()??FindAnyObjectByType<Canvas>();if(canvas==null)return;
            var rank=Image(canvas.transform,"P2PLeaderboard",Panel,new Vector2(.76f,.56f),new Vector2(.985f,.88f));
            Label(rank.transform,"Title","P2P RANKING",20,Cyan,new Vector2(.06f,.84f),new Vector2(.94f,.97f),TextAlignmentOptions.Left);
            leaderboard=Label(rank.transform,"Players","연결 대기",15,Text,new Vector2(.06f,.08f),new Vector2(.94f,.82f),TextAlignmentOptions.TopLeft);
            shopPanel=Image(canvas.transform,"P2PShop",Panel,new Vector2(.72f,.2f),new Vector2(.985f,.54f)).gameObject;
            Label(shopPanel.transform,"Title","CARE SHOP",20,Cyan,new Vector2(.07f,.8f),new Vector2(.93f,.95f),TextAlignmentOptions.Left);
            Button(shopPanel.transform,"EnergyDrink","에너지 드링크  $500",new Vector2(.08f,.62f),new Vector2(.92f,.77f),Cyan).onClick.AddListener(()=>Competition(P2PCompetitionAction.BuyItem,"energy_drink"));
            Button(shopPanel.transform,"Dessert","파르페  $600",new Vector2(.08f,.45f),new Vector2(.92f,.6f),Pink).onClick.AddListener(()=>Competition(P2PCompetitionAction.BuyItem,"dessert"));
            Button(shopPanel.transform,"Sedative","진정제  $1,000",new Vector2(.08f,.28f),new Vector2(.92f,.43f),Cyan).onClick.AddListener(()=>Competition(P2PCompetitionAction.BuyItem,"sedative"));
            Button(shopPanel.transform,"Supplement","영양제  $900",new Vector2(.08f,.11f),new Vector2(.92f,.26f),Cyan).onClick.AddListener(()=>Competition(P2PCompetitionAction.BuyItem,"supplement"));shopPanel.SetActive(false);
            eventPanel=Image(canvas.transform,"P2PChoiceEvent",new Color32(3,9,18,248),new Vector2(.28f,.3f),new Vector2(.72f,.7f)).gameObject;
            eventMessage=Label(eventPanel.transform,"EventText","돌발 이벤트",22,Text,new Vector2(.08f,.62f),new Vector2(.92f,.9f),TextAlignmentOptions.Center);
            Button(eventPanel.transform,"Rest","안정 (+체력)",new Vector2(.06f,.14f),new Vector2(.32f,.48f),Cyan).onClick.AddListener(()=>Competition(P2PCompetitionAction.ChooseEvent,"0"));
            Button(eventPanel.transform,"Focus","집중 (+멘탈)",new Vector2(.37f,.14f),new Vector2(.63f,.48f),Cyan).onClick.AddListener(()=>Competition(P2PCompetitionAction.ChooseEvent,"1"));
            Button(eventPanel.transform,"Risk","위험 (+현금)",new Vector2(.68f,.14f),new Vector2(.94f,.48f),Pink).onClick.AddListener(()=>Competition(P2PCompetitionAction.ChooseEvent,"2"));eventPanel.SetActive(false);
            resultPanel=Image(canvas.transform,"P2PResult",new Color32(3,9,18,252),new Vector2(.25f,.18f),new Vector2(.75f,.82f)).gameObject;
            Label(resultPanel.transform,"Title","MATCH RESULT",30,Cyan,new Vector2(.08f,.82f),new Vector2(.92f,.95f),TextAlignmentOptions.Center);
            resultRanking=Label(resultPanel.transform,"Ranking","",20,Text,new Vector2(.12f,.35f),new Vector2(.88f,.78f),TextAlignmentOptions.TopLeft);
            Button(resultPanel.transform,"Rematch","재대결 로비",new Vector2(.1f,.1f),new Vector2(.47f,.28f),Cyan).onClick.AddListener(Rematch);
            Button(resultPanel.transform,"Exit","로비 나가기",new Vector2(.53f,.1f),new Vector2(.9f,.28f),Pink).onClick.AddListener(Exit);resultPanel.SetActive(false);
        }

        private void RefreshMarket()
        {
            var s=P2PNetworkSessionManager.Instance?.MarketAuthority?.CurrentSnapshot??default;if(s.Sequence==0)return;
            if(price!=null)price.text=$"${s.Price:N2}";if(day!=null)day.text="DAY 01";if(time!=null)time.text=$"{s.Hour:00}:{s.Minute:00}";
            if(prices.Count==0||Math.Abs(prices[^1]-s.Price)>.0001){prices.Add(s.Price);if(prices.Count>50)prices.RemoveAt(0);DrawChart();}
            if(change!=null&&prices.Count>1){double d=s.Price-prices[0];change.text=$"{d:+0.00;-0.00;0.00}";change.color=d>=0?Cyan:Pink;}
        }
        private void RefreshPlayer()
        {
            var a=P2PNetworkSessionManager.Instance?.TradingAuthority;if(a==null)return;P2PPlayerTradeSnapshot? mine=null;var lines=new List<string>();
            foreach(var p in a.Players){lines.Add($"#{p.Rank}  {p.Name}   ${p.Equity:N0}\n     {p.ReturnRate:+0.0%;-0.0%;0.0%} · {p.Side} x{p.Leverage}");if(p.PlayerId==SteamRuntimeBootstrap.LocalSteamId)mine=p;}
            if(leaderboard!=null)leaderboard.text=string.Join("\n\n",lines);if(!mine.HasValue)return;var me=mine.Value;
            if(balance!=null)balance.text=$"${me.Equity:N2}";if(pnlPct!=null)pnlPct.text=$"{me.ReturnRate:+0.00%;-0.00%;0.00%}";if(pnlAmount!=null)pnlAmount.text=$"{me.Equity-7000:+$0.00;-$0.00;$0.00}";
        }
        private void RefreshCompetition()
        {
            var x=P2PNetworkSessionManager.Instance?.CompetitionAuthority?.Current;if(x==null)return;P2PCompetitionPlayerSnapshot? mine=null;foreach(var p in x.Players)if(p.PlayerId==SteamRuntimeBootstrap.LocalSteamId)mine=p;
            if(mine.HasValue){var p=mine.Value;if(health!=null)health.text=$"{p.Health:0}/100";if(mental!=null)mental.text=$"{p.Mental:0}/100";SetItemText("P2PEnergyDrink","에너지 드링크",p.EnergyDrink);SetItemText("P2PDessert","파르페",p.Dessert);SetItemText("P2PSedative","진정제",p.Sedative);SetItemText("P2PSupplement","영양제",p.Supplement);}
            eventPanel?.SetActive(x.EventActive);if(eventMessage!=null)eventMessage.text=$"{x.EventTitle}\n{x.EventSecondsLeft:0.0}초 안에 선택 · 미선택 시 무작위";
            resultPanel?.SetActive(x.Finished);if(x.Finished&&resultRanking!=null)resultRanking.text=leaderboard!=null?leaderboard.text:"최종 결과";
        }

        private void DrawChart(){if(chartArea==null||prices.Count<2)return;foreach(var x in chartTicks)Destroy(x);chartTicks.Clear();double min=double.MaxValue,max=double.MinValue;foreach(double p in prices){min=Math.Min(min,p);max=Math.Max(max,p);}double range=Math.Max(1,max-min);for(int i=1;i<prices.Count;i++){Vector2 a=new((i-1f)/(prices.Count-1)*chartArea.rect.width,(float)((prices[i-1]-min)/range)*chartArea.rect.height*.8f+20);Vector2 b=new(i/(float)(prices.Count-1)*chartArea.rect.width,(float)((prices[i]-min)/range)*chartArea.rect.height*.8f+20);var go=new GameObject("P2PChartTick",typeof(RectTransform),typeof(Image));go.transform.SetParent(chartArea,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=Vector2.zero;r.pivot=new Vector2(0,.5f);r.anchoredPosition=a;r.sizeDelta=new Vector2(Vector2.Distance(a,b),3);r.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);go.GetComponent<Image>().color=prices[i]>=prices[i-1]?Cyan:Pink;chartTicks.Add(go);}}
        private void Trade(P2PTradeAction a)=>P2PNetworkSessionManager.Instance?.TradingAuthority?.Submit(a,leverage,a==P2PTradeAction.ClosePosition?0:marginPercent/100d);
        private void Competition(P2PCompetitionAction a,string value)=>P2PNetworkSessionManager.Instance?.CompetitionAuthority?.Submit(a,value);
        private void SetLeverage(int value){int max=SteamLobbyManager.Instance?.CurrentLobby?.Settings.MaximumLeverage??125;leverage=Mathf.Clamp(value,1,max);if(leverageLabel!=null)leverageLabel.text=$"{leverage}x";}
        private void SetMargin(int value){int max=Mathf.RoundToInt((float)(SteamLobbyManager.Instance?.CurrentLobby?.Settings.MaximumMarginRatio??1d)*100);marginPercent=Mathf.Clamp(value,10,max);if(marginLabel!=null)marginLabel.text=$"{marginPercent}%";if(marginSlider!=null)marginSlider.SetValueWithoutNotify(marginPercent/100f);}
        private void Rematch(){SteamLobbyManager.Instance?.ResetMatchForRematch();P2PNetworkSessionManager.Instance?.ShutdownSession();SceneManager.LoadScene("TitleScene");}
        private void Exit(){P2PNetworkSessionManager.Instance?.ShutdownSession();SteamLobbyManager.Instance?.LeaveLobby();SceneManager.LoadScene("TitleScene");}

        private static void RebuildItemButton(string name,string label,UnityEngine.Events.UnityAction action,string resultName){var go=ObjectByName(name);if(go==null)return;go.name=resultName;go.SetActive(true);var b=go.GetComponent<Button>()??go.GetComponentInChildren<Button>(true);if(b==null)return;b.onClick.RemoveAllListeners();b.onClick.AddListener(action);var t=go.GetComponentInChildren<TMP_Text>(true);if(t!=null)t.text=label;}
        private static void AddCareButton(string label,Vector2 offset,UnityEngine.Events.UnityAction action,string resultName,string itemId){var source=ObjectByName("P2PEnergyDrink");if(source==null)return;var clone=Instantiate(source,source.transform.parent);clone.name=resultName;var r=clone.GetComponent<RectTransform>();r.anchoredPosition+=offset;var b=clone.GetComponent<Button>()??clone.GetComponentInChildren<Button>(true);if(b!=null){b.onClick.RemoveAllListeners();b.onClick.AddListener(action);}var t=clone.GetComponentInChildren<TMP_Text>(true);if(t!=null)t.text=label;ApplyOriginalIcon(clone,itemId);}
        private static void ApplyOriginalIcon(GameObject target,string itemId){var inventory=FindAnyObjectByType<Inventory>(FindObjectsInactive.Include);if(inventory==null)return;ItemData item=null;foreach(var slot in inventory.Slots)if(slot?.Item!=null&&slot.Item.ItemId==itemId){item=slot.Item;break;}if(item?.Icon==null)return;foreach(var image in target.GetComponentsInChildren<Image>(true))if(image.sprite!=null){image.sprite=item.Icon;break;}}
        private static void SetItemText(string objectName,string label,int count){var t=ObjectByName(objectName)?.GetComponentInChildren<TMP_Text>(true);if(t!=null)t.text=$"{label}\n×{count}";}
        private static void Bind(string name,UnityEngine.Events.UnityAction action){var go=ObjectByName(name);var b=go!=null?(go.GetComponent<Button>()??go.GetComponentInChildren<Button>(true)):null;if(b==null)return;b.onClick.RemoveAllListeners();b.onClick.AddListener(action);b.interactable=true;}
        private static TMP_Text TextOf(string name)=>ObjectByName(name)?.GetComponent<TMP_Text>()??ObjectByName(name)?.GetComponentInChildren<TMP_Text>(true);
        private static T ComponentOf<T>(string name) where T:Component=>ObjectByName(name)?.GetComponent<T>();
        private static GameObject ObjectByName(string name){foreach(var t in FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(t.name==name)return t.gameObject;return null;}
        private static void HideNames(params string[] names){foreach(string n in names){var x=ObjectByName(n);if(x!=null)x.SetActive(false);}}
        private static void DisableAll<T>() where T:Behaviour{foreach(var x in FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None))x.enabled=false;}
        private static Image Image(Transform p,string n,Color c,Vector2 min,Vector2 max){var g=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));g.transform.SetParent(p,false);var i=g.GetComponent<Image>();i.color=c;Rect(i.rectTransform,min,max);return i;}
        private static TMP_Text Label(Transform p,string n,string s,float z,Color c,Vector2 min,Vector2 max,TextAlignmentOptions a){var g=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));g.transform.SetParent(p,false);var t=g.GetComponent<TMP_Text>();t.text=s;t.fontSize=z;t.color=c;t.alignment=a;t.textWrappingMode=TextWrappingModes.Normal;Rect(t.rectTransform,min,max);return t;}
        private static Button Button(Transform p,string n,string s,Vector2 min,Vector2 max,Color c){var i=Image(p,n,new Color32(7,18,32,255),min,max);var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;var o=i.gameObject.AddComponent<Outline>();o.effectColor=c;o.effectDistance=new Vector2(2,-2);Label(i.transform,"Label",s,16,Text,new Vector2(.02f,.05f),new Vector2(.98f,.95f),TextAlignmentOptions.Center);return b;}
        private static void Rect(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
