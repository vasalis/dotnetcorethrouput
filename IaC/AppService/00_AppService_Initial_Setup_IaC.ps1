# Helping variables
$azureLocation = $env:azureLocation
$projectPrefix = $env:projectPrefix
$resourceGroupName = $projectPrefix +"_rg"

Write-Output "(Got from ENV): RG: $resourceGroupName location: $azureLocation"
Write-Output "Environment Azure CL: $(az --version)"

# App Service related variables
$appServiceName = $projectPrefix + "WebApp"
$appInsightsName = $appServiceName + 'AI'

# Cosmos Related variables
$cosmosDbAccount = $projectPrefix + 'cosmosdb'

Write-Output "Resource group name will be: $resourceGroupName App Service name will be: $appServiceName, Application insights name will be: $appInsightsName, Cosmos db account name will be: $cosmosDbAccount"

# Create the resource group
Write-Output "About to create resource group: $resourceGroupName" 
az group create -l $azureLocation -n $resourceGroupName

# Create the Cosmos Db
Write-Output "About to create Cosmos Db account: $cosmosDbAccount"
az cosmosdb create --name $cosmosDbAccount --resource-group $resourceGroupName

# Get Cosmos keys and pass them as Application variables
$cosmosPrimaryKey = az cosmosdb keys list --name $cosmosDbAccount --resource-group $resourceGroupName --type keys --query 'primaryMasterKey'
$cosmosConString = "AccountEndpoint=https://"+$cosmosDbAccount+".documents.azure.com:443/;AccountKey="+$cosmosPrimaryKey

# Create Application Insights for Monitoring the App
Write-Output "About to create Application Insights: $appInsightsName"
az extension add --name application-insights
az monitor app-insights component create -a $appInsightsName -l $azureLocation -g $resourceGroupName
$appInsightsConnectionString = az monitor app-insights component show --app $appInsightsName -g $resourceGroupName --query 'connectionString'
Write-Output "Got app insights connection string: $appInsightsConnectionString"

# Create an App Service plan in `P2V2` tier.
az appservice plan create --name $appServiceName --is-linux --resource-group $resourceGroupName --sku P2V2

# Create a web app.
az webapp create --name $appServiceName --resource-group $resourceGroupName --plan $appServiceName --runtime 'DOTNET:5.0'

# Setup environment variables for Azure Function
az webapp config appsettings set --name $appServiceName --resource-group $resourceGroupName --settings CosmosDb__CosmosConnectionString=$cosmosConString
az webapp config appsettings set --name $appServiceName --resource-group $resourceGroupName --settings ApplicationInsights__ConnectionString=$appInsightsConnectionString