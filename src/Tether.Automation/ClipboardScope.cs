using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Tether.Automation;

/// <summary>Backs up text/image clipboard content on creation and restores it on Dispose. STA thread only.</summary>
public sealed class ClipboardScope : IDisposable
{
    private readonly string? text;
    private readonly BitmapSource? image;

    public ClipboardScope()
    {
        if (Clipboard.ContainsText()) text = Clipboard.GetText();
        else if (Clipboard.ContainsImage()) image = Clipboard.GetImage();
    }

    public void SetText(string value) => Clipboard.SetDataObject(value, copy: true);

    public void Dispose()
    {
        try
        {
            if (text is not null) Clipboard.SetDataObject(text, copy: true);
            else if (image is not null) Clipboard.SetImage(image);
            else Clipboard.Clear();
        }
        catch (System.Runtime.InteropServices.COMException) { /* clipboard busy: best effort */ }
    }
}
