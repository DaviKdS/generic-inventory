targetScope = 'resourceGroup'

@description('Azure region for the FREE environment.')
param location string = resourceGroup().location

@description('Existing/new App Service Plan name. SKU is intentionally hardcoded to F1.')
param appServicePlanName string

@description('Globally unique Azure Web App name.')
param webAppName string

var requiredSkuName = 'F1'
var requiredSkuTier = 'Free'

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  sku: {
    name: requiredSkuName
    tier: requiredSkuTier
    size: requiredSkuName
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
    }
  }
}

output appServicePlanId string = appServicePlan.id
output appServicePlanSku string = requiredSkuName
output webAppName string = webApp.name
output webAppHostName string = webApp.properties.defaultHostName
