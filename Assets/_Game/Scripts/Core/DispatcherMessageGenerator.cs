using DoomDriver.Environment;

namespace DoomDriver.Core
{
    /// <summary>
    /// Builds the humorous morning dispatcher message, e.g.
    /// "Rainy Tuesday: Pizza orders are up 40%, watch out for speed cameras!".
    /// Deterministic per (day, weather) so the briefing does not change when re-shown.
    /// The weather opener uses the same keywords WeatherManager.ParseConditionFromBriefing reads,
    /// and the other fragments avoid them, so feeding the text back keeps the current weather.
    /// </summary>
    public static class DispatcherMessageGenerator
    {
        private const int MIN_PERCENT = 10;
        private const int PERCENT_STEP = 5;
        private const int PERCENT_STEPS = 9; // 10% .. 50%
        private const int SEED_PRIME = 7919;

        private static readonly string[] Foods =
        {
            "Pizza", "Kebab", "Sushi", "Burger", "Pho", "Bubble tea", "Fried cheese", "Halusky"
        };

        private static readonly string[] Warnings =
        {
            "watch out for speed cameras!",
            "the potholes on Main Street are hungry again.",
            "a gang of pigeons is guarding the bridge.",
            "customers tip zero if the fries are cold.",
            "HQ is watching your star rating. Smile.",
            "please stop scrolling Reels at red lights.",
            "the fuel light is a suggestion, not a rule.",
            "Mrs. Novak on 3rd floor still has no elevator."
        };

        public static string Build(int day, string dayName, WeatherCondition weather)
        {
            var random = new System.Random(day * SEED_PRIME + (int)weather);
            string food = Foods[random.Next(Foods.Length)];
            int percent = MIN_PERCENT + random.Next(PERCENT_STEPS) * PERCENT_STEP;
            string warning = Warnings[random.Next(Warnings.Length)];

            return $"{WeatherOpener(weather)} {dayName}: {food} orders are up {percent}%, {warning}";
        }

        private static string WeatherOpener(WeatherCondition weather)
        {
            switch (weather)
            {
                case WeatherCondition.Foggy: return "Foggy";
                case WeatherCondition.LightRain: return "Rainy";
                case WeatherCondition.RainAndFog: return "Stormy, foggy";
                default: return "Sunny";
            }
        }
    }
}
