resource "random_string" "suffix" {
  length  = 6
  upper   = false
  special = false
}

locals {
  base_name = "${var.name_prefix}-${var.environment}-${random_string.suffix.result}"
  tags = merge(var.tags, {
    application = "CPP Order Management Portal"
    environment = var.environment
    managed-by  = "terraform"
  })
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.base_name}"
  location = var.location
  tags     = local.tags
}

resource "azurerm_container_registry" "main" {
  name                = replace("acr${local.base_name}", "-", "")
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "Basic"
  admin_enabled       = false
  tags                = local.tags
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.base_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = "PerGB2018"
  retention_in_days   = var.log_retention_days
  tags                = local.tags
}

resource "azurerm_application_insights" "main" {
  name                = "appi-${local.base_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = local.tags
}

resource "azurerm_service_plan" "main" {
  name                = "asp-${local.base_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.app_service_sku
  tags                = local.tags
}

resource "azurerm_linux_web_app" "api" {
  name                = "api-${local.base_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.main.id
  https_only          = true

  identity { type = "SystemAssigned" }

  site_config {
    always_on                               = var.app_service_sku != "F1" && var.app_service_sku != "D1"
    container_registry_use_managed_identity = true
    minimum_tls_version                     = "1.2"
    ftps_state                              = "Disabled"
    health_check_path                       = "/health"

    application_stack {
      docker_image_name   = "cpp-api:${var.api_image_tag}"
      docker_registry_url = "https://${azurerm_container_registry.main.login_server}"
    }
  }

  app_settings = {
    WEBSITES_PORT                         = "8080"
    WEBSITES_ENABLE_APP_SERVICE_STORAGE   = "true"
    ASPNETCORE_ENVIRONMENT                = "Production"
    ConnectionStrings__Cpp                = "Data Source=/home/data/cpp.db"
    Cors__AllowedOrigins__0               = "https://${azurerm_linux_web_app.web.default_hostname}"
    MockJde__FailSubmissions              = tostring(var.mock_jde_fail_submissions)
    APPLICATIONINSIGHTS_CONNECTION_STRING = azurerm_application_insights.main.connection_string
  }

  logs {
    detailed_error_messages = true
    failed_request_tracing  = true
    application_logs { file_system_level = "Information" }
    http_logs {
      file_system {
        retention_in_days = 7
        retention_in_mb   = 35
      }
    }
  }

  tags = local.tags
}

resource "azurerm_linux_web_app" "web" {
  name                = "web-${local.base_name}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.main.id
  https_only          = true

  identity { type = "SystemAssigned" }

  site_config {
    always_on                               = var.app_service_sku != "F1" && var.app_service_sku != "D1"
    container_registry_use_managed_identity = true
    minimum_tls_version                     = "1.2"
    ftps_state                              = "Disabled"
    health_check_path                       = "/"

    application_stack {
      docker_image_name   = "cpp-web:${var.web_image_tag}"
      docker_registry_url = "https://${azurerm_container_registry.main.login_server}"
    }
  }

  app_settings = {
    WEBSITES_PORT                       = "80"
    WEBSITES_ENABLE_APP_SERVICE_STORAGE = "false"
  }

  logs {
    detailed_error_messages = true
    failed_request_tracing  = true
    http_logs {
      file_system {
        retention_in_days = 7
        retention_in_mb   = 35
      }
    }
  }

  tags = local.tags
}

resource "azurerm_role_assignment" "api_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.api.identity[0].principal_id
}

resource "azurerm_role_assignment" "web_acr_pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.web.identity[0].principal_id
}

resource "azurerm_monitor_diagnostic_setting" "api" {
  name                       = "send-to-log-analytics"
  target_resource_id         = azurerm_linux_web_app.api.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id

  enabled_log { category = "AppServiceHTTPLogs" }
  enabled_log { category = "AppServiceConsoleLogs" }
  enabled_log { category = "AppServiceAppLogs" }
  enabled_metric { category = "AllMetrics" }
}

resource "azurerm_monitor_diagnostic_setting" "web" {
  name                       = "send-to-log-analytics"
  target_resource_id         = azurerm_linux_web_app.web.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id

  enabled_log { category = "AppServiceHTTPLogs" }
  enabled_log { category = "AppServiceConsoleLogs" }
  enabled_metric { category = "AllMetrics" }
}
