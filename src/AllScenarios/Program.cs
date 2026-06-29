using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute.BulkActions.Models;
using Azure.ResourceManager.Resources;
using UtilityMethods;

namespace AllScenarios;

/// <summary>
/// A combined sample showing several Compute.BulkActions endpoints working together:
/// Start -> Hibernate -> Deallocate -> Delete. Each step submits the operation and polls
/// it to completion using the shared helpers. The individual GetOperationsStatus and
/// CancelOperations endpoints are exercised by the dedicated sample projects.
///
/// All values below are dummy placeholders — replace them before running against real VMs.
/// </summary>
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
                RetryWindowInMinutes = 45
            }
        };

        List<ResourceIdentifier> resourceIds = HelperMethods.BuildVmResourceIds(
            subscriptionId,
            resourceGroupName,
            new[] { "dummy-vm-600", "dummy-vm-611" });

        // Step 1: Start the virtual machines.
        Console.WriteLine("=== Step 1: BulkStartOperation ===");
        await BulkActionsOperations.BulkStartOperationAsync(resourceGroupResource, executionParams, resourceIds);

        // Step 2: Hibernate the virtual machines.
        Console.WriteLine("\n=== Step 2: BulkHibernateOperation ===");
        await BulkActionsOperations.BulkHibernateOperationAsync(resourceGroupResource, executionParams, resourceIds);

        // Step 3: Deallocate the virtual machines.
        Console.WriteLine("\n=== Step 3: BulkDeallocateOperation ===");
        await BulkActionsOperations.BulkDeallocateOperationAsync(resourceGroupResource, executionParams, resourceIds);

        // Step 4: Delete the virtual machines.
        Console.WriteLine("\n=== Step 4: BulkDeleteOperation ===");
        await BulkActionsOperations.BulkDeleteOperationAsync(resourceGroupResource, executionParams, resourceIds, forceDeletion: false);

        Console.WriteLine("\nAll scenarios completed.");
    }
}
