using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musicefy.Core.Models;
using System;

namespace Musicefy.Tests.Models
{
    [TestClass]
    public class MusicFileTests
    {
        [TestMethod]
        public void DefaultConstructor_ShouldGenerateNewId()
        {
            var musicFile = new MusicFile();

            Assert.IsFalse(string.IsNullOrEmpty(musicFile.Id));
        }

        [TestMethod]
        public void ParameterizedConstructor_ShouldSetAllProperties()
        {
            var duration = TimeSpan.FromMinutes(3.5);
            var musicFile = new MusicFile(
                title: "Test Song",
                artist: "Test Artist",
                album: "Test Album",
                year: 2024,
                sourceUri: "file://test.mp3",
                filePath: "/path/to/test.mp3",
                genre: "Rock",
                duration: duration,
                trackNumber: 5,
                sourceType: "Local",
                bitrate: 320,
                fileSize: 5000000,
                lyrics: "Test lyrics",
                coverPath: "/path/to/cover.jpg"
            );

            Assert.AreEqual("Test Song", musicFile.Title);
            Assert.AreEqual("Test Artist", musicFile.Artist);
            Assert.AreEqual("Test Album", musicFile.Album);
            Assert.AreEqual(2024, musicFile.Year);
            Assert.AreEqual("file://test.mp3", musicFile.SourceUri);
            Assert.AreEqual("/path/to/test.mp3", musicFile.FilePath);
            Assert.AreEqual("Rock", musicFile.Genre);
            Assert.AreEqual(duration, musicFile.Duration);
            Assert.AreEqual(5, musicFile.TrackNumber);
            Assert.AreEqual("Local", musicFile.SourceType);
            Assert.AreEqual(320, musicFile.Bitrate);
            Assert.AreEqual(5000000, musicFile.FileSize);
            Assert.AreEqual("Test lyrics", musicFile.Lyrics);
            Assert.AreEqual("/path/to/cover.jpg", musicFile.CoverPath);
        }

        [TestMethod]
        public void Path_Property_ShouldBeAliasForFilePath()
        {
            var musicFile = new MusicFile
            {
                FilePath = "/path/to/test.mp3"
            };

            Assert.AreEqual(musicFile.Path, musicFile.FilePath);
        }

        [TestMethod]
        public void Path_Setter_ShouldUpdateFilePath()
        {
            var musicFile = new MusicFile();

            musicFile.Path = "/new/path.mp3";

            Assert.AreEqual("/new/path.mp3", musicFile.FilePath);
        }

        [TestMethod]
        public void MarkPlayed_ShouldIncrementPlayCount()
        {
            var musicFile = new MusicFile { PlayCount = 0 };

            musicFile.MarkPlayed();

            Assert.AreEqual(1, musicFile.PlayCount);
        }

        [TestMethod]
        public void MarkPlayed_ShouldUpdateLastPlayed()
        {
            var musicFile = new MusicFile { LastPlayed = DateTime.MinValue };
            var before = DateTime.Now.AddMinutes(-1);

            musicFile.MarkPlayed();

            Assert.IsTrue(musicFile.LastPlayed >= before);
        }

        [TestMethod]
        public void ToggleFavourite_ShouldFlipBoolean()
        {
            var musicFile = new MusicFile { IsFavourite = false };

            musicFile.ToggleFavourite();

            Assert.IsTrue(musicFile.IsFavourite);

            musicFile.ToggleFavourite();

            Assert.IsFalse(musicFile.IsFavourite);
        }

        [TestMethod]
        public void ToString_WithTitleAndArtist_ShouldFormatCorrectly()
        {
            var musicFile = new MusicFile
            {
                Title = "Test Song",
                Artist = "Test Artist"
            };

            var result = musicFile.ToString();

            Assert.AreEqual("Test Song - Test Artist", result);
        }

        [TestMethod]
        public void ToString_WithOnlyTitle_ShouldReturnTitle()
        {
            var musicFile = new MusicFile
            {
                Title = "Test Song",
                Artist = ""
            };

            var result = musicFile.ToString();

            Assert.AreEqual("Test Song", result);
        }

        [TestMethod]
        public void ToString_WithNullArtist_ShouldReturnTitle()
        {
            var musicFile = new MusicFile
            {
                Title = "Test Song",
                Artist = null
            };

            var result = musicFile.ToString();

            Assert.AreEqual("Test Song", result);
        }

        [TestMethod]
        public void DefaultValues_ShouldBeInitializedCorrectly()
        {
            var musicFile = new MusicFile();

            Assert.AreEqual(0, musicFile.PlayCount);
            Assert.AreEqual(DateTime.MinValue, musicFile.LastPlayed);
            Assert.IsFalse(musicFile.IsFavourite);
            Assert.IsFalse(musicFile.IsDownloaded);
            Assert.AreEqual(0, musicFile.Year);
            Assert.AreEqual(0, musicFile.TrackNumber);
        }
    }
}