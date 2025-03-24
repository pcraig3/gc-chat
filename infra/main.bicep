targetScope = 'subscription'

param redeployNonce string = '20251005a'

@minLength(1)
@maxLength(64)
@description('Name which is used to generate a short unique hash for each resource')
param name string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''

@description('Flag to decide where to create OpenAI role for current user')
param createRoleForUser bool = true

param acaExists bool = false

@minValue(1)
@maxValue(365)
@description('Retention days for both blob soft delete and container soft delete.')
param softDeleteRetentionDays int = 90

// Parameters for the Azure OpenAI resource:
param openAiResourceName string = ''
param openAiResourceGroupName string = ''
@minLength(1)
@description('Location for the OpenAI resource')
// Look for the desired model in availability table. Default model is gpt-4o-mini:
// https://learn.microsoft.com/azure/ai-services/openai/concepts/models#standard-deployment-model-availability
@allowed([
  'canadaeast'
  'eastus'
  'eastus2'
  'northcentralus'
  'southcentralus'
  'swedencentral'
  'westus'
  'westus3'
])
@metadata({
  azd: {
    type: 'location'
  }
})
param openAiResourceLocation string
param openAiApiVersion string = '' // Used by the SDK in the app code
param disableKeyBasedAuth bool = false

// Parameters for the specific Azure OpenAI deployment:
param openAiDeploymentName string // Set in main.parameters.json
param openAiModelName string // Set in main.parameters.json
param openAiModelVersion string // Set in main.parameters.json

// Parameters for the embedding model to deploy (also OpenAI)
param embeddingDeploymentName string
param embeddingModelName string
param embeddingModelVersion string

@description('Flag to decide whether to create Azure OpenAI instance or not')
param createAzureOpenAi bool // Set in main.parameters.json

@description('Azure OpenAI key to use for authentication. If not provided, managed identity will be used (and is preferred)')
@secure()
param openAiKey string = ''

@description('Azure OpenAI endpoint to use. If provided, no Azure OpenAI instance will be created.')
param openAiEndpoint string = ''

@description('Enable Cosmos DB free tier (max one per subscription)')
param cosmosEnableFreeTier bool = false

var resourceToken = toLower(uniqueString(subscription().id, name, location))
var tags = { 'azd-env-name': name }

resource resourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: '${name}-rg'
  location: location
  tags: tags
}

resource openAiResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' existing = if (!empty(openAiResourceGroupName)) {
  name: !empty(openAiResourceGroupName) ? openAiResourceGroupName : resourceGroup.name
}

var prefix = toLower('${name}-${resourceToken}')
var storageSafePrefix = replace(prefix, '-', '') // remove dashes

module openAi 'core/ai/cognitiveservices.bicep' = if (createAzureOpenAi) {
  name: 'openai'
  scope: openAiResourceGroup
  params: {
    name: !empty(openAiResourceName) ? openAiResourceName : '${resourceToken}-cog'
    location: !empty(openAiResourceLocation) ? openAiResourceLocation : location
    tags: tags
    disableLocalAuth: disableKeyBasedAuth
    deployments: [
      {
        name: openAiDeploymentName
        model: {
          format: 'OpenAI'
          name: openAiModelName
          version: openAiModelVersion
        }
        capacity: 20 // => 20K TPM for gpt-4o
      }
      {
        name: embeddingDeploymentName
        model: {
          format: 'OpenAI'
          name: embeddingModelName
          version: embeddingModelVersion
        }
        capacity: 50 // => 50K TPM for text-embedding-3-large
      }
    ]
  }
}

param searchServiceExists bool = false

module searchService 'core/ai/search.bicep' = if (!searchServiceExists) {
  name: 'search'
  scope: resourceGroup
  params: {
    name: '${prefix}-search'
    location: location
    tags: tags
    skuName: 'basic' // or 'free', etc.
    replicaCount: 1
    partitionCount: 1
  }
}

param blobStorageExists bool = false

module blobStorage 'core/storage/blob.bicep' = if (!blobStorageExists) {
  name: 'blob-storage'
  scope: resourceGroup
  params: {
    name: '${storageSafePrefix}st'
    location: location
    tags: tags
    skuName: 'Standard_LRS'
    accessTier: 'Hot'
    containerName: 'docs'
    containerPublicAccess: 'None'
  }
}

module blobService 'core/storage/blob-service.bicep' = if (!blobStorageExists) {
  name: 'blob-service'
  scope: resourceGroup
  params: {
    storageAccountName: blobStorage.outputs.storageAccountName
    blobDeleteRetentionDays: softDeleteRetentionDays
    containerDeleteRetentionDays: softDeleteRetentionDays
  }
}

module cosmosDb 'core/storage/cosmos.bicep' = {
  name: 'cosmosdb'
  scope: resourceGroup
  params: {
    name: '${prefix}-cosmosdb'
    location: location
    tags: tags
    enableFreeTier: cosmosEnableFreeTier
  }
}

