using NLog;

namespace Curl_maui;

public static class DialogService
{
    static readonly string NullMainPageMessage = "The Application.Current.MainPage is null";
    static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Presents an alert dialog to the application user with an YES and a NO button.
    /// </summary>
    /// <returns>
    /// User's choice. 
    /// true indicates that the user clicked YES. 
    /// false indicates that the user clicked NO.
    /// </returns>
    public static async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        try
        {
            var mainPage = Application.Current?.MainPage ?? throw new NullReferenceException(NullMainPageMessage);
            var res = await mainPage.DisplayAlert(title, message, "YES", "NO");
            return res;
        }
        catch (Exception ex)
        { _logger.Error(ex); }
        return false;
    }

    /// <summary>
    /// Presents an message dialog to the application user with a OK button.
    /// </summary>
    /// <param name="title"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public static void ShowAlert(string title, string message)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var mainPage = Application.Current?.MainPage ?? throw new NullReferenceException(NullMainPageMessage);
                mainPage.DisplayAlert(title, message, "OK");
            });
        }
        catch (Exception ex)
        { _logger.Error(ex); }
    }

    /// <summary>
    /// Displays a native platform action sheet, allowing the application user to choose from several buttons.
    /// </summary>
    /// <param name="title">Title of the displayed action sheet. Must not be null.</param>
    /// <param name="cancel">Text to be displayed in the 'Cancel' button. Can be null to hide the cancel action.</param>
    /// <param name="destruction">Text to be displayed in the 'Destruct' button. Can be null to hide the destructive options.</param>
    /// <param name="buttons">Text labels for additional buttons. Must not be null.</param>
    /// <returns>An awaitable Task that displays an action sheet and returns the Text of the button pressed by the user.</returns>
    public static async Task<string> ShowActionsSheetAsync(string title, string cancel, string destruction, params string[] buttons)
    {
        try
        {
            var mainPage = Application.Current?.MainPage ?? throw new NullReferenceException(NullMainPageMessage);
            var res = await mainPage.DisplayActionSheet(title, cancel, destruction, buttons);
            return res;
        }
        catch (Exception ex)
        { _logger.Error(ex); }
        return "";
    }

    /// <summary>
    /// Displays a prompt dialog to the application user with the intent to capture a single string value.
    /// </summary>
    /// <param name="title">The title of the prompt dialog.</param>
    /// <param name="message">The body text of the prompt dialog.</param>
    /// <returns>A <see cref="Task"/> that displays a prompt display and returns the string value as entered by the user.</returns>
    /// <exception cref="NullReferenceException"></exception>
    public static async Task<string> ShowInputPromptAsync(string title, string message)
    {
        var mainPage = Application.Current?.MainPage ?? throw new NullReferenceException(NullMainPageMessage);
        var res = await mainPage.DisplayPromptAsync(title, message);
        return res;
    }
}
