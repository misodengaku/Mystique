﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dulcet.Twitter;
using Inscribe.Model;
using Inscribe.Storage;
using Inscribe.ViewModels.Common;
using Inscribe.ViewModels.Dialogs;
using Inscribe.ViewModels.PartBlocks.MainBlock;
using Livet;
using Livet.Commands;
using Livet.Messaging;
using Mystique.Views.Behaviors.Messages;
using Inscribe.ViewModels.PartBlocks.MainBlock.TimelineChild;

namespace Inscribe.ViewModels.PartBlocks.InputBlock
{
    public class InputBlockViewModel : ViewModel
    {
        public MainWindowViewModel Parent { get; private set; }
        public InputBlockViewModel(MainWindowViewModel parent)
        {
            this.Parent = parent;
            this._ImageStackingViewViewModel = new ImageStackingViewViewModel();
            this._UserSelectorViewModel = new UserSelectorViewModel();
            this._UserSelectorViewModel.LinkChanged += () => this.LinkUserChanged(this.UserSelectorViewModel.LinkElements);

            // Listen changing tab
            this.Parent.ColumnOwnerViewModel.CurrentTabChanged += new Action<TabViewModel>(CurrentTabChanged);
        }

        void UserSelectorLinkChanged(object sender, EventArgs e)
        {
            this.LinkUserChanged(this.UserSelectorViewModel.LinkElements);
        }

        void CurrentTabChanged(TabViewModel tab)
        {
            this.SetCurrentTab(tab);
        }

        UserSelectorViewModel _UserSelectorViewModel;

        public UserSelectorViewModel UserSelectorViewModel
        {
            get { return _UserSelectorViewModel; }
        }

        ImageStackingViewViewModel _ImageStackingViewViewModel;

        public ImageStackingViewViewModel ImageStackingViewViewModel
        {
            get { return _ImageStackingViewViewModel; }
        }
      
        #region MenuItems

        #region ShowConfigCommand
        DelegateCommand _ShowConfigCommand;

        public DelegateCommand ShowConfigCommand
        {
            get
            {
                if (_ShowConfigCommand == null)
                    _ShowConfigCommand = new DelegateCommand(ShowConfig);
                return _ShowConfigCommand;
            }
        }

        public void ShowConfig()
        {
            var vm = new SettingViewModel();
            Messenger.Raise(new TransitionMessage(vm, "ShowConfig"));
        }
        #endregion


        #region ShowAboutCommand
        DelegateCommand _ShowAboutCommand;

        public DelegateCommand ShowAboutCommand
        {
            get
            {
                if (_ShowAboutCommand == null)
                    _ShowAboutCommand = new DelegateCommand(ShowAbout);
                return _ShowAboutCommand;
            }
        }

        private void ShowAbout()
        {
            Messenger.Raise(new TransitionMessage("ShowAbout"));
        }
        #endregion
      

        #endregion

        #region Input control

        private InputDescription _currentInputDescription = null;
        public InputDescription CurrentInputDescription
        {
            get
            {
                if (this._currentInputDescription == null)
                {
                    this._currentInputDescription = new InputDescription();
                    this._currentInputDescription.PropertyChanged += (o, e) => UpdateCommand.RaiseCanExecuteChanged();
                }
                return this._currentInputDescription;
            }
        }

        private void ResetInputDescription()
        {
            this._currentInputDescription = null;
            this.overrideTargets = null;
            RaisePropertyChanged(() => CurrentInputDescription);
            UpdateAccountImages();
        }

        public void SetCurrentTab(TabViewModel tvm)
        {
            if (tvm != null)
                this.UserSelectorViewModel.LinkElements = tvm.TabProperty.LinkAccountInfos;
            else
                this.UserSelectorViewModel.LinkElements = AccountStorage.Accounts;
            UpdateAccountImages();
        }

