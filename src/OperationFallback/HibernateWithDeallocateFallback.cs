using Azure.Core;
using Azure.ResourceManager.Compute.BulkActions;
using Azure.ResourceManager.Compute.BulkActions.Models;
using Azure.ResourceManager.Resources;
using UtilityMethods;

namespace OperationFallback;

/// <summary>
/// Hibernate with Deallocate fallback.
///
/// If the Hibernate operation fails after all retries, setting OnFailureAction to
/// <see cref="ComputeBulkOperationKind.Deallocate"/> tells the system to deallocate the VM
/// instead — ensuring resources are released even when hibernation is not possible.
/// </summary>
public static class HibernateWithDeallocateFallback
{
    public static async Task RunAsync(
        ResourceGroupResource resourceGroup,
        AzureLocation location,
        List<ResourceIdentifier> resourceIds)
    {
        Console.WriteLine("[Scenario] Hibernate with Deallocate fallback\n");

        var executionParams = new BulkActionExecutionParameterDetail()
        {
            RetryPolicy = new BulkOperationRetryPolicy()
            {
                RetryWindowInMinutes = 30,
                OnFailureAction = ComputeBulkOperationKind.Deallocate
            }
        };

        HibernateResourceOperationResult result = await resourceGroup.BulkHibernateOperationAsync(
            location, new ExecuteHibernateContent(executionParams, new UserRequestResources(resourceIds)));

        var operationIds = HelperMethods.GetPollableOperationIds(result.Results);

        if (operationIds.Count == 0)
        {
            Console.WriteLine("[Submit] No operations were accepted. Check resource IDs and try again.");
            return;
        }

        Console.WriteLine($"[Submit] {operationIds.Count} operation(s) submitted. Polling for results...\n");
        Dictionary<string, ComputeBulkOperationDetails> completedOperations =
            await HelperMethods.PollOperationStatus(resourceGroup, location, operationIds, "hibernate");

        FallbackResultPrinter.Print(completedOperations, successMessage: "Hibernate succeeded — no fallback needed.",
            fallbackSuccessMessage: "Succeeded — VM was deallocated.");
    }
}
