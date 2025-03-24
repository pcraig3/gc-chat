param storageAccountName string

@minValue(1)
@maxValue(365)
param blobDeleteRetentionDays int = 7

@minValue(1)
@maxValue(365)
param containerDeleteRetentionDays int = 7

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  name: '${storageAccountName}/default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: blobDeleteRetentionDays
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: containerDeleteRetentionDays
    }
    isVersioningEnabled: false
  }
}
