using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using System.Threading.Tasks;

namespace Musicefy.Tests.Services
{
    [TestClass]
    public class SubsonicClientTests
    {
        private SubsonicClientImpl _client;

        [TestInitialize]
        public void Setup()
        {
            var source = new StreamingSource
            {
                Id = "test-source",
                Name = "Test Server",
                Type = "Subsonic",
                Url = "https://demo.subsonic.org",
                Username = "guest",
                Password = "guest",
                IsConnected = false,
                ClientVersion = "1.0"
            };
            _client = new SubsonicClientImpl(source);
        }

        [TestMethod]
        [Ignore("Requires a running Subsonic server (demo.subsonic.org is external)")]
        public async Task TestConnectionAsync_ShouldNotThrow()
        {
            bool result = await _client.TestConnectionAsync();
            Assert.IsFalse(result, "Expected connection to fail with guest credentials.");
        }

        [TestMethod]
        [Ignore("Requires a running Subsonic server (demo.subsonic.org is external)")]
        public async Task SearchAsync_ShouldReturnResults_WhenQueryIsValid()
        {
            var results = await _client.SearchAsync("Beatles");
            Assert.IsNotNull(results, "Search results should not be null.");
        }

        [TestMethod]
        public void GetStreamUrl_ShouldReturnValidUrl_WhenSongIdProvided()
        {
            string url = _client.GetStreamUrl("12345");
            Assert.IsNotNull(url, "Stream URL should not be null.");
            Assert.IsTrue(url.Contains("stream"), "URL should contain 'stream' endpoint.");
            Assert.IsTrue(url.Contains("12345"), "URL should contain the song ID.");
        }

        [TestMethod]
        [Ignore("Requires a running Subsonic server (demo.subsonic.org is external)")]
        public async Task GetRandomSongsAsync_ShouldNotThrow()
        {
            var songs = await _client.GetRandomSongsAsync(5);
            Assert.IsNotNull(songs, "Random songs should not be null.");
        }

        [TestMethod]
        [Ignore("Requires a running Subsonic server (demo.subsonic.org is external)")]
        public async Task GetMusicFoldersAsync_ShouldNotThrow()
        {
            var folders = await _client.GetMusicFoldersAsync();
            Assert.IsNotNull(folders, "Folders should not be null.");
        }
    }
}
