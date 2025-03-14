using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using CrossBusExplorer.ServiceBus.Contracts;
using CrossBusExplorer.ServiceBus.Contracts.Types;
using CrossBusExplorer.Website.Extensions;
using CrossBusExplorer.Website.Jobs;
using CrossBusExplorer.Website.Mappings;
using CrossBusExplorer.Website.Models;
using CrossBusExplorer.Website.Pages;
using CrossBusExplorer.Website.Shared;
using CrossBusExplorer.Website.Shared.Messages;
using CrossBusExplorer.Website.Shared.Queue;
using Microsoft.AspNetCore.Components;
using MudBlazor;
namespace CrossBusExplorer.Website.ViewModels;

public class SubscriptionViewModel : ISubscriptionViewModel
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event SubscriptionOperationEventHandler? OnSubscriptionOperation;

    private readonly ISnackbar _snackbar;
    private readonly NavigationManager _navigationManager;
    private readonly IDialogService _dialogService;
    private readonly IJobsViewModel _jobsViewModel;
    private readonly IMessageService _messageService;
    private readonly INavigationViewModel _navigationViewModel;
    private readonly ISubscriptionService _subscriptionService;
    public SubscriptionDetails? SubscriptionDetails { get; private set; }

    public SubscriptionViewModel(
        ISubscriptionService subscriptionService,
        ISnackbar snackbar,
        NavigationManager navigationManager,
        IDialogService dialogService,
        IJobsViewModel jobsViewModel,
        IMessageService messageService)
    {
        _subscriptionService = subscriptionService;
        _snackbar = snackbar;
        _navigationManager = navigationManager;
        _dialogService = dialogService;
        _jobsViewModel = jobsViewModel;
        _messageService = messageService;
    }

    private SubscriptionFormModel? _form;
    public SubscriptionFormModel? Form
    {
        get => _form;
        private set
        {
            _form = value;
            _form.PropertyChanged += (_, _) => this.Notify(PropertyChanged);
            this.Notify(PropertyChanged);
        }
    }

    public async Task InitializeFormAsync(
        string connectionName,
        string topicName,
        string? subscriptionName,
        CancellationToken cancellationToken)
    {
        if (topicName != null && subscriptionName != null)
        {
            var subscription = await _subscriptionService.GetAsync(
                connectionName,
                topicName,
                subscriptionName,
                cancellationToken);

            UpdateFormModel(subscription);

            OnSubscriptionOperation(connectionName, OperationType.Update, SubscriptionDetails.Info);
        }
        else
        {
            CreateFormModel(topicName);
        }
    }

    public async Task SaveSubscriptionFormAsync(string connectionName)
    {
        if (Form != null)
        {
            try
            {
                var result = await SaveSubscriptionAsync(connectionName, Form);

                HandleSaveResult(connectionName, result, Form.OperationType);
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Error while updating subscription: {ex.Message}", Severity.Error);
            }
        }
    }

    private void HandleSaveResult(
        string connectionName,
        OperationResult<SubscriptionDetails> result,
        OperationType operationType)
    {
        if (result.Success && result.Data != null)
        {
            if (operationType == OperationType.Create)
            {
                _navigationManager.NavigateTo(
                    $"subscription/{connectionName}/" +
                    $"{HttpUtility.UrlEncode(result.Data.Info.TopicName)}/" +
                    $"{HttpUtility.UrlEncode(result.Data.Info.SubscriptionName)}");
            }
            else
            {
                UpdateFormModel(result.Data);
            }

            OnSubscriptionOperation(connectionName, operationType, result.Data.Info);

            _snackbar.Add("Saved successfully", Severity.Success);
        }
        else
        {
            _snackbar.Add(
                "Not saved. Please correct parameters and try again.",
                Severity.Error);
        }
    }

    private async Task<OperationResult<SubscriptionDetails>> SaveSubscriptionAsync(
        string connectionName,
        SubscriptionFormModel form)
    {
        switch (form.OperationType)
        {
            case OperationType.Create:
                return await _subscriptionService.CreateAsync(
                    connectionName,
                    Form.ToCreateOptions(),
                    default);
            case OperationType.Update:
                return await _subscriptionService.UpdateAsync(
                    connectionName,
                    Form.ToUpdateOptions(),
                    default);
            default:
                return new OperationResult<SubscriptionDetails>(false, null);
        }
    }

    public void NavigateToNewSubscriptionForm(
        string connectionName,
        string topicName)
    {
        _navigationManager.NavigateTo($"new-subscription/{connectionName}/{topicName}");
    }

    public async Task CloneSubscriptionAsync(
        string connectionName,
        string sourceTopicName,
        string sourceSubscriptionName,
        CancellationToken cancellationToken)
    {
        var parameters = new DialogParameters();

        parameters.Add(nameof(CloneDialog.ConnectionName), connectionName);
        parameters.Add(nameof(CloneDialog.SourceDialogName), sourceTopicName);

        var dialog = _dialogService.Show<CloneDialog>(
            $"Clone subscription {sourceSubscriptionName}",
            parameters,
            new DialogOptions
            {
                FullWidth = true,
                CloseOnEscapeKey = true
            });

        var dialogResult = await dialog.Result;

        if (dialogResult is { Canceled: false, Data: string newSubscriptionName })
        {
            var result = await _subscriptionService.CloneAsync(
                connectionName,
                newSubscriptionName,
                sourceTopicName,
                sourceSubscriptionName,
                cancellationToken);

            HandleSaveResult(connectionName, result, OperationType.Create);
        }
    }
    public async Task DeleteSubscriptionAsync(
        string connectionName,
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken)
    {
        var parameters = new DialogParameters();
        parameters.Add(
            "ContentText",
            $"Are you sure you want to delete subscription {subscriptionName} " +
            $"from {topicName} topic?");

        var dialog = _dialogService.Show<ConfirmDialog>("Confirm", parameters);
        var dialogResult = await dialog.Result;

        if (dialogResult.Data is true)
        {
            var deleteResult =
                await _subscriptionService.DeleteAsync(
                    connectionName,
                    topicName,
                    subscriptionName,
                    cancellationToken);

            if (deleteResult.Success)
            {
                _snackbar.Add(
                    $"Subscription {subscriptionName} successfully deleted.",
                    Severity.Success);
                NavigateToNewSubscriptionForm(connectionName, topicName);
            }
            else
            {
                _snackbar.Add(
                    $"Subscription {subscriptionName} was not deleted. " +
                    $"Please check the subscription name and try again later.",
                    Severity.Error);
            }

            OnSubscriptionOperation(connectionName, OperationType.Delete, SubscriptionDetails.Info);
        }
    }

    public async Task UpdateSubscriptionStatusAsync(
        string connectionName,
        string topicName,
        string subscriptionName,
        ServiceBusEntityStatus status,
        CancellationToken cancellationToken)
    {
        OperationResult<SubscriptionDetails> result = await _subscriptionService.UpdateAsync(
            connectionName,
            new UpdateSubscriptionOptions(topicName, subscriptionName, Status: status),
            cancellationToken);

        HandleSaveResult(connectionName, result, OperationType.Update);
    }

    public async Task PurgeMessagesAsync(
        string connectionName,
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken)
    {
        var dialog = _dialogService.Show<PurgeMessagesDialog>();
        var dialogResult = await dialog.Result;

        if (dialogResult.Data is SubQueue subQueue)
        {
            var job = new PurgeMessagesJob(
                connectionName,
                topicName,
                subscriptionName,
                subQueue,
                GetTotalMessagesCount(subQueue),
                _messageService);

            job.OnCompleted += ReloadData;

            await _jobsViewModel.ScheduleJob(job);
        }
    }
    private long GetTotalMessagesCount(SubQueue subQueue) => subQueue switch
    {
        SubQueue.None => SubscriptionDetails.Info.ActiveMessagesCount,
        SubQueue.DeadLetter => SubscriptionDetails.Info.DeadLetterMessagesCount,
        SubQueue.TransferDeadLetter => SubscriptionDetails.Info.TransferMessagesCount
    };

    public async Task ResendDeadLettersAsync(
        string connectionName,
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken)
    {
        var parameters = new DialogParameters();
        parameters.Add(
            nameof(ConfirmDialog.ContentText),
            $"Are you sure you want to reprocess all dead letter messages from " +
            $"subscription {subscriptionName}?");

        var dialog = _dialogService.Show<ConfirmDialog>("Confirm", parameters);
        var dialogResult = await dialog.Result;

        if (dialogResult.Data is true)
        {
            await _jobsViewModel.ScheduleJob(
                new ResendMessagesJob(
                    connectionName,
                    topicName,
                    subscriptionName,
                    SubQueue.DeadLetter,
                    topicName,
                    GetTotalMessagesCount(SubQueue.DeadLetter),
                    _messageService));
        }
    }

    private async Task ReloadData(
        string connectionName,
        string queueOrTopicName,
        string? subscriptionName)
    {
        await InitializeFormAsync(connectionName, queueOrTopicName, subscriptionName, default);
    }

    private void UpdateFormModel(SubscriptionDetails resultData)
    {
        SubscriptionDetails = resultData;
        Form = SubscriptionDetails.ToFormModel(OperationType.Update);
    }

    private void CreateFormModel(string topicName)
    {
        Form = new SubscriptionFormModel(OperationType.Create)
        {
            TopicName = topicName,
            EnableBatchedOperations = true,
            AutoDeleteOnIdle = TimeSpan.FromDays(365),
            DefaultMessageTimeToLive = TimeSpan.FromDays(365),
            LockDuration = TimeSpan.FromMinutes(1),
            MaxDeliveryCount = 10
        };
    }
}