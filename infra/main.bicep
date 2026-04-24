// ======================================================================
// Sinco Presupuesto — infraestructura mínima para Azure Container Apps
// Despliega: Log Analytics, Container Apps Environment, Container Registry,
//            Azure Database for PostgreSQL Flexible Server, y el Container App.
//
// Uso (desde /infra):
//   az group create -n rg-sinco-presupuesto-dev -l eastus2
//   az deployment group create -g rg-sinco-presupuesto-dev -f main.bicep \
//       -p environmentName=dev postgresAdminLogin=sincoadmin postgresAdminPassword='...'
// ======================================================================
targetScope = 'resourceGroup'

@description('Nombre de ambiente (dev, qa, prod). Se usa como sufijo en nombres de recursos.')
@allowed(['dev', 'qa', 'prod'])
param environmentName string = 'dev'

@description('Región del resource group (hereda de la ubicación del RG por default).')
param location string = resourceGroup().location

@description('Login del admin de Postgres (primer bootstrap; en prod usar Entra ID authentication).')
param postgresAdminLogin string

@secure()
@description('Password del admin de Postgres.')
param postgresAdminPassword string

@description('Tag de la imagen a desplegar (del ACR).')
param imageTag string = 'latest'

@description('Número mínimo de réplicas (0 = scale-to-zero).')
@minValue(0)
@maxValue(25)
param minReplicas int = 0

@description('Número máximo de réplicas.')
@minValue(1)
@maxValue(300)
param maxReplicas int = 3

// ──────────────────────────────────────────────────────────────────────
// Nombres derivados
// ──────────────────────────────────────────────────────────────────────
var baseName = 'sincopresupuesto'
var nameSuffix = '${baseName}-${environmentName}'
var acrName = replace('${baseName}${environmentName}acr', '-', '')  // ACR no admite guiones
var logAnalyticsName = 'log-${nameSuffix}'
var cappEnvName = 'cae-${nameSuffix}'
var cappName = 'ca-${nameSuffix}'
var pgServerName = 'pg-${nameSuffix}'
var pgDatabaseName = 'sinco_presupuesto'
var identityName = 'id-${nameSuffix}'

// ──────────────────────────────────────────────────────────────────────
// User-assigned managed identity (para pull del ACR y Entra ID auth futuro)
// ──────────────────────────────────────────────────────────────────────
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

// ──────────────────────────────────────────────────────────────────────
// Log Analytics (sink de logs del Container App)
// ──────────────────────────────────────────────────────────────────────
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ──────────────────────────────────────────────────────────────────────
// Azure Container Registry
// ──────────────────────────────────────────────────────────────────────
resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: acrName
  location: location
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
  }
}

// AcrPull para la managed identity
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, identity.id, acrPullRoleId)
  scope: acr
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
  }
}

// ──────────────────────────────────────────────────────────────────────
// Postgres Flexible Server (Burstable B1ms para dev; escalar en prod)
// ──────────────────────────────────────────────────────────────────────
resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' = {
  name: pgServerName
  location: location
  sku: {
    name: environmentName == 'prod' ? 'Standard_D2ds_v5' : 'Standard_B1ms'
    tier: environmentName == 'prod' ? 'GeneralPurpose' : 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    storage: {
      storageSizeGB: 32
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: environmentName == 'prod' ? 14 : 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'  // Para dev; en prod mover a private endpoint.
    }
  }
}

// Permite tráfico desde servicios de Azure (ok para dev/qa; en prod usar VNET integration).
resource pgFirewallAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-12-01-preview' = {
  parent: postgres
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource pgDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-12-01-preview' = {
  parent: postgres
  name: pgDatabaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// ──────────────────────────────────────────────────────────────────────
// Container Apps Environment
// ──────────────────────────────────────────────────────────────────────
resource cappEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: cappEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

// ──────────────────────────────────────────────────────────────────────
// Container App
// ──────────────────────────────────────────────────────────────────────
var postgresConnectionString = 'Host=${postgres.properties.fullyQualifiedDomainName};Port=5432;Database=${pgDatabaseName};Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true'

resource capp 'Microsoft.App/containerApps@2024-03-01' = {
  name: cappName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: cappEnv.id
    workloadProfileName: 'Consumption'
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'Auto'
        allowInsecure: false
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: identity.id
        }
      ]
      secrets: [
        {
          name: 'postgres-connection-string'
          value: postgresConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${acr.properties.loginServer}/${baseName}-api:${imageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: environmentName == 'prod' ? 'Production' : 'Development'
            }
            {
              name: 'ConnectionStrings__Postgres'
              secretRef: 'postgres-connection-string'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 5
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '30'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [
    acrPullAssignment
    pgDatabase
  ]
}

// ──────────────────────────────────────────────────────────────────────
// Outputs
// ──────────────────────────────────────────────────────────────────────
output acrLoginServer string = acr.properties.loginServer
output containerAppFqdn string = capp.properties.configuration.ingress.fqdn
output postgresFqdn string = postgres.properties.fullyQualifiedDomainName
output managedIdentityClientId string = identity.properties.clientId
