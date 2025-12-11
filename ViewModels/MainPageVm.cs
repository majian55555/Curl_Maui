using CommunityToolkit.Maui.Views;
using System.Text;
using System.Text.RegularExpressions;

namespace Curl_maui;

public enum ContentType : int
{
    Video = 1, Image = 2, Text = 3
}

public class MainPageVm : ViewModelBase, IDisposable
{
    public static readonly ImageSource NoImage = "noimage.png";
    public string? Url { get; set; }
    public string? HeaderKey1 { get; set; }
    public string? HeaderVal1 { get; set; }
    public string? HeaderKey2 { get; set; }
    public string? HeaderVal2 { get; set; }
    public string? HeaderKey3 { get; set; }
    public string? HeaderVal3 { get; set; }
    public string? ResponseCode { get; set; }
    public string? ContentHeaders { get; set; }
    public string? ResponseHeaders { get; set; }
    public string? TextContent { get; set; }
    public string? RequestTimer { get; set; }

    private string? _tmpFilePath = null;
    private string? _fileExt = null;
    private List<string> _savedFiles = new List<string>();
    private ContentType _contentType = ContentType.Image;
    private byte[]? _imgBytes = null;
    private double _requestTimerSeconds = 0;

    public bool VideoVis
    {
        get
        {
            return _contentType == ContentType.Video;
        }
    }

    public bool ImageVis
    {
        get
        {
            return _contentType == ContentType.Image;
        }
    }

    public bool TextVis
    {
        get
        {
            return _contentType == ContentType.Text;
        }
    }

    public bool RequestButtonEnabled { get; set; } = true;
    public bool CancelButtonEnabled { get; set; } = true;
    public bool SaveButtonEnabled { 
        get
        { 
            return _tmpFilePath is not null 
                || _imgBytes is not null 
                || TextContent is not null; 
        } 
    }

    public void LoadConfig()
    {
        if (!File.Exists(StaticValues.CurrentConfigFilePath()))
        { return; }
        string[] lines = File.ReadAllLines(StaticValues.CurrentConfigFilePath());
        if (lines.Length > 0)
        {
            Url = lines[0];
            RaisePropertyChanged(nameof(Url));
        }
        if (lines.Length > 1)
        {
            HeaderKey1 = lines[1];
            RaisePropertyChanged(nameof(HeaderKey1));
        }
        if (lines.Length > 2)
        {
            HeaderVal1 = lines[2];
            RaisePropertyChanged(nameof(HeaderVal1));
        }
        if (lines.Length > 3)
        {
            HeaderKey2 = lines[3];
            RaisePropertyChanged(nameof(HeaderKey2));
        }
        if (lines.Length > 4)
        {
            HeaderVal2 = lines[4];
            RaisePropertyChanged(nameof(HeaderVal2));
        }
        if (lines.Length > 5)
        {
            HeaderKey3 = lines[5];
            RaisePropertyChanged(nameof(HeaderKey3));
        }
        if (lines.Length > 6)
        {
            HeaderVal3 = lines[6];
            RaisePropertyChanged(nameof(HeaderVal3));
        }
    }

    private Stream GetImageStream()
    {
        if (_imgBytes is null)
        { throw new ArgumentNullException(); }
        return new MemoryStream(_imgBytes, false);
    }

    public ImageSource? ImgSource
    {
        get
        {
            if (_imgBytes is null)
            { return NoImage; }
            return ImageSource.FromStream(GetImageStream);
        }
    }

    public MediaSource? MediaSource
    {
        get
        {
            if (_tmpFilePath != null)
            { return MediaSource.FromFile(_tmpFilePath); }
            return null;
        }
    }

