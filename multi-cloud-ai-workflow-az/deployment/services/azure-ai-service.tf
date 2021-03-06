locals {
  azure_ai_service_api_zip_file          = "./../services/Mcma.Azure.AzureAiService/ApiHandler/dist/function.zip"
  azure_ai_service_worker_zip_file       = "./../services/Mcma.Azure.AzureAiService/Worker/dist/function.zip"
  azure_ai_service_notification_zip_file = "./../services/Mcma.Azure.AzureAiService/Notifications/dist/function.zip"
  azure_ai_service_api_function_name     = "${var.global_prefix}-azure-ai-service-api"
  azure_ai_service_url                   = "https://${local.azure_ai_service_api_function_name}.azurewebsites.net"
  azure_ai_service_notification_func_key = "${lookup(azurerm_template_deployment.azure_ai_service_notification_func_key.outputs, "functionkey")}"
}

#===================================================================
# Worker Function
#===================================================================

resource "azurerm_storage_queue" "azure_ai_service_worker_function_queue" {
  name                 = "azure-ai-service-work-queue"
  storage_account_name = var.app_storage_account_name
}

resource "azurerm_storage_blob" "azure_ai_service_worker_function_zip" {
  name                   = "azure-ai-service/worker/function_${filesha256(local.azure_ai_service_worker_zip_file)}.zip"
  resource_group_name    = var.resource_group_name
  storage_account_name   = var.app_storage_account_name
  storage_container_name = var.deploy_container
  type                   = "block"
  source                 = local.azure_ai_service_worker_zip_file
}

resource "azurerm_function_app" "azure_ai_service_worker_function" {
  name                      = "${var.global_prefix}-azure-ai-service-worker"
  location                  = var.azure_location
  resource_group_name       = var.resource_group_name
  app_service_plan_id       = azurerm_app_service_plan.mcma_services.id
  storage_connection_string = var.app_storage_connection_string
  version                   = "~2"

  identity {
    type = "SystemAssigned"
  }

  app_settings = {
    FUNCTIONS_WORKER_RUNTIME       = "dotnet"
    FUNCTION_APP_EDIT_MODE         = "readonly"
    https_only                     = true
    HASH                           = filesha256(local.azure_ai_service_worker_zip_file)
    WEBSITE_RUN_FROM_PACKAGE       = "${local.deploy_container_url}/${azurerm_storage_blob.azure_ai_service_worker_function_zip.name}${var.app_storage_sas}"
    APPINSIGHTS_INSTRUMENTATIONKEY = azurerm_application_insights.services_appinsights.instrumentation_key

    WorkQueueStorage                 = var.app_storage_connection_string
    TableName                        = "AzureAiService"
    NotificationsUrl                 = "https://${var.global_prefix}-azure-ai-service-notification.azurewebsites.net/"
    CosmosDbEndpoint                 = var.cosmosdb_endpoint
    CosmosDbKey                      = var.cosmosdb_key
    CosmosDbDatabaseId               = local.cosmosdb_id
    CosmosDbRegion                   = var.azure_location
    ServicesUrl                      = local.services_url
    ServicesAuthType                 = "AzureAD"
    ServicesAuthContext              = "{ \"scope\": \"${local.service_registry_url}/.default\" }"
    MediaStorageAccountName          = var.media_storage_account_name
    MediaStorageConnectionString     = var.media_storage_connection_string
    AzureVideoIndexerLocation        = var.azure_videoindexer_location
    AzureVideoIndexerAccountId       = var.azure_videoindexer_account_id
    AzureVideoIndexerApiUrl          = var.azure_videoindexer_api_url
    AzureVideoIndexerSubscriptionKey = var.azure_videoindexer_subscription_key
    NotificationHandlerKey           = local.azure_ai_service_notification_func_key
  }

  provisioner "local-exec" {
    command = "az webapp start --resource-group ${var.resource_group_name} --name ${azurerm_function_app.azure_ai_service_worker_function.name}"
  }
}

#===================================================================
# API Function
#===================================================================

resource "azuread_application" "azure_ai_service_app" {
  name            = local.azure_ai_service_api_function_name
  identifier_uris = [local.azure_ai_service_url]
}

resource "azuread_service_principal" "azure_ai_service_sp" {
  application_id               = azuread_application.azure_ai_service_app.application_id
  app_role_assignment_required = false
}

resource "azurerm_storage_blob" "azure_ai_service_api_function_zip" {
  name                   = "azure-ai-service/api/function_${filesha256(local.azure_ai_service_api_zip_file)}.zip"
  resource_group_name    = var.resource_group_name
  storage_account_name   = var.app_storage_account_name
  storage_container_name = var.deploy_container
  type                   = "block"
  source                 = local.azure_ai_service_api_zip_file
}

