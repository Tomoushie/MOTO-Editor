// Moto.Editor/Views/ThreadListView.xaml.cs
using System;
using Microsoft.Maui.Controls;
using Moto.Editor.Models;
using Moto.Editor.Services;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Panneau gauche : liste des conversations IA avec recherche.
    /// </summary>
    public partial class ThreadListView : ContentView
    {
        private readonly ChatService _chat;

        public ThreadListView(ChatService chat)
        {
            InitializeComponent();

            _chat = chat;
            ThreadList.ItemsSource = _chat.Threads;

            ThreadList.SelectionChanged += (s, e) =>
            {
                if (e.CurrentSelection.FirstOrDefault() is ChatThread thread)
                {
                    _chat.SwitchThread(thread);
                }
            };
        }

        private void OnNewThreadClicked(object sender, EventArgs e)
        {
            _chat.CreateThread();
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            // Filtre la liste via le service.
            var results = _chat.SearchThreads(e.NewTextValue);

            ThreadList.ItemsSource = results;
        }
    }
}