    private async Task StartRequestTimer()
    {
        _requestTimerSeconds = 0;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RaisePropertyChanged(nameof(RequestTimer));
        });
        while (!RequestButtonEnabled)
        {
            await Task.Delay(100).CAF();
            _requestTimerSeconds += 0.1;
            RequestTimer = _requestTimerSeconds.ToString("F1") + " seconds";
            MainThread.BeginInvokeOnMainThread(() =>
            {
                RaisePropertyChanged(nameof(RequestTimer));
            });
        }
    }

    public Command SaveCmd => new Command(async () =>
    {
        if (Url is null)
        { throw new Exception("Url is null"); }
        string newPath = StaticValues.GetDownloadsFolderPath();
        string url = Regex.Replace(Url, @"http://localhost:\d+/", "");
        url = Regex.Replace(url, @"[\\/:*?""<>|.]", "_");
        url += _fileExt;
        newPath = Path.Combine(newPath, url);
        if (_tmpFilePath is not null)
        {
            File.Copy(_tmpFilePath, newPath, overwrite: true);
        }
        else if (_imgBytes is not null)
        {
            await File.WriteAllBytesAsync(newPath, _imgBytes).CAF();
        }
        else if (TextContent is not null)
        {
            await File.WriteAllTextAsync(newPath, TextContent).CAF();
        }    
    });

    CancellationTokenSource? _request_cts;
    public Command RequestUrlCmd => new Command(async () =>
    {
        RequestButtonEnabled = false;
        RaisePropertyChanged(nameof(RequestButtonEnabled));
        CancelButtonEnabled = true;
        RaisePropertyChanged(nameof(CancelButtonEnabled));
        _ = StartRequestTimer();
        if (string.IsNullOrEmpty(Url))
        {
            DialogService.ShowAlert("Error", "Please input a valid URL.");
            return;
        }
        using var httpClient = new HttpClient();
        // Add custom headers
        if (!string.IsNullOrEmpty(HeaderKey1) && !string.IsNullOrEmpty(HeaderVal1))
        {
            httpClient.DefaultRequestHeaders.Add(HeaderKey1, HeaderVal1);
        }
        if (!string.IsNullOrEmpty(HeaderKey2) && !string.IsNullOrEmpty(HeaderVal2))
        {
            httpClient.DefaultRequestHeaders.Add(HeaderKey2, HeaderVal2);
        }
        if (!string.IsNullOrEmpty(HeaderKey3) && !string.IsNullOrEmpty(HeaderVal3))
        {
            httpClient.DefaultRequestHeaders.Add(HeaderKey3, HeaderVal3);
        }
        _request_cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            var response = await httpClient.GetAsync(Url, _request_cts.Token).CAF();
            response.EnsureSuccessStatusCode(); // Throws exception if not success (2xx)
            
            ResponseCode = $"Response Code: {response.StatusCode}";
            RaisePropertyChanged(nameof(ResponseCode));

            StringBuilder sb = new();
            sb.AppendLine($"Content Headers: {response.Content.Headers.Count()}");
            foreach (var h in response.Content.Headers)
            {
                sb.AppendLine($"{h.Key} : {string.Join(' ', h.Value)}");
                if (h.Key == "Content-Type")
                {
                    if (h.Value.First().StartsWith("video/"))
                    {
                        _contentType = ContentType.Video;
                        byte[] bytes = await response.Content.ReadAsByteArrayAsync(_request_cts.Token).CAF();
                        _tmpFilePath = StaticValues.CurrentSaveVideoFilePath();
                        await File.WriteAllBytesAsync(_tmpFilePath, bytes, _request_cts.Token).CAF();
                        _savedFiles.Add(_tmpFilePath);
                        _fileExt = ".mp4";
                        _imgBytes = null;
                        TextContent = null;
                        RaisePropertyChanged(nameof(MediaSource));
                    }
                    else if (h.Value.First().StartsWith("image/"))
                    {
                        _contentType = ContentType.Image;
                        _imgBytes = await response.Content.ReadAsByteArrayAsync(_request_cts.Token).CAF();
                        var parts = h.Value.First().Split('/');
                        _fileExt = parts.Length > 1 ? "." + parts[1] : null;
                        _tmpFilePath = null;
                        TextContent = null;
                        RaisePropertyChanged(nameof(ImgSource));
                    }
                    else
                    {
                        _contentType = ContentType.Text;
                        TextContent = await response.Content.ReadAsStringAsync(_request_cts.Token).CAF();
                        _fileExt = ".txt";
                        _tmpFilePath = null;
                        _imgBytes = null;
                        RaisePropertyChanged(nameof(TextContent));
                    }
                    RaisePropertyChanged(nameof(VideoVis));
                    RaisePropertyChanged(nameof(ImageVis));
                    RaisePropertyChanged(nameof(TextVis));
                }
            }
            ContentHeaders = sb.ToString();
            RaisePropertyChanged(nameof(ContentHeaders));

            sb.Clear();
            sb.AppendLine($"Response Headers: {response.Headers.Count()}");
            foreach (var header in response.Headers)
            {
                sb.AppendLine($"{header.Key}: {string.Join(' ', header.Value)}");
            }
            ResponseHeaders = sb.ToString();
            RaisePropertyChanged(nameof(ResponseHeaders));
            RaisePropertyChanged(nameof(SaveButtonEnabled));

        }
        catch (TaskCanceledException e)
        {
            DialogService.ShowAlert("Error", $"Task cancelled: {e.Message}");
        }
        catch (HttpRequestException e)
        {
            DialogService.ShowAlert("Error", $"Request error: {e.Message}");
        }
        catch (Exception e)
        {
            DialogService.ShowAlert("Error", $"Other error: {e.Message}");
        }
        finally
        {
            RequestButtonEnabled = true;
            RaisePropertyChanged(nameof(RequestButtonEnabled));
            CancelButtonEnabled = false;
            RaisePropertyChanged(nameof(CancelButtonEnabled));
            _request_cts?.Dispose();
            _request_cts = null;
        }
    });

    public Command CancelUrlCmd => new Command(async () =>
    {
        if (_request_cts is not null)
        {
            await _request_cts.CancelAsync().CAF();
        }
    });

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
            List<string> lines =
            [
                Url ?? "",
                HeaderKey1 ?? "",
                HeaderVal1 ?? "",
                HeaderKey2 ?? "",
                HeaderVal2 ?? "",
                HeaderKey3 ?? "",
                HeaderVal3 ?? "",
            ];

            File.WriteAllLines(StaticValues.CurrentConfigFilePath(), lines);
            foreach (var file in _savedFiles)
            {
                File.Delete(file);
            }
        }
    }
    #endregion
}
