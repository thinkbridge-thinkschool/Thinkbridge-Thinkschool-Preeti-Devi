@description('The location used for all deployed resources')
param location string = resourceGroup().location

@description('Tags that will be applied to all resources')
param tags object = {}

param day5Piece2Exists bool

@description('Id of the user or app to assign application roles')
param principalId string

@description('Principal type of user or app')
param principalType string

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
    roleAssignments: [
      {
        principalId: day5Piece2Identity.outputs.principalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: subscriptionResourceId(
          'Microsoft.Authorization/roleDefinitions',
          '7f951dda-4ed3-4680-a7ca-43fe172d538d'
        )
      }
    ]
  }
}

// User-assigned managed identity
module day5Piece2Identity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'day5Piece2identity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}day5Piece2-${resourceToken}'
    location: location
  }
}

// Fetch the latest container image if the application already exists
module day5Piece2FetchLatestImage './modules/fetch-container-image.bicep' = {
  name: 'day5Piece2-fetch-image'
  params: {
    exists: day5Piece2Exists
    name: 'day-5-piece-2'
  }
}

// Deploy Container App into the EXISTING Container Apps environment
module day5Piece2 'br/public:avm/res/app/container-app:0.8.0' = {
  name: 'day5Piece2'
  params: {
    name: 'day-5-piece-2'
    ingressTargetPort: 8080
    scaleMinReplicas: 1
    scaleMaxReplicas: 10

    secrets: {
      secureList: []
    }

    containers: [
      {
        image: day5Piece2FetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
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
            value: day5Piece2Identity.outputs.clientId
          }
          {
            name: 'PORT'
            value: '8080'
          }
        ]
      }
    ]

    managedIdentities: {
      systemAssigned: false
      userAssignedResourceIds: [
        day5Piece2Identity.outputs.resourceId
      ]
    }

    registries: [
      {
        server: containerRegistry.outputs.loginServer
        identity: day5Piece2Identity.outputs.resourceId
      }
    ]

    environmentResourceId: '/subscriptions/ac177eb4-4211-4f5d-af55-555a3fbed197/resourceGroups/thinkschool-rg/providers/Microsoft.App/managedEnvironments/thinkschool-env'

    location: location

    tags: union(tags, {
      'azd-service-name': 'day-5-piece-2'
    })
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer

output AZURE_RESOURCE_DAY_5_PIECE_2_ID string = day5Piece2.outputs.resourceId