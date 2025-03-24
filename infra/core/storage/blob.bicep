metadata description = 'Creates a Blob Storage account (and optional container)'

param name string
param location string = resourceGroup().location
param tags object = {}

@allowed([
  'Standard_LRS'
  'Standard_GRS'
  'Standard_ZRS'
])
param skuName string = 'Standard_LRS'

param accessTier string = 'Hot'

@description('Create a container as part of the same deployment (leave blank to skip)')
param containerName string = ''

@allowed(['None', 'Blob', 'Container'])
@description('Public access level for the container')
param containerPublicAccess string = 'None'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: name
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: skuName
  }
  properties: {
    accessTier: accessTier
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = if (containerName != '') {
  name: '${storageAccount.name}/default/${containerName}'
  properties: {
    publicAccess: containerPublicAccess
  }
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
output storageAccountEndpoint string = 'https://${storageAccount.name}.blob.${environment().suffixes.storage}'
output containerUri string = containerName == ''
  ? ''
  : 'https://${storageAccount.name}.blob.${environment().suffixes.storage}/${containerName}'
output containerName string = containerName
