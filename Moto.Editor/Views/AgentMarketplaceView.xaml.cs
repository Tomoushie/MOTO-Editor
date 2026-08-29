using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Agents;

namespace Moto.Editor.Views;

public partial class AgentMarketplaceView : ContentView
{
    private readonly AgentMarketplaceService _marketplace;
    public ObservableCollection<AgentDescriptor> Agents { get; } = new();

    public AgentMarketplaceView(AgentMarketplaceService marketplace)
    {
        InitializeComponent();
        _marketplace = marketplace;
        BindingContext = this;
        AgentsList.ItemsSource = Agents;
        Refresh();
    }

    private void Refresh()
    {
        Agents.Clear();
        foreach (var agent in _marketplace.ListAvailable())
            Agents.Add(agent);
    }

    private void OnInstallClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string id)
        {
            _marketplace.Install(id);
            StatusLabel.Text = $"✅ Agent {id} installé";
        }
    }
}
