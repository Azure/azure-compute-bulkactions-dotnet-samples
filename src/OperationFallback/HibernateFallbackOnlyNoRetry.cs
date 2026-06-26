using Azure.Core;
using Azure.ResourceManager.Compute.BulkActions;
using Azure.ResourceManager.Compute.BulkActions.Models;
using Azure.ResourceManager.Resources;
using UtilityMethods;

namespace OperationFallback;

/// <summary>
/// Hibernate with Deallocate fallback, no retry window.
///
/// When RetryWindowInMinutes is omitted (or set to 0), the operation is attempted once. If it
/// fails with a retriable error, the system skips retries and goes directly to the fallback action.
/// </summary>
public static class HibernateFallbackOnlyNoRetry
{
    public static async Task RunAsync(
        ResourceGroupResource resourceGroup,
        List<ResourceIdentifier> resourceIds)
    {
        Console.WriteLine("[Scenario] Hibernate with Deallocate fallback (no retries)\n");

        var executionParams = new ScheduledActionExecutionParameterDetail()
        {
            RetryPolicy = new BulkOperationRetryPolicy()
            {
                OnFailureAction = ComputeBulkOperationType.Deallocate
            }
        };

        HibernateResourceOperationResult result = await resourceGroup.BulkHibernateOperationAsync(
            new ExecuteHibernateContent(executionParams, new UserRequestResources(resourceIds)));

        var operationIds = HelperMethods.GetPollableOperationIds(result.Results);

        if (operationIds.Count == 0)
        {
            Console.WriteLine("[Submit] No operations were accepted. Check resource IDs and try again.");
            return;
        }

        Console.WriteLine($"[Submit] {operationIds.Count} operation(s) submitted. Polling for results...\n");
        Dictionary<string, ComputeBulkOperationDetails> completedOperations =
            await HelperMethods.PollOperationStatus(resourceGroup, operationIds, "hibernate");

        FallbackResultPrinter.Print(completedOperations, successMessage: "Hibernate succeeded — no fallback needed.",
            fallbackSuccessMessage: "Succeeded — VM was deallocated (no retries attempted).");
    }
}
