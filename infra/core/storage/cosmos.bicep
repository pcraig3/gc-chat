@description('Cosmos DB account name')
param name string
@description('Resource group location')
param location string
param tags object = {}

@description('Create this account as the single free-tier account in the subscription')
@allowed([false, true])
param enableFreeTier bool = false

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-12-01-preview' = {
  name: name
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
      maxIntervalInSeconds: 5
      maxStalenessPrefix: 100
    }
    enableAutomaticFailover: true
    enableFreeTier: enableFreeTier
    publicNetworkAccess: 'Enabled'
    capabilities: []
    cors: []
    backupPolicy: {
      type: 'Continuous'
      continuousModeProperties: {
        tier: 'Continuous7Days'
      }
    }
  }
}

output cosmosAccountName string = cosmosAccount.name
