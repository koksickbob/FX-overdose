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
        private TMP_Text leaderboard, spectatorLabel;
        private RectTransform leaderboardPanel;
        private Slider marginSlider;
        private readonly List<Button> tradingInputs=new();
        private readonly List<GameObject> dialogueBalloons=new();
        private FXOverdose.UI.Chart.ChartUIController originalChart;
        private FXOverdose.Trading.MarketSimulationEngine originalMarket;
        private GameManager originalGameManager; private TraderStatus originalTraderStatus; private FXOverdose.Trading.TradingController originalTrading;
        private ulong lastChartSequence;
        private int lastChartMinute=-1; private double lastChartVolume;
        private float refresh; private int leverage=10, marginPercent=30;
        private string connectionError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
        private static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!="GameScene"||P2PNetworkSessionManager.Instance?.IsRunning!=true)return;
            var root=new GameObject(nameof(P2PGameplayUIController));root.AddComponent<P2PGameplayUIController>();
        }

        private void Awake(){DisableSinglePlayerSystems();BindExistingScene();BuildMultiplayerOnlyPanels();}
        private IEnumerator Start(){yield return null;ConfigureExistingControls();HideForbiddenUI();RestoreYomiSprite();var n=P2PNetworkSessionManager.Instance;if(n!=null){n.ConnectionFailed-=OnConnectionFailed;n.ConnectionFailed+=OnConnectionFailed;}}
        private void Update(){RefreshOriginalChart();AdjustLeaderboardForDialogue();refresh+=Time.unscaledDeltaTime;if(refresh<.15f)return;refresh=0;RefreshMarket();RefreshPlayer();RefreshCompetition();HideForbiddenUI();RestoreYomiSprite();}

        private static void DisableSinglePlayerSystems()
        {
            DisableAll<FXOverdose.Events.ChoiceEventController>(); DisableAll<ActiveSkillHUDController>();
            DisableAll<FXOverdose.AI.AITradingBrain>(); DisableAll<FXOverdose.AI.MentalDrainGimmickController>();
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
            if(marginSlider!=null){marginSlider.onValueChanged.RemoveAllListeners();marginSlider.onValueChanged.AddListener(v=>SetMargin(Mathf.RoundToInt(marginSlider.maxValue>1.01f?v:v*100f)));}
            SetLeverage(10);SetMargin(30);
        }

        private void BuildMultiplayerOnlyPanels()
        {
            Canvas canvas=ObjectByName("TradingViewCanvas")?.GetComponent<Canvas>()??FindAnyObjectByType<Canvas>();if(canvas==null)return;
            var rank=Image(canvas.transform,"P2PLeaderboard",Panel,new Vector2(.70f,.57f),new Vector2(.985f,.94f));
            leaderboardPanel=rank.rectTransform;
            var outline=rank.gameObject.AddComponent<Outline>();outline.effectColor=Cyan;outline.effectDistance=new Vector2(2,-2);
            Label(rank.transform,"Title","RANK",26,Cyan,new Vector2(.06f,.82f),new Vector2(.94f,.96f),TextAlignmentOptions.Left);
            leaderboard=Label(rank.transform,"Players","연결 대기",19,Text,new Vector2(.06f,.06f),new Vector2(.94f,.80f),TextAlignmentOptions.TopLeft);
            foreach(var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))if(t.name=="DialogueBalloonPanel")dialogueBalloons.Add(t.gameObject);
            var spectator=Image(canvas.transform,"P2PSpectatorPanel",new Color32(8,20,36,225),new Vector2(.35f,.88f),new Vector2(.65f,.96f));
            spectatorLabel=Label(spectator.transform,"Label","관전 중",23,Pink,new Vector2(.03f,.05f),new Vector2(.97f,.95f),TextAlignmentOptions.Center);
            spectator.gameObject.SetActive(false);
        }

        private void AdjustLeaderboardForDialogue()
        {
            if(leaderboardPanel==null)return;
            bool balloonActive=false;
            foreach(var balloon in dialogueBalloons)if(balloon!=null&&balloon.activeInHierarchy){balloonActive=true;break;}
            // 말풍선 출력 중에는 우측 상단을 비우고 순위판을 우측 하단 안전 영역으로 이동합니다.
            Rect(leaderboardPanel,balloonActive?new Vector2(.70f,.03f):new Vector2(.70f,.57f),balloonActive?new Vector2(.985f,.37f):new Vector2(.985f,.94f));
        }

        private void RefreshMarket()
        {
            var s=P2PNetworkSessionManager.Instance?.MarketAuthority?.CurrentSnapshot??default;if(s.Sequence==0)return;
            if(day!=null)day.text="DAY 01";if(time!=null)time.text=$"{s.Hour:00}:{s.Minute:00}";
            originalGameManager?.ApplyP2PState(s.TotalMinutes,mineCash);
        }
        private void RefreshPlayer()
        {
            var a=P2PNetworkSessionManager.Instance?.TradingAuthority;if(a==null)return;P2PPlayerTradeSnapshot? mine=null;var lines=new List<string>();
            foreach(var p in a.Players){lines.Add($"#{p.Rank}  {p.Name}   PnL {p.Equity-7000:+$0;-$0;$0}\n     {p.ReturnRate:+0.0%;-0.0%;0.0%} · {p.Side} x{p.Leverage}");if(p.PlayerId==SteamRuntimeBootstrap.LocalSteamId)mine=p;}
            if(leaderboard!=null)leaderboard.text=string.Join("\n\n",lines);if(!mine.HasValue)return;var me=mine.Value;mineCash=(float)me.Cash;
            originalTrading?.ApplyP2PVisualState(me,(float)(P2PNetworkSessionManager.Instance?.MarketAuthority?.AuthoritativePrice??0));
        }
        private void RefreshCompetition()
        {
            var x=P2PNetworkSessionManager.Instance?.CompetitionAuthority?.Current;if(x==null)return;P2PCompetitionPlayerSnapshot? mine=null;foreach(var p in x.Players)if(p.PlayerId==SteamRuntimeBootstrap.LocalSteamId)mine=p;
            if(mine.HasValue){var p=mine.Value;originalTraderStatus?.ApplyP2PVitals((float)p.Health,(float)p.Mental);if(health!=null)health.text=$"{p.Health:0}/100";if(mental!=null)mental.text=$"{p.Mental:0}/100";SetSpectating(p.IsEliminated,p.Reason);}
        }

        private void Trade(P2PTradeAction a)
        {
            if(a!=P2PTradeAction.ClosePosition&&originalTrading!=null&&originalTrading.IsPlayerTradeOnCooldown)return;
            P2PNetworkSessionManager.Instance?.TradingAuthority?.Submit(a,leverage,a==P2PTradeAction.ClosePosition?0:marginPercent/100d);
        }
        private float mineCash=7000;
        private void RefreshOriginalChart(){var s=P2PNetworkSessionManager.Instance?.MarketAuthority?.CurrentSnapshot??default;if(s.Sequence==0||s.Sequence==lastChartSequence)return;lastChartSequence=s.Sequence;if(s.TotalMinutes!=lastChartMinute){originalGameManager?.ApplyP2PState(s.TotalMinutes,mineCash);lastChartMinute=s.TotalMinutes;lastChartVolume=0;}float volume=(float)Math.Max(0,s.Volume-lastChartVolume);lastChartVolume=s.Volume;originalMarket?.ApplyP2PExternalTick((float)s.Price,(float)s.Bid,(float)s.Ask,volume);}
        private static void HideForbiddenUI()=>HideNames("Container_AIStyleMode","BtnTabAIStyleMode","ActiveSkillHUD","ActiveSkillButtonRow","ActiveSkillInfoOverlay","SkillInfoPanel","SkillUpgradePanel","SkillTimeTransitionOverlay","TimeFastForwardButton","FastForwardButton","Temp_TradingModeToggleBtn");
        private static void RestoreYomiSprite(){var yomi=ObjectByName("ProtagonistCharacterImage");if(yomi==null)return;for(Transform t=yomi.transform;t!=null;t=t.parent)t.gameObject.SetActive(true);var image=yomi.GetComponent<Image>();if(image!=null){image.enabled=true;image.raycastTarget=false;var c=image.color;c.a=1;image.color=c;}yomi.transform.SetAsLastSibling();}
        private void Competition(P2PCompetitionAction a,string value)=>P2PNetworkSessionManager.Instance?.CompetitionAuthority?.Submit(a,value);
        private void SetLeverage(int value){int max=SteamLobbyManager.Instance?.CurrentLobby?.Settings.MaximumLeverage??125;leverage=Mathf.Clamp(value,1,max);if(leverageLabel!=null)leverageLabel.text=$"{leverage}x";}
        private void SetMargin(int value){int max=Mathf.RoundToInt((float)(SteamLobbyManager.Instance?.CurrentLobby?.Settings.MaximumMarginRatio??1d)*100);marginPercent=Mathf.Clamp(value,10,max);if(marginLabel!=null)marginLabel.text=$"{marginPercent}%";if(marginSlider!=null)marginSlider.SetValueWithoutNotify(marginSlider.maxValue>1.01f?marginPercent:marginPercent/100f);}
        private void SetSpectating(bool active,P2PEliminationReason reason){bool show=active||!string.IsNullOrEmpty(connectionError);if(spectatorLabel!=null){spectatorLabel.transform.parent.gameObject.SetActive(show);spectatorLabel.text=!string.IsNullOrEmpty(connectionError)?$"연결 끊김 · {connectionError}":$"관전 중 · {reason}";}foreach(var b in tradingInputs)if(b!=null)b.interactable=!show;if(marginSlider!=null)marginSlider.interactable=!show;}
        private void OnConnectionFailed(string reason){connectionError=string.IsNullOrWhiteSpace(reason)?"호스트와 연결이 종료됐어용":reason;SetSpectating(false,P2PEliminationReason.None);Debug.LogError($"[P2P Gameplay] 연결 종료: {connectionError}");}
        private void Bind(string name,UnityEngine.Events.UnityAction action){var go=ObjectByName(name);var b=go!=null?(go.GetComponent<Button>()??go.GetComponentInChildren<Button>(true)):null;if(b==null)return;b.onClick.RemoveAllListeners();b.onClick.AddListener(action);b.interactable=true;if(!tradingInputs.Contains(b))tradingInputs.Add(b);}
        private static TMP_Text TextOf(string name)=>ObjectByName(name)?.GetComponent<TMP_Text>()??ObjectByName(name)?.GetComponentInChildren<TMP_Text>(true);
        private static T ComponentOf<T>(string name) where T:Component=>ObjectByName(name)?.GetComponent<T>();
        private static GameObject ObjectByName(string name){foreach(var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))if(t.name==name)return t.gameObject;return null;}
        private static void HideNames(params string[] names){foreach(string n in names){var x=ObjectByName(n);if(x!=null)x.SetActive(false);}}
        private static void DisableAll<T>() where T:Behaviour{foreach(var x in FindObjectsByType<T>(FindObjectsInactive.Include))x.enabled=false;}
        private static Image Image(Transform p,string n,Color c,Vector2 min,Vector2 max){var g=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));g.transform.SetParent(p,false);var i=g.GetComponent<Image>();i.color=c;Rect(i.rectTransform,min,max);return i;}
        private static TMP_Text Label(Transform p,string n,string s,float z,Color c,Vector2 min,Vector2 max,TextAlignmentOptions a){var g=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));g.transform.SetParent(p,false);var t=g.GetComponent<TMP_Text>();t.text=s;t.fontSize=z;t.color=c;t.alignment=a;t.textWrappingMode=TextWrappingModes.Normal;Rect(t.rectTransform,min,max);return t;}
        private static Button Button(Transform p,string n,string s,Vector2 min,Vector2 max,Color c){var i=Image(p,n,new Color32(7,18,32,255),min,max);var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;var o=i.gameObject.AddComponent<Outline>();o.effectColor=c;o.effectDistance=new Vector2(2,-2);Label(i.transform,"Label",s,16,Text,new Vector2(.02f,.05f),new Vector2(.98f,.95f),TextAlignmentOptions.Center);return b;}
        private static void Rect(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
        private void OnDestroy(){var n=P2PNetworkSessionManager.Instance;if(n!=null)n.ConnectionFailed-=OnConnectionFailed;}
    }
}