        public void SetInReplyTo(TweetViewModel tweet)
        {
            if (tweet == null)
            {
                // clear in reply to
                this.CurrentInputDescription.InReplyToId = 0;
            }
            else
            {
                if (this.CurrentInputDescription.InputText.StartsWith(".@"))
                {
                    // multi reply mode
                    string remain;
                    var screens = SplitTweet(this.CurrentInputDescription.InputText, out remain);
                    if (screens.FirstOrDefault(s => s.Equals(tweet.Status.User.ScreenName, StringComparison.CurrentCultureIgnoreCase)) != null)
                    {
                        this.CurrentInputDescription.InputText = "." +
                            screens.Where(s => s.Equals(tweet.Status.User.ScreenName, StringComparison.CurrentCultureIgnoreCase)).JoinString(" ") + " " +
                            remain;
                    }
                    else
                    {
                        this.CurrentInputDescription.InputText = "." +
                            screens.JoinString(" ") + " " +
                            tweet.Status.User.ScreenName + " " +
                            remain;
                    }
                    this.CurrentInputDescription.InReplyToId = 0;
                }
                else if (this.CurrentInputDescription.InReplyToId != 0 && this.CurrentInputDescription.InputText.StartsWith("@"))
                {
                    // single reply mode -> muliti reply mode
                    if (this.CurrentInputDescription.InReplyToId == tweet.Status.Id)
                    {
                        this.CurrentInputDescription.InputText = "." + this.CurrentInputDescription.InputText;
                        this.CurrentInputDescription.InReplyToId = 0;
                    }
                    else
                    {
                        string remain;
                        var screens = SplitTweet(this.CurrentInputDescription.InputText, out remain);
                        this.CurrentInputDescription.InputText = "." +
                            screens.JoinString(" ") + " " +
                            tweet.Status.User.ScreenName +  " " +
                            remain;
                        this.CurrentInputDescription.InReplyToId = 0;
                    }
                    this.overrideTargets = null;
                }
                else
                {
                    // single reply mode
                    this.CurrentInputDescription.InReplyToId = tweet.Status.Id;
                    if (tweet.Status is TwitterDirectMessage)
                    {
                        this.OverrideTarget(new[] { AccountStorage.Get(((TwitterDirectMessage)tweet.Status).Recipient.ScreenName) });
                        this.CurrentInputDescription.InputText = "d @" + tweet.Status.User.ScreenName;
                    }
                    else
                    {
                        if (tweet.Status is TwitterStatus && AccountStorage.Contains(((TwitterStatus)tweet.Status).InReplyToUserScreenName))
                        {
                            this.OverrideTarget(new[] { AccountStorage.Get(((TwitterStatus)tweet.Status).InReplyToUserScreenName) });
                        }
                        this.CurrentInputDescription.InputText = "@" + tweet.Status.User.ScreenName;
                    }
                }
            }
        }

