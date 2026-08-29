// Moto.Editor/Pages/SettingsPage.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Moto.Core.Settings;
using Moto.Editor.Settings;

namespace Moto.Editor.Pages
{
    /// <summary>
    /// Page de paramètres complète, type IDE classique.
    /// Sidebar catégories/sections + recherche + liste data-driven.
    /// </summary>
    public partial class SettingsPage : ContentPage
    {
        private readonly SettingsEngine _engine;
        private readonly Action<string> _openFile;

        private string _selectedSection = "Tous";
        private string _search = string.Empty;

        public SettingsPage(SettingsEngine engine, Action<string> openFile)
        {
            InitializeComponent();

            _engine = engine;
            _openFile = openFile;

            // Sidebar : "Tous" + chaque "Catégorie > Section".
            var sections = new List<string> { "Tous" };

            sections.AddRange(SettingsCatalog.All
                .Select(d => $"{d.Category} › {d.Section}")
                .Distinct()
                .OrderBy(s => s));

            SectionList.ItemsSource = sections;

            SectionList.SelectionChanged += (s, e) =>
            {
                if (e.CurrentSelection.FirstOrDefault() is string section)
                {
                    _selectedSection = section;
                    Refresh();
                }
            };

            Refresh();
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            _search = e.NewTextValue ?? string.Empty;
            Refresh();
        }

        /// <summary>Reconstruit la liste filtrée des paramètres.</summary>
        private void Refresh()
        {
            var query = SettingsCatalog.All.AsEnumerable();

            if (_selectedSection != "Tous")
            {
                var parts = _selectedSection.Split('›');
                var cat = parts[0].Trim();
                var sec = parts.Length > 1 ? parts[1].Trim() : "";

                query = query.Where(d => d.Category == cat && d.Section == sec);
            }

            if (!string.IsNullOrWhiteSpace(_search))
            {
                query = query.Where(d =>
                    d.Title.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                    d.Description.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                    d.Id.Contains(_search, StringComparison.OrdinalIgnoreCase));
            }

            SettingsList.ItemsSource = query
                .Select(d => new SettingItem(d, _engine))
                .ToList();
        }

        private void OnEditJsonClicked(object sender, EventArgs e)
        {
            _openFile?.Invoke(_engine.StoragePath);
        }
    }
}
