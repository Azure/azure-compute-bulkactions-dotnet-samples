using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute.BulkActions;
using Azure.ResourceManager.Compute.BulkActions.Models;
using Azure.ResourceManager.Resources;

namespace UtilityMethods;

/// <summary>
/// Shared helpers used across the Compute.BulkActions samples: resolving resources,
/// building VM resource identifiers, extracting pollable operation IDs from a submit
/// response, and polling operation status until completion.
/// </summary>
public static class HelperMethods
{
    // Initial delay before the first status poll, giving the service time to register operations.
    public static readonly TimeSpan InitialPollDelay = TimeSpan.FromSeconds(20);

    // Interval between subsequent status polls.
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    // Maximum time to wait for all operations to reach a terminal state before giving up.
    public static readonly TimeSpan PollTimeout = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Resolves the <see cref="SubscriptionResource"/> for the given subscription id.
    /// </summary>
    public static SubscriptionResource GetSubscriptionResource(ArmClient client, string subscriptionId)
    {
        var subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
        return client.GetSubscriptionResource(subscriptionResourceId);
    }

    /// <summary>
    /// Builds fully-qualified virtual machine <see cref="ResourceIdentifier"/> values for the
    /// supplied VM names. All VMs must live in the same subscription and resource group.
    /// </summary>
    public static List<ResourceIdentifier> BuildVmResourceIds(
        string subscriptionId,
        string resourceGroupName,
        IEnumerable<string> vmNames)
    {
        return vmNames
            .Select(vmName => new ResourceIdentifier(
                $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}"))
            .ToList();
    }

    /// <summary>
    /// Extracts the operation IDs that were accepted for processing from a submit response.
    /// Resources rejected up front (those carrying an <c>ErrorCode</c>) are logged and skipped.
    /// </summary>
    public static HashSet<string> GetPollableOperationIds(IEnumerable<ComputeBulkOperationResult> operationResults)
    {
        var operationIds = new HashSet<string>();

        foreach (ComputeBulkOperationResult result in operationResults)
        {
            if (!string.IsNullOrWhiteSpace(result.ErrorCode))
            {
                Console.WriteLine(
                    $"[Submit] Request rejected for resourceId={result.ResourceId}. errorCode={result.ErrorCode}, errorDetails={result.ErrorDetails}");
                continue;
            }

            if (result.Operation is null || string.IsNullOrWhiteSpace(result.Operation.OperationId))
            {
                continue;
            }

            operationIds.Add(result.Operation.OperationId);
        }

        return operationIds;
    }

    /// <summary>
    /// Polls <c>BulkGetOperationsStatus</c> until every supplied operation reaches a terminal
    /// state (Succeeded, Failed, or Cancelled) or the poll timeout elapses.
    /// </summary>
    public static async Task<Dictionary<string, ComputeBulkOperationDetails>> PollOperationStatus(
        ResourceGroupResource resourceGroup,
        HashSet<string> operationIds,
        string operationLabel)
    {
        var pending = new HashSet<string>(operationIds);
        var completed = new Dictionary<string, ComputeBulkOperationDetails>();

        if (pending.Count == 0)
        {
            return completed;
        }

        await Task.Delay(InitialPollDelay);
        var deadlineUtc = DateTimeOffset.UtcNow + PollTimeout;

        while (pending.Count > 0)
        {
            if (DateTimeOffset.UtcNow > deadlineUtc)
            {
                throw new TimeoutException($"Timed out while polling {operationLabel} operations.");
            }

            GetBulkOperationStatusResult status = await resourceGroup.BulkGetOperationsStatusAsync(
                new GetBulkOperationStatusContent(pending));

            foreach (ComputeBulkOperationResult result in status.Results)
            {
                if (result.Operation is null || string.IsNullOrWhiteSpace(result.Operation.OperationId))
                {
                    continue;
                }

                string operationId = result.Operation.OperationId;
                if (!pending.Contains(operationId))
                {
                    continue;
                }

                Console.WriteLine($"[{operationLabel}] operationId={operationId}, state={result.Operation.State}");

                if (IsTerminal(result.Operation.State))
                {
                    if (result.Operation.Error is not null)
                    {
                        Console.WriteLine(
                            $"[{operationLabel}] operationId={operationId} errorCode={result.Operation.Error.ErrorCode}, errorDetails={result.Operation.Error.ErrorDetails}");
                    }

                    completed[operationId] = result.Operation;
                    pending.Remove(operationId);
                }
            }

            if (pending.Count > 0)
            {
                await Task.Delay(PollInterval);
            }
        }

        return completed;
    }

    /// <summary>
    /// Returns <c>true</c> when the operation state is terminal and no further polling is needed.
    /// </summary>
    public static bool IsTerminal(ScheduledActionOperationState? state)
    {
        return state == ScheduledActionOperationState.Succeeded
            || state == ScheduledActionOperationState.Failed
            || state == ScheduledActionOperationState.Cancelled;
    }
}