        private IEnumerable<string> SplitTweet(string input, out string after)
        {
            List<string> screens = new List<string>();
            if (input.StartsWith("."))
                input = input.Substring(1);
            var splitted = input.Split(' ');
            int i = 0;
            for (; i < splitted.Length; i++)
            {
                if (splitted[i].Length == 0)
                    continue;
                if (splitted[i][0] == '@' &&
                    splitted[i].Skip(1).All(
                    c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    // valid screen name
                    screens.Add(splitted[i]);
                }
                else
                {
                    // invalid screen name
                    break;
                }
            }
            if (i >= splitted.Length)
                after = String.Empty;
            else
                after = splitted.Skip(i).JoinString(" ");
            return screens.Distinct().ToArray();
        }

        public void SetText(string text)
        {
            this.CurrentInputDescription.InputText = text;
        }

        public void SetInputCaretIndex(int selectionStart, int selectionLength = 0)
        {
            this.Messenger.Raise(new TextBoxSetCaretMessage("SetCaret", selectionStart, selectionLength));
        }

        IEnumerable<AccountInfo> overrideTargets = null;

        /// <summary>
        /// 現在の投稿対象をオーバーライドします。
        /// </summary>
        public void OverrideTarget(IEnumerable<AccountInfo> accounts)
        {
            this.overrideTargets = accounts;
            UpdateAccountImages();
            UpdateCommand.RaiseCanExecuteChanged();
        }

        private void LinkUserChanged(IEnumerable<AccountInfo> info)
        {
            var ctab = this.Parent.ColumnOwnerViewModel.CurrentTab;
            this.overrideTargets = null;
            if (ctab == null) return;
            ctab.TabProperty.LinkAccountInfos = info.ToArray();
            UpdateAccountImages();
            UpdateCommand.RaiseCanExecuteChanged();
        }

        private void UpdateAccountImages()
        {
            IEnumerable<AccountInfo> currentTarget = null;
            if (this.overrideTargets != null)
                currentTarget = this.overrideTargets;
            else
                currentTarget = this.UserSelectorViewModel.LinkElements;
            if (currentTarget != null)
                currentTarget = AccountStorage.Accounts.Where(i => currentTarget.Contains(i)).ToArray();
            Task.Factory.StartNew(() => currentTarget.Select(ai => ai.ProfileImage).ToArray())
                .ContinueWith(r => DispatcherHelper.BeginInvoke(() => ImageStackingViewViewModel.ImageUrls = r.Result));
        }

        #endregion

        #region State control

        private bool _isOpenInput =false;
        public bool IsOpenInput
        {
            get { return this._isOpenInput; }
            private set
            {
                this._isOpenInput = value;
                RaisePropertyChanged(() => IsOpenInput);
            }
        }

        public void SetOpenText(bool isOpen, bool setFocus = false)
        {
            this.IsOpenInput = isOpen;
            ResetInputDescription();
            if (setFocus && isOpen)
                this.Messenger.Raise(new InteractionMessage("FocusToText"));
        }


        #endregion

        #region Commands

        #region OpenInputCommand
        DelegateCommand _OpenInputCommand;

        public DelegateCommand OpenInputCommand
        {
            get
            {
                if (_OpenInputCommand == null)
                    _OpenInputCommand = new DelegateCommand(OpenInput);
                return _OpenInputCommand;
            }
        }

        private void OpenInput()
        {
            SetOpenText(true, true);
        }
        #endregion

        #region CloseInputCommand
        DelegateCommand _CloseInputCommand;

        public DelegateCommand CloseInputCommand
        {
            get
            {
                if (_CloseInputCommand == null)
                    _CloseInputCommand = new DelegateCommand(CloseInput);
                return _CloseInputCommand;
            }
        }

        private void CloseInput()
        {
            SetOpenText(false);
        }
        #endregion

        #region RemoveInReplyToCommand
        DelegateCommand _RemoveInReplyToCommand;

        public DelegateCommand RemoveInReplyToCommand
        {
            get
            {
                if (_RemoveInReplyToCommand == null)
                    _RemoveInReplyToCommand = new DelegateCommand(RemoveInReplyTo);
                return _RemoveInReplyToCommand;
            }
        }

        private void RemoveInReplyTo()
        {
            this.CurrentInputDescription.InReplyToId = 0;
        }
        #endregion

        #region URL Shortening

        private bool _isUrlShortening = false;
        public bool IsUrlShortening
        {
            get { return this._isUrlShortening; }
            set

            {
                this._isUrlShortening = value;
                RaisePropertyChanged(() => IsUrlShortening);
            }
        }

        #endregion

        #region AttachImageCommand
        DelegateCommand _AttachImageCommand;

        public DelegateCommand AttachImageCommand
        {
            get
            {
                if (_AttachImageCommand == null)
                    _AttachImageCommand = new DelegateCommand(AttachImage);
                return _AttachImageCommand;
            }
        }

        private void AttachImage()
        {
            if (this.CurrentInputDescription.AttachedImage != null)
            {
                this.CurrentInputDescription.AttachedImage = null;
            }
            else
            {
                var ofm = new Livet.Messaging.IO.OpeningFileSelectionMessage("OpenFile");
                ofm.Filter = "画像ファイル|*.jpg; *.png; *.gif; *.bmp|すべてのファイル|*.*";
                ofm.Title = "添付する画像を選択";
                var ret = this.Messenger.GetResponse(ofm);
                if (ret.Response != null)
                {
                    this.CurrentInputDescription.AttachedImage = ret.Response;
                }
            }
        }
        #endregion

        #region UpdateCommand
        DelegateCommand _UpdateCommand;

        public DelegateCommand UpdateCommand
        {
            get
            {
                if (_UpdateCommand == null)
                    _UpdateCommand = new DelegateCommand(Update, CanUpdate);
                return _UpdateCommand;
            }
        }

        private bool CanUpdate()
        {
            return !String.IsNullOrEmpty(this.CurrentInputDescription.InputText)
                && this.CurrentInputDescription.InputText.Length <= TwitterDefine.TweetMaxLength &&
                (this.overrideTargets != null || this.UserSelectorViewModel.LinkElements.Count() > 0);
        }

        private void Update()
        {
            if (this.overrideTargets != null)
                this.CurrentInputDescription.ReadyUpdate(this,this.overrideTargets.ToArray()).ForEach(AddUpdateWorker);
            else
                this.CurrentInputDescription.ReadyUpdate(this, this.UserSelectorViewModel.LinkElements.ToArray()).ForEach(AddUpdateWorker);
            ResetInputDescription();
        }

        #endregion
      
        #endregion

        #region Posing control

        private void AddUpdateWorker(TweetWorker w)
        {
            DispatcherHelper.BeginInvoke(() => this._updateWorkers.Add(w));
            w.RemoveRequired += () => DispatcherHelper.BeginInvoke(() => this._updateWorkers.Remove(w));
            w.DoWork().ContinueWith(t =>
            {
                if (t.Result)
                {
                    Thread.Sleep(3000);
                    DispatcherHelper.BeginInvoke(() => this._updateWorkers.Remove(w));
                }
            });
        }

        ObservableCollection<TweetWorker> _updateWorkers = new ObservableCollection<TweetWorker>();
        public ObservableCollection<TweetWorker> UpdateWorkers
        {
            get { return this._updateWorkers; }
        }

        #endregion

    }
}