namespace Curl_maui;

public partial class MainPage : ContentPage
{
    public MainPage(MainPageVm vm)
    {
        BindingContext = vm;
        InitializeComponent();
    }

    public Task OnCreated()
    {
        MainPageVm vm = BindingContext as MainPageVm ??
            throw App.LogNException("MainPageVm is null");
        return Task.CompletedTask;
    }

    public Task OnDestroy()
    {
        MainPageVm vm = BindingContext as MainPageVm ??
            throw App.LogNException("CameraHmiViewModel is null");
        vm.Dispose();
        return Task.CompletedTask;
    }
}