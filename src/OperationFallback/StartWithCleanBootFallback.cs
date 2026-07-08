using Azure.Core;
using Azure.ResourceManager.Compute.BulkActions;
using Azure.ResourceManager.Compute.BulkActions.Models;
using Azure.ResourceManager.Resources;
using UtilityMethods;

namespace OperationFallback;

/// <summary>
/// Start with clean-boot fallback.
///
/// When a hibernated VM fails to resume after all retries, setting OnFailureAction to
/// <see cref="ComputeBulkOperationKind.Start"/> tells the system to discard the hibernated
/// session state and perform a fresh boot — maximizing the chance of the VM coming back online.
///
/// [WARN] The fallback discards the hibernated session state.
/// </summary>
public static class StartWithCleanBootFallback
{
    public static async Task RunAsync(
        ResourceGroupResource resourceGroup,
        AzureLocation location,
        List<ResourceIdentifier> resourceIds)
    {
        Console.WriteLine("[Scenario] Start with clean-boot fallback\n");

        var executionParams = new BulkActionExecutionParameterDetail()
        {
            RetryPolicy = new BulkOperationRetryPolicy()
            {
                RetryWindowInMinutes = 30,
                OnFailureAction = ComputeBulkOperationKind.Start
            }
        };

        StartResourceOperationResult result = await resourceGroup.BulkStartOperationAsync(
            location, new ExecuteStartContent(executionParams, new UserRequestResources(resourceIds)));

        var operationIds = HelperMethods.GetPollableOperationIds(result.Results);

        if (operationIds.Count == 0)
        {
            Console.WriteLine("[Submit] No operations were accepted. Check resource IDs and try again.");
            return;
        }

        Console.WriteLine($"[Submit] {operationIds.Count} operation(s) submitted. Polling for results...\n");
        Dictionary<string, ComputeBulkOperationDetails> completedOperations =
            await HelperMethods.PollOperationStatus(resourceGroup, location, operationIds, "start");

        FallbackResultPrinter.Print(completedOperations, successMessage: "Start (resume) succeeded — no fallback needed.",
            fallbackSuccessMessage: "Succeeded — VM was clean-booted (hibernated state discarded).");
    }
}
