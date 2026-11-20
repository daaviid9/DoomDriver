namespace DoomDriver.Phone
{
    /// <summary>
    /// Generic interface for any interactive UI element on the phone screen
    /// (e.g. Order Cards, App Icons, Shop Upgrades, Bill Items).
    /// Decouples navigation controllers from specific UI implementations.
    /// </summary>
    public interface IPhoneNavigable
    {
        /// <summary>
        /// Applies visual focus state (e.g. scale, outline thickness, highlight).
        /// </summary>
        void SetFocused(bool isFocused);

        /// <summary>
        /// Executes the primary action of this element (e.g. accept order, launch app, purchase upgrade).
        /// </summary>
        void Select();

        /// <summary>
        /// Event fired when the pointer/mouse enters this element.
        /// Allows mouse hover to synchronize seamlessly with keyboard navigation.
        /// </summary>
        event System.Action<IPhoneNavigable> OnHovered;
    }
}