resource "azurerm_function_app" "azure_ai_service_api_function" {
  name                      = local.azure_ai_service_api_function_name
  location                  = var.azure_location
  resource_group_name       = var.resource_group_name
  app_service_plan_id       = azurerm_app_service_plan.mcma_services.id
  storage_connection_string = var.app_storage_connection_string
  version                   = "~2"

  auth_settings {
    enabled                       = true
    issuer                        = "https://sts.windows.net/${var.azure_tenant_id}"
    default_provider              = "AzureActiveDirectory"
    unauthenticated_client_action = "RedirectToLoginPage"
    active_directory {
      client_id         = azuread_application.azure_ai_service_app.application_id
      allowed_audiences = [local.azure_ai_service_url]
    }
  }

  app_settings = {
    FUNCTIONS_WORKER_RUNTIME       = "dotnet"
    FUNCTION_APP_EDIT_MODE         = "readonly"
    https_only                     = true
    HASH                           = filesha256(local.azure_ai_service_api_zip_file)
    WEBSITE_RUN_FROM_PACKAGE       = "${local.deploy_container_url}/${azurerm_storage_blob.azure_ai_service_api_function_zip.name}${var.app_storage_sas}"
    APPINSIGHTS_INSTRUMENTATIONKEY = azurerm_application_insights.services_appinsights.instrumentation_key

    TableName          = "AzureAiService"
    PublicUrl          = local.azure_ai_service_url
    CosmosDbEndpoint   = var.cosmosdb_endpoint
    CosmosDbKey        = var.cosmosdb_key
    CosmosDbDatabaseId = local.cosmosdb_id
    CosmosDbRegion     = var.azure_location
    WorkerFunctionId   = azurerm_storage_queue.azure_ai_service_worker_function_queue.name
  }
}

#===================================================================
# Azure Video Indexer notification Function
#===================================================================

resource "azurerm_storage_blob" "azure_ai_service_notification_function_zip" {
  name                   = "azure-ai-service/sns/function_${filesha256(local.azure_ai_service_notification_zip_file)}.zip"
  resource_group_name    = var.resource_group_name
  storage_account_name   = var.app_storage_account_name
  storage_container_name = var.deploy_container
  type                   = "block"
  source                 = local.azure_ai_service_notification_zip_file
}

resource "azurerm_function_app" "azure_ai_service_notification_function" {
  name                      = "${var.global_prefix}-azure-ai-service-notification"
  location                  = var.azure_location
  resource_group_name       = var.resource_group_name
  app_service_plan_id       = azurerm_app_service_plan.mcma_services.id
  storage_connection_string = var.app_storage_connection_string
  version                   = "~2"

  app_settings = {
    FUNCTIONS_WORKER_RUNTIME       = "dotnet"
    FUNCTION_APP_EDIT_MODE         = "readonly"
    https_only                     = true
    HASH                           = filesha256(local.azure_ai_service_notification_zip_file)
    WEBSITE_RUN_FROM_PACKAGE       = "${local.deploy_container_url}/${azurerm_storage_blob.azure_ai_service_notification_function_zip.name}${var.app_storage_sas}"
    APPINSIGHTS_INSTRUMENTATIONKEY = azurerm_application_insights.services_appinsights.instrumentation_key

    TableName          = "AzureAiService"
    PublicUrl          = local.azure_ai_service_url
    CosmosDbEndpoint   = var.cosmosdb_endpoint
    CosmosDbKey        = var.cosmosdb_key
    CosmosDbDatabaseId = local.cosmosdb_id
    CosmosDbRegion     = var.azure_location
    WorkerFunctionId   = azurerm_storage_queue.azure_ai_service_worker_function_queue.name
  }
}

resource "azurerm_template_deployment" "azure_ai_service_notification_func_key" {
  name                = "azure-ai-service-notification-func-keys"
  resource_group_name = var.resource_group_name
  deployment_mode     = "Incremental"

  parameters = {
    functionApp = azurerm_function_app.azure_ai_service_notification_function.name
  }

  template_body = <<BODY
  {
      "$schema": "https://schema.management.azure.com/schemas/2015-01-01/deploymentTemplate.json#",
      "contentVersion": "1.0.0.0",
      "parameters": {
          "functionApp": {"type": "string", "defaultValue": ""}
      },
      "variables": {
          "functionAppId": "[resourceId('Microsoft.Web/sites', parameters('functionApp'))]"
      },
      "resources": [
      ],
      "outputs": {
          "functionkey": {
              "type": "string",
              "value": "[listkeys(concat(variables('functionAppId'), '/host/default'), '2018-11-01').functionKeys.default]"
          }
      }
  }
  BODY
}

output azure_ai_service_url {
  value = "${local.azure_ai_service_url}/"
}

output azure_ai_service_worker_url {
  value = "https://${azurerm_function_app.azure_ai_service_worker_function.default_hostname}/"
}
