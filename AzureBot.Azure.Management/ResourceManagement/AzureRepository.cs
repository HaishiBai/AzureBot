namespace AzureBot.Azure.Management.ResourceManagement
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Management.Automation;
    using Microsoft.Azure.Management.Compute;
    using Microsoft.Azure.Subscriptions;
    using Models;
    using AzureModels = Microsoft.Azure.Management.Automation.Models;
    using Microsoft.Azure;
    using Microsoft.Rest;
    public class AzureRepository
    {
        public async Task<IEnumerable<Subscription>> ListSubscriptionsAsync(string accessToken)
        {
            var credentials = new TokenCloudCredentials(accessToken);

            using (SubscriptionClient client = new SubscriptionClient(credentials))
            {
                var subscriptionsResult = await client.Subscriptions.ListAsync();
                var subscriptions = subscriptionsResult.Subscriptions.Select(sub => new Subscription { SubscriptionId = sub.SubscriptionId, DisplayName = sub.DisplayName }).ToList();
                return subscriptions;
            }
        }

        public async Task<IEnumerable<VirtualMachine>> ListVirtualMachinesAsync(string accessToken, string subscriptionId)
        {
            var credentials = new TokenCredentials(accessToken);
            
            using (var client = new ComputeManagementClient(credentials))
            {
                client.SubscriptionId = subscriptionId;
                var virtualMachinesResult = await client.VirtualMachines.ListAllAsync();
                var all = virtualMachinesResult.Select(async (vm) =>
                {
                    var resourceGroupName = GetResourceGroup(vm.Id);
                    var response = await client.VirtualMachines.GetAsync(resourceGroupName, vm.Name);
                    //var vmStatus = response.InstanceView.Statuses.Where(p => p.Code.StartsWith("PowerState/")).FirstOrDefault();
                    return new VirtualMachine
                    {
                        SubscriptionId = subscriptionId,
                        ResourceGroup = resourceGroupName,
                        Name = vm.Name,
                        Status = "NA" //vmStatus?.DisplayStatus ?? "NA"
                    };
                });

                return await Task.WhenAll(all.ToArray());
            }
        }
        public async Task<IEnumerable<ScaleSet>> ListScaleSetsAsync(string accessToken, string subscriptionId)
        {
            var credentials = new TokenCredentials(accessToken);
            
            
            using (var client = new ComputeManagementClient(credentials))
            {
                client.SubscriptionId = subscriptionId;
                var virtualMachinesResult = await client.VirtualMachineScaleSets.ListAllAsync();
                var all = virtualMachinesResult.Select(async (vm) =>
                {
                    var resourceGroupName = GetResourceGroup(vm.Id);
                    var response = await client.VirtualMachineScaleSets.GetAsync(resourceGroupName, vm.Name);
                    return new ScaleSet
                    {
                        SubscriptionId = subscriptionId,
                        ResourceGroup = resourceGroupName,
                        Name = vm.Name,
                        Status = response.ProvisioningState,
                        Capacity = response.Sku.Capacity ?? 0
                    };
                });

                return await Task.WhenAll(all.ToArray());
            }
        }

        public async Task<IEnumerable<AutomationAccount>> ListAutomationAccountsAsync(string accessToken, string subscriptionId)
        {
            var credentials = new TokenCloudCredentials(subscriptionId, accessToken);

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
            var credentials = new TokenCloudCredentials(subscriptionId, accessToken);

            using (var automationClient = new AutomationManagementClient(credentials))
            {
                var automationRunBooksResult = await automationClient.Runbooks.ListAsync(resourceGroupName, automationAccountName);

                var automationRunBooks = automationRunBooksResult.Runbooks.Select(
                    runBook => new RunBook { RunBookId = runBook.Id, RunBookName = runBook.Name }).ToList();

                return automationRunBooks;
            }
        }

        public async Task<bool> StartVirtualMachineAsync(string accessToken, string subscriptionId, string resourceGroupName, string virtualMachineName)
        {
            var credentials = new TokenCredentials(accessToken);
            using (var client = new ComputeManagementClient(credentials))
            {
                client.SubscriptionId = subscriptionId;
                var statusTask = client.VirtualMachines.StartAsync(resourceGroupName, virtualMachineName);
                statusTask.Wait();
                return statusTask.IsCompleted;
            }
        }

        public async Task<bool> ScaleScaleSetAsync(string accessToken, string subscriptionId, string resourceGroupName, string scaleSetName)
        {
            var credentials = new TokenCredentials(accessToken);
            using (var client = new ComputeManagementClient(credentials))
            {
                client.SubscriptionId = subscriptionId;
                var scaleSet = await client.VirtualMachineScaleSets.GetAsync(resourceGroupName, scaleSetName);
                scaleSet.Sku.Capacity = 4;
                var statusTask = client.VirtualMachineScaleSets.BeginCreateOrUpdateAsync(resourceGroupName, scaleSetName, scaleSet);
                statusTask.Wait();
                return statusTask.IsCompleted;
            }

        }

            public async Task<bool> StopVirtualMachineAsync(string accessToken, string subscriptionId, string resourceGroupName, string virtualMachineName)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);
            using (var client = new ComputeManagementClient(credentials))
            {
                var statusTask = client.VirtualMachines.PowerOffAsync(resourceGroupName, virtualMachineName);
                statusTask.Wait();
                return statusTask.IsCompleted;
            }
        }

        public async Task<bool> StartRunBookAsync(string accessToken, string subscriptionId, string resourceGroupName, string automationAccountName, string runBookName)
        {
            var credentials = new TokenCloudCredentials(subscriptionId, accessToken);

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