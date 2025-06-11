namespace Curl_maui;

public class MainPageVm : ViewModelBase, IDisposable
{
    

    #region IDispose
    bool _disposed = false;
    public void Dispose()
    {
        if (_disposed)
        { return; }
        Dispose(true);
        GC.SuppressFinalize(this);
        _disposed = true;
    }
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            //
        }
    }
    #endregion
}
