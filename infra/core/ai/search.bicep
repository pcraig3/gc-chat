metadata description = 'Creates an Azure AI Search service.'

param name string
param location string = resourceGroup().location
param skuName string = 'basic' // allowed values: free, basic, standard, standard2, standard3
param replicaCount int = 1
param partitionCount int = 1
param tags object = {}

resource search 'Microsoft.Search/searchServices@2025-02-01-preview' = {
  name: name
  location: location
  sku: {
    name: skuName
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    replicaCount: replicaCount
    partitionCount: partitionCount
    hostingMode: 'default'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
    authOptions: {
      apiKeyOnly: {}
    }
    semanticSearch: 'free'
    encryptionWithCmk: {
      enforcement: 'Unspecified'
    }
    networkRuleSet: {
      ipRules: []
      bypass: 'None'
    }
  }
  tags: tags
}

output searchServiceName string = search.name
output searchServiceEndpoint string = 'https://${search.name}.search.windows.net'
output searchServicePrincipalId string = search.identity.principalId