module cosmosSql 'core/storage/cosmos-sql.bicep' = {
  name: 'cosmos-sql'
  scope: resourceGroup
  params: {
    cosmosAccountName: cosmosDb.outputs.cosmosAccountName
    databaseName: '${prefix}-cosmosdb-db'
    containerName: '${prefix}-cosmosdb-container'
    location: location
    tags: tags
    partitionKeyPath: '/userId'
  }
}

module logAnalyticsWorkspace 'core/monitor/loganalytics.bicep' = {
  name: 'loganalytics'
  scope: resourceGroup
  params: {
    name: '${prefix}-loganalytics'
    location: location
    tags: tags
  }
}

// Container apps host (including container registry)
module containerApps 'core/host/container-apps.bicep' = {
  name: 'container-apps'
  scope: resourceGroup
  params: {
    name: 'app'
    location: location
    tags: tags
    containerAppsEnvironmentName: '${prefix}-containerapps-env'
    containerRegistryName: '${replace(prefix, '-', '')}registry'
    logAnalyticsWorkspaceName: logAnalyticsWorkspace.outputs.name
  }
}

// Container app frontend
module aca 'app/aca.bicep' = {
  name: 'aca'
  scope: resourceGroup
  params: {
    name: replace('${take(prefix,19)}-ca', '--', '-')
    location: location
    tags: tags
    identityName: '${prefix}-id-aca'
    containerAppsEnvironmentName: containerApps.outputs.environmentName
    containerRegistryName: containerApps.outputs.registryName
    openAiDeploymentName: openAiDeploymentName
    openAiEndpoint: createAzureOpenAi ? openAi.outputs.endpoint : openAiEndpoint
    openAiApiVersion: openAiApiVersion
    openAiKey: openAiKey
    cosmosAccountName: cosmosDb.outputs.cosmosAccountName
    cosmosDatabaseName: cosmosSql.outputs.sqlDatabaseName
    cosmosContainerName: cosmosSql.outputs.sqlContainerName
    storageAccountName: blobStorage.outputs.storageAccountName
    storageContainerName: blobStorage.outputs.containerName
    exists: acaExists
  }
}

module openAiRoleUser 'core/security/role.bicep' = if (createRoleForUser && createAzureOpenAi) {
  scope: openAiResourceGroup
  name: 'openai-role-user'
  params: {
    principalId: principalId
    roleDefinitionId: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    principalType: 'User'
  }
}

module openAiRoleBackend 'core/security/role.bicep' = if (createAzureOpenAi) {
  scope: openAiResourceGroup
  name: 'openai-role-backend'
  params: {
    principalId: aca.outputs.SERVICE_ACA_IDENTITY_PRINCIPAL_ID
    roleDefinitionId: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    principalType: 'ServicePrincipal'
  }
}

output AZURE_LOCATION string = location
output AZURE_RESOURCE_GROUP string = resourceGroup.name

output AZURE_OPENAI_DEPLOYMENT string = openAiDeploymentName
output AZURE_OPENAI_RESOURCE_LOCATION string = openAiResourceLocation
output AZURE_OPENAI_API_VERSION string = openAiApiVersion
output AZURE_OPENAI_ENDPOINT string = createAzureOpenAi ? openAi.outputs.endpoint : openAiEndpoint

output AZURE_OPENAI_EMBEDDING_DEPLOYMENT string = embeddingDeploymentName
output AZURE_OPENAI_EMBEDDING_DIMENSIONS string = embeddingModelName == 'text-embedding-3-large' ? '3072' : '1536'

output SERVICE_ACA_IDENTITY_PRINCIPAL_ID string = aca.outputs.SERVICE_ACA_IDENTITY_PRINCIPAL_ID
output SERVICE_ACA_NAME string = aca.outputs.SERVICE_ACA_NAME
output SERVICE_ACA_URI string = aca.outputs.SERVICE_ACA_URI
output SERVICE_ACA_IMAGE_NAME string = aca.outputs.SERVICE_ACA_IMAGE_NAME

output AZURE_CONTAINER_ENVIRONMENT_NAME string = containerApps.outputs.environmentName
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerApps.outputs.registryLoginServer
output AZURE_CONTAINER_REGISTRY_NAME string = containerApps.outputs.registryName

output STORAGE_ACCOUNT_NAME string = blobStorage.outputs.storageAccountName
output STORAGE_ACCOUNT_ENDPOINT string = blobStorage.outputs.storageAccountEndpoint
output STORAGE_CONTAINER_NAME string = blobStorage.outputs.containerName

output AZURE_SEARCH_SERVICE_NAME string = searchService.outputs.searchServiceName
output AZURE_SEARCH_SERVICE_ENDPOINT string = searchService.outputs.searchServiceEndpoint
output AZURE_SEARCH_SERVICE_PRINCIPAL_ID string = searchService.outputs.searchServicePrincipalId

output AZURE_COSMOSDB_ACCOUNT_NAME string = cosmosDb.outputs.cosmosAccountName
output AZURE_COSMOSDB_DATABASE_NAME string = cosmosSql.outputs.sqlDatabaseName
output AZURE_COSMOSDB_CONTAINER_NAME string = cosmosSql.outputs.sqlContainerName
