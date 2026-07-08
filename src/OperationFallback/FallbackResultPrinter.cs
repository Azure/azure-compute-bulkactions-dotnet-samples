using Azure.ResourceManager.Compute.BulkActions.Models;

namespace OperationFallback;

/// <summary>
/// Interprets completed operation details and prints the outcome, including any fallback
/// (OnFailureAction) result captured in <see cref="ComputeBulkOperationDetails.FallbackOperationInfo"/>.
/// </summary>
internal static class FallbackResultPrinter
{
    public static void Print(
        Dictionary<string, ComputeBulkOperationDetails> completedOperations,
        string successMessage,
        string fallbackSuccessMessage)
    {
        foreach ((string opId, ComputeBulkOperationDetails details) in completedOperations)
        {
            Console.WriteLine($"[Result] Operation {opId}: State = {details.State}");

            if (details.State == BulkActionOperationState.Succeeded)
            {
                Console.WriteLine($"[OK] {successMessage}");
                continue;
            }

            if (details.State != BulkActionOperationState.Failed)
            {
                continue;
            }

            if (details.Error is not null)
            {
                Console.WriteLine($"[Error] Primary: {details.Error.ErrorCode} — {details.Error.ErrorDetails}");
            }

            if (details.FallbackOperationInfo is null)
            {
                Console.WriteLine("[Fallback] Not executed (may indicate a non-retriable error).");
                continue;
            }

            ComputeBulkFallbackOperationInfo fallback = details.FallbackOperationInfo;
            Console.WriteLine($"[Fallback] {fallback.LastOperationKind}: Status = {fallback.Status}");

            if (string.Equals(fallback.Status, BulkActionOperationState.Succeeded.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Fallback] [OK] {fallbackSuccessMessage}");
            }
            else
            {
                Console.WriteLine("[Fallback] [FAIL] Failed. Manual intervention may be needed.");
                if (fallback.Error is not null)
                {
                    Console.WriteLine($"[Fallback] Error: {fallback.Error.ErrorCode} — {fallback.Error.ErrorDetails}");
                }
            }
        }
    }
}
