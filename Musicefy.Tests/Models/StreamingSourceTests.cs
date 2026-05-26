using Microsoft.VisualStudio.TestTools.UnitTesting;
using Musicefy.Core.Models;
using System;

namespace Musicefy.Tests.Models
{
    [TestClass]
    public class StreamingSourceTests
    {
        [TestMethod]
        public void DefaultConstructor_ShouldInitializeCorrectly()
        {
            var source = new StreamingSource();

            Assert.IsTrue(string.IsNullOrEmpty(source.Id));
            Assert.IsTrue(string.IsNullOrEmpty(source.Name));
            Assert.IsTrue(string.IsNullOrEmpty(source.Type));
            Assert.IsFalse(source.IsConnected);
        }

        [TestMethod]
        public void FullConstructor_ShouldSetAllProperties()
        {
            var source = new StreamingSource
            {
                Id = "test-id",
                Name = "Test Source",
                Type = "Subsonic",
                Url = "https://example.com",
                Username = "testuser",
                Password = "testpass",
                IsConnected = true,
                ClientVersion = "1.5.0"
            };

            Assert.AreEqual("test-id", source.Id);
            Assert.AreEqual("Test Source", source.Name);
            Assert.AreEqual("Subsonic", source.Type);
            Assert.AreEqual("https://example.com", source.Url);
            Assert.AreEqual("testuser", source.Username);
            Assert.AreEqual("testpass", source.Password);
            Assert.IsTrue(source.IsConnected);
            Assert.AreEqual("1.5.0", source.ClientVersion);
        }

        [TestMethod]
        public void ClientVersion_ShouldHaveDefaultValue()
        {
            var source = new StreamingSource();

            Assert.AreEqual("1.0", source.ClientVersion);
        }

        [TestMethod]
        public void ToString_ShouldFormatCorrectly()
        {
            var source = new StreamingSource
            {
                Name = "Test Source",
                Type = "Subsonic"
            };

            var result = source.ToString();

            Assert.AreEqual("Test Source (Subsonic)", result);
        }

        [TestMethod]
        public void ToString_WithDifferentTypes_ShouldFormatCorrectly()
        {
            var subsonicSource = new StreamingSource { Name = "My Server", Type = "Subsonic" };
            var localSource = new StreamingSource { Name = "Local Music", Type = "Local" };

            Assert.AreEqual("My Server (Subsonic)", subsonicSource.ToString());
            Assert.AreEqual("Local Music (Local)", localSource.ToString());
        }
    }
}