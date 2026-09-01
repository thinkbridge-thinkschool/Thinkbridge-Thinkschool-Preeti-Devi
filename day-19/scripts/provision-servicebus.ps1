<#
  Creates the minimum Azure resources this exercise needs - one Standard
  namespace, one topic, two subscriptions - and grants the signed-in user a
  Service Bus data role. Standard tier is required because Basic has no topics.

  Run:  ./provision-servicebus.ps1
#>

param(
    [string]$ResourceGroup    = 'thinkschool-rg',
    [string]$Location         = 'eastasia',
    [string]$Namespace        = "sb-day19-quotes-$(Get-Random -Minimum 10000 -Maximum 99999)",
    [string]$Topic            = 'quote-events',
    [string[]]$Subscriptions  = @('sub-a', 'sub-b'),

    # Low on purpose, so a failing message reaches the DLQ quickly.
    [int]$MaxDeliveryCount    = 3
)

$ErrorActionPreference = 'Stop'

Write-Host "Subscription:" -ForegroundColor Cyan
# --refresh, because `az account show` reads a local cache that can report
# "Enabled" long after the server has disabled the subscription.
az account list --refresh --all --query "[?isDefault].{name:name, id:id, state:state}" -o tsv

$state = az rest --method get `
    --url "https://management.azure.com/subscriptions/$(az account show --query id -o tsv)?api-version=2022-12-01" `
    --query state -o tsv

if ($state -ne 'Enabled') {
    throw "The subscription is '$state'. Every write will fail with ReadOnlyDisabledSubscription until it is re-enabled in the portal."
}

# Namespace creation fails unless this provider is registered. Registration is
# itself a write, so it must follow the state check above.
Write-Host "`nRegistering the Microsoft.ServiceBus resource provider..." -ForegroundColor Cyan
az provider register --namespace Microsoft.ServiceBus --wait -o none

az provider show --namespace Microsoft.ServiceBus --query "{provider:namespace, state:registrationState}" -o tsv

Write-Host "`nCreating Service Bus namespace $Namespace (Standard) in $ResourceGroup..." -ForegroundColor Cyan
az servicebus namespace create `
    --name $Namespace `
    --resource-group $ResourceGroup `
    --location $Location `
    --sku Standard `
    -o none
if (-not $?) { throw "Namespace creation failed." }

Write-Host "Creating topic $Topic..." -ForegroundColor Cyan
az servicebus topic create `
    --name $Topic `
    --namespace-name $Namespace `
    --resource-group $ResourceGroup `
    -o none

foreach ($subscription in $Subscriptions) {
    Write-Host "Creating subscription $subscription (MaxDeliveryCount=$MaxDeliveryCount)..." -ForegroundColor Cyan
    az servicebus topic subscription create `
        --name $subscription `
        --topic-name $Topic `
        --namespace-name $Namespace `
        --resource-group $ResourceGroup `
        --max-delivery-count $MaxDeliveryCount `
        --lock-duration PT1M `
        -o none
}

# Data Owner covers send, receive and dead-letter access in one assignment;
# narrower Sender/Receiver roles would need two.
$principalId = az ad signed-in-user show --query id -o tsv
$scope = az servicebus namespace show --name $Namespace --resource-group $ResourceGroup --query id -o tsv

Write-Host "`nGranting 'Azure Service Bus Data Owner' to the signed-in user..." -ForegroundColor Cyan
az role assignment create `
    --assignee-object-id $principalId `
    --assignee-principal-type User `
    --role 'Azure Service Bus Data Owner' `
    --scope $scope `
    -o none

$fqdn = "$Namespace.servicebus.windows.net"

Write-Host "`nDone." -ForegroundColor Green
Write-Host "Namespace : $fqdn"
Write-Host "Topic     : $Topic"
Write-Host "Subs      : $($Subscriptions -join ', ')"
Write-Host ""
Write-Host "Role assignments can take a minute to take effect. Then run:" -ForegroundColor Yellow
Write-Host "  ./verify-azure.ps1 -Namespace $fqdn"
