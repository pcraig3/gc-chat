metadata description = 'Creates an Azure Cognitive Services instance.'
param name string
param location string = resourceGroup().location
param tags object = {}
@description('The custom subdomain name used to access the API. Defaults to the value of the name parameter.')
param customSubDomainName string = name
param disableLocalAuth bool = false
param deployments array = []
param kind string = 'OpenAI'

@allowed(['Enabled', 'Disabled'])
param publicNetworkAccess string = 'Enabled'

param allowedIpRules array = []
param networkAcls object = empty(allowedIpRules)
  ? {
      defaultAction: 'Allow'
    }
  : {
      ipRules: allowedIpRules
      defaultAction: 'Deny'
    }

resource account 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  kind: kind
  properties: {
    customSubDomainName: customSubDomainName
    publicNetworkAccess: publicNetworkAccess
    networkAcls: networkAcls
    disableLocalAuth: disableLocalAuth
  }
  sku: {
    name: 'S0'
  }
}

@batchSize(1)
resource deployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = [
  for d in deployments: {
    parent: account
    name: d.name
    // Capacity is in thousands of tokens per minute (1 = 1K TPM)
    sku: contains(d, 'capacity')
      ? {
          name: 'Standard'
          capacity: int(d.capacity) // e.g., 50 => 50K TPM
        }
      : {
          name: 'Standard'
          capacity: 1 // default to 1K TPM if not provided
        }
    properties: {
      model: d.model
      raiPolicyName: contains(d, 'raiPolicyName') ? d.raiPolicyName : null
    }
  }
]

output endpoint string = account.properties.endpoint
output endpoints object = account.properties.endpoints
output id string = account.id
output name string = account.name
