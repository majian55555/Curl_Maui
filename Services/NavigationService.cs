using OneOf;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Curl_maui;

/// <summary>
/// Interface for NavigationService.
/// Must implement NavigateBack and NavigateToMainPage functions.
/// </summary>
public interface INavigationService
{
    Task NavigateBack();
    Task NavigateToMainPage();
}

public interface IHasValue<out T>
{
    T Value { get; }
}

/// <summary>
/// An object that invokes an action every time it is changed
/// </summary>
/// <typeparam name="T">The object to observe</typeparam>
public class Changeable<T> : Changeable, IHasValue<T>
{
    private T _value;

    public T Value
    {
        get { return _value; }
        set
        {
            if ((_value == null && value != null) || (_value != null && !_value.Equals(value)))
            {
                _value = value;
                Changed?.Invoke(_value);
                RaisePropertyChanged(nameof(Value));
            }
        }
    }

    public event Action<T> Changed;

    AsyncManualResetEvent<T> _changed = new AsyncManualResetEvent<T>();

    public Changeable(T initialValue)
    { _value = initialValue; }

    /// <param name="timeout">maximum time to wait for a change to occur</param>
    /// <param name="token">token to cancel waiting</param>
    /// <returns>true if changed, false if timed out our cancelled</returns>
    public Task<OneOf<Canceled, TimedOut, T>> WaitForChange(TimeSpan timeout, CancellationToken token = default)
    { return _changed.TryWaitAsync(timeout, token); }

    /// <summary>
    /// Wait for either the value to change or the given token to be canceled
    /// </summary>
    /// <param name="token">cancels waiting for a change</param>
    /// <returns>discriminated union object indicating the way in which the wait ended</returns>
    public Task<OneOf<Canceled, TimedOut, T>> WaitForChange(CancellationToken token = default)
    { return _changed.TryWaitAsync(TimeSpan.FromMilliseconds(-1), token); }

    public void Set(T newValue, bool firePropertyChanged = true, bool fireChanged = true)
    {
        _value = newValue;
        ThreadPool.QueueUserWorkItem(o => {
            if (firePropertyChanged)
            { RaisePropertyChanged(nameof(Value)); }
            if (fireChanged)
            {
                Changed?.Invoke(newValue);
                _changed.SetReset(newValue);
            }
        });
    }
}

[Serializable]
public abstract class Changeable : INotifyPropertyChanged
{
    [field: NonSerialized]
    public event PropertyChangedEventHandler PropertyChanged;

    public static Changeable<T> New<T>(T initial)
    { return new Changeable<T>(initial); }

    protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
    }

    //protected void RaisePropertyChanged<T>(Expression<Func<T>> propertyExpresssion)
    //{
    //    var propertyName = PropertySupport.ExtractPropertyName(propertyExpresssion);
    //    RaisePropertyChanged(propertyName);
    //}

    protected void RaisePropertyChanged(String propertyName)
    {
        VerifyPropertyName(propertyName);
        OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        { return false; }

        storage = value;
        RaisePropertyChanged(propertyName);
        return true;
    }

    protected void SetPropertyUncheck<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
    {
        storage = value;
        RaisePropertyChanged(propertyName);
    }

    /// <summary>
    /// Warns the developer if this Object does not have a public property with
    /// the specified name. This method does not exist in a Release build.
    /// </summary>
    [Conditional("DEBUG")]
    //[DebuggerStepThrough]
    public void VerifyPropertyName(String propertyName)
    {
        // verify that the property name matches a real,  
        // public, instance property on this Object.
        if (TypeDescriptor.GetProperties(this)[propertyName] == null)
        {
            Debug.Fail("Invalid property name: " + propertyName);
        }
    }
}

public abstract class ViewModelBase : Changeable
{
    public virtual Task OnNavigatingTo(object parameter)
        => Task.CompletedTask;

    public virtual Task OnNavigatedFrom(bool isForwardNavigation)
        => Task.CompletedTask;

    public virtual Task OnNavigatedTo()
        => Task.CompletedTask;
}

public abstract class AbsNavigationService : INavigationService
{
    readonly IServiceProvider _services;

    protected INavigation Navigation
    {
        get
        {
            INavigation? navigation = Application.Current?.MainPage?.Navigation;
            if (navigation is not null)
            { return navigation; }
            else
            { throw new NullReferenceException("MainPage Navigation is null"); }
        }
    }

    public AbsNavigationService(IServiceProvider services)
    { _services = services; }

    public abstract Task NavigateToMainPage();

    public Task NavigateBack()
    {
        if (Navigation.NavigationStack.Count > 1)
        { return Navigation.PopAsync(); }
        throw new InvalidOperationException("No pages to navigate back to!");
    }

    protected async Task NavigateToPage<T>(object? parameter = null) where T : Page
    {
        var toPage = ResolvePage<T>();

        if (toPage is not null)
        {
            //Subscribe to the toPage's NavigatedTo event
            toPage.NavigatedTo += Page_NavigatedTo;

            //Get VM of the toPage
            var toViewModel = GetPageViewModelBase(toPage);

            //Call navigatingTo on VM, passing in the paramter
            if (toViewModel is not null && parameter is not null)
            { await toViewModel.OnNavigatingTo(parameter).CAF(); }

            //Navigate to requested page
            await Navigation.PushAsync(toPage, true).CAF();

            //Subscribe to the toPage's NavigatedFrom event
            toPage.NavigatedFrom += Page_NavigatedFrom;
        }
        else
        { throw new InvalidOperationException($"Unable to resolve type {typeof(T).FullName}"); }
    }

    private async void Page_NavigatedFrom(object? sender, NavigatedFromEventArgs e)
    {
        Page p = sender as Page ?? throw new NullReferenceException();
        //To determine forward navigation, we look at the 2nd to last item on the NavigationStack
        //If that entry equals the sender, it means we navigated forward from the sender to another page
        bool isForwardNavigation = Navigation.NavigationStack.Count > 1
            && Navigation.NavigationStack[^2] == p;
        if (!isForwardNavigation)
        {
            p.NavigatedTo -= Page_NavigatedTo;
            p.NavigatedFrom -= Page_NavigatedFrom;
        }
        await CallNavigatedFrom(p, isForwardNavigation).ConfigureAwait(false);
    }

    private async Task CallNavigatedFrom(Page p, bool isForward)
    {
        var fromViewModel = GetPageViewModelBase(p);
        if (fromViewModel is not null)
        { await fromViewModel.OnNavigatedFrom(isForward).CAF(); }
    }

    private async void Page_NavigatedTo(object? sender, NavigatedToEventArgs e)
    {
        Page p = sender as Page ?? throw new NullReferenceException();
        await CallNavigatedTo(p).CAF();
    }

    private async Task CallNavigatedTo(Page p)
    {
        var fromViewModel = GetPageViewModelBase(p);
        if (fromViewModel is not null)
        { await fromViewModel.OnNavigatedTo().CAF(); }
    }

    private ViewModelBase GetPageViewModelBase(Page p)
    { return p?.BindingContext as ViewModelBase ?? throw new NullReferenceException(); }

    private T ResolvePage<T>() where T : Page
    { return _services.GetService<T>() ?? throw new NullReferenceException(); }
}

public class NavigationService : AbsNavigationService
{
    public NavigationService(IServiceProvider services) : base(services)
    { }

    public override Task NavigateToMainPage()
    { return NavigateToPage<MainPage>(); }
}