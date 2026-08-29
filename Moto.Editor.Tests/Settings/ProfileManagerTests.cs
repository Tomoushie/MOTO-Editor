// Moto.Core.Tests/Settings/ProfileManagerTests.cs
using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moto.Core.Settings;
using Moto.Core.Settings.Profiles;
using Xunit;

namespace Moto.Core.Tests.Settings
{
    /// <summary>
    /// Tests des profils de configuration : application, sauvegarde, chargement.
    /// </summary>
    public class ProfileManagerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly FakeSettingsStore _store;
        private readonly ProfileManager _manager;

        public ProfileManagerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);

            _store = new FakeSettingsStore();
            _manager = new ProfileManager(_store, _tempDir, NullLogger<ProfileManager>.Instance);
        }

        [Fact]
        public void BuiltIn_Profiles_Are_Available()
        {
            var profiles = ProfileManager.BuiltInProfiles;

            Assert.NotEmpty(profiles);
            Assert.Contains(profiles, p => p.Id == "beginner");
            Assert.Contains(profiles, p => p.Id == "expert");
            Assert.Contains(profiles, p => p.Id == "turbo");
        }

        [Fact]
        public void ApplyProfile_Sets_All_Profile_Settings()
        {
            // Arrange : profil expert.
            var expert = ProfileManager.BuiltInProfiles[1]; // "expert"

            // Act
            _manager.ApplyProfile(expert);

            // Assert : chaque clé du profil est appliquée.
            foreach (var (key, expected) in expert.Settings)
            {
                var actual = _store.GetRaw(key);
                Assert.NotNull(actual);
            }

            // Le profil actif est mémorisé.
            Assert.Equal("expert", _store.GetString("active_profile"));
        }

        [Fact]
        public void SaveCurrentAsProfile_Creates_File_On_Disk()
        {
            // Arrange : paramètres actuels.
            _store.Set("theme", "dark");
            _store.Set("editor.font_size", 12);

            // Act
            _manager.SaveCurrentAsProfile("Mon Profil Test");

            // Assert : un fichier JSON est créé.
            var files = Directory.GetFiles(_tempDir, "*.json");
            Assert.NotEmpty(files);
        }

        [Fact]
        public void LoadCustomProfiles_Returns_BuiltIn_Plus_Saved()
        {
            // Arrange : on sauvegarde un profil custom.
            _store.Set("theme", "light");
            _manager.SaveCurrentAsProfile("Custom");

            // Act
            var all = _manager.LoadCustomProfiles();

            // Assert : built-in + custom.
            Assert.True(all.Count >= ProfileManager.BuiltInProfiles.Count + 1);
            Assert.Contains(all, p => p.DisplayName == "Custom");
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* ignoré */ }
        }
    }
}
