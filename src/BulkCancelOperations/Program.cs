using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using UtilityMethods;

namespace BulkCancelOperations;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // SubscriptionId: The subscription under which the operations were submitted (dummy value).
        const string subscriptionId = "a4f8220e-84cb-47a6-b2c0-c1900805f616";

        // ResourceGroupName: The resource group under which the operations were submitted (dummy value).
        const string resourceGroupName = "demo-rg";

        // Credential: The Azure credential used to authenticate the request.
        TokenCredential cred = new DefaultAzureCredential();

        // Client: The Azure Resource Manager client used to interact with ARM.
        ArmClient client = new(cred);

        SubscriptionResource subscriptionResource = HelperMethods.GetSubscriptionResource(client, subscriptionId);
        ResourceGroupResource resourceGroupResource = await subscriptionResource.GetResourceGroupAsync(resourceGroupName);

        // Operation IDs to cancel. These are returned when a Start/Deallocate/Delete/Hibernate
        // operation is submitted, and can be cancelled while the operation is still pending.
        var operationIds = new[]
        {
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000002",
        };

        await BulkActionsOperations.BulkCancelOperationsAsync(resourceGroupResource, operationIds);
    }
}
