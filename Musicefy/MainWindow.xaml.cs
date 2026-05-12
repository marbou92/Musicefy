using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using Musicefy.Views;
using Musicefy.Services; // ThemeManager
using NAudio.Wave;
using IOFile = System.IO.File;   // ✅ alias for System.IO.File
using TagLibFile = TagLib.File; // ✅ alias for TagLib.File

namespace Musicefy
{
    public partial class MainWindow : Window
    {
        // ... unchanged fields and constructor ...

        private void LoadAlbumArt(MusicFile track)
        {
            try
            {
                if (!string.IsNullOrEmpty(track.Path) && IOFile.Exists(track.Path))
                {
                    var file = TagLibFile.Create(track.Path);
                    if (file.Tag.Pictures.Length > 0)
                    {
                        var pic = file.Tag.Pictures[0];
                        using (var ms = new MemoryStream(pic.Data.Data))
                        {
                            var img = new BitmapImage();
                            img.BeginInit();
                            img.StreamSource = ms;
                            img.CacheOption = BitmapCacheOption.OnLoad;
                            img.EndInit();
                            AlbumArtImage.Source = img;
                        }
                    }
                    else
                    {
                        AlbumArtImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));
                    }
                }
                else
                {
                    AlbumArtImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));
                }
            }
            catch
            {
                AlbumArtImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));
            }
        }

        // ... rest of your methods unchanged ...
    }
}
