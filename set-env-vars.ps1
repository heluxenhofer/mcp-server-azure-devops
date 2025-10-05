# set-env-vars.ps1
#
# This script is executed as a postdeploy hook by Azure Developer CLI (azd) after deploying the MCP Server to Azure.
#
# Purpose:
#   - Sets the MCP_RESOURCE_FQDN environment variable in the Azure Container App using the FQDN output from the Bicep deployment.
#   - Can be customized to set additional environment variables or use other Bicep outputs as needed.
#
# Usage:
#   - Automatically run by azd as specified in azure.yaml:
#       hooks:
#         postdeploy:
#           shell: pwsh
#           run: ./set-env-vars.ps1
#   - Assumes required environment variables (such as resource group and FQDN) are set by azd from Bicep outputs.
#
# Prerequisites:
#   - Azure CLI must be installed and authenticated.
#   - Script expects to be run in the context of azd deployment.
#
# For more details, see the project README.md.

$resourceGroup = $env:AZURE_RESOURCE_GROUP_NAME
$appName = $env:AZURE_CONTAINER_APP_NAME
# (Optional) Ensure the Microsoft.App provider is registered in your subscription
# az provider register -n Microsoft.App --wait


$fqdn = $env:AZURE_CONTAINER_APP_FQDN
# Set the MCP_RESOURCE_FQDN environment variable in the container app
# This is used by the app to construct its public resource metadata URL
az containerapp update -n $appName -g $resourceGroup --set-env-vars MCP_RESOURCE_FQDN=$fqdn

# Output confirmation
Write-Host "Set MCP_RESOURCE_FQDN to $fqdn for $appName in $resourceGroup"