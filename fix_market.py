import sys

file_path = r'd:\Project\fx overdose\Assets\Scripts\Trading\MarketSimulationEngine.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace(
    'public void RestoreFromSaveData(FXOverdose.Core.SaveData data)\n        {\n            currentPrice = data.CurrentChartPrice;',
    'private bool wasLoaded = false;\n        \n        public void RestoreFromSaveData(FXOverdose.Core.SaveData data)\n        {\n            wasLoaded = true;\n            currentPrice = data.CurrentChartPrice;'
)

start_target = '''        private void Start()
        {
            EnsureCandleHistoriesInitialized();
            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced += OnGameMinuteAdvanced;
                gameManager.OnFastForwardEnded += HandleFastForwardEnded;
            }

            ResetEngine(initialPrice);
        }'''

start_replacement = '''        private void Start()
        {
            EnsureCandleHistoriesInitialized();
            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced += OnGameMinuteAdvanced;
                gameManager.OnFastForwardEnded += HandleFastForwardEnded;
            }

            if (!wasLoaded)
            {
                ResetEngine(initialPrice);
            }
            else
            {
                UnityEngine.Debug.Log("[MarketSimulationEngine] 세이브 로드로 인해 ResetEngine(초기화)을 건너뜁니다.");
                IsDataPrepared = true;
            }
        }'''

content = content.replace(start_target, start_replacement)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)
