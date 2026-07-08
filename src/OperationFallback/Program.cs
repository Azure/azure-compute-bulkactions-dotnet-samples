using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Configuration;
using UtilityMethods;

namespace OperationFallback;

/// <summary>
/// Demonstrates the operation fallback (OnFailureAction) feature of Compute.BulkActions.
///
/// Configure your environment in appsettings.json before running. The Scenario field selects
/// which fallback example to run:
///   - HibernateFallback:        Hibernate with Deallocate fallback (with a retry window).
///   - StartFallback:            Start with clean-boot fallback (with a retry window).
///   - HibernateFallbackNoRetry: Hibernate with Deallocate fallback, no retry window.
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        // Load settings from appsettings.json
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var settings = config.GetSection("Settings");
        string subscriptionId = settings["SubscriptionId"] ?? throw new InvalidOperationException("SubscriptionId is required in appsettings.json");
        string resourceGroupName = settings["ResourceGroupName"] ?? throw new InvalidOperationException("ResourceGroupName is required in appsettings.json");
        // A resource group's location can differ from its resources', so the operation location is supplied explicitly.
        AzureLocation location = settings["Location"] ?? throw new InvalidOperationException("Location is required in appsettings.json");
        var vmNames = settings.GetSection("VmNames").GetChildren().Select(c => c.Value!).ToArray();
        if (vmNames.Length == 0) throw new InvalidOperationException("VmNames is required in appsettings.json");
        string scenario = settings["Scenario"] ?? "HibernateFallback";

        TokenCredential cred = new DefaultAzureCredential();
        ArmClient client = new(cred);

        SubscriptionResource subscriptionResource = HelperMethods.GetSubscriptionResource(client, subscriptionId);
        ResourceGroupResource resourceGroupResource = await subscriptionResource.GetResourceGroupAsync(resourceGroupName);

        List<ResourceIdentifier> resourceIds = HelperMethods.BuildVmResourceIds(subscriptionId, resourceGroupName, vmNames);

        switch (scenario)
        {
            case "HibernateFallback":
                await HibernateWithDeallocateFallback.RunAsync(resourceGroupResource, location, resourceIds);
                break;
            case "StartFallback":
                await StartWithCleanBootFallback.RunAsync(resourceGroupResource, location, resourceIds);
                break;
            case "HibernateFallbackNoRetry":
                await HibernateFallbackOnlyNoRetry.RunAsync(resourceGroupResource, location, resourceIds);
                break;
            default:
                Console.WriteLine($"Unknown scenario: {scenario}");
                Console.WriteLine("Valid scenarios: HibernateFallback, StartFallback, HibernateFallbackNoRetry");
                break;
        }
    }
}
