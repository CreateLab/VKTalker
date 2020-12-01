﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using DynamicData.Binding;
using ReactiveUI;
using VkNet;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Model;
using VkNet.Model.RequestParams;
using VKTalker.Models;

namespace VKTalker.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private bool _isEnabled = false;
        private ScrollModel _scrollModel;
        private DialogModel _selectedModel;
        private object lockDialog = new object();
        private object lockMessage = new object();
        public ObservableCollectionExtended<DialogModel> DialogModels { get; } =
            new ObservableCollectionExtended<DialogModel>();
        public ObservableCollectionExtended<MessageModel> MessageModels { get; } =
            new ObservableCollectionExtended<MessageModel>();

        private VkApi api = new VkApi();
        Timer dTimer = new Timer(1000);
        Timer mTimer = new Timer(1000);
        private ReactiveCommand<Unit, Unit> authCommand { get; }
        private ReactiveCommand<Unit, Unit> dialogsDataCommand { get; }
        private ReactiveCommand<Unit, Unit> messagesDataCommand { get; }

        private long? ChatId { get; set; }
        public MainWindowViewModel()
        {
            dTimer.AutoReset = true;
            dTimer.Enabled = true;
            mTimer.AutoReset = true;
            mTimer.Enabled = true;
            authCommand = ReactiveCommand.CreateFromTask(Auth);
            dialogsDataCommand = ReactiveCommand.CreateFromTask(SetStartupDialog);
            messagesDataCommand = ReactiveCommand.CreateFromTask(GetMessages);
            authCommand.ThrownExceptions.Subscribe(e => Console.WriteLine(e.Message));
            authCommand.Execute().Subscribe();
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                dTimer.Elapsed += (sender, args) =>
                {
                    dialogsDataCommand.Execute().Wait();
                };
                mTimer.Elapsed += (sender, args) =>
                {
                    messagesDataCommand.Execute().Wait();
                };
                this.RaiseAndSetIfChanged(ref _isEnabled, value);
            }
        }

        public DialogModel SelectedModel
        {
            get => _selectedModel;
            set
            {
                ChatId = value?.ChatId;
                this.RaiseAndSetIfChanged(ref _selectedModel, value);
            }
        }

        public ScrollModel ScrollModelProperty
        {
            get => _scrollModel;
            set => this.RaiseAndSetIfChanged(ref _scrollModel , value);
        }

        private async Task Auth()
        {
            var data = File.ReadLines("Config").ToList();
            await api.AuthorizeAsync(new ApiAuthParams
            {
                Login = data[1],
                Password = data[2],
                ApplicationId = Convert.ToUInt64(data[0]),
                Settings = Settings.All
            });
            if (api.UserId != default(long))
            {
                IsEnabled = true;
                await SetStartupDialog();
            }
        }

        private async Task SetStartupDialog()
        {
            var dialogs = await api.Messages.GetConversationsAsync(new GetConversationsParams
            {
                Filter = GetConversationFilter.All,
                Count = 20,
                Offset = 0,
                Extended = true
            });
            var models = dialogs.Items.Select(dialog => new DialogModel
            {
                Message = GetText(dialog.LastMessage),
                Name = GetName(dialog, dialogs),
                ChatId = dialog.Conversation.Peer.Id
            }).ToList();

            if (SelectedModel is not null)
            {
                var newModel = models.FirstOrDefault(m => m.ChatId == SelectedModel.ChatId);
                if (newModel is not null)
                {
                    var id = models.IndexOf(newModel);
                    models[id] = SelectedModel;
                }
            }

            lock (lockDialog)
            {
                DialogModels.Clear();
                DialogModels.AddRange(models);
            }
            
        }

        private async Task GetMessages()
        {
            if(ChatId is null) 
                return;
            try
            {
                var history = await api.Messages.GetHistoryAsync(new MessagesGetHistoryParams
                {
                    Count = 100,
                    Extended = true,
                    PeerId = ChatId,
                });
                var messages = history.Messages.Select(m => new MessageModel
                {
                    Name = GetName(m, history.Users),
                    Message = m?.Text ?? m?.Body,
                    ChatId = m.Id
                }).Reverse().ToList();
                lock (lockMessage)
                {
                    var m = MessageModels.LastOrDefault()?.ChatId;
                    var m1 = messages.LastOrDefault()?.ChatId;
                    if (MessageModels.LastOrDefault()?.ChatId != messages.LastOrDefault()?.ChatId)
                    {
                        MessageModels.Clear();
                        MessageModels.AddRange(messages);
                        ScrollModelProperty = new ScrollModel{Count = MessageModels.Count};
                    }
                }
            }
            catch (Exception e)
            {
                
            }
        }

        private string GetName(Message message, IEnumerable<User> getConversationsResult)
        {
            var user = getConversationsResult.FirstOrDefault(u => u.Id == message.FromId);
            return user?.FirstName ?? string.Empty;
        }

        private string GetName(ConversationAndLastMessage dialog, GetConversationsResult getConversationsResult)
        {
            var title = dialog.Conversation.ChatSettings?.Title;
            if (!string.IsNullOrEmpty(title))
                return title;
            var user = getConversationsResult.Profiles.FirstOrDefault(u => u.Id == dialog.LastMessage.FromId);
            return user?.FirstName ?? string.Empty;
        }

        private string GetText(Message dialogLastMessage)
        {
            if (dialogLastMessage?.Text == null) return string.Empty;
            return dialogLastMessage.Text.Length > 10
                ? dialogLastMessage.Text.Substring(0, 10)+"..."
                : dialogLastMessage.Text;
        }
    }
}