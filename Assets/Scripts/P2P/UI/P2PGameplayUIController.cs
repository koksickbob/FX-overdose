using System;
using System.Collections.Generic;
using System.Collections;
using FXOverdose.P2P.Core;
using FXOverdose.P2P.Infrastructure;
using FXOverdose.Events;
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
        private TMP_Text leaderboard, spectatorLabel, eventTitle, eventTimer, resultPlayers;
        private RectTransform leaderboardPanel;
        private RectTransform characterLevelHudRect;
        private GameObject eventPanel, resultPanel, spectatorPanel;
        private Slider marginSlider;
        private readonly List<Button> tradingInputs=new();
        private readonly List<GameObject> dialogueBalloons=new();
        private FXOverdose.UI.Chart.ChartUIController originalChart;
        private ChoiceEventPopupUIController originalEventPopup;
        private ChoiceEventSO p2pEventView;
        private int shownEventId=-1;
        private FXOverdose.Trading.MarketSimulationEngine originalMarket;
        private FXOverdose.UI.Chart.TradingPanelUIController originalTradingPanel;
        private ShopManager originalShop;
        private GameManager originalGameManager; private TraderStatus originalTraderStatus; private FXOverdose.Trading.TradingController originalTrading;
        private ulong lastChartSequence;
        private bool p2pChartHistoryPrepared;
        private int lastChartMinute=-1; private double lastChartVolume;
        private float refresh; private int leverage=10, marginPercent=30;
        private string connectionError;
        private bool resultShown;
        private bool leaderboardHasRoom=true;
        private bool localEliminated;
        private ulong spectatedPlayerId;
        private P2PPlayerTradeSnapshot? displayedTradeState;
        private int lastEnergy=-1,lastDessert=-1,lastSedative=-1,lastSupplement=-1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
        private static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!="GameScene"||P2PNetworkSessionManager.Instance?.IsRunning!=true)return;
            var root=new GameObject(nameof(P2PGameplayUIController));root.AddComponent<P2PGameplayUIController>();
        }

        private void Awake(){DisableSinglePlayerSystems();BindExistingScene();BuildMultiplayerOnlyPanels();}
        private IEnumerator Start(){yield return null;EnsureEventPopup();ConfigureExistingControls();HideForbiddenUI();RestoreYomiSprite();var n=P2PNetworkSessionManager.Instance;if(n!=null){n.ConnectionFailed-=OnConnectionFailed;n.ConnectionFailed+=OnConnectionFailed;}}
        private void Update(){RefreshOriginalChart();AdjustLeaderboardForDialogue();refresh+=Time.unscaledDeltaTime;if(refresh<.15f)return;refresh=0;RefreshMarket();RefreshCompetition();RefreshPlayer();HideForbiddenUI();RestoreYomiSprite();}

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
            originalEventPopup=FindActiveEventPopup();
            originalMarket=FindAnyObjectByType<FXOverdose.Trading.MarketSimulationEngine>(FindObjectsInactive.Include);originalMarket?.EnableP2PExternalMode();
            originalGameManager=FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);originalGameManager?.EnableP2PExternalMode();
            originalTraderStatus=TraderStatus.CanonicalInstance??FindAnyObjectByType<TraderStatus>(FindObjectsInactive.Include);originalTraderStatus?.EnableP2PExternalMode();
            originalTrading=FindAnyObjectByType<FXOverdose.Trading.TradingController>(FindObjectsInactive.Include);originalTrading?.EnableP2PExternalMode();
            originalShop=FindAnyObjectByType<ShopManager>(FindObjectsInactive.Include);
            originalTradingPanel=FindAnyObjectByType<FXOverdose.UI.Chart.TradingPanelUIController>(FindObjectsInactive.Include);originalTradingPanel?.EnableP2PExternalMode();
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
            // 보스전 상태판과 동일한 우측 상단 고정 슬롯을 사용합니다.
            var rank=Image(canvas.transform,"P2PLeaderboard",new Color32(7,17,31,255),Vector2.zero,Vector2.one);
            leaderboardPanel=rank.rectTransform;
            leaderboardPanel.anchorMin=leaderboardPanel.anchorMax=new Vector2(1f,1f);
            leaderboardPanel.pivot=new Vector2(1f,1f);
            leaderboardPanel.sizeDelta=new Vector2(460f,195f);
            leaderboardPanel.anchoredPosition=new Vector2(-16f,-120f);
            var outline=rank.gameObject.AddComponent<Outline>();outline.effectColor=Cyan;outline.effectDistance=new Vector2(2,-2);
            Label(rank.transform,"Title","RANK",21,Cyan,new Vector2(.05f,.72f),new Vector2(.95f,.94f),TextAlignmentOptions.Left);
            leaderboard=Label(rank.transform,"Players","연결 대기",20,Text,new Vector2(.05f,.08f),new Vector2(.95f,.70f),TextAlignmentOptions.TopLeft);
            foreach(var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))if(t.name=="DialogueBalloonPanel")dialogueBalloons.Add(t.gameObject);
            var spectator=Image(canvas.transform,"P2PSpectatorPanel",new Color32(8,20,36,225),new Vector2(.31f,.87f),new Vector2(.69f,.965f));
            spectatorPanel=spectator.gameObject;
            spectatorLabel=Label(spectator.transform,"Label","관전 중",23,Pink,new Vector2(.18f,.05f),new Vector2(.82f,.95f),TextAlignmentOptions.Center);
            Button(spectator.transform,"PreviousPlayer","◀",new Vector2(.02f,.12f),new Vector2(.16f,.88f),Cyan).onClick.AddListener(()=>ChangeSpectatedPlayer(-1));
            Button(spectator.transform,"NextPlayer","▶",new Vector2(.84f,.12f),new Vector2(.98f,.88f),Cyan).onClick.AddListener(()=>ChangeSpectatedPlayer(1));
            spectatorPanel.SetActive(false);

            // 이벤트 화면과 제한 시간 표시는 원본 FX WIRE 팝업 내부에서 처리합니다.

            resultPanel=Image(canvas.transform,"MultiplayerResult",new Color32(2,8,18,252),new Vector2(.20f,.14f),new Vector2(.80f,.86f)).gameObject;
            Label(resultPanel.transform,"Title","MATCH RESULT",34,Cyan,new Vector2(.08f,.82f),new Vector2(.92f,.95f),TextAlignmentOptions.Center);
            resultPlayers=Label(resultPanel.transform,"Players","결과 집계 중",22,Text,new Vector2(.10f,.27f),new Vector2(.90f,.79f),TextAlignmentOptions.TopLeft);
            Button(resultPanel.transform,"Exit","타이틀로 나가기",new Vector2(.30f,.08f),new Vector2(.70f,.19f),Pink).onClick.AddListener(ExitToTitle);
            resultPanel.SetActive(false);
        }

        private void AdjustLeaderboardForDialogue()
        {
            if(leaderboardPanel==null)return;
            AlignLeaderboardLikeBossHud();
            // 말풍선과 영역을 분리했으므로 동시에 표시해도 서로 가리지 않습니다.
            leaderboardPanel.gameObject.SetActive(!resultShown&&leaderboardHasRoom);
        }

        private void AlignLeaderboardLikeBossHud()
        {
            if(leaderboardPanel==null)return;
            if(characterLevelHudRect==null)characterLevelHudRect=ObjectByName("CharacterLevelExpHUD")?.GetComponent<RectTransform>();
            RectTransform parent=leaderboardPanel.parent as RectTransform;
            if(characterLevelHudRect==null||parent==null){leaderboardHasRoom=true;leaderboardPanel.sizeDelta=new Vector2(460f,195f);leaderboardPanel.anchoredPosition=new Vector2(-16f,-120f);return;}

            // 상단 상태 HUD의 실제 하단보다 12px 아래에서 시작합니다.
            Vector3[] corners=new Vector3[4];characterLevelHudRect.GetWorldCorners(corners);
            float panelTop=parent.InverseTransformPoint(corners[0]).y-12f;

            // 씬에 말풍선 프리셋이 둘 이상 있어도 가장 낮은 상단 경계를 사용해 모두 피합니다.
            float balloonTop=float.PositiveInfinity;
            foreach(GameObject balloon in dialogueBalloons)
            {
                RectTransform rect=balloon!=null?balloon.GetComponent<RectTransform>():null;if(rect==null)continue;
                Vector3[] balloonCorners=new Vector3[4];rect.GetWorldCorners(balloonCorners);
                float candidate=parent.InverseTransformPoint(balloonCorners[2]).y;
                if(candidate<panelTop)balloonTop=Mathf.Min(balloonTop,candidate);
            }

            const float gap=12f;
            float available=float.IsPositiveInfinity(balloonTop)?195f:panelTop-balloonTop-gap;
            leaderboardHasRoom=available>=105f;
            float panelHeight=Mathf.Clamp(available,105f,195f);
            leaderboardPanel.sizeDelta=new Vector2(460f,panelHeight);
            leaderboardPanel.anchoredPosition=new Vector2(-16f,panelTop-parent.rect.yMax);
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
            foreach(var p in a.Players)
            {
                string line=$"#{p.Rank} {EscapeRichText(p.Name)}  PnL {p.Equity-7000:+$0;-$0;$0}";
                // 현재 1위는 다른 순위보다 크게 표시하고 골드 컬러로 강조합니다.
                if(p.Rank==1)line=$"<size=125%><color=#FACC15><b>{line}</b></color></size>";
                lines.Add(line);
                if(p.PlayerId==SteamRuntimeBootstrap.LocalSteamId)mine=p;
            }
            if(leaderboard!=null)leaderboard.text=string.Join("\n",lines);if(!mine.HasValue)return;
            P2PPlayerTradeSnapshot shown=mine.Value;
            if(localEliminated)
            {
                EnsureSpectatedPlayer(a.Players);
                foreach(var p in a.Players)if(p.PlayerId==spectatedPlayerId){shown=p;break;}
            }
            mineCash=(float)shown.Cash;
            displayedTradeState=shown;
            originalTrading?.ApplyP2PVisualState(shown,(float)(P2PNetworkSessionManager.Instance?.MarketAuthority?.AuthoritativePrice??0));
            UpdateSpectatorLabel(shown.Name);
        }
        private static string EscapeRichText(string value)=>string.IsNullOrEmpty(value)?string.Empty:value.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");
        private void RefreshCompetition()
        {
            var x=P2PNetworkSessionManager.Instance?.CompetitionAuthority?.Current;if(x==null)return;P2PCompetitionPlayerSnapshot? mine=null;foreach(var p in x.Players)if(p.PlayerId==SteamRuntimeBootstrap.LocalSteamId)mine=p;
            if(mine.HasValue){var p=mine.Value;SyncLocalInventory(p);localEliminated=p.IsEliminated;var shown=p;if(localEliminated&&spectatedPlayerId!=0)foreach(var candidate in x.Players)if(candidate.PlayerId==spectatedPlayerId&&!candidate.IsEliminated){shown=candidate;break;}originalTraderStatus?.ApplyP2PVitals((float)shown.Health,(float)shown.Mental);if(health!=null)health.text=$"{shown.Health:0}/100";if(mental!=null)mental.text=$"{shown.Mental:0}/100";SetSpectating(p.IsEliminated,p.Reason);PlayConsumedItemVisual(p);}
            if(x.EventActive)originalEventPopup?.SetNetworkCountdown(x.EventSecondsLeft);
            if(x.EventActive&&shownEventId!=x.EventId)ShowOriginalChoiceEvent(x);
            else if(!x.EventActive&&shownEventId>=0){originalEventPopup?.Hide();shownEventId=-1;}
            if(x.Finished&&!resultShown)ShowResult();
        }

        private void ShowOriginalChoiceEvent(P2PCompetitionSnapshot snapshot)
        {
            EnsureEventPopup();
            if(originalEventPopup==null)return;
            if(p2pEventView!=null)Destroy(p2pEventView);
            p2pEventView=ScriptableObject.CreateInstance<ChoiceEventSO>();
            p2pEventView.EventID=snapshot.EventKey;
            p2pEventView.ScenarioTitle=snapshot.EventTitle;
            p2pEventView.ScenarioDescription=snapshot.EventDescription;
            p2pEventView.AIMonologue=snapshot.EventMonologue;
            p2pEventView.Options=new[]
            {
                new ChoiceOptionData{OptionType=(ChoiceOptionType)snapshot.Choice1Type,OptionTitle=snapshot.Choice1Title,Description=snapshot.Choice1Description},
                new ChoiceOptionData{OptionType=(ChoiceOptionType)snapshot.Choice2Type,OptionTitle=snapshot.Choice2Title,Description=snapshot.Choice2Description}
            };
            shownEventId=snapshot.EventId;
            originalEventPopup.Show(p2pEventView,index=>
            {
                if(P2PChoiceRules.IsValid(index))Competition(P2PCompetitionAction.ChooseEvent,index.ToString());
                originalEventPopup.Hide();
            });
        }

        /// <summary>
        /// 비활성 프리셋에 Show를 호출하면 이벤트 효과만 적용되고 팝업 코루틴은 실행되지 않습니다.
        /// 항상 활성 컨트롤러를 사용하고, 씬에 없다면 P2P용 팝업 호스트를 생성합니다.
        /// </summary>
        private void EnsureEventPopup()
        {
            if(originalEventPopup!=null&&originalEventPopup.isActiveAndEnabled)return;

            originalEventPopup=FindActiveEventPopup();
            if(originalEventPopup!=null)return;

            GameObject popupHost=new("P2PChoiceEventPopupHost");
            popupHost.transform.SetParent(transform,false);
            originalEventPopup=popupHost.AddComponent<ChoiceEventPopupUIController>();
            Debug.Log("[P2P Gameplay] 활성 돌발 이벤트 UI가 없어 P2P 전용 팝업을 생성했어용.");
        }

        private static ChoiceEventPopupUIController FindActiveEventPopup()
        {
            foreach(var popup in FindObjectsByType<ChoiceEventPopupUIController>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
            {
                if(popup!=null&&popup.isActiveAndEnabled)return popup;
            }
            return null;
        }

        private void PlayConsumedItemVisual(P2PCompetitionPlayerSnapshot p)
        {
            if(lastEnergy>=0){string used=p.EnergyDrink<lastEnergy?"energy_drink":p.Dessert<lastDessert?"dessert":p.Sedative<lastSedative?"sedative":p.Supplement<lastSupplement?"supplement":null;if(used!=null){FindAnyObjectByType<FXOverdose.AI.AIVisualController>(FindObjectsInactive.Include)?.ShowItemUse(used,1f);FindAnyObjectByType<FXOverdose.AI.YomiSpriteController>(FindObjectsInactive.Include)?.ShowItemUse(used,1f);}}
            lastEnergy=p.EnergyDrink;lastDessert=p.Dessert;lastSedative=p.Sedative;lastSupplement=p.Supplement;
        }

        private void SyncLocalInventory(P2PCompetitionPlayerSnapshot state)
        {
            if(originalShop?.Inventory==null)return;
            foreach(ItemData item in originalShop.CatalogItems)
            {
                if(item==null)continue;
                int quantity=item.ItemId switch
                {
                    "energy_drink"=>state.EnergyDrink,
                    "dessert"=>state.Dessert,
                    "sedative"=>state.Sedative,
                    "supplement"=>state.Supplement,
                    _=>-1
                };
                if(quantity>=0)originalShop.Inventory.ApplyNetworkQuantity(item,quantity);
            }
        }

        private void ShowResult()
        {
            resultShown=true;if(eventPanel!=null)eventPanel.SetActive(false);if(resultPanel==null)return;resultPanel.SetActive(true);resultPanel.transform.SetAsLastSibling();
            var players=P2PNetworkSessionManager.Instance?.TradingAuthority?.Players;var lines=new List<string>();if(players!=null)foreach(var p in players)lines.Add($"#{p.Rank}   {p.Name}\n      PnL {p.Equity-7000:+$0.00;-$0.00;$0.00}     수익률 {p.ReturnRate:+0.00%;-0.00%;0.00%}");resultPlayers.text=string.Join("\n\n",lines);
        }

        private void ExitToTitle(){P2PNetworkSessionManager.Instance?.ShutdownSession();SteamLobbyManager.Instance?.LeaveLobby();SceneManager.LoadScene("TitleScene");}

        private void Trade(P2PTradeAction a)
        {
            if(a!=P2PTradeAction.ClosePosition&&originalTrading!=null&&originalTrading.IsPlayerTradeOnCooldown)return;
            P2PNetworkSessionManager.Instance?.TradingAuthority?.Submit(a,leverage,a==P2PTradeAction.ClosePosition?0:marginPercent/100d);
        }
        private float mineCash=7000;
        private void RefreshOriginalChart(){var s=P2PNetworkSessionManager.Instance?.MarketAuthority?.CurrentSnapshot??default;if(s.Sequence==0||s.Sequence==lastChartSequence)return;if(!p2pChartHistoryPrepared&&originalMarket!=null){originalMarket.PrepareP2PChartHistory(s.Seed,(float)s.Price);p2pChartHistoryPrepared=true;}lastChartSequence=s.Sequence;if(s.TotalMinutes!=lastChartMinute){originalGameManager?.ApplyP2PState(s.TotalMinutes,mineCash);lastChartMinute=s.TotalMinutes;lastChartVolume=0;}float volume=(float)Math.Max(0,s.Volume-lastChartVolume);lastChartVolume=s.Volume;originalMarket?.ApplyP2PExternalTick((float)s.Price,(float)s.Bid,(float)s.Ask,volume,(float)s.High,(float)s.Low);if(displayedTradeState.HasValue)originalTrading?.ApplyP2PVisualState(displayedTradeState.Value,(float)s.Price);}
        private static void HideForbiddenUI()=>HideNames("Container_AIStyleMode","BtnTabAIStyleMode","ActiveSkillHUD","ActiveSkillButtonRow","ActiveSkillInfoOverlay","SkillInfoPanel","SkillUpgradePanel","SkillTimeTransitionOverlay","TimeFastForwardButton","FastForwardButton","Temp_TradingModeToggleBtn");
        private static void RestoreYomiSprite(){var yomi=ObjectByName("ProtagonistCharacterImage");if(yomi==null)return;for(Transform t=yomi.transform;t!=null;t=t.parent)t.gameObject.SetActive(true);var image=yomi.GetComponent<Image>();if(image!=null){image.enabled=true;image.raycastTarget=false;var c=image.color;c.a=1;image.color=c;}yomi.transform.SetAsLastSibling();}
        private void Competition(P2PCompetitionAction a,string value)=>P2PNetworkSessionManager.Instance?.CompetitionAuthority?.Submit(a,value);
        private void SetLeverage(int value){int max=SteamLobbyManager.Instance?.CurrentLobby?.Settings.MaximumLeverage??125;leverage=Mathf.Clamp(value,1,max);if(leverageLabel!=null)leverageLabel.text=$"{leverage}x";}
        private void SetMargin(int value){int max=Mathf.RoundToInt((float)(SteamLobbyManager.Instance?.CurrentLobby?.Settings.MaximumMarginRatio??1d)*100);marginPercent=Mathf.Clamp(value,10,max);if(marginLabel!=null)marginLabel.text=$"{marginPercent}%";if(marginSlider!=null)marginSlider.SetValueWithoutNotify(marginSlider.maxValue>1.01f?marginPercent:marginPercent/100f);}
        private void SetSpectating(bool active,P2PEliminationReason reason){bool show=active||!string.IsNullOrEmpty(connectionError);if(spectatorPanel!=null)spectatorPanel.SetActive(show);if(spectatorLabel!=null&&!string.IsNullOrEmpty(connectionError))spectatorLabel.text=$"연결 끊김 · {connectionError}";originalTradingPanel?.SetP2PSpectating(show);foreach(var b in tradingInputs)if(b!=null)b.interactable=!show;if(marginSlider!=null)marginSlider.interactable=!show;if(!active)spectatedPlayerId=0;}
        private void EnsureSpectatedPlayer(IReadOnlyList<P2PPlayerTradeSnapshot> players)
        {
            if(IsAliveSpectatorTarget(spectatedPlayerId))return;
            spectatedPlayerId=0;
            foreach(var p in players)if(p.PlayerId!=SteamRuntimeBootstrap.LocalSteamId&&IsAliveSpectatorTarget(p.PlayerId)){spectatedPlayerId=p.PlayerId;break;}
        }
        private bool IsAliveSpectatorTarget(ulong id)
        {
            if(id==0||id==SteamRuntimeBootstrap.LocalSteamId)return false;
            var state=P2PNetworkSessionManager.Instance?.CompetitionAuthority?.Current;if(state==null)return false;
            foreach(var p in state.Players)if(p.PlayerId==id)return !p.IsEliminated;
            return false;
        }
        private void ChangeSpectatedPlayer(int direction)
        {
            if(!localEliminated)return;
            var players=P2PNetworkSessionManager.Instance?.TradingAuthority?.Players;if(players==null)return;
            var targets=new List<ulong>();foreach(var p in players)if(IsAliveSpectatorTarget(p.PlayerId))targets.Add(p.PlayerId);
            if(targets.Count==0){spectatedPlayerId=0;return;}
            int index=targets.IndexOf(spectatedPlayerId);if(index<0)index=0;else index=(index+direction+targets.Count)%targets.Count;spectatedPlayerId=targets[index];
        }
        private void UpdateSpectatorLabel(string observedName)
        {
            if(!localEliminated||spectatorLabel==null||!string.IsNullOrEmpty(connectionError))return;
            spectatorLabel.text=spectatedPlayerId==0?"관전할 생존자가 없어용":$"관전 중 · {observedName}";
        }
        private void OnConnectionFailed(string reason)
        {
            connectionError=string.IsNullOrWhiteSpace(reason)?"호스트와 연결이 종료됐어용":reason;
            resultShown=true;
            SetSpectating(false,P2PEliminationReason.None);
            foreach(var button in tradingInputs)if(button!=null)button.interactable=false;
            if(resultPanel!=null)
            {
                resultPanel.SetActive(true);resultPanel.transform.SetAsLastSibling();
                TMP_Text title=resultPanel.transform.Find("Title")?.GetComponent<TMP_Text>();
                if(title!=null){title.text="MATCH INVALID";title.color=Pink;}
                if(resultPlayers!=null)resultPlayers.text=$"호스트 연결이 종료되어 경기 결과를 무효 처리했어용.\n\n{connectionError}";
            }
            Debug.LogError($"[P2P Gameplay][{P2PNetworkSessionManager.Instance?.MatchId}] 경기 무효: {connectionError}");
        }
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
