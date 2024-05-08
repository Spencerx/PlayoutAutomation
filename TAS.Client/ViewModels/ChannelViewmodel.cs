﻿using System.Windows.Controls;
using System.Windows.Input;
using TAS.Client.Common;
using TAS.Common;
using TAS.Common.Interfaces;

namespace TAS.Client.ViewModels
{
    public class ChannelViewmodel : ViewModelBase
    {
        private int _selectedTabIndex;

        public ChannelViewmodel(IEngine engine, bool showEngine, bool showMedia)
        {
            DisplayName = engine.EngineName;
            if (showEngine)
                Engine = new EngineViewmodel(engine, engine.Preview);
            if (showMedia)
                MediaManager = new MediaManagerViewmodel(engine, engine.HaveRight(EngineRight.Preview) ? engine.Preview : null);
            CommandSwitchTab = new UiCommand(CommandName(nameof(SwitchTab)), SwitchTab, _ => showEngine && showMedia);
            SelectedTabIndex = showEngine ? 0 : 1;
        }

        public ICommand CommandSwitchTab { get; }

        public string DisplayName { get; }

        public EngineViewmodel Engine { get; }

        public MediaManagerViewmodel MediaManager { get; }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetField(ref _selectedTabIndex, value);
        }

        public TabItem SelectedItem
        {
            set
            {
                if (value?.DataContext is EngineViewmodel engine)
                    OnIdle(() => engine.Focus());
            }
        }

        protected override void OnDispose()
        {
            Engine?.Dispose();
            MediaManager?.Dispose();
        }

        private void SwitchTab(object _) => SelectedTabIndex = _selectedTabIndex == 0 ? 1 : 0;
    }
}
