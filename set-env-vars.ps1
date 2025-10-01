###############################################################
# set-env-vars.ps1
#
# This script sets required environment variables for an Azure Container App
# after deployment, so the app can access its FQDN and Azure AD credentials.
#
# Usage: Run after deploying the container app to Azure.
###############################################################

# Optionally use environment variables for resource group and app name, or hardcode for local testing
$resourceGroup = $env:AZURE_RESOURCE_GROUP_NAME
$appName = $env:AZURE_SERVICE_NAME_DEVOPS_MCP_SERVER

# (Optional) Ensure the Microsoft.App provider is registered in your subscription
# az provider register -n Microsoft.App --wait

# Get the fully qualified domain name (FQDN) of the deployed container app
$fqdn = az containerapp show --resource-group $resourceGroup --name $appName --query properties.configuration.ingress.fqdn -o tsv

# Set the MCP_RESOURCE_FQDN environment variable in the container app
# This is used by the app to construct its public resource metadata URL
az containerapp update -n $appName -g $resourceGroup --set-env-vars MCP_RESOURCE_FQDN=$fqdn

# Set Azure AD environment variables for authentication
# These are required for the app to authenticate with Azure AD
az containerapp update -n $appName -g $resourceGroup --set-env-vars AzureAd__TenantId=$env:AzureAd__TenantId
az containerapp update -n $appName -g $resourceGroup --set-env-vars AzureAd__ClientId=$env:AzureAd__ClientId
az containerapp update -n $appName -g $resourceGroup --set-env-vars AzureAd__ClientSecret=$env:AzureAd__ClientSecret

# Output confirmation
Write-Host "Set MCP_RESOURCE_FQDN to $fqdn for $appName in $resourceGroup"