﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Livet;
using Inscribe.Storage;
using Livet.Commands;
using Inscribe.ViewModels.PartBlocks.BlockCommon;
using Inscribe.Util;

namespace Inscribe.ViewModels.PartBlocks.NotifyBlock
{
    public class TwitterEventInfoViewModel : ViewModel
    {
        public TwitterEventInfoViewModel()
        {
            ViewModelHelper.BindNotification(EventStorage.EventChangedEvent, this, (o, e) => TwitterEventInfoChanged());
        }

        private void TwitterEventInfoChanged()
        {
            var news = EventStorage.Events.Except(this._events.Select(vm => vm.Description)).ToArray();
            var rems = this._events.Where(vm => !EventStorage.Events.Contains(vm.Description)).ToArray();
            rems.ForEach(vm => _events.Remove(vm));
            news.Select(s => new NotificationItemViewModel(s, false)).ForEach(_events.Add);
        }

        private ConcurrentObservable<NotificationItemViewModel> _events = new ConcurrentObservable<NotificationItemViewModel>();
        public ConcurrentObservable<NotificationItemViewModel> Events
        {
            get { return this._events; }
        }
    }
}
