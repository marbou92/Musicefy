using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musicefy.Core.Services;
using System.Threading.Tasks;

namespace Musicefy.Tests.Services
{
    [TestClass]
    public class SubsonicClientTests
    {
        private SubsonicClient _client;

        [TestInitialize]
        public void Setup()
        {
            // Replace with your test server URL + credentials
            _client = new SubsonicClient(
                url: "https://squidify.org",
                username: "testuser",
                password: "testpass",
                clientName: "MusicefyTest"
            );
        }

        [TestMethod]
        public async Task TestConnectionAsync_ShouldReturnTrue_WhenServerIsReachable()
        {
            bool result = await _client.TestConnectionAsync();
            Assert.IsTrue(result, "Expected connection to succeed.");
        }

        [TestMethod]
        public async Task SearchAsync_ShouldReturnResults_WhenQueryIsValid()
        {
            var results = await _client.SearchAsync("Beatles");
            Assert.IsNotNull(results, "Search results should not be null.");
            Assert.IsTrue(results.Count > 0, "Expected at least one search result.");
        }

        [TestMethod]
        public async Task GetPlaylistsAsync_ShouldReturnList_WhenPlaylistsExist()
        {
            var playlists = await _client.GetPlaylistsAsync();
            Assert.IsNotNull(playlists, "Playlists should not be null.");
        }

        [TestMethod]
        public async Task GetPlaylistAsync_ShouldReturnPlaylist_WhenIdIsValid()
        {
            var playlists = await _client.GetPlaylistsAsync();
            if (playlists.Count > 0)
            {
                var playlist = await _client.GetPlaylistAsync(playlists[0].Id);
                Assert.IsNotNull(playlist, "Playlist should not be null.");
                Assert.AreEqual(playlists[0].Id, playlist.Id, "Playlist IDs should match.");
            }
        }
    }
}
