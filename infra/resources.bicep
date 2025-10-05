@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}


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

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = uniqueString(subscription().id, resourceGroup().id, location)

// Monitor application with Azure Monitor
module monitoring 'br/public:avm/ptn/azd/monitoring:0.1.0' = {
  name: 'monitoring'
  params: {
    logAnalyticsName: '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: '${abbrs.insightsComponents}${resourceToken}'
    applicationInsightsDashboardName: '${abbrs.portalDashboards}${resourceToken}'
    location: location
    tags: tags
  }
}
// Container registry
module containerRegistry 'br/public:avm/res/container-registry/registry:0.1.1' = {
  name: 'registry'
  params: {
    name: '${abbrs.containerRegistryRegistries}${resourceToken}'
    location: location
    tags: tags
    publicNetworkAccess: 'Enabled'
    roleAssignments:[
      {
        principalId: devopsmcpServerIdentity.outputs.principalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
      }
    ]
  }
}

// Container apps environment
module containerAppsEnvironment 'br/public:avm/res/app/managed-environment:0.4.5' = {
  name: 'container-apps-environment'
  params: {
    logAnalyticsWorkspaceResourceId: monitoring.outputs.logAnalyticsWorkspaceResourceId
    name: '${abbrs.appManagedEnvironments}${resourceToken}'
    location: location
    zoneRedundant: false
  }
}

module devopsmcpServerIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'devopsmcpServeridentity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}devopsmcpServer-${resourceToken}'
    location: location
  }
}
module devopsmcpServerFetchLatestImage './modules/fetch-container-image.bicep' = {
  name: 'devopsmcpServer-fetch-image'
  params: {
    exists: devopsmcpServerExists
    name: 'devopsmcp-server'
  }
}

module devopsmcpServer 'br/public:avm/res/app/container-app:0.8.0' = {
  name: 'devopsmcpServer'
  params: {
    name: 'devopsmcp-server'
    ingressTargetPort: 8080
    scaleMinReplicas: 1
    scaleMaxReplicas: 10
    secrets: {

      secureList:  [
        {
          name: toLower(azureAdClientSecretName)
          keyVaultUrl: '${keyVault.outputs.uri}secrets/${azureAdClientSecretName}'
          identity: devopsmcpServerIdentity.outputs.resourceId
        }  
      ]
    }
    containers: [
      {
        image: devopsmcpServerFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
        name: 'main'
        resources: {
          cpu: json('0.5')
          memory: '1.0Gi'
        }
        env: [
          {
            name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
            value: monitoring.outputs.applicationInsightsConnectionString
          }
          {
            name: 'AZURE_CLIENT_ID'
            value: devopsmcpServerIdentity.outputs.clientId
          }
          {
            name: 'AZURE_KEY_VAULT_NAME'
            value: keyVault.outputs.name
          }
          {
            name: 'AZURE_KEY_VAULT_ENDPOINT'
            value: keyVault.outputs.uri
          }
          {
            name: 'PORT'
            value: '8080'
          }
          {
            name: 'AzureAd__ClientId'
            value: azureAdClientId
          }
          {
            name: 'AzureAd__TenantId'
            value: azureAdTenantId
          }
          {
            name: 'AzureAd__ClientSecret'
            secretRef: toLower(azureAdClientSecretName)
          }
        ]
      }
    ]
    managedIdentities:{
      systemAssigned: false
      userAssignedResourceIds: [devopsmcpServerIdentity.outputs.resourceId]
    }
    registries:[
      {
        server: containerRegistry.outputs.loginServer
        identity: devopsmcpServerIdentity.outputs.resourceId
      }
    ]
    environmentResourceId: containerAppsEnvironment.outputs.resourceId
    location: location
    tags: union(tags, { 'azd-service-name': 'devopsmcp-server' })
  }
}
// Create a keyvault to store secrets
module keyVault 'br/public:avm/res/key-vault/vault:0.12.0' = {
  name: 'keyvault'
  params: {
    name: '${abbrs.keyVaultVaults}${resourceToken}'
    location: location
    tags: tags
    enableRbacAuthorization: false
    accessPolicies: [
      {
        objectId: principalId
        permissions: {
          secrets: [ 'get', 'list', 'set' ]
        }
      }
      {
        objectId: devopsmcpServerIdentity.outputs.principalId
        permissions: {
          secrets: [ 'get', 'list' ]
        }
      }
    ]
    secrets: [
      {
        name: azureAdClientSecretName
        value: azureAdClientSecret
      }
    ]
  }
}
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer
output AZURE_KEY_VAULT_ENDPOINT string = keyVault.outputs.uri
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name
output AZURE_RESOURCE_VAULT_ID string = keyVault.outputs.resourceId
output AZURE_CONTAINER_APP_FQDN string = devopsmcpServer.outputs.fqdn
output AZURE_CONTAINER_APP_NAME string = devopsmcpServer.outputs.name
