using CommunityToolkit.Maui.Views;
using System;
using System.Text;

namespace Curl_maui;

public class MainPageVm : ViewModelBase, IDisposable
{
    public static readonly ImageSource NoImage = "noimage.png";
    public string? Url { get; set; } = "http://localhost:8001/http://localhost:8889/short-landscape.mp4?fm=mp4";
    public string? HeaderKey1 { get; set; } = "X-IXSource-Capabilities";
    public string? HeaderVal1 { get; set; } = "{\"painter_video\": true}";
    public string? HeaderKey2 { get; set; }
    public string? HeaderVal2 { get; set; }
    public string? HeaderKey3 { get; set; }
    public string? HeaderVal3 { get; set; }
    public string? ResponseHeaders { get; set; }

    private string? _tmpFilePath = null;
    private List<string> _savedFiles = new List<string>();

    public ImageSource? ImgSource
    {
        get
        {
            return NoImage;
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

    public Command RequestUrlCmd => new Command(async () =>
    {
        if (Url is null)
        {
            DialogService.ShowAlert("Error", "Please input a valid URL.");
            return;
        }
        using var httpClient = new HttpClient();
        // Add custom headers
        if (HeaderKey1 != null && HeaderVal1 != null)
        {
            httpClient.DefaultRequestHeaders.Add(HeaderKey1, HeaderVal1);
        }
        if (HeaderKey2 != null && HeaderVal2 != null)
        {
            httpClient.DefaultRequestHeaders.Add(HeaderKey2, HeaderVal2);
        }
        if (HeaderKey3 != null && HeaderVal3 != null)
        {
            httpClient.DefaultRequestHeaders.Add(HeaderKey3, HeaderVal3);
        }
        try
        {
            var response = await httpClient.GetAsync(Url).CAF();
            response.EnsureSuccessStatusCode(); // Throws exception if not success (2xx)
            byte[] bytes = await response.Content.ReadAsByteArrayAsync().CAF();
            _tmpFilePath = StaticValues.CurrentSaveVideoFilePath();
            await File.WriteAllBytesAsync(_tmpFilePath, bytes).CAF();
            _savedFiles.Add(_tmpFilePath);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Response Code: {response.StatusCode}");
            sb.AppendLine($"Content length: {bytes.Length} bytes");
            // Print response headers
            sb.AppendLine("Response Headers:");
            foreach (var header in response.Headers)
            {
                sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }
            ResponseHeaders = sb.ToString();
            RaisePropertyChanged(nameof(ResponseHeaders));
            RaisePropertyChanged(nameof(MediaSource));
        }
        catch (HttpRequestException e)
        {
            DialogService.ShowAlert("Error", $"Request error: {e.Message}");
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
            foreach (var file in _savedFiles)
            {
                File.Delete(file);
            }
        }
    }
    #endregion
}
