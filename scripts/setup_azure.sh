#!/bin/bash

# Configuration - PIVOT TO CENTRAL US TO BYPASS QUOTA
RG_NAME="fault-agent-v3"
LOCATION="centralus"
ACR_NAME="faultagentv3reg"
APP_NAME="fault-dashboard"

echo "🚀 Starting FRESH Azure Setup in $LOCATION..."

# 1. Create Resource Group
echo "--- Creating Resource Group: $RG_NAME ---"
az group create --name $RG_NAME --location $LOCATION

# 2. Create Azure Container Registry
echo "--- Creating Azure Container Registry: $ACR_NAME ---"
az acr create --resource-group $RG_NAME --name $ACR_NAME --sku Basic --admin-enabled true

# 3. Create Container App Environment
echo "--- Creating Container App Environment ---"
az containerapp env create --name "$APP_NAME-env" --resource-group $RG_NAME --location $LOCATION

# 4. Create Container App (Initial Placeholder)
echo "--- Creating Container App (Target Port 8080) ---"
az containerapp create \
  --name $APP_NAME \
  --resource-group $RG_NAME \
  --environment "$APP_NAME-env" \
  --image mcr.microsoft.com/azuredocs/containerapps-helloworld:latest \
  --target-port 8080 \
  --ingress external \
  --query properties.configuration.ingress.fqdn

# 5. Generate GitHub Service Principal
echo "--- Generating GitHub Service Principal ---"
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
az ad sp create-for-rbac --name "github-actions-deployer-v3" --role contributor \
  --scopes /subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RG_NAME \
  --sdk-auth

echo "✅ Setup Complete!"
echo "⚠️  IMPORTANT: Copy the JSON output above and paste it into a GitHub Secret named AZURE_CREDENTIALS"
