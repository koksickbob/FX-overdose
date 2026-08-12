using System;
using System.Collections.Generic;
using System.Collections;
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
        private TMP_Text day, time, balance, pnlPct, pnlAmount, health, mental, leverageLabel, marginLabel;
        private TMP_Text leaderboard;
        private Slider marginSlider;
        private FXOverdose.UI.Chart.ChartUIController originalChart;
        private FXOverdose.Trading.MarketSimulationEngine originalMarket;
        private GameManager originalGameManager; private TraderStatus originalTraderStatus; private FXOverdose.Trading.TradingController originalTrading;
        private ulong lastChartSequence;
        private int lastChartMinute=-1; private double lastChartVolume;
        private float refresh; private int leverage=10, marginPercent=30;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
        private static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!="GameScene"||P2PNetworkSessionManager.Instance?.IsRunning!=true)return;
            var root=new GameObject(nameof(P2PGameplayUIController));root.AddComponent<P2PGameplayUIController>();
        }

        private void Awake(){DisableSinglePlayerSystems();BindExistingScene();BuildMultiplayerOnlyPanels();}
        private IEnumerator Start(){yield return null;ConfigureExistingControls();HideForbiddenUI();RestoreYomiSprite();}
        private void Update(){RefreshOriginalChart();refresh+=Time.unscaledDeltaTime;if(refresh<.15f)return;refresh=0;RefreshMarket();RefreshPlayer();RefreshCompetition();HideForbiddenUI();RestoreYomiSprite();}

        private static void DisableSinglePlayerSystems()
        {
            DisableAll<FXOverdose.Events.ChoiceEventController>(); DisableAll<ActiveSkillHUDController>();
            // P2P에서 허용하지 않는 자동매매, 시간가속, 스킬 UI만 숨깁니다.
            HideForbiddenUI();
        }

        private void BindExistingScene()
        {
            day=TextOf("DayLabel");time=TextOf("TimeLabel");
            balance=TextOf("BalanceValue");pnlPct=TextOf("PnLPct");pnlAmount=TextOf("PnLAmt");health=TextOf("HealthValue");mental=TextOf("MentalValue");
            leverageLabel=TextOf("LevDisplay");marginLabel=TextOf("MarDisplay");marginSlider=ComponentOf<Slider>("MarginPercentageSlider");
            originalChart=FindAnyObjectByType<FXOverdose.UI.Chart.ChartUIController>(FindObjectsInactive.Include);
            originalMarket=FindAnyObjectByType<FXOverdose.Trading.MarketSimulationEngine>(FindObjectsInactive.Include);originalMarket?.EnableP2PExternalMode();
            originalGameManager=FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);originalGameManager?.EnableP2PExternalMode();
            originalTraderStatus=TraderStatus.CanonicalInstance??FindAnyObjectByType<TraderStatus>(FindObjectsInactive.Include);originalTraderStatus?.EnableP2PExternalMode();
            originalTrading=FindAnyObjectByType<FXOverdose.Trading.TradingController>(FindObjectsInactive.Include);originalTrading?.EnableP2PExternalMode();
        }

        private void ConfigureExistingControls()
        {
            Bind("LongButtonCard",()=>Trade(P2PTradeAction.OpenLong));Bind("ShortButtonCard",()=>Trade(P2PTradeAction.OpenShort));Bind("ClosePositionButton",()=>Trade(P2PTradeAction.ClosePosition));
            Bind("BtnLevMinus",()=>SetLeverage(leverage-5));Bind("BtnLevPlus",()=>SetLeverage(leverage+5));
            Bind("BtnMarMinus",()=>SetMargin(marginPercent-10));Bind("BtnMarPlus",()=>SetMargin(marginPercent+10));
            foreach(int n in new[]{1,5,10,25,50,100,125}){int value=n;Bind("Btn"+n+"x",()=>SetLeverage(value));}
            foreach(int n in new[]{10,25,50,75,100}){int value=n;Bind("BtnMar"+n,()=>SetMargin(value));}
            if(marginSlider!=null){marginSlider.onValueChanged.RemoveAllListeners();marginSlider.onValueChanged.AddListener(v=>SetMargin(Mathf.RoundToInt(v*100)));}
            SetLeverage(10);SetMargin(30);
        }

        private void BuildMultiplayerOnlyPanels()
        {
            Canvas canvas=ObjectByName("TradingViewCanvas")?.GetComponent<Canvas>()??FindAnyObjectByType<Canvas>();if(canvas==null)return;
            var rank=Image(canvas.transform,"P2PLeaderboard",Panel,new Vector2(.76f,.64f),new Vector2(.985f,.88f));
            Label(rank.transform,"Title","ALL PLAYERS P&L",20,Cyan,new Vector2(.06f,.8f),new Vector2(.94f,.96f),TextAlignmentOptions.Left);
            leaderboard=Label(rank.transform,"Players","연결 대기",15,Text,new Vector2(.06f,.08f),new Vector2(.94f,.78f),TextAlignmentOptions.TopLeft);
        }

        private void RefreshMarket()
        {
            var s=P2PNetworkSessionManager.Instance?.MarketAuthority?.CurrentSnapshot??default;if(s.Sequence==0)return;
            if(day!=null)day.text="DAY 01";if(time!=null)time.text=$"{s.Hour:00}:{s.Minute:00}";
            originalGameManager?.ApplyP2PState(s.TotalMinutes,mineEquity);
        }
        private void RefreshPlayer()
        {
            var a=P2PNetworkSessionManager.Instance?.TradingAuthority;if(a==null)return;P2PPlayerTradeSnapshot? mine=null;var lines=new List<string>();
            foreach(var p in a.Players){lines.Add($"#{p.Rank}  {p.Name}   ${p.Equity:N0}\n     {p.ReturnRate:+0.0%;-0.0%;0.0%} · {p.Side} x{p.Leverage}");if(p.PlayerId==SteamRuntimeBootstrap.LocalSteamId)mine=p;}
            if(leaderboard!=null)leaderboard.text=string.Join("\n\n",lines);if(!mine.HasValue)return;var me=mine.Value;mineEquity=(float)me.Equity;
            if(balance!=null)balance.text=$"${me.Equity:N2}";if(pnlPct!=null)pnlPct.text=$"{me.ReturnRate:+0.00%;-0.00%;0.00%}";if(pnlAmount!=null)pnlAmount.text=$"{me.Equity-7000:+$0.00;-$0.00;$0.00}";
            originalTrading?.ApplyP2PVisualState(me,(float)(P2PNetworkSessionManager.Instance?.MarketAuthority?.AuthoritativePrice??0));
        }
        private void RefreshCompetition()
        {
            var x=P2PNetworkSessionManager.Instance?.CompetitionAuthority?.Current;if(x==null)return;P2PCompetitionPlayerSnapshot? mine=null;foreach(var p in x.Players)if(p.PlayerId==SteamRuntimeBootstrap.LocalSteamId)mine=p;
            if(mine.HasValue){var p=mine.Value;originalTraderStatus?.ApplyP2PVitals((float)p.Health,(float)p.Mental);if(health!=null)health.text=$"{p.Health:0}/100";if(mental!=null)mental.text=$"{p.Mental:0}/100";}
        }

        private void Trade(P2PTradeAction a)
        {
            if(a!=P2PTradeAction.ClosePosition&&originalTrading!=null&&originalTrading.IsPlayerTradeOnCooldown)return;
            P2PNetworkSessionManager.Instance?.TradingAuthority?.Submit(a,leverage,a==P2PTradeAction.ClosePosition?0:marginPercent/100d);
        }
        private float mineEquity=7000;
        private void RefreshOriginalChart(){var s=P2PNetworkSessionManager.Instance?.MarketAuthority?.CurrentSnapshot??default;if(s.Sequence==0||s.Sequence==lastChartSequence)return;lastChartSequence=s.Sequence;if(s.TotalMinutes!=lastChartMinute){originalGameManager?.ApplyP2PState(s.TotalMinutes,mineEquity);lastChartMinute=s.TotalMinutes;lastChartVolume=0;}float volume=(float)Math.Max(0,s.Volume-lastChartVolume);lastChartVolume=s.Volume;originalMarket?.ApplyP2PExternalTick((float)s.Price,(float)s.Bid,(float)s.Ask,volume);}
        private static void HideForbiddenUI()=>HideNames("Container_AIStyleMode","BtnTabAIStyleMode","ActiveSkillHUD","ActiveSkillButtonRow","ActiveSkillInfoOverlay","SkillInfoPanel","SkillUpgradePanel","SkillTimeTransitionOverlay","TimeFastForwardButton","FastForwardButton");
        private static void RestoreYomiSprite(){var yomi=ObjectByName("ProtagonistCharacterImage");if(yomi==null)return;for(Transform t=yomi.transform;t!=null;t=t.parent)t.gameObject.SetActive(true);var image=yomi.GetComponent<Image>();if(image!=null){image.enabled=true;image.raycastTarget=false;var c=image.color;c.a=1;image.color=c;}yomi.transform.SetAsLastSibling();}
        private void Competition(P2PCompetitionAction a,string value)=>P2PNetworkSessionManager.Instance?.CompetitionAuthority?.Submit(a,value);
        private void SetLeverage(int value){int max=SteamLobbyManager.Instance?.CurrentLobby?.Settings.MaximumLeverage??125;leverage=Mathf.Clamp(value,1,max);if(leverageLabel!=null)leverageLabel.text=$"{leverage}x";}
        private void SetMargin(int value){int max=Mathf.RoundToInt((float)(SteamLobbyManager.Instance?.CurrentLobby?.Settings.MaximumMarginRatio??1d)*100);marginPercent=Mathf.Clamp(value,10,max);if(marginLabel!=null)marginLabel.text=$"{marginPercent}%";if(marginSlider!=null)marginSlider.SetValueWithoutNotify(marginPercent/100f);}
        private static void Bind(string name,UnityEngine.Events.UnityAction action){var go=ObjectByName(name);var b=go!=null?(go.GetComponent<Button>()??go.GetComponentInChildren<Button>(true)):null;if(b==null)return;b.onClick.RemoveAllListeners();b.onClick.AddListener(action);b.interactable=true;}
        private static TMP_Text TextOf(string name)=>ObjectByName(name)?.GetComponent<TMP_Text>()??ObjectByName(name)?.GetComponentInChildren<TMP_Text>(true);
        private static T ComponentOf<T>(string name) where T:Component=>ObjectByName(name)?.GetComponent<T>();
        private static GameObject ObjectByName(string name){foreach(var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))if(t.name==name)return t.gameObject;return null;}
        private static void HideNames(params string[] names){foreach(string n in names){var x=ObjectByName(n);if(x!=null)x.SetActive(false);}}
        private static void DisableAll<T>() where T:Behaviour{foreach(var x in FindObjectsByType<T>(FindObjectsInactive.Include))x.enabled=false;}
        private static Image Image(Transform p,string n,Color c,Vector2 min,Vector2 max){var g=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));g.transform.SetParent(p,false);var i=g.GetComponent<Image>();i.color=c;Rect(i.rectTransform,min,max);return i;}
        private static TMP_Text Label(Transform p,string n,string s,float z,Color c,Vector2 min,Vector2 max,TextAlignmentOptions a){var g=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));g.transform.SetParent(p,false);var t=g.GetComponent<TMP_Text>();t.text=s;t.fontSize=z;t.color=c;t.alignment=a;t.textWrappingMode=TextWrappingModes.Normal;Rect(t.rectTransform,min,max);return t;}
        private static Button Button(Transform p,string n,string s,Vector2 min,Vector2 max,Color c){var i=Image(p,n,new Color32(7,18,32,255),min,max);var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;var o=i.gameObject.AddComponent<Outline>();o.effectColor=c;o.effectDistance=new Vector2(2,-2);Label(i.transform,"Label",s,16,Text,new Vector2(.02f,.05f),new Vector2(.98f,.95f),TextAlignmentOptions.Center);return b;}
        private static void Rect(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
