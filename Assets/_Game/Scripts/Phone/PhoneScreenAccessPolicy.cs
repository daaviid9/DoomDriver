namespace DoomDriver.Phone
{
    /// <summary>
    /// Pure domain policy determining whether a PhoneScreen can be opened in the current GameState.
    /// Contains no UI copy or MonoBehaviour dependencies.
    /// </summary>
    public static class PhoneScreenAccessPolicy
    {
        /// <summary>
        /// Evaluates whether <paramref name="screen"/> may be opened for <paramref name="currentState"/>.
        /// When <paramref name="currentState"/> is null (isolated test scenes without GameManager),
        /// state-dependent screens like ShopApp are allowed, while permanently locked screens remain blocked.
        /// </summary>
        public static bool CanOpen(PhoneScreen screen, GameState? currentState)
        {
            switch (screen)
            {
                case PhoneScreen.ShopApp:
                    return IsShopAccessible(currentState);

                case PhoneScreen.BillsApp:
                    return false;

                default:
                    return true;
            }
        }

        /// <summary>
        /// Doom Garage (ShopApp) is only accessible at the end of the day (DaySummary),
        /// or in isolated test scenes where GameManager is absent.
        /// </summary>
        public static bool IsShopAccessible(GameState? currentState)
        {
            return !currentState.HasValue || currentState.Value == GameState.DaySummary;
        }
    }
}
