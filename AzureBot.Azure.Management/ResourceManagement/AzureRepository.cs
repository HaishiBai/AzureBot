﻿namespace AzureBot.Azure.Management.ResourceManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Management.Automation;
    using Microsoft.Azure.Management.Compute;
    using Microsoft.Azure.Subscriptions;
    using Models;
    using AzureModels = Microsoft.Azure.Management.Automation.Models;
    using TokenCredentials = Microsoft.Azure.TokenCloudCredentials;

    [Serializable]
    public enum OperationStatus
    {
        InProgress = 1,
        Failed = 2,
        Succeeded = 3,
    }

    public class AzureRepository
    {
        public async Task<IEnumerable<Subscription>> ListSubscriptionsAsync(string accessToken)
        {
            var credentials = new TokenCredentials(accessToken);

            using (SubscriptionClient client = new SubscriptionClient(credentials))
            {
                var subscriptionsResult = await client.Subscriptions.ListAsync();
                var subscriptions = subscriptionsResult.Subscriptions.Select(sub => new Subscription { SubscriptionId = sub.SubscriptionId, DisplayName = sub.DisplayName }).ToList();
                return subscriptions;
            }
        }

        public async Task<IEnumerable<VirtualMachine>> ListVirtualMachinesAsync(string accessToken, string subscriptionId)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);
            using (var client = new ComputeManagementClient(credentials))
            {
                var virtualMachinesResult = await client.VirtualMachines.ListAllAsync(null);
                var all = virtualMachinesResult.VirtualMachines.Select(async (vm) =>
                {
                    var resourceGroupName = GetResourceGroup(vm.Id);
                    var response = await client.VirtualMachines.GetWithInstanceViewAsync(resourceGroupName, vm.Name);
                    var vmStatus = response.VirtualMachine.InstanceView.Statuses.Where(p => p.Code.StartsWith("PowerState/")).FirstOrDefault();
                    return new VirtualMachine
                    {
                        SubscriptionId = subscriptionId,
                        ResourceGroup = resourceGroupName,
                        Name = vm.Name,
                        Status = vmStatus?.DisplayStatus ?? "NA"
                    };
                });

                return await Task.WhenAll(all.ToArray());
            }
        }

        public async Task<IEnumerable<AutomationAccount>> ListAutomationAccountsAsync(string accessToken, string subscriptionId)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            using (var automationClient = new AutomationManagementClient(credentials))
            {
                var automationAccountsResult = await automationClient.AutomationAccounts.ListAsync(null);
                var automationAccounts = await Task.WhenAll(
                    automationAccountsResult.AutomationAccounts.Select(
                        async account => new AutomationAccount
                        {
                            SubscriptionId = subscriptionId,
                            ResourceGroup = GetResourceGroup(account.Id),
                            AutomationAccountId = account.Id,
                            AutomationAccountName = account.Name,
                            RunBooks = await this.ListAutomationRunBooks(accessToken, subscriptionId, GetResourceGroup(account.Id), account.Name)
                        }).ToList());
                return automationAccounts;
            }
        }

        public async Task<IEnumerable<RunBook>> ListAutomationRunBooks(string accessToken, string subscriptionId, string resourceGroupName, string automationAccountName)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            using (var automationClient = new AutomationManagementClient(credentials))
            {
                var automationRunBooksResult = await automationClient.Runbooks.ListAsync(resourceGroupName, automationAccountName);

                var automationRunBooks = automationRunBooksResult.Runbooks.Select(
                    runBook => new RunBook { RunBookId = runBook.Id, RunBookName = runBook.Name }).ToList();

                return automationRunBooks;
            }
        }

        public async Task<string> StartVirtualMachineAsync(string accessToken, string subscriptionId, string resourceGroupName, string virtualMachineName)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);
            using (var client = new ComputeManagementClient(credentials))
            {
                var status = await client.VirtualMachines.BeginStartingAsync(resourceGroupName, virtualMachineName);
                return status.AzureAsyncOperation;
            }
        }

        public async Task<OperationStatus> GetVirtualMachineLongRunningOperationStatusAsync(string accessToken, string subscriptionId, string operationStatus)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);
            using (var client = new ComputeManagementClient(credentials))
            {
                var status = await client.GetLongRunningOperationStatusAsync(operationStatus).ConfigureAwait(false);

                return (OperationStatus)Enum.Parse(typeof(OperationStatus), status.Status.ToString());
            }
        }

        public async Task<bool> StopVirtualMachineAsync(string accessToken, string subscriptionId, string resourceGroupName, string virtualMachineName)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);
            using (var client = new ComputeManagementClient(credentials))
            {
                var status = await client.VirtualMachines.PowerOffAsync(resourceGroupName, virtualMachineName);
                return status.Status != Microsoft.Azure.Management.Compute.Models.ComputeOperationStatus.Failed;
            }
        }

        public async Task<bool> StartRunBookAsync(string accessToken, string subscriptionId, string resourceGroupName, string automationAccountName, string runBookName)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            using (var client = new AutomationManagementClient(credentials))
            {
                var parameters = new AzureModels.JobCreateParameters(
                    new AzureModels.JobCreateProperties(
                        new AzureModels.RunbookAssociationProperty
                        {
                            Name = runBookName
                        }));
                var jobCreateResult = await client.Jobs.CreateAsync(resourceGroupName, automationAccountName, parameters);
                return jobCreateResult.StatusCode == System.Net.HttpStatusCode.Created;
            }
        }

        private static string GetResourceGroup(string id)
        {
            var segments = id.Split('/');
            var resourceGroupName = segments.SkipWhile(segment => segment != "resourceGroups").ElementAtOrDefault(1);
            return resourceGroupName;
        }
    }
}