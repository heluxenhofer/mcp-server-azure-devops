targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string


param devopsmcpServerExists bool

@description('Id of the user or app to assign application roles')
param principalId string

@description('Principal type of user or app')
param principalType string
@description('The Azure AD tenant ID for the application')
param azureAdTenantId string
@description('The Azure AD client ID for the application')
param azureAdClientId string
@description('Name of the secret in Key Vault that contains the Azure AD client secret for the application')
param azureAdClientSecretName string
@description('The Azure AD client secret for the application')
@secure()
param azureAdClientSecret string
// Tags that should be applied to all resources.
// 
// Note that 'azd-service-name' tags should be applied separately to service host resources.
// Example usage:
//   tags: union(tags, { 'azd-service-name': <service name in azure.yaml> })
var tags = {
  'azd-env-name': environmentName
}

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  scope: rg
  name: 'resources'
  params: {
    location: location
    tags: tags
    principalId: principalId
    principalType: principalType
    devopsmcpServerExists: devopsmcpServerExists
    azureAdClientId: azureAdClientId
    azureAdTenantId: azureAdTenantId
    azureAdClientSecretName: azureAdClientSecretName
    azureAdClientSecret: azureAdClientSecret
  }
}
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_KEY_VAULT_ENDPOINT string = resources.outputs.AZURE_KEY_VAULT_ENDPOINT
output AZURE_KEY_VAULT_NAME string = resources.outputs.AZURE_KEY_VAULT_NAME
output AZURE_RESOURCE_VAULT_ID string = resources.outputs.AZURE_RESOURCE_VAULT_ID
output AZURE_CONTAINER_APP_FQDN string = resources.outputs.AZURE_CONTAINER_APP_FQDN
output AZURE_CONTAINER_APP_NAME string = resources.outputs.AZURE_CONTAINER_APP_NAME
output AZURE_RESOURCE_GROUP_NAME string = rg.name
