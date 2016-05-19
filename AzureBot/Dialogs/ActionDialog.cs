﻿namespace AzureBot.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Azure.Management.ResourceManagement;
    using FormTemplates;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Builder.FormFlow;
    using Microsoft.Bot.Builder.Luis;
    using Microsoft.Bot.Builder.Luis.Models;

    [LuisModel("1b58a513-e98a-4a13-a5c4-f61ac6dc6c84", "0e64d2ae951547f692182b4ae74262cb")]
    [Serializable]
    public class ActionDialog : LuisDialog<string>
    {
        private static string[] ordinals = { "first", "second", "third", "fourth", "fifth" };

        private readonly string originalMessage;

        private readonly ILuisService luisService;

        public ActionDialog(string originalMessage)
        {
            this.originalMessage = originalMessage;

            if (this.luisService == null)
            {
                var type = this.GetType();
                var luisModel = type.GetCustomAttribute<LuisModelAttribute>(inherit: true);
                if (luisModel == null)
                {
                    throw new Exception("Luis model attribute is not set for the class");
                }

                this.luisService = new LuisService(luisModel);
            }

            this.handlerByIntent = new Dictionary<string, IntentHandler>(this.GetHandlersByIntent());
        }

        public override async Task StartAsync(IDialogContext context)
        {
            var luisResult = await this.luisService.QueryAsync(this.originalMessage);

            var intent = luisResult.Intents.OrderByDescending(i => i.Score).FirstOrDefault();

            IntentHandler intentHandler;

            if (intent != null &&
                this.handlerByIntent.TryGetValue(intent.Intent, out intentHandler))
            {
                await intentHandler(context, luisResult);
            }
            else
            {
                await this.None(context, luisResult);
            }
        }

        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            string message = $"Sorry I did not understand: " + string.Join(", ", result.Intents.Select(i => i.Intent));

            await context.PostAsync(message);

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("ListSubscriptions")]
        public async Task ListSubscriptionsAsync(IDialogContext context, LuisResult result)
        {
            int index = 0;
            var accessToken = context.GetAccessToken();

            var subscriptions = await new AzureRepository().ListSubscriptionsAsync(accessToken);

            var subscriptionsText = subscriptions.Aggregate(
                string.Empty,
                (current, next) =>
                    {
                        index++;
                        return current += $"\r\n{index}. {next.DisplayName}";
                    });

            await context.PostAsync($"Your subscriptions are: {subscriptionsText}");

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("UseSubscription")]
        public async Task UseSubscriptionAsync(IDialogContext context, LuisResult result)
        {
            var accessToken = context.GetAccessToken();

            var availableSubscriptions = await new AzureRepository().ListSubscriptionsAsync(accessToken);

            var form = new FormDialog<SubscriptionFormState>(
                new SubscriptionFormState(availableSubscriptions),
                EntityForms.BuildSubscriptionForm,
                FormOptions.PromptInStart,
                result.Entities);

            context.Call(form, this.UseSubscriptionFormComplete);
        }

        [LuisIntent("ListVms")]
        public async Task ListVmsAsync(IDialogContext context, LuisResult result)
        {
            var accessToken = context.GetAccessToken();
            var subscriptionId = context.GetSubscriptionId();

            var virtualMachines = await new AzureRepository().ListVirtualMachinesAsync(accessToken, subscriptionId);

            var virtualMachinesText = virtualMachines.Aggregate(
                string.Empty,
                (current, next) =>
                    {
                        return current += $"\r\n• {next.Name} ({next.Status})";
                    });

            await context.PostAsync($"Available VMs are:\r\n {virtualMachinesText}");
            context.Wait(this.MessageReceived);
        }

        [LuisIntent("StartVm")]
        public async Task StartVmAsync(IDialogContext context, LuisResult result)
        {
            var accessToken = context.GetAccessToken();
            var subscriptionId = context.GetSubscriptionId();

            var availableVMs = (await new AzureRepository().ListVirtualMachinesAsync(accessToken, subscriptionId))
                                .ToArray();

            var form = new FormDialog<VirtualMachineFormState>(
                new VirtualMachineFormState(availableVMs, Operations.Start),
                EntityForms.BuildVirtualMachinesForm,
                FormOptions.PromptInStart,
                result.Entities);
            context.Call(form, this.StartVirtualMachineFormComplete);
        }

        [LuisIntent("StopVm")]
        public async Task StopVmAsync(IDialogContext context, LuisResult result)
        {
            var accessToken = context.GetAccessToken();
            var subscriptionId = context.GetSubscriptionId();

            var availableVMs = (await new AzureRepository().ListVirtualMachinesAsync(accessToken, subscriptionId))
                               .ToArray();

            var form = new FormDialog<VirtualMachineFormState>(
                new VirtualMachineFormState(availableVMs, Operations.Stop),
                EntityForms.BuildVirtualMachinesForm,
                FormOptions.PromptInStart,
                result.Entities);
            context.Call(form, this.StopVirtualMachineFormComplete);
        }

        [LuisIntent("RunRunbook")]
        public async Task StartRunbookAsync(IDialogContext context, LuisResult result)
        {
            var accessToken = context.GetAccessToken();
            var subscriptionId = context.GetSubscriptionId();

            var availableAutomationAccounts = (await new AzureRepository().ListAutomationAccountsAsync(accessToken, subscriptionId)).ToList();

            var form = new FormDialog<RunbookFormState>(
                new RunbookFormState(availableAutomationAccounts),
                EntityForms.BuildRunbookForm,
                FormOptions.PromptInStart,
                result.Entities);
            context.Call(form, this.StartRunbookParametersAsync);
        }

        public async Task StartRunbookParametersAsync(IDialogContext context, IAwaitable<RunbookFormState> result)
        {
            var runbookFormState = await result;
            if (!runbookFormState.SelectedRunbook.RunbookParameters.Any())
            {
                await this.RunbookFormComplete(context, result);
            }

            var form = new FormDialog<RunbookFormState>(
                await result,
                () => EntityForms.BuildRunbookParametersForm(runbookFormState),
                FormOptions.PromptInStart);
            context.Call(form, this.RunbookFormComplete);
        }

        private async Task RunbookFormComplete(IDialogContext context, IAwaitable<RunbookFormState> result)
        {
            try
            {
                var accessToken = context.GetAccessToken();
                var runbookFormState = await result;

                await context.PostAsync($"Running the '{runbookFormState.RunbookName}' runbook in '{runbookFormState.AutomationAccountName}' automation account.");

                await new AzureRepository().StartRunbookAsync(
                    accessToken,
                    runbookFormState.SelectedAutomationAccount.SubscriptionId, 
                    runbookFormState.SelectedAutomationAccount.ResourceGroup,
                    runbookFormState.SelectedAutomationAccount.AutomationAccountName, 
                    runbookFormState.RunbookName);
            }
            catch (FormCanceledException<VirtualMachineFormState>)
            {
                await context.PostAsync("You have canceled the operation. What would you like to do next?");
            }

            context.Wait(this.MessageReceived);
        }

        private async Task StartVirtualMachineFormComplete(IDialogContext context, IAwaitable<VirtualMachineFormState> result)
        {
            try
            {
                var virtualMachineFormState = await result;

                await context.PostAsync($"Starting the {virtualMachineFormState.VirtualMachine} virtual machine.");

                var accessToken = context.GetAccessToken();
                await new AzureRepository().StartVirtualMachineAsync(
                    accessToken,
                    virtualMachineFormState.SelectedVM.SubscriptionId,
                    virtualMachineFormState.SelectedVM.ResourceGroup,
                    virtualMachineFormState.SelectedVM.Name);
            }
            catch (FormCanceledException<VirtualMachineFormState>)
            {
                await context.PostAsync("You have canceled the operation. What would you like to do next?");
            }

            context.Wait(this.MessageReceived);
        }

        private async Task StopVirtualMachineFormComplete(IDialogContext context, IAwaitable<VirtualMachineFormState> result)
        {
            try
            {
                var virtualMachineFormState = await result;

                await context.PostAsync($"Stopping the {virtualMachineFormState.VirtualMachine} virtual machine.");

                var selectedVM = virtualMachineFormState.SelectedVM;
                var accessToken = context.GetAccessToken();
                await new AzureRepository().StopVirtualMachineAsync(
                    accessToken, 
                    virtualMachineFormState.SelectedVM.SubscriptionId,
                    virtualMachineFormState.SelectedVM.ResourceGroup,
                    virtualMachineFormState.SelectedVM.Name);
            }
            catch (FormCanceledException<VirtualMachineFormState>)
            {
                await context.PostAsync("You have canceled the operation. What would you like to do next?");
            }

            context.Wait(this.MessageReceived);
        }

        private async Task UseSubscriptionFormComplete(IDialogContext context, IAwaitable<SubscriptionFormState> result)
        {
            try
            {
                var subscriptionFormState = await result;
                var selectedSubscription = subscriptionFormState.AvailableSubscriptions.Single(sub => sub.SubscriptionId == subscriptionFormState.SubscriptionId);
                context.StoreSubscriptionId(subscriptionFormState.SubscriptionId);
                await context.PostAsync($"Setting {selectedSubscription.DisplayName} as the current subscription.");
            }
            catch (FormCanceledException<SubscriptionFormState>)
            {
                await context.PostAsync("You have canceled the operation. What would you like to do next?");
            }

            context.Wait(this.MessageReceived);
        }
    }
}