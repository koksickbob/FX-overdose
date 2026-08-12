using System.Collections.Generic;
using FXOverdose.P2P.Core;
using FXOverdose.P2P.Infrastructure;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FXOverdose.P2P.UI
{
    /// <summary>싱글 시스템을 실행하지 않고 P2P 권위 상태만 표시하는 실제 경기 화면입니다.</summary>
    public sealed class P2PGameplayUIController : MonoBehaviour
    {
        private static readonly Color32 Navy = new(5, 13, 25, 255), Panel = new(11, 27, 46, 250);
        private static readonly Color32 Cyan = new(6, 182, 212, 255), Pink = new(255, 72, 114, 255);
        private static readonly Color32 Text = new(225, 242, 255, 255), Muted = new(126, 160, 184, 255);
        private TMP_Text priceText, timeText, standingsText, positionText, eventText, vitalsText, inventoryText, messageText;
        private GameObject eventPanel, resultPanel;
        private RectTransform chart;
        private readonly List<double> prices = new();
        private readonly List<GameObject> chartLines = new();
        private float refreshTimer;
        private int leverage = 20;
        private double margin = 0.5;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() { SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded; }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "GameScene" || P2PNetworkSessionManager.Instance?.IsRunning != true) return;
            foreach (var gameManager in FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) gameManager.enabled = false;
            foreach (var engine in FindObjectsByType<FXOverdose.Trading.MarketSimulationEngine>(FindObjectsInactive.Include, FindObjectsSortMode.None)) engine.enabled = false;
            foreach (var trader in FindObjectsByType<FXOverdose.Trading.TradingController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) trader.enabled = false;
            foreach (var choice in FindObjectsByType<FXOverdose.Events.ChoiceEventController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) choice.enabled = false;
            var root = new GameObject("P2PGameplayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 9000;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            root.AddComponent<P2PGameplayUIController>();
        }

        private void Awake() => Build();
        private void Update()
        {
            refreshTimer += Time.unscaledDeltaTime; if (refreshTimer < 0.2f) return; refreshTimer = 0;
            var market = P2PNetworkSessionManager.Instance?.MarketAuthority?.CurrentSnapshot ?? default;
            if (market.Sequence > 0)
            {
                priceText.text = $"BTC / USD   {market.Price:N1}"; timeText.text = $"DAY 1   {market.Hour:00}:{market.Minute:00}";
                if (prices.Count == 0 || prices[^1] != market.Price) { prices.Add(market.Price); if (prices.Count > 80) prices.RemoveAt(0); DrawChart(); }
            }
            RefreshTrading();
            RefreshCompetition();
        }

        private void Build()
        {
            Image bg = Image(transform, "Background", Navy, Vector2.zero, Vector2.one);
            Image header = Image(bg.transform, "Header", Panel, new Vector2(0.02f, .91f), new Vector2(.98f, .98f));
            priceText = Label(header.transform, "Price", "BTC / USD", 30, Text, new Vector2(.02f, .1f), new Vector2(.45f, .9f), TextAlignmentOptions.Left);
            timeText = Label(header.transform, "Time", "DAY 1  09:00", 25, Cyan, new Vector2(.70f, .1f), new Vector2(.98f, .9f), TextAlignmentOptions.Right);
            Image chartPanel = Image(bg.transform, "MarketChart", Panel, new Vector2(.02f, .25f), new Vector2(.72f, .89f));
            chart = chartPanel.rectTransform;
            Label(chart, "Hint", "HOST-AUTHORITATIVE LIVE MARKET", 14, Muted, new Vector2(.03f, .91f), new Vector2(.55f, .98f), TextAlignmentOptions.Left);
            Image side = Image(bg.transform, "Standings", Panel, new Vector2(.74f, .25f), new Vector2(.98f, .89f));
            Label(side.transform, "Title", "LEADERBOARD", 22, Cyan, new Vector2(.06f, .88f), new Vector2(.94f, .97f), TextAlignmentOptions.Left);
            standingsText = Label(side.transform, "Players", "연결 대기", 17, Text, new Vector2(.06f, .35f), new Vector2(.94f, .86f), TextAlignmentOptions.TopLeft);
            positionText = Label(side.transform, "Position", "POSITION: NONE", 16, Muted, new Vector2(.06f, .18f), new Vector2(.94f, .34f), TextAlignmentOptions.TopLeft);
            eventText = Label(side.transform, "Event", "EVENT: NONE", 15, Pink, new Vector2(.06f, .04f), new Vector2(.94f, .16f), TextAlignmentOptions.TopLeft);
            vitalsText = Label(chartPanel.transform, "Vitals", "HEALTH 100  /  MENTAL 100", 17, Text, new Vector2(.52f, .91f), new Vector2(.97f, .98f), TextAlignmentOptions.Right);
            Image controls = Image(bg.transform, "TradingControls", Panel, new Vector2(.02f, .04f), new Vector2(.98f, .22f));
            Button(controls.transform, "Long", "LONG", new Vector2(.02f, .18f), new Vector2(.16f, .82f), Cyan).onClick.AddListener(() => Submit(P2PTradeAction.OpenLong));
            Button(controls.transform, "Short", "SHORT", new Vector2(.18f, .18f), new Vector2(.32f, .82f), Pink).onClick.AddListener(() => Submit(P2PTradeAction.OpenShort));
            Button(controls.transform, "Close", "CLOSE", new Vector2(.34f, .18f), new Vector2(.47f, .82f), Muted).onClick.AddListener(() => Submit(P2PTradeAction.ClosePosition));
            Button(controls.transform, "LevDown", "LEV -", new Vector2(.51f, .18f), new Vector2(.60f, .82f), Cyan).onClick.AddListener(() => { leverage = Mathf.Max(1, leverage - 5); });
            Button(controls.transform, "LevUp", "LEV +", new Vector2(.61f, .18f), new Vector2(.70f, .82f), Cyan).onClick.AddListener(() => { leverage += 5; });
            Button(controls.transform, "MarginDown", "MARGIN -", new Vector2(.72f, .18f), new Vector2(.82f, .82f), Cyan).onClick.AddListener(() => margin = System.Math.Max(.1, margin - .1));
            Button(controls.transform, "MarginUp", "MARGIN +", new Vector2(.83f, .18f), new Vector2(.93f, .82f), Cyan).onClick.AddListener(() => margin = System.Math.Min(1, margin + .1));
            inventoryText = Label(side.transform,"Inventory","CARE ITEMS",14,Muted,new Vector2(.06f,.12f),new Vector2(.94f,.23f),TextAlignmentOptions.BottomLeft);
            Button(side.transform,"UseWater","물 사용",new Vector2(.05f,.01f),new Vector2(.33f,.055f),Cyan).onClick.AddListener(()=>Competition(P2PCompetitionAction.UseItem,"water"));
            Button(side.transform,"UseMedicine","약 사용",new Vector2(.36f,.01f),new Vector2(.64f,.055f),Cyan).onClick.AddListener(()=>Competition(P2PCompetitionAction.UseItem,"medicine"));
            Button(side.transform,"UseComfort","휴식 사용",new Vector2(.67f,.01f),new Vector2(.95f,.055f),Pink).onClick.AddListener(()=>Competition(P2PCompetitionAction.UseItem,"comfort"));
            Button(side.transform,"BuyWater","물 구매",new Vector2(.05f,.065f),new Vector2(.33f,.11f),Cyan).onClick.AddListener(()=>Competition(P2PCompetitionAction.BuyItem,"water"));
            Button(side.transform,"BuyMedicine","약 구매",new Vector2(.36f,.065f),new Vector2(.64f,.11f),Cyan).onClick.AddListener(()=>Competition(P2PCompetitionAction.BuyItem,"medicine"));
            Button(side.transform,"BuyComfort","휴식 구매",new Vector2(.67f,.065f),new Vector2(.95f,.11f),Pink).onClick.AddListener(()=>Competition(P2PCompetitionAction.BuyItem,"comfort"));
            eventPanel=Image(bg.transform,"ChoiceEvent",new Color32(4,10,20,245),new Vector2(.28f,.31f),new Vector2(.72f,.68f)).gameObject;
            Label(eventPanel.transform,"Title","돌발 이벤트",28,Pink,new Vector2(.08f,.72f),new Vector2(.92f,.93f),TextAlignmentOptions.Center);
            messageText=Label(eventPanel.transform,"Message","15초 안에 선택해용",17,Text,new Vector2(.08f,.55f),new Vector2(.92f,.72f),TextAlignmentOptions.Center);
            Button(eventPanel.transform,"Rest","안정 (+체력)",new Vector2(.08f,.15f),new Vector2(.34f,.48f),Cyan).onClick.AddListener(()=>Competition(P2PCompetitionAction.ChooseEvent,"0"));
            Button(eventPanel.transform,"Focus","집중 (+멘탈)",new Vector2(.37f,.15f),new Vector2(.63f,.48f),Cyan).onClick.AddListener(()=>Competition(P2PCompetitionAction.ChooseEvent,"1"));
            Button(eventPanel.transform,"Risk","위험 (+현금)",new Vector2(.66f,.15f),new Vector2(.92f,.48f),Pink).onClick.AddListener(()=>Competition(P2PCompetitionAction.ChooseEvent,"2"));
            eventPanel.SetActive(false);
            resultPanel=Image(bg.transform,"Results",new Color32(4,10,20,252),new Vector2(.22f,.18f),new Vector2(.78f,.82f)).gameObject;
            Label(resultPanel.transform,"Title","MATCH RESULT",34,Cyan,new Vector2(.08f,.79f),new Vector2(.92f,.94f),TextAlignmentOptions.Center);
            Label(resultPanel.transform,"Guide","최종 순위는 오른쪽 리더보드에서 확인해용",20,Text,new Vector2(.08f,.48f),new Vector2(.92f,.75f),TextAlignmentOptions.Center);
            Button(resultPanel.transform,"Rematch","재대결 로비",new Vector2(.12f,.12f),new Vector2(.48f,.3f),Cyan).onClick.AddListener(ReturnToRematchLobby);
            Button(resultPanel.transform,"Leave","로비 나가기",new Vector2(.52f,.12f),new Vector2(.88f,.3f),Pink).onClick.AddListener(ReturnToLobby);
            resultPanel.SetActive(false);
        }

        private void Submit(P2PTradeAction action) => P2PNetworkSessionManager.Instance?.TradingAuthority?.Submit(action, leverage, action == P2PTradeAction.ClosePosition ? 0 : margin);
        private void RefreshTrading()
        {
            var authority = P2PNetworkSessionManager.Instance?.TradingAuthority; if (authority == null) return;
            var lines = new List<string>(); P2PPlayerTradeSnapshot? mine = null;
            foreach (var p in authority.Players) { lines.Add($"#{p.Rank}  {p.Name}\n     EQUITY {p.Equity:N0}  {p.ReturnRate:P1}"); if (p.PlayerId == SteamRuntimeBootstrap.LocalSteamId) mine = p; }
            standingsText.text = string.Join("\n\n", lines);
            if (mine.HasValue) { var p = mine.Value; positionText.text = $"POSITION {p.Side}  x{p.Leverage}\nCASH {p.Cash:N0}  PnL {p.UnrealizedPnL:+0;-0;0}\nLEV {leverage} / MARGIN {margin:P0}\nLAST {authority.LastResult.RejectReason}"; }
        }

        private void Competition(P2PCompetitionAction action,string value)=>P2PNetworkSessionManager.Instance?.CompetitionAuthority?.Submit(action,value);
        private void RefreshCompetition()
        {
            var state=P2PNetworkSessionManager.Instance?.CompetitionAuthority?.Current;if(state==null)return;
            P2PCompetitionPlayerSnapshot? mine=null;foreach(var p in state.Players)if(p.PlayerId==SteamRuntimeBootstrap.LocalSteamId)mine=p;
            if(mine.HasValue){var p=mine.Value;vitalsText.text=$"HEALTH {p.Health:0}  /  MENTAL {p.Mental:0}"+(p.IsEliminated?$"  ·  관전 중 ({p.Reason})":"");inventoryText.text=$"물 {p.Water}  약 {p.Medicine}  휴식 {p.Comfort}";}
            eventText.text=state.EventActive?$"EVENT  {state.EventSecondsLeft:0.0}s":"EVENT: NONE";
            if(eventPanel!=null){eventPanel.SetActive(state.EventActive);messageText.text=$"{state.EventTitle}\n남은 시간 {state.EventSecondsLeft:0.0}초 · 미선택 시 무작위";}
            if(resultPanel!=null)resultPanel.SetActive(state.Finished);
        }
        private void ReturnToLobby(){P2PNetworkSessionManager.Instance?.ShutdownSession();SteamLobbyManager.Instance?.LeaveLobby();SceneManager.LoadScene("TitleScene");}
        private void ReturnToRematchLobby(){SteamLobbyManager.Instance?.ResetMatchForRematch();P2PNetworkSessionManager.Instance?.ShutdownSession();SceneManager.LoadScene("TitleScene");}

        private void DrawChart()
        {
            foreach (var line in chartLines) Destroy(line); chartLines.Clear(); if (prices.Count < 2) return;
            double min = double.MaxValue, max = double.MinValue; foreach (double p in prices) { min = System.Math.Min(min, p); max = System.Math.Max(max, p); }
            double range = System.Math.Max(1, max - min);
            for (int i = 1; i < prices.Count; i++)
            {
                Vector2 a = new((i - 1f) / (prices.Count - 1) * chart.rect.width, (float)((prices[i - 1] - min) / range) * chart.rect.height * .75f + 40);
                Vector2 b = new(i / (float)(prices.Count - 1) * chart.rect.width, (float)((prices[i] - min) / range) * chart.rect.height * .75f + 40);
                GameObject go = new("Tick", typeof(RectTransform), typeof(Image)); go.transform.SetParent(chart, false); var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = new Vector2(0, .5f); rect.anchoredPosition = a; rect.sizeDelta = new Vector2(Vector2.Distance(a, b), 3); rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y-a.y, b.x-a.x)*Mathf.Rad2Deg);
                go.GetComponent<Image>().color = prices[i] >= prices[i-1] ? Cyan : Pink; chartLines.Add(go);
            }
        }

        private static Image Image(Transform p, string n, Color c, Vector2 min, Vector2 max) { var g=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));g.transform.SetParent(p,false);var i=g.GetComponent<Image>();i.color=c;Rect(g.GetComponent<RectTransform>(),min,max);return i; }
        private static TMP_Text Label(Transform p,string n,string s,float z,Color c,Vector2 min,Vector2 max,TextAlignmentOptions a){var g=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));g.transform.SetParent(p,false);var t=g.GetComponent<TMP_Text>();t.text=s;t.fontSize=z;t.color=c;t.alignment=a;t.textWrappingMode=TextWrappingModes.Normal;Rect(t.rectTransform,min,max);return t;}
        private static Button Button(Transform p,string n,string s,Vector2 min,Vector2 max,Color c){var i=Image(p,n,Navy,min,max);var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;var o=i.gameObject.AddComponent<Outline>();o.effectColor=c;o.effectDistance=new Vector2(2,-2);Label(i.transform,"Label",s,17,Text,new Vector2(.02f,.05f),new Vector2(.98f,.95f),TextAlignmentOptions.Center);return b;}
        private static void Rect(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
