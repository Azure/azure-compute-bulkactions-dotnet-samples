using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute.BulkActions.Models;
using Azure.ResourceManager.Resources;
using UtilityMethods;

namespace BulkHibernate;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // SubscriptionId: The subscription under which the virtual machines are located (dummy value).
        const string subscriptionId = "a4f8220e-84cb-47a6-b2c0-c1900805f616";

        // ResourceGroupName: The resource group under which the virtual machines are located (dummy value).
        const string resourceGroupName = "demo-rg";

        // Credential: The Azure credential used to authenticate the request.
        TokenCredential cred = new DefaultAzureCredential();

        // Client: The Azure Resource Manager client used to interact with ARM.
        ArmClient client = new(cred);

        SubscriptionResource subscriptionResource = HelperMethods.GetSubscriptionResource(client, subscriptionId);
        // The bulk operation runs in this resource group's region (location is derived from the RG).
        ResourceGroupResource resourceGroupResource = await subscriptionResource.GetResourceGroupAsync(resourceGroupName);

        // Execution parameters including the retry policy applied to each operation on failure.
        var executionParams = new ScheduledActionExecutionParameterDetail()
        {
            RetryPolicy = new BulkOperationRetryPolicy()
            {
                // Retry window in minutes: range 5-120.
                RetryWindowInMinutes = 45
            }
        };

        // Virtual machine resource identifiers to hibernate. All VMs must be in the same subscription and resourceGroup.
        List<ResourceIdentifier> resourceIds = HelperMethods.BuildVmResourceIds(
            subscriptionId,
            resourceGroupName,
            new[] { "dummy-vm-600", "dummy-vm-611", "dummy-vm-612" });

        await BulkActionsOperations.BulkHibernateOperationAsync(resourceGroupResource, executionParams, resourceIds);
    }
}
