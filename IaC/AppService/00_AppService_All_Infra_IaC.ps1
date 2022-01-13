$azureLocation = $env:azureLocation
$resourceGroupName = "DotNetCoreThroughtPut_RG"
$AKSClusterName = "dotAKSCluster"
$acrName = "dotnetcorethroughput"

Write-Output "(Got from ENV): RG: $resourceGroupName, location: $azureLocation"
Write-Output "Environment Azure CL: $(az --version)"

# Create the resource group
Write-Output "About to create resource group: $resourceGroupName" 
az group create -l $azureLocation -n $resourceGroupName

Write-Output "About to create AKS cluster: $resourceGroupName" 
az aks create --resource-group $resourceGroupName --name $AKSClusterName --node-count 1 --enable-addons monitoring --generate-ssh-keys --vm-set-type VirtualMachineScaleSets --network-plugin azure --load-balancer-sku standard --uptime-sla --node-vm-size=Standard_D2_v2

