namespace FXOverdose.Core
{
    public enum StoryDifficulty
    {
        Hard = 0,
        Normal = 1,
        Easy = 2
    }

    public readonly struct StoryDifficultyTable
    {
        public StoryDifficultyTable(float startingBalance, float shopPriceMultiplier, float inflationScale)
        {
            StartingBalance = startingBalance;
            ShopPriceMultiplier = shopPriceMultiplier;
            InflationScale = inflationScale;
        }

        public float StartingBalance { get; }
        public float ShopPriceMultiplier { get; }
        public float InflationScale { get; }
    }

    public static class StoryDifficultyTables
    {
        public static StoryDifficultyTable Get(StoryDifficulty difficulty)
        {
            return difficulty switch
            {
                StoryDifficulty.Easy => new StoryDifficultyTable(40000f, 0.40f, 0f),
                StoryDifficulty.Normal => new StoryDifficultyTable(20000f, 0.70f, 0.5f),
                _ => new StoryDifficultyTable(7000f, 1f, 1f)
            };
        }
    }
}
